using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Catalog;
using LearnPath.Api.Modules.Exams;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Practice;

/// <summary>Null on a topic without a quiz (its quiz length is 0 or it has no published questions).</summary>
public record TopicQuizSummaryDto(int QuestionCount, int PassPercent, int Attempts, int? BestPercent, int? LastPercent, bool Passed);
public record QuizDto(Guid SessionId, Guid TopicId, string Topic, int PassPercent, List<PracticeQuestionDto> Questions);
public record QuizAnswer([Required] Guid QuestionId, AnswerResponse? Response, [Range(0, 3600)] int TimeSpentSeconds);
public record QuizSubmitRequest([Required, MaxLength(50)] List<QuizAnswer> Answers);
/// <summary>Items follow the quiz's question order; unanswered questions are "Skipped" and still show the answer.</summary>
public record QuizResultDto(Guid SessionId, Guid TopicId, int Total, int Correct, int Incorrect, int Partial, int Skipped, int ScorePercent,
    int PassPercent, bool Passed, List<PracticeFeedbackDto> Items, Guid? NextTopicId);

/// <summary>
/// The short quiz at the end of a topic: a few of the topic's published questions (ones the student hasn't had in an
/// earlier quiz first), answered together and scored on submit. Answers count towards accuracy and weak topics like
/// practice answers do.
/// </summary>
[ApiController]
[Authorize]
public class TopicQuizController(AppDbContext db, EntitlementService entitlements, RequestLanguage lang, TimeProvider clock) : ControllerBase
{
    public const string Mode = "quiz";

    [HttpPost("api/v1/topics/{topicId:guid}/quiz")]
    public async Task<QuizDto> Start(Guid topicId, CancellationToken ct)
    {
        var userId = User.UserId();
        var topic = await db.Topics.AsNoTracking().Include(t => t.Chapter!).ThenInclude(c => c.Subject)
            .FirstOrDefaultAsync(t => t.Id == topicId, ct) ?? throw AppException.NotFound("Topic");
        if (!await entitlements.HasCourseAccessAsync(userId, topic.Chapter!.Subject!.CourseId, ct)) throw AppException.PaymentRequired();
        if (topic.QuizQuestionCount <= 0) throw AppException.Conflict("no_quiz", "This topic has no quiz.");

        var pool = await db.Questions.AsNoTracking().Where(q => q.TopicId == topicId && q.Status == PublishStatus.Published).Select(q => q.Id).ToListAsync(ct);
        if (pool.Count == 0) throw AppException.Conflict("no_questions", "No questions are available for this topic yet.");
        var seen = (await db.PracticeSessions.AsNoTracking().Where(s => s.UserId == userId && s.Mode == Mode && s.TopicId == topicId)
            .Select(s => s.QuestionIdsJson).ToListAsync(ct)).SelectMany(j => JsonSerializer.Deserialize<List<Guid>>(j)!).ToHashSet();
        var pickedIds = pool.OrderBy(id => seen.Contains(id)).ThenBy(_ => Random.Shared.Next()).Take(topic.QuizQuestionCount).ToList();
        var byId = await db.Questions.AsNoTracking().Include(q => q.Options).Where(q => pickedIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var questions = pickedIds.Select(id => byId[id]).ToList();

        var session = new PracticeSession
        {
            UserId = userId, Mode = Mode, TopicId = topicId, StartedAt = clock.GetUtcNow(), QuestionIdsJson = JsonSerializer.Serialize(pickedIds),
        };
        db.PracticeSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return new QuizDto(session.Id, topicId, lang.Pick(topic.TitleEn, topic.TitleHi), topic.QuizPassPercent,
            questions.Select(q => PracticeController.ToPlayer(q, lang, withHint: false)).ToList());
    }

    /// <summary>Scores the quiz. Submitting again (e.g. after a dropped connection) returns the same result.</summary>
    [HttpPost("api/v1/practice/quizzes/{sessionId:guid}/submit")]
    public async Task<QuizResultDto> Submit(Guid sessionId, QuizSubmitRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var session = await db.PracticeSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.Mode == Mode, ct)
                      ?? throw AppException.NotFound("Quiz");
        var topic = await db.Topics.AsNoTracking().FirstAsync(t => t.Id == session.TopicId, ct);
        var ids = JsonSerializer.Deserialize<List<Guid>>(session.QuestionIdsJson)!;
        var questions = await db.Questions.AsNoTracking().Include(q => q.Options).Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var now = clock.GetUtcNow();

        var results = new Dictionary<Guid, ScoredQuestion>();
        if (session.CompletedAt is null)
        {
            var responses = new Dictionary<Guid, QuizAnswer>();
            foreach (var a in req.Answers)
            {
                if (!ids.Contains(a.QuestionId)) throw AppException.BadRequest("not_in_session", "This question is not part of the quiz.");
                responses[a.QuestionId] = a;
            }
            foreach (var id in ids)
            {
                if (!questions.TryGetValue(id, out var q)) continue; // deleted since the quiz began
                responses.TryGetValue(id, out var a);
                var snap = PracticeController.SnapshotOf(q);
                if (a?.Response is { IsEmpty: false } r) Scoring.Validate(snap, r);
                var scored = results[id] = Scoring.Score(snap, a?.Response);
                if (scored.Outcome == AnswerOutcome.Skipped) continue;
                db.PracticeAnswers.Add(new PracticeAnswer
                {
                    SessionId = sessionId, UserId = userId, QuestionId = id, TopicId = q.TopicId, ResponseJson = Scoring.Serialize(a!.Response!),
                    Outcome = scored.Outcome, TimeSpentSeconds = a.TimeSpentSeconds, AnsweredAt = now,
                });
                db.LearningEvents.Add(new LearningEvent { UserId = userId, EventType = LearningEventTypes.QuestionAnswered, EntityId = id, OccurredAt = now });
            }
            session.CompletedAt = now;
            session.ScorePercent = Percent(results.Values.Select(r => r.Outcome), ids.Count);
            session.Passed = session.ScorePercent >= topic.QuizPassPercent;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            // Already submitted: rebuild the result from the stored answers.
            var stored = await db.PracticeAnswers.AsNoTracking().Where(a => a.SessionId == sessionId).ToListAsync(ct);
            foreach (var a in stored.Where(a => questions.ContainsKey(a.QuestionId)))
                results[a.QuestionId] = Scoring.Score(PracticeController.SnapshotOf(questions[a.QuestionId]), Scoring.Parse(a.ResponseJson));
        }

        var items = ids.Where(questions.ContainsKey).Select(id =>
        {
            var r = results.GetValueOrDefault(id) ?? new ScoredQuestion(AnswerOutcome.Skipped, 0);
            return PracticeController.FeedbackOf(questions[id], r.Outcome, r.Marks, lang);
        }).ToList();
        int c = items.Count(i => i.Outcome == nameof(AnswerOutcome.Correct)), w = items.Count(i => i.Outcome == nameof(AnswerOutcome.Incorrect)),
            p = items.Count(i => i.Outcome == nameof(AnswerOutcome.PartiallyCorrect));
        return new QuizResultDto(sessionId, topic.Id, ids.Count, c, w, p, ids.Count - c - w - p, session.ScorePercent ?? 0, topic.QuizPassPercent,
            session.Passed ?? false, items, await NextTopicAsync(topic, ct));
    }

    /// <summary>Correct answers count fully, partly correct ones half.</summary>
    internal static int Percent(IEnumerable<AnswerOutcome> outcomes, int total)
    {
        if (total == 0) return 0;
        var list = outcomes.ToList();
        var points = list.Count(o => o == AnswerOutcome.Correct) + 0.5 * list.Count(o => o == AnswerOutcome.PartiallyCorrect);
        return (int)Math.Round(100 * points / total, MidpointRounding.AwayFromZero);
    }

    private async Task<Guid?> NextTopicAsync(Topic topic, CancellationToken ct)
    {
        var courseId = await db.Chapters.Where(c => c.Id == topic.ChapterId).Select(c => c.Subject!.CourseId).FirstAsync(ct);
        var order = await db.Topics.AsNoTracking().Where(t => t.Chapter!.Subject!.CourseId == courseId)
            .OrderBy(t => t.Chapter!.Subject!.SortOrder).ThenBy(t => t.Chapter!.SortOrder).ThenBy(t => t.SortOrder).Select(t => t.Id).ToListAsync(ct);
        var i = order.IndexOf(topic.Id);
        return i >= 0 && i < order.Count - 1 ? order[i + 1] : null;
    }
}

public static class TopicQuizzes
{
    /// <summary>The quiz on a topic page: its length (capped by the questions available) and the student's results so far.</summary>
    public static async Task<TopicQuizSummaryDto?> SummaryAsync(AppDbContext db, Guid userId, Topic topic, CancellationToken ct)
    {
        if (topic.QuizQuestionCount <= 0) return null;
        var available = await db.Questions.CountAsync(q => q.TopicId == topic.Id && q.Status == PublishStatus.Published, ct);
        if (available == 0) return null;
        var done = await db.PracticeSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.Mode == TopicQuizController.Mode && s.TopicId == topic.Id && s.CompletedAt != null)
            .OrderBy(s => s.CompletedAt).Select(s => new { s.ScorePercent, s.Passed }).ToListAsync(ct);
        return new TopicQuizSummaryDto(Math.Min(topic.QuizQuestionCount, available), topic.QuizPassPercent, done.Count,
            done.Count == 0 ? null : done.Max(s => s.ScorePercent ?? 0), done.Count == 0 ? null : done[^1].ScorePercent ?? 0, done.Any(s => s.Passed == true));
    }
}

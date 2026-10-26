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

public record CreatePracticeRequest(
    [RegularExpression("^(topic|chapter|incorrect|bookmarked|previous_year)$")] string Mode,
    Guid? TopicId, Guid? ChapterId, Difficulty? Difficulty, [Range(1, 50)] int Count = 10);
public record PracticeQuestionDto(Guid QuestionId, string Type, string Difficulty, string Stem, string? ImageUrl, string? Hint, List<PlayerOptionDto> Options, decimal Marks, decimal NegativeMarks);
public record PracticeSessionDto(Guid SessionId, string Mode, List<PracticeQuestionDto> Questions);
public record PracticeAnswerRequest([Required] Guid QuestionId, [Required] AnswerResponse Response, [Range(0, 3600)] int TimeSpentSeconds);
public record PracticeFeedbackDto(Guid QuestionId, string Outcome, decimal MarksIfInTest, List<Guid> CorrectOptionIds, decimal? CorrectNumericAnswer, string? Explanation, string? VideoSolutionId);
public record PracticeSummaryDto(Guid SessionId, int Total, int Answered, int Correct, int Incorrect, int Partial, double Accuracy);
public record ReportQuestionRequest([Required, RegularExpression("^(wrong_answer|typo|unclear|other)$")] string Reason, [MaxLength(2000)] string? Details);

[ApiController]
[Authorize]
[Route("api/v1/practice")]
public class PracticeController(AppDbContext db, EntitlementService entitlements, RequestLanguage lang, TimeProvider clock) : ControllerBase
{
    [HttpPost("sessions")]
    public async Task<PracticeSessionDto> Create(CreatePracticeRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var q = db.Questions.AsNoTracking().Include(x => x.Options).Where(x => x.Status == PublishStatus.Published);
        switch (req.Mode)
        {
            case "topic":
                var topic = await db.Topics.AsNoTracking().Include(t => t.Chapter!).ThenInclude(c => c.Subject)
                    .FirstOrDefaultAsync(t => t.Id == req.TopicId, ct) ?? throw AppException.NotFound("Topic");
                await EnsureAccess(userId, topic.Chapter!.Subject!.CourseId, ct);
                q = q.Where(x => x.TopicId == topic.Id);
                break;
            case "chapter":
                var chapter = await db.Chapters.AsNoTracking().Include(c => c.Subject).FirstOrDefaultAsync(c => c.Id == req.ChapterId, ct) ?? throw AppException.NotFound("Chapter");
                await EnsureAccess(userId, chapter.Subject!.CourseId, ct);
                q = q.Where(x => x.Topic!.ChapterId == chapter.Id);
                break;
            case "incorrect":
                // Latest outcome per question in practice or tests was wrong.
                var wrongPractice = db.PracticeAnswers.Where(a => a.UserId == userId && a.Outcome == AnswerOutcome.Incorrect).Select(a => a.QuestionId);
                var wrongTests = from r in db.AttemptQuestionResults
                                 join a in db.TestAttempts on r.AttemptId equals a.Id
                                 where a.UserId == userId && r.Outcome == AnswerOutcome.Incorrect
                                 select r.QuestionId;
                q = q.Where(x => wrongPractice.Contains(x.Id) || wrongTests.Contains(x.Id));
                break;
            case "bookmarked":
                q = q.Where(x => db.Bookmarks.Any(b => b.UserId == userId && b.EntityType == BookmarkType.Question && b.EntityId == x.Id));
                break;
            case "previous_year":
                q = q.Where(x => x.PreviousYear != null);
                if (req.ChapterId is not null) q = q.Where(x => x.Topic!.ChapterId == req.ChapterId);
                break;
        }
        if (req.Mode is "incorrect" or "bookmarked" or "previous_year")
        {
            // Only questions from courses the student can access.
            var allowed = await entitlements.ActiveCourseIdsAsync(userId, ct);
            var free = await db.Courses.Where(c => c.IsFree).Select(c => c.Id).ToListAsync(ct);
            allowed.UnionWith(free);
            q = q.Where(x => allowed.Contains(x.Topic!.Chapter!.Subject!.CourseId));
        }
        if (req.Difficulty is not null) q = q.Where(x => x.Difficulty == req.Difficulty);

        var questions = await q.OrderBy(x => EF.Functions.Random()).Take(req.Count).ToListAsync(ct);
        if (questions.Count == 0) throw AppException.Conflict("no_questions", "No questions are available for this selection yet.");

        var session = new PracticeSession
        {
            UserId = userId, Mode = req.Mode, TopicId = req.TopicId, ChapterId = req.ChapterId, StartedAt = clock.GetUtcNow(),
            QuestionIdsJson = JsonSerializer.Serialize(questions.Select(x => x.Id)),
        };
        db.PracticeSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return new PracticeSessionDto(session.Id, session.Mode, questions.Select(x => new PracticeQuestionDto(x.Id, x.Type.ToString(), x.Difficulty.ToString(),
            lang.Pick(x.StemEn, x.StemHi), x.ImageUrl, x.HintEn,
            x.Options.OrderBy(o => o.SortOrder).Select(o => new PlayerOptionDto(o.Id, lang.Pick(o.TextEn, o.TextHi))).ToList(),
            x.DefaultMarks, x.DefaultNegativeMarks)).ToList());
    }

    /// <summary>Scores one practice answer and reveals the solution. Retries add new rows; test attempts are never touched.</summary>
    [HttpPost("sessions/{sessionId:guid}/answers")]
    public async Task<PracticeFeedbackDto> Answer(Guid sessionId, PracticeAnswerRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var session = await db.PracticeSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct) ?? throw AppException.NotFound("Practice session");
        var ids = JsonSerializer.Deserialize<List<Guid>>(session.QuestionIdsJson)!;
        if (!ids.Contains(req.QuestionId)) throw AppException.BadRequest("not_in_session", "This question is not part of the session.");
        var q = await db.Questions.AsNoTracking().Include(x => x.Options).FirstAsync(x => x.Id == req.QuestionId, ct);

        var snap = new QuestionSnapshot(Guid.Empty, q.Id, q.Version, null, null, Guid.Empty, "", Guid.Empty, "", q.TopicId, "", q.Type, q.Difficulty,
            q.StemEn, q.StemHi, q.ImageUrl, q.Options.Select(o => new OptionSnapshot(o.Id, o.TextEn, o.TextHi, o.IsCorrect)).ToList(),
            q.NumericAnswer, q.NumericTolerance, q.DefaultMarks, q.DefaultNegativeMarks, q.AllowPartialMarking, q.ExplanationEn, q.ExplanationHi);
        Scoring.Validate(snap, req.Response);
        var scored = Scoring.Score(snap, req.Response);

        var now = clock.GetUtcNow();
        db.PracticeAnswers.Add(new PracticeAnswer
        {
            SessionId = sessionId, UserId = userId, QuestionId = q.Id, TopicId = q.TopicId, ResponseJson = Scoring.Serialize(req.Response),
            Outcome = scored.Outcome, TimeSpentSeconds = req.TimeSpentSeconds, AnsweredAt = now,
        });
        db.LearningEvents.Add(new LearningEvent { UserId = userId, EventType = LearningEventTypes.QuestionAnswered, EntityId = q.Id, OccurredAt = now });
        await db.SaveChangesAsync(ct);

        return new PracticeFeedbackDto(q.Id, scored.Outcome.ToString(), scored.Marks, q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToList(),
            q.NumericAnswer, lang.PickOpt(q.ExplanationEn, q.ExplanationHi), q.VideoSolutionId);
    }

    [HttpPost("sessions/{sessionId:guid}/complete")]
    public async Task<PracticeSummaryDto> Complete(Guid sessionId, CancellationToken ct)
    {
        var userId = User.UserId();
        var session = await db.PracticeSessions.Include(s => s.Answers).FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct)
                      ?? throw AppException.NotFound("Practice session");
        session.CompletedAt ??= clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        // Summary uses the latest answer per question so retries reflect the final understanding.
        var latest = session.Answers.GroupBy(a => a.QuestionId).Select(g => g.OrderByDescending(a => a.AnsweredAt).First()).ToList();
        int c = latest.Count(a => a.Outcome == AnswerOutcome.Correct), i = latest.Count(a => a.Outcome == AnswerOutcome.Incorrect),
            p = latest.Count(a => a.Outcome == AnswerOutcome.PartiallyCorrect);
        var total = JsonSerializer.Deserialize<List<Guid>>(session.QuestionIdsJson)!.Count;
        return new PracticeSummaryDto(sessionId, total, latest.Count, c, i, p, ResultsService.Accuracy(c, i, p));
    }

    [HttpPost("/api/v1/questions/{questionId:guid}/report")]
    public async Task<IActionResult> Report(Guid questionId, ReportQuestionRequest req, CancellationToken ct)
    {
        if (!await db.Questions.AnyAsync(q => q.Id == questionId, ct)) throw AppException.NotFound("Question");
        db.QuestionReports.Add(new QuestionReport { UserId = User.UserId(), QuestionId = questionId, Reason = req.Reason, Details = req.Details, CreatedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        return Accepted();
    }

    private async Task EnsureAccess(Guid userId, Guid courseId, CancellationToken ct)
    {
        if (!await entitlements.HasCourseAccessAsync(userId, courseId, ct)) throw AppException.PaymentRequired();
    }
}

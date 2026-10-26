using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Exams;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Analytics;

public record TopicAccuracyDto(Guid TopicId, string Topic, string Chapter, string Subject, int Attempted, int Correct, double Accuracy);
public record SubjectAccuracyDto(Guid SubjectId, string Subject, int Attempted, int Correct, double Accuracy);
public record ScorePointDto(Guid AttemptId, string Test, DateTimeOffset SubmittedAt, decimal Score, decimal MaxScore, double Percentage);
public record StudentAnalyticsDto(int TestsTaken, double? AveragePercentage, int PracticeQuestionsAnswered, double? PracticeAccuracy,
    int LessonsCompleted, int StreakDays, List<ScorePointDto> ScoreTrend, List<SubjectAccuracyDto> Subjects, List<TopicAccuracyDto> WeakTopics);

/// <summary>Aggregates only recorded activity — no estimates or invented numbers.</summary>
public class LearnerStatsService(AppDbContext db, RequestLanguage lang, AnalyticsOptions options, TimeProvider clock)
{
    private record Answered(Guid TopicId, AnswerOutcome Outcome);

    /// <summary>All graded answers (practice + submitted tests) for a user, by topic.</summary>
    private async Task<List<Answered>> AnsweredAsync(Guid userId, CancellationToken ct)
    {
        var practice = await db.PracticeAnswers.AsNoTracking().Where(a => a.UserId == userId).Select(a => new Answered(a.TopicId, a.Outcome)).ToListAsync(ct);
        var tests = await (from r in db.AttemptQuestionResults.AsNoTracking()
                           join a in db.TestAttempts on r.AttemptId equals a.Id
                           where a.UserId == userId && r.Outcome != AnswerOutcome.Skipped
                           select new Answered(r.TopicId, r.Outcome)).ToListAsync(ct);
        return practice.Concat(tests).ToList();
    }

    public async Task<List<TopicAccuracyDto>> TopicAccuracyAsync(Guid userId, CancellationToken ct)
    {
        var answered = await AnsweredAsync(userId, ct);
        var topicIds = answered.Select(a => a.TopicId).Distinct().ToList();
        var topics = await db.Topics.AsNoTracking().Include(t => t.Chapter!).ThenInclude(c => c.Subject)
            .Where(t => topicIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        return answered.GroupBy(a => a.TopicId).Where(g => topics.ContainsKey(g.Key)).Select(g =>
        {
            var t = topics[g.Key];
            int c = g.Count(x => x.Outcome == AnswerOutcome.Correct), p = g.Count(x => x.Outcome == AnswerOutcome.PartiallyCorrect);
            return new TopicAccuracyDto(t.Id, lang.Pick(t.TitleEn, t.TitleHi), lang.Pick(t.Chapter!.TitleEn, t.Chapter.TitleHi),
                lang.Pick(t.Chapter.Subject!.TitleEn, t.Chapter.Subject.TitleHi), g.Count(), c, ResultsService.Accuracy(c, g.Count() - c - p, p));
        }).ToList();
    }

    public async Task<List<TopicAccuracyDto>> WeakTopicsAsync(Guid userId, int take, CancellationToken ct) =>
        (await TopicAccuracyAsync(userId, ct))
        .Where(t => t.Attempted >= options.WeakTopicMinQuestions && t.Accuracy < options.WeakTopicAccuracyThreshold * 100)
        .OrderBy(t => t.Accuracy).ThenByDescending(t => t.Attempted).Take(take).ToList();

    /// <summary>Consecutive IST days with any learning activity, ending today (or yesterday if nothing yet today).</summary>
    public async Task<int> StreakAsync(Guid userId, CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddDays(-400);
        var times = await db.LearningEvents.AsNoTracking().Where(e => e.UserId == userId && e.OccurredAt >= since).Select(e => e.OccurredAt).ToListAsync(ct);
        var days = times.Select(India.DateOf).ToHashSet();
        var day = India.Today(clock);
        if (!days.Contains(day)) day = day.AddDays(-1);
        var streak = 0;
        while (days.Contains(day)) { streak++; day = day.AddDays(-1); }
        return streak;
    }

    public async Task<StudentAnalyticsDto> AnalyticsAsync(Guid userId, CancellationToken ct)
    {
        var attempts = await db.TestAttempts.AsNoTracking().Include(a => a.Test)
            .Where(a => a.UserId == userId && a.Status != AttemptStatus.InProgress).OrderBy(a => a.SubmittedAt).ToListAsync(ct);
        var trend = attempts.TakeLast(20).Select(a => new ScorePointDto(a.Id, lang.Pick(a.Test!.TitleEn, a.Test.TitleHi), a.SubmittedAt!.Value,
            a.Score ?? 0, a.MaxScore, a.MaxScore == 0 ? 0 : Math.Round((double)((a.Score ?? 0) / a.MaxScore) * 100, 1))).ToList();

        var practice = await db.PracticeAnswers.AsNoTracking().Where(a => a.UserId == userId).Select(a => a.Outcome).ToListAsync(ct);
        int pc = practice.Count(o => o == AnswerOutcome.Correct), pp = practice.Count(o => o == AnswerOutcome.PartiallyCorrect);

        var topicAcc = await TopicAccuracyAsync(userId, ct);
        var topicSubject = await db.Topics.AsNoTracking().Where(t => topicAcc.Select(x => x.TopicId).Contains(t.Id))
            .Select(t => new { t.Id, t.Chapter!.SubjectId }).ToDictionaryAsync(x => x.Id, x => x.SubjectId, ct);
        var subjects = topicAcc.GroupBy(t => topicSubject[t.TopicId]).Select(g =>
        {
            int att = g.Sum(x => x.Attempted), c = g.Sum(x => x.Correct);
            return new SubjectAccuracyDto(g.Key, g.First().Subject, att, c, att == 0 ? 0 : Math.Round(100.0 * c / att, 1));
        }).OrderBy(s => s.Subject).ToList();

        return new StudentAnalyticsDto(attempts.Count, trend.Count == 0 ? null : Math.Round(trend.Average(t => t.Percentage), 1),
            practice.Count, practice.Count == 0 ? null : ResultsService.Accuracy(pc, practice.Count - pc - pp, pp),
            await db.ResourceProgress.CountAsync(p => p.UserId == userId && p.Completed, ct), await StreakAsync(userId, ct),
            trend, subjects, await WeakTopicsAsync(userId, 5, ct));
    }
}

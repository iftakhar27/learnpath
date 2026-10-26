using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Exams;

public record BreakdownDto(Guid Id, string Name, int Total, int Correct, int Incorrect, int Partial, int Skipped, decimal Score, decimal MaxScore, double Accuracy);
public record ReviewOptionDto(Guid Id, string Text, bool IsCorrect, bool Selected);
public record ReviewQuestionDto(int Number, Guid TestQuestionId, Guid QuestionId, string Subject, string Chapter, string Topic, string Type, string Difficulty,
    string Stem, string? ImageUrl, List<ReviewOptionDto> Options, decimal? CorrectNumericAnswer, decimal? YourNumericAnswer,
    string Outcome, decimal MarksAwarded, decimal Marks, int TimeSpentSeconds, string? Explanation);
public record PreviousAttemptDto(Guid AttemptId, int AttemptNumber, decimal Score, decimal MaxScore, DateTimeOffset SubmittedAt);
public record PercentileDto(double Percentile, int CohortSize, string Basis);
public record ResultDto(Guid AttemptId, Guid TestId, string TestTitle, string Status, int AttemptNumber, decimal Score, decimal MaxScore, double Percentage,
    double Accuracy, int Correct, int Incorrect, int Partial, int Skipped, int TimeTakenSeconds,
    List<BreakdownDto> Subjects, List<BreakdownDto> Chapters, List<BreakdownDto> Difficulty, List<ReviewQuestionDto> Questions,
    List<PreviousAttemptDto> PreviousAttempts, PercentileDto? Percentile, List<string> Recommendations);

public class AnalyticsOptions
{
    /// <summary>Percentile is only shown once at least this many students have a first attempt on the test.</summary>
    public int MinCohortForPercentile { get; set; } = 100;
    public double WeakTopicAccuracyThreshold { get; set; } = 0.6;
    public int WeakTopicMinQuestions { get; set; } = 3;
}

public class ResultsService(AppDbContext db, RequestLanguage lang, AnalyticsOptions options)
{
    public static double Accuracy(int correct, int incorrect, int partial)
    {
        var attempted = correct + incorrect + partial;
        return attempted == 0 ? 0 : Math.Round((correct + 0.5 * partial) / attempted * 100, 1);
    }

    public async Task<ResultDto> GetAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        var attempt = await db.TestAttempts.AsNoTracking().Include(a => a.Test).Include(a => a.Answers)
                          .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct) ?? throw AppException.NotFound("Attempt");
        if (attempt.Status == AttemptStatus.InProgress)
            throw AppException.Conflict("attempt_in_progress", "Submit the test to see results.");

        var snapshot = ExamService.ReadSnapshot(attempt);
        var results = await db.AttemptQuestionResults.AsNoTracking().Where(r => r.AttemptId == attemptId).ToDictionaryAsync(r => r.TestQuestionId, ct);
        var responses = attempt.Answers.ToDictionary(a => a.TestQuestionId, a => Scoring.Parse(a.ResponseJson));

        var rows = snapshot.Select(q => (Q: q, R: results[q.TestQuestionId])).ToList();
        List<BreakdownDto> Group(Func<QuestionSnapshot, Guid> key, Func<QuestionSnapshot, string> name) =>
            rows.GroupBy(x => key(x.Q)).Select(g =>
            {
                int c = g.Count(x => x.R.Outcome == AnswerOutcome.Correct), i = g.Count(x => x.R.Outcome == AnswerOutcome.Incorrect),
                    p = g.Count(x => x.R.Outcome == AnswerOutcome.PartiallyCorrect), s = g.Count(x => x.R.Outcome == AnswerOutcome.Skipped);
                return new BreakdownDto(g.Key, name(g.First().Q), g.Count(), c, i, p, s, g.Sum(x => x.R.MarksAwarded), g.Sum(x => x.Q.Marks), Accuracy(c, i, p));
            }).ToList();

        var subjects = Group(q => q.SubjectId, q => q.SubjectTitle);
        var chapters = Group(q => q.ChapterId, q => q.ChapterTitle);
        var difficulty = rows.GroupBy(x => x.Q.Difficulty).Select(g =>
        {
            int c = g.Count(x => x.R.Outcome == AnswerOutcome.Correct), i = g.Count(x => x.R.Outcome == AnswerOutcome.Incorrect),
                p = g.Count(x => x.R.Outcome == AnswerOutcome.PartiallyCorrect), s = g.Count(x => x.R.Outcome == AnswerOutcome.Skipped);
            return new BreakdownDto(Guid.Empty, g.Key.ToString(), g.Count(), c, i, p, s, g.Sum(x => x.R.MarksAwarded), g.Sum(x => x.Q.Marks), Accuracy(c, i, p));
        }).OrderBy(b => b.Name).ToList();

        var review = rows.Select((x, idx) =>
        {
            responses.TryGetValue(x.Q.TestQuestionId, out var resp);
            var selected = resp?.SelectedOptionIds?.ToHashSet() ?? [];
            return new ReviewQuestionDto(idx + 1, x.Q.TestQuestionId, x.Q.QuestionId, x.Q.SubjectTitle, x.Q.ChapterTitle, x.Q.TopicTitle,
                x.Q.Type.ToString(), x.Q.Difficulty.ToString(), lang.Pick(x.Q.StemEn, x.Q.StemHi), x.Q.ImageUrl,
                x.Q.Options.Select(o => new ReviewOptionDto(o.Id, lang.Pick(o.TextEn, o.TextHi), o.IsCorrect, selected.Contains(o.Id))).ToList(),
                x.Q.NumericAnswer, resp?.NumericValue, x.R.Outcome.ToString(), x.R.MarksAwarded, x.Q.Marks, x.R.TimeSpentSeconds,
                lang.PickOpt(x.Q.ExplanationEn, x.Q.ExplanationHi));
        }).ToList();

        var previous = await db.TestAttempts.AsNoTracking()
            .Where(a => a.UserId == userId && a.TestId == attempt.TestId && a.Id != attemptId && a.Status != AttemptStatus.InProgress)
            .OrderBy(a => a.AttemptNumber)
            .Select(a => new PreviousAttemptDto(a.Id, a.AttemptNumber, a.Score ?? 0, a.MaxScore, a.SubmittedAt!.Value)).ToListAsync(ct);

        var score = attempt.Score ?? 0;
        var pct = attempt.MaxScore == 0 ? 0 : Math.Round((double)(score / attempt.MaxScore) * 100, 1);
        var timeTaken = (int)((attempt.SubmittedAt ?? attempt.ExpiresAt) - attempt.StartedAt).TotalSeconds;

        return new ResultDto(attempt.Id, attempt.TestId, lang.Pick(attempt.Test!.TitleEn, attempt.Test.TitleHi), attempt.Status.ToString(), attempt.AttemptNumber,
            score, attempt.MaxScore, pct, Accuracy(attempt.CorrectCount, attempt.IncorrectCount, attempt.PartialCount),
            attempt.CorrectCount, attempt.IncorrectCount, attempt.PartialCount, attempt.SkippedCount, timeTaken,
            subjects, chapters, difficulty, review, previous, await PercentileAsync(attempt, ct), Recommend(chapters, attempt));
    }

    /// <summary>Percentile among first attempts on this test on this platform. Null until the cohort is large enough.</summary>
    private async Task<PercentileDto?> PercentileAsync(TestAttempt attempt, CancellationToken ct)
    {
        var firstScores = db.TestAttempts.Where(a => a.TestId == attempt.TestId && a.AttemptNumber == 1 && a.Status != AttemptStatus.InProgress);
        var cohort = await firstScores.CountAsync(ct);
        if (cohort < options.MinCohortForPercentile) return null;
        var below = await firstScores.CountAsync(a => a.Score < attempt.Score, ct);
        return new PercentileDto(Math.Round(100.0 * below / cohort, 1), cohort, "First attempts by students on this platform. Not an official rank.");
    }

    private List<string> Recommend(List<BreakdownDto> chapters, TestAttempt a)
    {
        var tips = new List<string>();
        foreach (var ch in chapters.Where(c => c.Total >= 2 && c.Accuracy < options.WeakTopicAccuracyThreshold * 100).OrderBy(c => c.Accuracy).Take(3))
            tips.Add($"Revise \"{ch.Name}\" — accuracy {ch.Accuracy}% on {ch.Total} questions.");
        var total = a.CorrectCount + a.IncorrectCount + a.PartialCount + a.SkippedCount;
        if (total > 0 && a.SkippedCount > total / 3)
            tips.Add($"You skipped {a.SkippedCount} of {total} questions. Practise timed sets to build speed.");
        if (a.IncorrectCount > a.CorrectCount && a.IncorrectCount > 0)
            tips.Add("More answers were wrong than right. With negative marking, skip questions you cannot narrow down.");
        if (tips.Count == 0) tips.Add("Good work. Try a longer mixed test to check retention.");
        return tips;
    }
}

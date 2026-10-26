using System.Security.Cryptography;
using System.Text.Json;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Exams;

public record PlayerOptionDto(Guid Id, string Text);
public record PlayerQuestionDto(int Number, Guid TestQuestionId, string? Section, string Subject, string Type, string Stem, string? ImageUrl,
    List<PlayerOptionDto> Options, decimal Marks, decimal NegativeMarks, AnswerResponse? Response, bool MarkedForReview, int TimeSpentSeconds);
public record AttemptDto(Guid AttemptId, Guid TestId, string TestTitle, string? Instructions, string Status, DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt, int RemainingSeconds, DateTimeOffset ServerTime, List<PlayerQuestionDto> Questions);
public record SaveAnswerRequest(Guid TestQuestionId, AnswerResponse? Response, bool MarkedForReview, int TimeSpentSeconds);

public class ExamOptions
{
    /// <summary>Answers arriving this long after the deadline are still accepted (network latency on mobile).</summary>
    public int SubmissionGraceSeconds { get; set; } = 30;
}

public class ExamService(AppDbContext db, EntitlementService entitlements, TimeProvider clock, RequestLanguage lang, ExamOptions options)
{
    public async Task<AttemptDto> StartAsync(Guid userId, Guid testId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var test = await db.Tests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == testId && t.Status == PublishStatus.Published, ct)
                   ?? throw AppException.NotFound("Test");
        if (test.AvailableFrom > now) throw AppException.Conflict("test_not_open", "This test has not opened yet.");
        if (test.AvailableUntil < now) throw AppException.Conflict("test_closed", "This test is closed.");
        await entitlements.EnsureTestAccessAsync(userId, test, ct);

        // Resume an unfinished attempt instead of creating a duplicate.
        var open = await db.TestAttempts.Where(a => a.UserId == userId && a.TestId == testId && a.Status == AttemptStatus.InProgress).FirstOrDefaultAsync(ct);
        if (open is not null)
        {
            if (!IsPastGrace(open, now))
            {
                db.AttemptEvents.Add(new AttemptEvent { AttemptId = open.Id, EventType = "resumed", OccurredAt = now });
                await db.SaveChangesAsync(ct);
                return await GetAsync(userId, open.Id, ct);
            }
            await FinalizeAsync(userId, open.Id, ct);
        }

        var previous = await db.TestAttempts.CountAsync(a => a.UserId == userId && a.TestId == testId, ct);
        if (test.MaxAttempts is { } max && previous >= max)
            throw AppException.Conflict("max_attempts_reached", $"You have used all {max} attempt(s) for this test.");

        var seed = RandomNumberGenerator.GetInt32(int.MaxValue);
        var snapshot = await BuildSnapshotAsync(test, seed, ct);
        if (snapshot.Count == 0) throw AppException.Conflict("test_empty", "This test has no questions yet.");

        var attempt = new TestAttempt
        {
            TestId = testId, UserId = userId, AttemptNumber = previous + 1, Status = AttemptStatus.InProgress,
            StartedAt = now, ExpiresAt = now.AddMinutes(test.DurationMinutes), Seed = seed,
            SnapshotJson = JsonSerializer.Serialize(snapshot, Scoring.Json), MaxScore = snapshot.Sum(q => q.Marks),
        };
        db.TestAttempts.Add(attempt);
        db.AttemptEvents.Add(new AttemptEvent { AttemptId = attempt.Id, EventType = "started", OccurredAt = now });
        db.LearningEvents.Add(new LearningEvent { UserId = userId, EventType = LearningEventTypes.TestStarted, EntityId = testId, OccurredAt = now });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent Start won the unique (TestId, UserId, AttemptNumber) race: return that attempt.
            db.ChangeTracker.Clear();
            var winner = await db.TestAttempts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId && a.TestId == testId && a.Status == AttemptStatus.InProgress, ct);
            if (winner is null) throw;
            return await GetAsync(userId, winner.Id, ct);
        }
        return await GetAsync(userId, attempt.Id, ct);
    }

    public async Task<AttemptDto> GetAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        var attempt = await db.TestAttempts.AsNoTracking().Include(a => a.Test).Include(a => a.Answers)
                          .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct) ?? throw AppException.NotFound("Attempt");
        var now = clock.GetUtcNow();
        if (attempt.Status == AttemptStatus.InProgress && IsPastGrace(attempt, now))
        {
            await FinalizeAsync(userId, attemptId, ct);
            return await GetAsync(userId, attemptId, ct);
        }

        var answers = attempt.Answers.ToDictionary(a => a.TestQuestionId);
        var snapshot = ReadSnapshot(attempt);
        var questions = snapshot.Select((q, i) =>
        {
            answers.TryGetValue(q.TestQuestionId, out var a);
            return new PlayerQuestionDto(i + 1, q.TestQuestionId, q.SectionTitle, q.SubjectTitle, q.Type.ToString(), lang.Pick(q.StemEn, q.StemHi), q.ImageUrl,
                q.Options.Select(o => new PlayerOptionDto(o.Id, lang.Pick(o.TextEn, o.TextHi))).ToList(),
                q.Marks, q.NegativeMarks, Scoring.Parse(a?.ResponseJson), a?.MarkedForReview ?? false, a?.TimeSpentSeconds ?? 0);
        }).ToList();

        var remaining = attempt.Status == AttemptStatus.InProgress ? Math.Max(0, (int)(attempt.ExpiresAt - now).TotalSeconds) : 0;
        return new AttemptDto(attempt.Id, attempt.TestId, lang.Pick(attempt.Test!.TitleEn, attempt.Test.TitleHi),
            lang.PickOpt(attempt.Test.InstructionsEn, attempt.Test.InstructionsHi), attempt.Status.ToString(),
            attempt.StartedAt, attempt.ExpiresAt, remaining, now, questions);
    }

    public async Task SaveAnswerAsync(Guid userId, Guid attemptId, SaveAnswerRequest req, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var attempt = await LockAsync(userId, attemptId, ct);
        var now = clock.GetUtcNow();
        if (attempt.Status != AttemptStatus.InProgress)
            throw AppException.Conflict("attempt_closed", "This attempt has already been submitted.");
        if (IsPastGrace(attempt, now))
            throw AppException.Conflict("attempt_expired", "Time is up for this attempt.");

        var q = ReadSnapshot(attempt).FirstOrDefault(x => x.TestQuestionId == req.TestQuestionId) ?? throw AppException.NotFound("Question");
        if (req.Response is not null) Scoring.Validate(q, req.Response);

        var answer = await db.AttemptAnswers.FirstOrDefaultAsync(a => a.AttemptId == attemptId && a.TestQuestionId == req.TestQuestionId, ct);
        if (answer is null)
        {
            answer = new AttemptAnswer { AttemptId = attemptId, TestQuestionId = req.TestQuestionId };
            db.AttemptAnswers.Add(answer);
        }
        answer.ResponseJson = req.Response is null || req.Response.IsEmpty ? null : Scoring.Serialize(req.Response);
        answer.MarkedForReview = req.MarkedForReview;
        answer.TimeSpentSeconds = Math.Clamp(req.TimeSpentSeconds, 0, 24 * 3600);
        answer.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Idempotent: submitting an already-submitted attempt returns without changing anything.</summary>
    public async Task SubmitAsync(Guid userId, Guid attemptId, CancellationToken ct) => await FinalizeAsync(userId, attemptId, ct);

    private async Task FinalizeAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var attempt = await LockAsync(userId, attemptId, ct);
        if (attempt.Status != AttemptStatus.InProgress)
        {
            await tx.RollbackAsync(ct);
            return;
        }
        var now = clock.GetUtcNow();
        var auto = IsPastGrace(attempt, now);
        var snapshot = ReadSnapshot(attempt);
        var saved = await db.AttemptAnswers.Where(a => a.AttemptId == attemptId).ToListAsync(ct);
        var responses = saved.ToDictionary(a => a.TestQuestionId, a => Scoring.Parse(a.ResponseJson));
        var times = saved.ToDictionary(a => a.TestQuestionId, a => a.TimeSpentSeconds);

        var summary = Scoring.Summarize(snapshot, responses, out var perQuestion);
        attempt.Status = auto ? AttemptStatus.AutoSubmitted : AttemptStatus.Submitted;
        attempt.SubmittedAt = auto ? attempt.ExpiresAt : now;
        attempt.Score = summary.Score;
        attempt.MaxScore = summary.MaxScore;
        attempt.CorrectCount = summary.Correct;
        attempt.IncorrectCount = summary.Incorrect;
        attempt.PartialCount = summary.Partial;
        attempt.SkippedCount = summary.Skipped;

        db.AttemptQuestionResults.AddRange(perQuestion.Select(x => new AttemptQuestionResult
        {
            AttemptId = attemptId, TestQuestionId = x.Q.TestQuestionId, QuestionId = x.Q.QuestionId,
            SubjectId = x.Q.SubjectId, ChapterId = x.Q.ChapterId, TopicId = x.Q.TopicId, Difficulty = x.Q.Difficulty,
            Outcome = x.R.Outcome, MarksAwarded = x.R.Marks, TimeSpentSeconds = times.GetValueOrDefault(x.Q.TestQuestionId),
        }));
        db.AttemptEvents.Add(new AttemptEvent { AttemptId = attemptId, EventType = auto ? "auto_submitted" : "submitted", OccurredAt = now });
        db.LearningEvents.Add(new LearningEvent { UserId = userId, EventType = LearningEventTypes.TestSubmitted, EntityId = attempt.TestId, OccurredAt = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Row lock serialises save/submit for one attempt, so an answer can never land after scoring.</summary>
    private async Task<TestAttempt> LockAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"TestAttempts\" WHERE \"Id\" = {attemptId} FOR UPDATE", ct);
        var attempt = await db.TestAttempts.FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct) ?? throw AppException.NotFound("Attempt");
        await db.Entry(attempt).ReloadAsync(ct);
        return attempt;
    }

    private bool IsPastGrace(TestAttempt a, DateTimeOffset now) => now > a.ExpiresAt.AddSeconds(options.SubmissionGraceSeconds);

    public static List<QuestionSnapshot> ReadSnapshot(TestAttempt a) => JsonSerializer.Deserialize<List<QuestionSnapshot>>(a.SnapshotJson, Scoring.Json)!;

    private async Task<List<QuestionSnapshot>> BuildSnapshotAsync(Test test, int seed, CancellationToken ct)
    {
        var sections = await db.TestSections.AsNoTracking().Where(s => s.TestId == test.Id).ToDictionaryAsync(s => s.Id, ct);
        var rows = await db.TestQuestions.AsNoTracking().Where(tq => tq.TestId == test.Id)
            .Include(tq => tq.Question!).ThenInclude(q => q.Options)
            .Include(tq => tq.Question!).ThenInclude(q => q.Topic!).ThenInclude(t => t.Chapter!).ThenInclude(c => c.Subject)
            .AsSplitQuery().ToListAsync(ct);

        var groups = rows.GroupBy(r => r.SectionId)
            .OrderBy(g => g.Key is { } id && sections.TryGetValue(id, out var s) ? s.SortOrder : int.MaxValue);
        var result = new List<QuestionSnapshot>();
        var salt = 0;
        foreach (var g in groups)
        {
            var ordered = g.OrderBy(r => r.SortOrder).ToList();
            if (test.ShuffleQuestions) ordered = Scoring.Shuffle(ordered, seed + salt++);
            foreach (var tq in ordered)
            {
                var q = tq.Question!;
                var opts = q.Options.OrderBy(o => o.SortOrder).ToList();
                if (test.ShuffleOptions && q.Type != QuestionType.TrueFalse) opts = Scoring.Shuffle(opts, seed ^ q.Id.GetHashCode());
                TestSection? section = tq.SectionId is { } sid ? sections.GetValueOrDefault(sid) : null;
                var ch = q.Topic!.Chapter!;
                result.Add(new QuestionSnapshot(tq.Id, q.Id, q.Version, section?.Id, section?.TitleEn,
                    ch.SubjectId, ch.Subject!.TitleEn, ch.Id, ch.TitleEn, q.TopicId, q.Topic.TitleEn,
                    q.Type, q.Difficulty, q.StemEn, q.StemHi, q.ImageUrl,
                    opts.Select(o => new OptionSnapshot(o.Id, o.TextEn, o.TextHi, o.IsCorrect)).ToList(),
                    q.NumericAnswer, q.NumericTolerance, tq.Marks, tq.NegativeMarks, q.AllowPartialMarking, q.ExplanationEn, q.ExplanationHi));
            }
        }
        return result;
    }
}

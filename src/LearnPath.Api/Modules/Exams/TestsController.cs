using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Exams;

public record TestSummaryDto(Guid Id, string Title, string Type, int DurationMinutes, int QuestionCount, decimal MaxMarks, bool IsFree, bool Locked,
    int AttemptsUsed, int? MaxAttempts, Guid? InProgressAttemptId, Guid? LastAttemptId, DateTimeOffset? AvailableFrom, DateTimeOffset? AvailableUntil);
public record TestDetailDto(TestSummaryDto Summary, string? Instructions, List<string> Sections, decimal? NegativeMarksPerWrong);
public record AttemptHistoryDto(Guid AttemptId, Guid TestId, string TestTitle, int AttemptNumber, string Status, decimal? Score, decimal MaxScore, DateTimeOffset StartedAt, DateTimeOffset? SubmittedAt);

[ApiController]
[Authorize]
[Route("api/v1")]
public class TestsController(AppDbContext db, ExamService exams, ResultsService results, EntitlementService entitlements, RequestLanguage lang, TimeProvider clock) : ControllerBase
{
    [HttpGet("tests")]
    public async Task<List<TestSummaryDto>> List([FromQuery] Guid? courseId, [FromQuery] Guid? examId, [FromQuery] TestType? type, CancellationToken ct)
    {
        var userId = User.UserId();
        var q = db.Tests.AsNoTracking().Where(t => t.Status == PublishStatus.Published);
        if (courseId is not null) q = q.Where(t => t.CourseId == courseId);
        if (examId is not null) q = q.Where(t => t.ExamId == examId);
        if (type is not null) q = q.Where(t => t.Type == type);
        var tests = await q.OrderBy(t => t.CreatedAt).Take(200).ToListAsync(ct);
        return await Summaries(userId, tests, ct);
    }

    [HttpGet("tests/{testId:guid}")]
    public async Task<TestDetailDto> Detail(Guid testId, CancellationToken ct)
    {
        var test = await db.Tests.AsNoTracking().Include(t => t.Sections).Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == testId && t.Status == PublishStatus.Published, ct) ?? throw AppException.NotFound("Test");
        var summary = (await Summaries(User.UserId(), [test], ct))[0];
        var negatives = test.Questions.Select(x => x.NegativeMarks).Distinct().ToList();
        return new TestDetailDto(summary, lang.PickOpt(test.InstructionsEn, test.InstructionsHi),
            test.Sections.OrderBy(s => s.SortOrder).Select(s => lang.Pick(s.TitleEn, s.TitleHi)).ToList(), negatives.Count == 1 ? negatives[0] : null);
    }

    [HttpPost("tests/{testId:guid}/attempts")]
    public Task<AttemptDto> Start(Guid testId, CancellationToken ct) => exams.StartAsync(User.UserId(), testId, ct);

    [HttpGet("attempts/{attemptId:guid}")]
    public Task<AttemptDto> Get(Guid attemptId, CancellationToken ct) => exams.GetAsync(User.UserId(), attemptId, ct);

    [HttpPut("attempts/{attemptId:guid}/answers")]
    public async Task<IActionResult> Save(Guid attemptId, SaveAnswerRequest req, CancellationToken ct)
    {
        await exams.SaveAnswerAsync(User.UserId(), attemptId, req, ct);
        return Ok(new { savedAt = clock.GetUtcNow() });
    }

    [HttpPost("attempts/{attemptId:guid}/submit")]
    public async Task<ResultDto> Submit(Guid attemptId, CancellationToken ct)
    {
        await exams.SubmitAsync(User.UserId(), attemptId, ct);
        return await results.GetAsync(User.UserId(), attemptId, ct);
    }

    [HttpGet("attempts/{attemptId:guid}/results")]
    public Task<ResultDto> Results(Guid attemptId, CancellationToken ct) => results.GetAsync(User.UserId(), attemptId, ct);

    [HttpGet("attempts")]
    public async Task<List<AttemptHistoryDto>> History([FromQuery] Guid? testId, CancellationToken ct)
    {
        var userId = User.UserId();
        var rows = await db.TestAttempts.AsNoTracking().Include(a => a.Test)
            .Where(a => a.UserId == userId && (testId == null || a.TestId == testId))
            .OrderByDescending(a => a.StartedAt).Take(100).ToListAsync(ct);
        return rows.Select(a => new AttemptHistoryDto(a.Id, a.TestId, lang.Pick(a.Test!.TitleEn, a.Test.TitleHi), a.AttemptNumber, a.Status.ToString(), a.Score, a.MaxScore, a.StartedAt, a.SubmittedAt)).ToList();
    }

    private async Task<List<TestSummaryDto>> Summaries(Guid userId, List<Test> tests, CancellationToken ct)
    {
        var ids = tests.Select(t => t.Id).ToList();
        var stats = await db.TestQuestions.Where(x => ids.Contains(x.TestId)).GroupBy(x => x.TestId)
            .Select(g => new { g.Key, Count = g.Count(), Max = g.Sum(x => x.Marks) }).ToDictionaryAsync(x => x.Key, ct);
        var attempts = await db.TestAttempts.AsNoTracking().Where(a => a.UserId == userId && ids.Contains(a.TestId)).ToListAsync(ct);
        var courses = await entitlements.ActiveCourseIdsAsync(userId, ct);
        var freeCourses = (await db.Courses.Where(c => c.IsFree).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        return tests.Select(t =>
        {
            var mine = attempts.Where(a => a.TestId == t.Id).OrderBy(a => a.AttemptNumber).ToList();
            var locked = !t.IsFree && (t.CourseId is not { } cid || (!courses.Contains(cid) && !freeCourses.Contains(cid)));
            stats.TryGetValue(t.Id, out var s);
            return new TestSummaryDto(t.Id, lang.Pick(t.TitleEn, t.TitleHi), t.Type.ToString(), t.DurationMinutes, s?.Count ?? 0, s?.Max ?? 0, t.IsFree, locked,
                mine.Count, t.MaxAttempts, mine.FirstOrDefault(a => a.Status == AttemptStatus.InProgress)?.Id,
                mine.LastOrDefault(a => a.Status != AttemptStatus.InProgress)?.Id, t.AvailableFrom, t.AvailableUntil);
        }).ToList();
    }
}

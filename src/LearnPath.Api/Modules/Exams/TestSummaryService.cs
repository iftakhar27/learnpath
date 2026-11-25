using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Exams;

/// <summary>Test cards as students see them: size, lock, attempts used and where to resume. Shared by the test list and custom tests.</summary>
public class TestSummaryService(AppDbContext db, EntitlementService entitlements, RequestLanguage lang)
{
    public async Task<List<TestSummaryDto>> SummariesAsync(Guid userId, List<Test> tests, CancellationToken ct)
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

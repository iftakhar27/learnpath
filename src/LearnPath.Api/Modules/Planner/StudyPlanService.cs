using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Analytics;
using LearnPath.Api.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Planner;

public record PlanTaskDto(Guid Id, string Type, Guid EntityId, string Title, string Reason, int EstimatedMinutes, string Status);
public record StudyPlanDto(DateOnly Date, int DailyStudyMinutes, int PlannedMinutes, List<PlanTaskDto> Tasks, string Basis);
public record PlanDayDto(DateOnly Date, int Planned, int Completed);

/// <summary>
/// Transparent rule-based planner (no AI). Rules, in priority order:
///  1. Finish any unfinished test.
///  2. Practise up to two weak topics (accuracy below threshold with enough answers).
///  3. Continue the next unfinished lessons in enrolled courses, in syllabus order, until the daily time budget is used.
///  4. Suggest an untried test when 3+ resources were completed in the last 7 days.
/// </summary>
public class StudyPlanService(AppDbContext db, LearnerStatsService stats, EntitlementService entitlements, TimeProvider clock, RequestLanguage lang)
{
    public const int PracticeTaskMinutes = 20;
    public const string Basis = "Suggested from your syllabus progress, recent accuracy and daily study time.";

    public async Task<StudyPlanDto> TodayAsync(Guid userId, CancellationToken ct)
    {
        var today = India.Today(clock);
        var tasks = await db.StudyPlanTasks.AsNoTracking().Where(t => t.UserId == userId && t.PlanDate == today).OrderBy(t => t.SortOrder).ToListAsync(ct);
        if (tasks.Count == 0)
        {
            tasks = await GenerateAsync(userId, today, ct);
            db.StudyPlanTasks.AddRange(tasks);
            await db.SaveChangesAsync(ct);
        }
        var minutes = await db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.DailyStudyMinutes).FirstOrDefaultAsync(ct);
        return new StudyPlanDto(today, minutes, tasks.Sum(t => t.EstimatedMinutes), tasks.Select(ToDto).ToList(), Basis);
    }

    public async Task<StudyPlanDto> RegenerateAsync(Guid userId, CancellationToken ct)
    {
        var today = India.Today(clock);
        await db.StudyPlanTasks.Where(t => t.UserId == userId && t.PlanDate == today && t.Status == PlanTaskStatus.Pending).ExecuteDeleteAsync(ct);
        var kept = await db.StudyPlanTasks.Where(t => t.UserId == userId && t.PlanDate == today).ToListAsync(ct);
        var fresh = (await GenerateAsync(userId, today, ct)).Where(n => kept.All(k => k.EntityId != n.EntityId)).ToList();
        var order = kept.Count;
        fresh.ForEach(t => t.SortOrder = order++);
        db.StudyPlanTasks.AddRange(fresh);
        await db.SaveChangesAsync(ct);
        return await TodayAsync(userId, ct);
    }

    public async Task<PlanTaskDto> SetStatusAsync(Guid userId, Guid taskId, PlanTaskStatus status, CancellationToken ct)
    {
        var task = await db.StudyPlanTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, ct) ?? throw AppException.NotFound("Task");
        var now = clock.GetUtcNow();
        if (status == PlanTaskStatus.Completed && task.Status != PlanTaskStatus.Completed)
        {
            task.CompletedAt = now;
            db.LearningEvents.Add(new LearningEvent { UserId = userId, EventType = LearningEventTypes.PlanTaskCompleted, EntityId = task.Id, OccurredAt = now });
        }
        task.Status = status;
        await db.SaveChangesAsync(ct);
        return ToDto(task);
    }

    public async Task<List<PlanDayDto>> HistoryAsync(Guid userId, int days, CancellationToken ct)
    {
        var from = India.Today(clock).AddDays(-(days - 1));
        var rows = await db.StudyPlanTasks.AsNoTracking().Where(t => t.UserId == userId && t.PlanDate >= from)
            .GroupBy(t => t.PlanDate).Select(g => new PlanDayDto(g.Key, g.Count(), g.Count(t => t.Status == PlanTaskStatus.Completed))).ToListAsync(ct);
        return rows.OrderBy(r => r.Date).ToList();
    }

    private async Task<List<StudyPlanTask>> GenerateAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        var budget = await db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.DailyStudyMinutes).FirstOrDefaultAsync(ct);
        if (budget <= 0) budget = 120;
        var tasks = new List<StudyPlanTask>();
        var used = 0;
        void Add(PlanTaskType type, Guid id, string title, string reason, int minutes)
        {
            tasks.Add(new StudyPlanTask { UserId = userId, PlanDate = date, Type = type, EntityId = id, Title = title, Reason = reason, EstimatedMinutes = minutes, SortOrder = tasks.Count });
            used += minutes;
        }

        // 1. Unfinished tests
        var open = await db.TestAttempts.AsNoTracking().Include(a => a.Test).Where(a => a.UserId == userId && a.Status == AttemptStatus.InProgress).ToListAsync(ct);
        foreach (var a in open)
            Add(PlanTaskType.Test, a.TestId, $"Finish: {lang.Pick(a.Test!.TitleEn, a.Test.TitleHi)}", "You have an unfinished test.", a.Test.DurationMinutes);

        // 2. Weak topics
        foreach (var w in await stats.WeakTopicsAsync(userId, 2, ct))
            Add(PlanTaskType.Practice, w.TopicId, $"Practise: {w.Topic}", $"Accuracy {w.Accuracy}% on {w.Attempted} questions.", PracticeTaskMinutes);

        // 3. Next lessons in syllabus order
        var courseIds = await entitlements.ActiveCourseIdsAsync(userId, ct);
        if (courseIds.Count > 0)
        {
            var completed = (await db.ResourceProgress.Where(p => p.UserId == userId && p.Completed).Select(p => p.ResourceId).ToListAsync(ct)).ToHashSet();
            var next = await db.LearningResources.AsNoTracking()
                .Where(r => r.Status == PublishStatus.Published && courseIds.Contains(r.Topic!.Chapter!.Subject!.CourseId))
                .OrderBy(r => r.Topic!.Chapter!.Subject!.CourseId).ThenBy(r => r.Topic!.Chapter!.Subject!.SortOrder)
                .ThenBy(r => r.Topic!.Chapter!.SortOrder).ThenBy(r => r.Topic!.SortOrder).ThenBy(r => r.SortOrder)
                .Select(r => new { r.Id, r.TitleEn, r.TitleHi, r.Type, r.DurationSeconds, TopicEn = r.Topic!.TitleEn })
                .ToListAsync(ct);
            foreach (var r in next.Where(r => !completed.Contains(r.Id)))
            {
                var minutes = r.Type == ResourceType.Video ? Math.Max(5, (int)Math.Ceiling((r.DurationSeconds ?? 600) / 60.0)) : 15;
                if (used + minutes > budget && tasks.Any(t => t.Type == PlanTaskType.Learn)) break;
                Add(PlanTaskType.Learn, r.Id, lang.Pick(r.TitleEn, r.TitleHi), $"Next in your syllabus ({r.TopicEn}).", minutes);
            }
        }

        // 4. Test suggestion after steady progress
        var weekAgo = clock.GetUtcNow().AddDays(-7);
        var recentCompletions = await db.ResourceProgress.CountAsync(p => p.UserId == userId && p.Completed && p.CompletedAt >= weekAgo, ct);
        if (recentCompletions >= 3 && courseIds.Count > 0 && tasks.All(t => t.Type != PlanTaskType.Test))
        {
            var tried = db.TestAttempts.Where(a => a.UserId == userId).Select(a => a.TestId);
            var test = await db.Tests.AsNoTracking()
                .Where(t => t.Status == PublishStatus.Published && t.CourseId != null && courseIds.Contains(t.CourseId.Value) && !tried.Contains(t.Id))
                .OrderBy(t => t.DurationMinutes).FirstOrDefaultAsync(ct);
            if (test is not null)
                Add(PlanTaskType.Test, test.Id, $"Take: {lang.Pick(test.TitleEn, test.TitleHi)}", $"You completed {recentCompletions} lessons this week — check your understanding.", test.DurationMinutes);
        }
        return tasks;
    }

    private static PlanTaskDto ToDto(StudyPlanTask t) => new(t.Id, t.Type.ToString(), t.EntityId, t.Title, t.Reason, t.EstimatedMinutes, t.Status.ToString());
}

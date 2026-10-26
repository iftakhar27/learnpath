using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Catalog;
using LearnPath.Api.Modules.Planner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Analytics;

public record ContinueItemDto(Guid ResourceId, Guid TopicId, string Title, string Topic, string Type, int PositionSeconds, int? DurationSeconds);
public record EnrolledCourseDto(Guid CourseId, string Title, DateTimeOffset? ExpiresAt, int CompletionPercent);
public record RecentResultDto(Guid AttemptId, string Test, decimal Score, decimal MaxScore, DateTimeOffset SubmittedAt);
public record DashboardDto(string FullName, string? ExamName, int? TargetYear, int? DaysToExam, int StreakDays, int QuestionsAnsweredThisWeek,
    int LessonsCompletedThisWeek, List<ContinueItemDto> ContinueLearning, List<EnrolledCourseDto> Courses, List<RecentResultDto> RecentResults,
    List<TopicAccuracyDto> WeakTopics, StudyPlanDto TodayPlan, int PendingTests, int UnreadNotifications);

[ApiController]
[Authorize]
[Route("api/v1/student")]
public class StudentController(AppDbContext db, LearnerStatsService stats, StudyPlanService planner, RequestLanguage lang, TimeProvider clock) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<DashboardDto> Dashboard(CancellationToken ct)
    {
        var userId = User.UserId();
        var now = clock.GetUtcNow();
        var weekAgo = now.AddDays(-7);
        var user = await db.Users.AsNoTracking().Include(u => u.Profile).FirstAsync(u => u.Id == userId, ct);
        var exam = user.Profile?.TargetExamId is { } eid ? await db.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eid, ct) : null;
        int? daysToExam = exam?.NextExamDate is { } d ? Math.Max(0, d.DayNumber - India.Today(clock).DayNumber) : null;

        var cont = await (from p in db.ResourceProgress.AsNoTracking()
                          join r in db.LearningResources on p.ResourceId equals r.Id
                          join t in db.Topics on r.TopicId equals t.Id
                          where p.UserId == userId && !p.Completed
                          orderby p.UpdatedAt descending
                          select new { r.Id, TopicId = t.Id, r.TitleEn, r.TitleHi, TEn = t.TitleEn, THi = t.TitleHi, r.Type, p.PositionSeconds, r.DurationSeconds })
            .Take(3).ToListAsync(ct);

        var enrollments = await db.Enrollments.AsNoTracking().Include(e => e.Course)
            .Where(e => e.UserId == userId && (e.ExpiresAt == null || e.ExpiresAt > now)).ToListAsync(ct);
        var courseIds = enrollments.Select(e => e.CourseId).ToList();
        var totals = await db.LearningResources.Where(r => r.Status == PublishStatus.Published && courseIds.Contains(r.Topic!.Chapter!.Subject!.CourseId))
            .GroupBy(r => r.Topic!.Chapter!.Subject!.CourseId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var done = await (from p in db.ResourceProgress
                          join r in db.LearningResources on p.ResourceId equals r.Id
                          where p.UserId == userId && p.Completed && courseIds.Contains(r.Topic!.Chapter!.Subject!.CourseId)
                          group p by r.Topic!.Chapter!.Subject!.CourseId into g
                          select new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var recent = await db.TestAttempts.AsNoTracking().Include(a => a.Test)
            .Where(a => a.UserId == userId && a.Status != AttemptStatus.InProgress).OrderByDescending(a => a.SubmittedAt).Take(3).ToListAsync(ct);

        return new DashboardDto(user.FullName, exam is null ? null : lang.Pick(exam.NameEn, exam.NameHi), user.Profile?.TargetYear, daysToExam,
            await stats.StreakAsync(userId, ct),
            await db.LearningEvents.CountAsync(e => e.UserId == userId && e.EventType == LearningEventTypes.QuestionAnswered && e.OccurredAt >= weekAgo, ct),
            await db.LearningEvents.CountAsync(e => e.UserId == userId && e.EventType == LearningEventTypes.ResourceCompleted && e.OccurredAt >= weekAgo, ct),
            cont.Select(c => new ContinueItemDto(c.Id, c.TopicId, lang.Pick(c.TitleEn, c.TitleHi), lang.Pick(c.TEn, c.THi), c.Type.ToString(), c.PositionSeconds, c.DurationSeconds)).ToList(),
            enrollments.Select(e => new EnrolledCourseDto(e.CourseId, lang.Pick(e.Course!.TitleEn, e.Course.TitleHi), e.ExpiresAt,
                totals.GetValueOrDefault(e.CourseId) is var t and > 0 ? (int)Math.Round(100.0 * done.GetValueOrDefault(e.CourseId) / t) : 0)).ToList(),
            recent.Select(a => new RecentResultDto(a.Id, lang.Pick(a.Test!.TitleEn, a.Test.TitleHi), a.Score ?? 0, a.MaxScore, a.SubmittedAt!.Value)).ToList(),
            await stats.WeakTopicsAsync(userId, 3, ct),
            await planner.TodayAsync(userId, ct),
            await db.TestAttempts.CountAsync(a => a.UserId == userId && a.Status == AttemptStatus.InProgress, ct),
            await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct));
    }

    [HttpGet("analytics")]
    public Task<StudentAnalyticsDto> Analytics(CancellationToken ct) => stats.AnalyticsAsync(User.UserId(), ct);

    [HttpGet("study-plan")]
    public Task<StudyPlanDto> StudyPlan(CancellationToken ct) => planner.TodayAsync(User.UserId(), ct);

    [HttpPost("study-plan/regenerate")]
    public Task<StudyPlanDto> Regenerate(CancellationToken ct) => planner.RegenerateAsync(User.UserId(), ct);

    [HttpGet("study-plan/history")]
    public Task<List<PlanDayDto>> PlanHistory([FromQuery] int days = 7, CancellationToken ct = default) => planner.HistoryAsync(User.UserId(), Math.Clamp(days, 1, 60), ct);

    [HttpPost("study-plan/tasks/{taskId:guid}/complete")]
    public Task<PlanTaskDto> CompleteTask(Guid taskId, CancellationToken ct) => planner.SetStatusAsync(User.UserId(), taskId, PlanTaskStatus.Completed, ct);

    [HttpPost("study-plan/tasks/{taskId:guid}/skip")]
    public Task<PlanTaskDto> SkipTask(Guid taskId, CancellationToken ct) => planner.SetStatusAsync(User.UserId(), taskId, PlanTaskStatus.Skipped, ct);
}

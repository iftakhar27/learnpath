using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Practice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Catalog;

public record ExamDto(Guid Id, string Code, string Name, DateOnly? NextExamDate);
/// <summary>
/// <c>LessonCount</c> and <c>TestCount</c> are the course's published lessons and tests; a paid course with neither can't be
/// bought yet ("classes start soon").
/// </summary>
public record CourseSummaryDto(Guid Id, string Slug, Guid ExamId, string Title, string? Description, string? ClassLevel, bool IsFree, long PriceInPaise, int? AccessDays, bool Enrolled,
    int LessonCount, int TestCount);
public record ResourceDto(Guid Id, string Type, string Title, string Language, int? DurationSeconds, bool IsFreePreview, bool Locked, bool Completed, int PositionSeconds);
public record TopicDto(Guid Id, string Title, int EstimatedMinutes, bool Completed, int ResourceCount, int CompletedResources);
public record ChapterDto(Guid Id, string? Unit, string Title, List<TopicDto> Topics, int CompletionPercent);
public record SubjectDto(Guid Id, string Title, List<ChapterDto> Chapters, int CompletionPercent);
public record CourseDetailDto(CourseSummaryDto Course, bool HasAccess, int CompletionPercent, List<SubjectDto> Subjects);
public record TopicDetailDto(Guid Id, Guid CourseId, string Title, string? Objectives, int EstimatedMinutes, string Chapter, string Subject, bool HasAccess,
    List<ResourceDto> Resources, int PracticeQuestionCount, Guid? PreviousTopicId, Guid? NextTopicId, TopicQuizSummaryDto? Quiz);
/// <summary><c>Url</c> is the web player (videos) or download link (notes); <c>StreamUrl</c> is HLS for native video players.</summary>
public record PlaybackDto(Guid ResourceId, string Type, string Url, string? StreamUrl, DateTimeOffset ExpiresAt, int ResumeAtSeconds,
    string Title, Guid TopicId, string Topic, int? DurationSeconds, bool Completed);

[ApiController]
[Route("api/v1")]
public class CatalogController(AppDbContext db, RequestLanguage lang, EntitlementService entitlements, TimeProvider clock,
    IVideoUrlSigner videoSigner, IDocumentUrlSigner docSigner, SignedFiles signed, IFileStore files) : ControllerBase
{
    [HttpGet("exams")]
    [AllowAnonymous]
    public async Task<List<ExamDto>> Exams(CancellationToken ct) =>
        (await db.Exams.AsNoTracking().Where(e => e.IsActive).OrderBy(e => e.SortOrder).ToListAsync(ct))
        .Select(e => new ExamDto(e.Id, e.Code, lang.Pick(e.NameEn, e.NameHi), e.NextExamDate)).ToList();

    [HttpGet("courses")]
    [AllowAnonymous]
    public async Task<PagedResult<CourseSummaryDto>> Courses([FromQuery] Guid? examId, [FromQuery] string? q, [FromQuery] bool? free,
        [FromQuery] string? sort, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, pageSize);
        var now = clock.GetUtcNow();
        var query = db.Courses.AsNoTracking().Where(c => c.Status == PublishStatus.Published && (c.PublishAt == null || c.PublishAt <= now));
        if (examId is not null) query = query.Where(c => c.ExamId == examId);
        if (free is not null) query = query.Where(c => c.IsFree == free);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(c => EF.Functions.ILike(c.TitleEn, $"%{q}%") || (c.TitleHi != null && EF.Functions.ILike(c.TitleHi, $"%{q}%")));
        query = sort switch
        {
            "price_asc" => query.OrderBy(c => c.PriceInPaise),
            "price_desc" => query.OrderByDescending(c => c.PriceInPaise),
            "newest" => query.OrderByDescending(c => c.CreatedAt),
            // By class (8, 9, 10, 11, 12, then batches without one), then title.
            _ => query.OrderBy(c => c.ClassLevel == null).ThenBy(c => c.ClassLevel!.Length).ThenBy(c => c.ClassLevel).ThenBy(c => c.TitleEn),
        };
        var total = await query.CountAsync(ct);
        var items = await query.Skip((p - 1) * size).Take(size).ToListAsync(ct);
        var enrolled = User.Identity?.IsAuthenticated == true ? await entitlements.ActiveCourseIdsAsync(User.UserId(), ct) : [];
        var ids = items.Select(c => c.Id).ToList();
        var lessons = await LessonCountsAsync(db, ids, ct);
        var tests = await TestCountsAsync(db, ids, ct);
        return new(items.Select(c => ToSummary(c, enrolled.Contains(c.Id), lessons.GetValueOrDefault(c.Id), tests.GetValueOrDefault(c.Id))).ToList(), p, size, total);
    }

    [HttpGet("courses/{courseId:guid}/syllabus")]
    [AllowAnonymous]
    public async Task<CourseDetailDto> Syllabus(Guid courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking()
            .Include(c => c.Subjects.OrderBy(s => s.SortOrder)).ThenInclude(s => s.Chapters.OrderBy(ch => ch.SortOrder))
            .ThenInclude(ch => ch.Topics.OrderBy(t => t.SortOrder)).ThenInclude(t => t.Resources.Where(r => r.Status == PublishStatus.Published))
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == courseId && c.Status == PublishStatus.Published, ct) ?? throw AppException.NotFound("Course");

        var userId = User.Identity?.IsAuthenticated == true ? User.UserId() : (Guid?)null;
        var hasAccess = userId is not null && await entitlements.HasCourseAccessAsync(userId.Value, courseId, ct);
        var enrolled = userId is not null && await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId, ct);
        var completed = userId is null ? [] : (await db.ResourceProgress.Where(r => r.UserId == userId && r.Completed).Select(r => r.ResourceId).ToListAsync(ct)).ToHashSet();

        int Pct(int done, int total) => total == 0 ? 0 : (int)Math.Round(100.0 * done / total);
        var subjects = course.Subjects.Select(s =>
        {
            var chapters = s.Chapters.Select(ch =>
            {
                var topics = ch.Topics.Select(t =>
                {
                    var done = t.Resources.Count(r => completed.Contains(r.Id));
                    return new TopicDto(t.Id, lang.Pick(t.TitleEn, t.TitleHi), t.EstimatedMinutes, t.Resources.Count > 0 && done == t.Resources.Count, t.Resources.Count, done);
                }).ToList();
                return new ChapterDto(ch.Id, ch.UnitTitleEn, lang.Pick(ch.TitleEn, ch.TitleHi), topics, Pct(topics.Sum(t => t.CompletedResources), topics.Sum(t => t.ResourceCount)));
            }).ToList();
            var all = chapters.SelectMany(c => c.Topics).ToList();
            return new SubjectDto(s.Id, lang.Pick(s.TitleEn, s.TitleHi), chapters, Pct(all.Sum(t => t.CompletedResources), all.Sum(t => t.ResourceCount)));
        }).ToList();
        var allTopics = subjects.SelectMany(s => s.Chapters).SelectMany(c => c.Topics).ToList();
        var lessonCount = course.Subjects.SelectMany(s => s.Chapters).SelectMany(ch => ch.Topics).Sum(t => t.Resources.Count);
        var testCount = (await TestCountsAsync(db, [courseId], ct)).GetValueOrDefault(courseId);
        return new CourseDetailDto(ToSummary(course, enrolled, lessonCount, testCount), hasAccess, Pct(allTopics.Sum(t => t.CompletedResources), allTopics.Sum(t => t.ResourceCount)), subjects);
    }

    [HttpPost("courses/{courseId:guid}/enroll")]
    [Authorize]
    public async Task<IActionResult> EnrollFree(Guid courseId, CancellationToken ct)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId && c.Status == PublishStatus.Published, ct) ?? throw AppException.NotFound("Course");
        if (!course.IsFree) throw AppException.PaymentRequired("This is a paid course. Purchase it to enroll.");
        await entitlements.GrantAsync(User.UserId(), course, EnrollmentSource.Free, null, ct);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("topics/{topicId:guid}")]
    [Authorize]
    public async Task<TopicDetailDto> Topic(Guid topicId, CancellationToken ct)
    {
        var userId = User.UserId();
        var topic = await db.Topics.AsNoTracking()
            .Include(t => t.Chapter!).ThenInclude(c => c.Subject)
            .Include(t => t.Resources.Where(r => r.Status == PublishStatus.Published).OrderBy(r => r.SortOrder))
            .FirstOrDefaultAsync(t => t.Id == topicId, ct) ?? throw AppException.NotFound("Topic");
        var courseId = topic.Chapter!.Subject!.CourseId;
        var hasAccess = await entitlements.HasCourseAccessAsync(userId, courseId, ct);
        var progress = await db.ResourceProgress.AsNoTracking().Where(p => p.UserId == userId && p.TopicId == topicId).ToDictionaryAsync(p => p.ResourceId, ct);
        var practiceCount = await db.Questions.CountAsync(q => q.TopicId == topicId && q.Status == PublishStatus.Published, ct);

        // Ordered topic list across the course for previous/next navigation.
        var order = await db.Topics.AsNoTracking().Where(t => t.Chapter!.Subject!.CourseId == courseId)
            .OrderBy(t => t.Chapter!.Subject!.SortOrder).ThenBy(t => t.Chapter!.SortOrder).ThenBy(t => t.SortOrder)
            .Select(t => t.Id).ToListAsync(ct);
        var idx = order.IndexOf(topicId);

        var resources = topic.Resources.Select(r =>
        {
            progress.TryGetValue(r.Id, out var pr);
            return new ResourceDto(r.Id, r.Type.ToString(), lang.Pick(r.TitleEn, r.TitleHi), r.Language, r.DurationSeconds, r.IsFreePreview,
                !hasAccess && !r.IsFreePreview, pr?.Completed ?? false, pr?.PositionSeconds ?? 0);
        }).ToList();

        return new TopicDetailDto(topic.Id, courseId, lang.Pick(topic.TitleEn, topic.TitleHi), lang.PickOpt(topic.ObjectivesEn, topic.ObjectivesHi),
            topic.EstimatedMinutes, lang.Pick(topic.Chapter.TitleEn, topic.Chapter.TitleHi), lang.Pick(topic.Chapter.Subject.TitleEn, topic.Chapter.Subject.TitleHi),
            hasAccess, resources, practiceCount, idx > 0 ? order[idx - 1] : null, idx >= 0 && idx < order.Count - 1 ? order[idx + 1] : null,
            await TopicQuizzes.SummaryAsync(db, userId, topic, ct));
    }

    /// <summary>Returns a short-lived signed URL. Paid resources require an active entitlement.</summary>
    [HttpGet("resources/{resourceId:guid}/playback")]
    [Authorize]
    public async Task<PlaybackDto> Playback(Guid resourceId, CancellationToken ct)
    {
        var userId = User.UserId();
        var r = await db.LearningResources.AsNoTracking().Include(x => x.Topic!).ThenInclude(t => t.Chapter!).ThenInclude(c => c.Subject)
            .FirstOrDefaultAsync(x => x.Id == resourceId && x.Status == PublishStatus.Published, ct) ?? throw AppException.NotFound("Resource");
        await entitlements.EnsureResourceAccessAsync(userId, r, r.Topic!.Chapter!.Subject!.CourseId, ct);

        var validFor = TimeSpan.FromHours(2);
        // Uploaded notes: a signed link relative to the API (the website serves it through its own server, the app adds
        // the API's address). Otherwise the external file host's signed URL.
        var url = r.Type == ResourceType.Video
            ? videoSigner.GetPlaybackUrl(r.VideoProviderId ?? throw AppException.NotFound("Video"), userId, validFor)
            : r.FileId is not null
                ? $"/api/v1/resources/{r.Id}/file?{signed.Sign("resource", r.Id, validFor).Query}"
                : docSigner.GetDownloadUrl(r.DocumentStorageKey ?? throw AppException.NotFound("Document"), validFor);
        var stream = r.Type == ResourceType.Video ? videoSigner.GetStreamUrl(r.VideoProviderId!, validFor) : null;
        var progress = await db.ResourceProgress.Where(p => p.UserId == userId && p.ResourceId == resourceId)
            .Select(p => new { p.PositionSeconds, p.Completed }).FirstOrDefaultAsync(ct);
        return new PlaybackDto(r.Id, r.Type.ToString(), url, stream, clock.GetUtcNow().Add(validFor), progress?.PositionSeconds ?? 0,
            lang.Pick(r.TitleEn, r.TitleHi), r.TopicId, lang.Pick(r.Topic.TitleEn, r.Topic.TitleHi), r.DurationSeconds, progress?.Completed ?? false);
    }

    /// <summary>Uploaded notes, for a signed link from <see cref="Playback"/> (access was checked there).</summary>
    [HttpGet("resources/{resourceId:guid}/file")]
    [AllowAnonymous]
    public async Task<IActionResult> ResourceFile(Guid resourceId, [FromQuery] long expires, [FromQuery] string? sig, CancellationToken ct)
    {
        signed.EnsureValid("resource", resourceId, expires, sig);
        var r = await db.LearningResources.AsNoTracking().FirstOrDefaultAsync(x => x.Id == resourceId && x.Status == PublishStatus.Published, ct)
                ?? throw AppException.NotFound("Resource");
        var file = r.FileId is { } fid ? await files.GetAsync(fid, ct) : null;
        if (file is null) throw AppException.NotFound("File");
        return SignedFiles.Inline(Response, file, file.FileName, TimeSpan.FromMinutes(10));
    }

    private CourseSummaryDto ToSummary(Course c, bool enrolled, int lessons, int tests) =>
        new(c.Id, c.Slug, c.ExamId, lang.Pick(c.TitleEn, c.TitleHi), lang.PickOpt(c.DescriptionEn, c.DescriptionHi), c.ClassLevel, c.IsFree, c.PriceInPaise, c.AccessDays, enrolled, lessons, tests);

    /// <summary>Whether a course has anything to study yet: a published lesson or a published test (test series).</summary>
    public static async Task<bool> HasContentAsync(AppDbContext db, Guid courseId, CancellationToken ct) =>
        (await LessonCountsAsync(db, [courseId], ct)).Count > 0 || (await TestCountsAsync(db, [courseId], ct)).Count > 0;

    /// <summary>Published tests per course (courses without any are left out); students' own custom tests don't count.</summary>
    public static async Task<Dictionary<Guid, int>> TestCountsAsync(AppDbContext db, List<Guid> courseIds, CancellationToken ct) =>
        await db.Tests.Where(t => t.CourseId != null && courseIds.Contains(t.CourseId.Value) && t.Status == PublishStatus.Published && t.OwnerUserId == null)
            .GroupBy(t => t.CourseId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    /// <summary>Published lessons per course (courses without any are left out).</summary>
    public static async Task<Dictionary<Guid, int>> LessonCountsAsync(AppDbContext db, List<Guid> courseIds, CancellationToken ct) =>
        await (from r in db.LearningResources
               join t in db.Topics on r.TopicId equals t.Id
               join ch in db.Chapters on t.ChapterId equals ch.Id
               join s in db.Subjects on ch.SubjectId equals s.Id
               where r.Status == PublishStatus.Published && courseIds.Contains(s.CourseId)
               group r by s.CourseId into g
               select new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
}

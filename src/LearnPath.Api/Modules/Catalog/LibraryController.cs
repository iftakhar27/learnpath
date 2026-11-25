using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Catalog;

/// <summary>A tile of the subject grid: chapters in the NCERT book, how many have lessons, and the student's progress.</summary>
public record LibraryTileDto(string ClassLevel, string Subject, string Title, int Chapters, int ReadyChapters, int ProgressPercent);
public record LibraryResumeDto(Guid ChapterId, string Title, string ClassLevel, string Subject);
public record LibraryDto(List<LibraryTileDto> Tiles, LibraryResumeDto? Resume);
/// <summary><c>Ready</c>: some course teaches it; <c>Locked</c>: ready, but only in courses the student doesn't have (free previews aside).</summary>
public record LibraryChapterDto(Guid Id, int Number, string Title, int Topics, int Videos, int Notes, bool Ready, bool Locked, int ProgressPercent);
public record LibrarySubjectDto(string ClassLevel, string Subject, string Title, List<LibraryChapterDto> Chapters, int ProgressPercent);
public record LibraryTopicDto(Guid Id, string Title, Guid CourseId, string Course, bool HasAccess, List<ResourceDto> Resources);
public record LibraryChapterDetailDto(Guid Id, int Number, string Title, string ClassLevel, string Subject, string SubjectTitle,
    Guid? PreviousChapterId, Guid? NextChapterId, List<LibraryTopicDto> Topics, int PracticeQuestionCount, bool CanPractise, Guid? BuyCourseId);

/// <summary>
/// Browse by NCERT book: the subject grid (Physics, Chemistry, Biology × Class 11, 12), each book's chapter list, and a
/// chapter's lessons from every published course that teaches it. Open to visitors; progress and access need sign-in.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/library")]
public class LibraryController(AppDbContext db, EntitlementService entitlements, RequestLanguage lang, TimeProvider clock) : ControllerBase
{
    private string SubjectTitle(string subject) => subject switch
    {
        NcertSubjects.Physics => lang.Pick("Physics", "भौतिकी"),
        NcertSubjects.Chemistry => lang.Pick("Chemistry", "रसायन विज्ञान"),
        _ => lang.Pick("Biology", "जीव विज्ञान"),
    };

    private Guid? UserId => User.Identity?.IsAuthenticated == true ? User.UserId() : null;

    /// <summary>One published lesson of a course chapter linked to an NCERT chapter.</summary>
    private sealed record Lesson(Guid NcertId, Guid CourseId, Guid TopicId, Guid ResourceId, ResourceType Type, bool FreePreview);

    private async Task<List<Lesson>> LessonsAsync(IEnumerable<Guid>? ncertIds, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var ids = ncertIds?.ToList();
        return await (from r in db.LearningResources.AsNoTracking()
                      join t in db.Topics on r.TopicId equals t.Id
                      join ch in db.Chapters on t.ChapterId equals ch.Id
                      join s in db.Subjects on ch.SubjectId equals s.Id
                      join c in db.Courses on s.CourseId equals c.Id
                      where ch.NcertChapterId != null && r.Status == PublishStatus.Published
                            && c.Status == PublishStatus.Published && (c.PublishAt == null || c.PublishAt <= now)
                            && (ids == null || ids.Contains(ch.NcertChapterId!.Value))
                      select new Lesson(ch.NcertChapterId!.Value, c.Id, t.Id, r.Id, r.Type, r.IsFreePreview)).ToListAsync(ct);
    }

    /// <summary>Courses whose lessons the visitor may open: free courses for everyone, plus the student's enrollments.</summary>
    private async Task<HashSet<Guid>> AccessibleCoursesAsync(CancellationToken ct)
    {
        var free = await db.Courses.Where(c => c.IsFree && c.Status == PublishStatus.Published).Select(c => c.Id).ToListAsync(ct);
        var set = UserId is { } id ? await entitlements.ActiveCourseIdsAsync(id, ct) : [];
        set.UnionWith(free);
        return set;
    }

    private async Task<HashSet<Guid>> CompletedAsync(CancellationToken ct) => UserId is { } id
        ? (await db.ResourceProgress.Where(p => p.UserId == id && p.Completed).Select(p => p.ResourceId).ToListAsync(ct)).ToHashSet()
        : [];

    private static int Percent(IEnumerable<Lesson> lessons, HashSet<Guid> access, HashSet<Guid> done)
    {
        var open = lessons.Where(l => access.Contains(l.CourseId) || l.FreePreview).Select(l => l.ResourceId).Distinct().ToList();
        return open.Count == 0 ? 0 : (int)Math.Round(100.0 * open.Count(done.Contains) / open.Count);
    }

    [HttpGet]
    public async Task<LibraryDto> Grid(CancellationToken ct)
    {
        var ncert = await db.NcertChapters.AsNoTracking().ToListAsync(ct);
        var lessons = await LessonsAsync(null, ct);
        var access = await AccessibleCoursesAsync(ct);
        var done = await CompletedAsync(ct);
        var byChapter = lessons.ToLookup(l => l.NcertId);

        var tiles = (from cls in NcertSubjects.Classes
                     from subject in NcertSubjects.All
                     let book = ncert.Where(n => n.ClassLevel == cls && n.Subject == subject).ToList()
                     let bookLessons = book.SelectMany(n => byChapter[n.Id]).ToList()
                     select new LibraryTileDto(cls, subject, SubjectTitle(subject), book.Count, book.Count(n => byChapter[n.Id].Any()),
                         Percent(bookLessons, access, done))).ToList();

        LibraryResumeDto? resume = null;
        if (UserId is { } uid)
        {
            var last = await (from p in db.ResourceProgress.AsNoTracking()
                              join r in db.LearningResources on p.ResourceId equals r.Id
                              join t in db.Topics on r.TopicId equals t.Id
                              join ch in db.Chapters on t.ChapterId equals ch.Id
                              where p.UserId == uid && ch.NcertChapterId != null
                              orderby p.UpdatedAt descending
                              select ch.NcertChapterId).FirstOrDefaultAsync(ct);
            if (last is { } lastId && ncert.FirstOrDefault(n => n.Id == lastId) is { } n)
                resume = new LibraryResumeDto(n.Id, lang.Pick(n.TitleEn, n.TitleHi), n.ClassLevel, n.Subject);
        }
        return new LibraryDto(tiles, resume);
    }

    [HttpGet("{classLevel}/{subject}")]
    public async Task<LibrarySubjectDto> Book(string classLevel, string subject, CancellationToken ct)
    {
        var key = NcertSubjects.All.FirstOrDefault(s => s.Equals(subject, StringComparison.OrdinalIgnoreCase)) ?? throw AppException.NotFound("Subject");
        var book = await db.NcertChapters.AsNoTracking().Where(n => n.ClassLevel == classLevel && n.Subject == key).OrderBy(n => n.Number).ToListAsync(ct);
        if (book.Count == 0) throw AppException.NotFound("Subject");
        var lessons = await LessonsAsync(book.Select(n => n.Id), ct);
        var access = await AccessibleCoursesAsync(ct);
        var done = await CompletedAsync(ct);
        var byChapter = lessons.ToLookup(l => l.NcertId);

        var chapters = book.Select(n =>
        {
            var ls = byChapter[n.Id].ToList();
            var ready = ls.Count > 0;
            // Locked until a course that teaches it is owned (free-preview lessons inside stay playable).
            var open = ls.Any(l => access.Contains(l.CourseId));
            return new LibraryChapterDto(n.Id, n.Number, lang.Pick(n.TitleEn, n.TitleHi), ls.Select(l => l.TopicId).Distinct().Count(),
                ls.Count(l => l.Type == ResourceType.Video), ls.Count(l => l.Type == ResourceType.Document), ready, ready && !open, Percent(ls, access, done));
        }).ToList();
        return new LibrarySubjectDto(classLevel, key, SubjectTitle(key), chapters, Percent(lessons, access, done));
    }

    [HttpGet("chapters/{id:guid}")]
    public async Task<LibraryChapterDetailDto> Chapter(Guid id, CancellationToken ct)
    {
        var n = await db.NcertChapters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Chapter");
        var siblings = await db.NcertChapters.AsNoTracking().Where(x => x.ClassLevel == n.ClassLevel && x.Subject == n.Subject)
            .OrderBy(x => x.Number).Select(x => x.Id).ToListAsync(ct);
        var idx = siblings.IndexOf(id);
        var now = clock.GetUtcNow();
        var access = await AccessibleCoursesAsync(ct);

        // Every published course chapter that teaches it: courses the student has first, then free, then the rest.
        var courseChapters = await db.Chapters.AsNoTracking()
            .Include(ch => ch.Subject)
            .Include(ch => ch.Topics.OrderBy(t => t.SortOrder)).ThenInclude(t => t.Resources.Where(r => r.Status == PublishStatus.Published).OrderBy(r => r.SortOrder))
            .Where(ch => ch.NcertChapterId == id)
            .AsSplitQuery().ToListAsync(ct);
        var courseIds = courseChapters.Select(ch => ch.Subject!.CourseId).Distinct().ToList();
        var topicIds = courseChapters.SelectMany(ch => ch.Topics).Select(t => t.Id).ToList();
        var courses = await db.Courses.AsNoTracking()
            .Where(c => courseIds.Contains(c.Id) && c.Status == PublishStatus.Published && (c.PublishAt == null || c.PublishAt <= now))
            .ToDictionaryAsync(c => c.Id, ct);
        var progress = UserId is { } uid
            ? await db.ResourceProgress.AsNoTracking().Where(p => p.UserId == uid && topicIds.Contains(p.TopicId)).ToDictionaryAsync(p => p.ResourceId, ct)
            : [];

        var ordered = courseChapters.Where(ch => courses.ContainsKey(ch.Subject!.CourseId))
            .OrderByDescending(ch => access.Contains(ch.Subject!.CourseId)).ThenByDescending(ch => courses[ch.Subject!.CourseId].IsFree).ThenBy(ch => ch.SortOrder).ToList();
        var topics = ordered.SelectMany(ch =>
        {
            var course = courses[ch.Subject!.CourseId];
            var has = access.Contains(course.Id);
            return ch.Topics.Where(t => t.Resources.Count > 0).Select(t => new LibraryTopicDto(t.Id, lang.Pick(t.TitleEn, t.TitleHi), course.Id, lang.Pick(course.TitleEn, course.TitleHi), has,
                t.Resources.Select(r =>
                {
                    progress.TryGetValue(r.Id, out var p);
                    return new ResourceDto(r.Id, r.Type.ToString(), lang.Pick(r.TitleEn, r.TitleHi), r.Language, r.DurationSeconds, r.IsFreePreview,
                        !has && !r.IsFreePreview, p?.Completed ?? false, p?.PositionSeconds ?? 0);
                }).ToList()));
        }).ToList();

        var practiceTopicIds = ordered.Where(ch => access.Contains(ch.Subject!.CourseId)).SelectMany(ch => ch.Topics).Select(t => t.Id).ToList();
        var practice = practiceTopicIds.Count == 0 ? 0 : await db.Questions.CountAsync(q => practiceTopicIds.Contains(q.TopicId) && q.Status == PublishStatus.Published, ct);
        // Nothing open yet: point to the cheapest course that teaches it.
        var buy = practiceTopicIds.Count == 0 && ordered.Count > 0
            ? ordered.Select(ch => courses[ch.Subject!.CourseId]).OrderBy(c => c.PriceInPaise).First().Id
            : (Guid?)null;

        return new LibraryChapterDetailDto(n.Id, n.Number, lang.Pick(n.TitleEn, n.TitleHi), n.ClassLevel, n.Subject, SubjectTitle(n.Subject),
            idx > 0 ? siblings[idx - 1] : null, idx >= 0 && idx < siblings.Count - 1 ? siblings[idx + 1] : null, topics, practice, practiceTopicIds.Count > 0, buy);
    }
}

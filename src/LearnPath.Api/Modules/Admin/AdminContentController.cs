using System.ComponentModel.DataAnnotations;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Admin;

public static class Policies
{
    public const string ContentEdit = nameof(ContentEdit);       // Teacher, ContentAdmin, SuperAdmin
    public const string ContentPublish = nameof(ContentPublish); // ContentAdmin, SuperAdmin
    public const string SupportOps = nameof(SupportOps);         // Support, SuperAdmin
    public const string Finance = nameof(Finance);               // Finance, SuperAdmin
    public const string StaffRead = nameof(StaffRead);           // any staff role
    public const string SuperAdmin = nameof(SuperAdmin);
}

public record ExamUpsert([Required, MaxLength(32)] string Code, [Required, MaxLength(200)] string NameEn, string? NameHi, DateOnly? NextExamDate, bool IsActive = true, int SortOrder = 0);
public record CourseUpsert([Required] Guid ExamId, [Required, MaxLength(120), RegularExpression("^[a-z0-9-]+$")] string Slug, [Required, MaxLength(200)] string TitleEn,
    string? TitleHi, string? DescriptionEn, string? DescriptionHi, string? ClassLevel, string? Board, bool IsFree, [Range(0, 10_000_000_00)] long PriceInPaise,
    [Range(1, 3650)] int? AccessDays, DateTimeOffset? PublishAt);
public record NodeUpsert([Required] Guid ParentId, [Required, MaxLength(200)] string TitleEn, string? TitleHi, int SortOrder = 0, string? UnitTitleEn = null,
    string? ObjectivesEn = null, string? ObjectivesHi = null, [Range(0, 1000)] int EstimatedMinutes = 0);
public record ResourceUpsert([Required] Guid TopicId, [Required] ResourceType Type, [Required, MaxLength(200)] string TitleEn, string? TitleHi,
    [RegularExpression("^(en|hi)$")] string Language, int SortOrder, bool IsFreePreview, string? VideoProviderId, [Range(1, 36000)] int? DurationSeconds,
    string? DocumentStorageKey, bool AllowDownload);
public record StatusChange([Required] PublishStatus Status);
public record IdDto(Guid Id);

[ApiController]
[Authorize(Policy = Policies.ContentEdit)]
[Route("api/v1/admin")]
public class AdminContentController(AppDbContext db, AuditService audit, TimeProvider clock) : ControllerBase
{
    // ----- Exams -----
    [HttpGet("exams")]
    public Task<List<Exam>> Exams(CancellationToken ct) => db.Exams.AsNoTracking().OrderBy(e => e.SortOrder).ToListAsync(ct);

    [HttpPost("exams")]
    [Authorize(Policy = Policies.ContentPublish)]
    public async Task<IdDto> CreateExam(ExamUpsert req, CancellationToken ct)
    {
        if (await db.Exams.AnyAsync(e => e.Code == req.Code, ct)) throw AppException.Conflict("duplicate_code", "Exam code already exists.");
        var e = new Exam { Code = req.Code, NameEn = req.NameEn, NameHi = req.NameHi, NextExamDate = req.NextExamDate, IsActive = req.IsActive, SortOrder = req.SortOrder };
        db.Exams.Add(e);
        return await Save("exam.create", "Exam", e.Id, req, ct);
    }

    [HttpPut("exams/{id:guid}")]
    [Authorize(Policy = Policies.ContentPublish)]
    public async Task<IdDto> UpdateExam(Guid id, ExamUpsert req, CancellationToken ct)
    {
        var e = await db.Exams.FindAsync([id], ct) ?? throw AppException.NotFound("Exam");
        (e.Code, e.NameEn, e.NameHi, e.NextExamDate, e.IsActive, e.SortOrder) = (req.Code, req.NameEn, req.NameHi, req.NextExamDate, req.IsActive, req.SortOrder);
        return await Save("exam.update", "Exam", id, req, ct);
    }

    // ----- Courses -----
    [HttpGet("courses")]
    public async Task<object> Courses(CancellationToken ct) =>
        await db.Courses.AsNoTracking().OrderByDescending(c => c.UpdatedAt)
            .Select(c => new { c.Id, c.Slug, c.TitleEn, c.ExamId, c.IsFree, c.PriceInPaise, c.AccessDays, Status = c.Status.ToString(), c.PublishAt, c.UpdatedAt }).ToListAsync(ct);

    [HttpGet("courses/{id:guid}/tree")]
    public async Task<Course> CourseTree(Guid id, CancellationToken ct) =>
        await db.Courses.AsNoTracking().Include(c => c.Subjects.OrderBy(s => s.SortOrder)).ThenInclude(s => s.Chapters.OrderBy(x => x.SortOrder))
            .ThenInclude(ch => ch.Topics.OrderBy(t => t.SortOrder)).ThenInclude(t => t.Resources.OrderBy(r => r.SortOrder))
            .AsSplitQuery().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw AppException.NotFound("Course");

    [HttpPost("courses")]
    public async Task<IdDto> CreateCourse(CourseUpsert req, CancellationToken ct)
    {
        await ValidateCourse(req, null, ct);
        var now = clock.GetUtcNow();
        var c = new Course { CreatedAt = now };
        ApplyCourse(c, req, now);
        db.Courses.Add(c);
        return await Save("course.create", "Course", c.Id, req, ct);
    }

    [HttpPut("courses/{id:guid}")]
    public async Task<IdDto> UpdateCourse(Guid id, CourseUpsert req, CancellationToken ct)
    {
        var c = await db.Courses.FindAsync([id], ct) ?? throw AppException.NotFound("Course");
        await ValidateCourse(req, id, ct);
        ApplyCourse(c, req, clock.GetUtcNow());
        return await Save("course.update", "Course", id, req, ct);
    }

    [HttpPost("courses/{id:guid}/status")]
    [Authorize(Policy = Policies.ContentPublish)]
    public async Task<IdDto> CourseStatus(Guid id, StatusChange req, CancellationToken ct)
    {
        var c = await db.Courses.FindAsync([id], ct) ?? throw AppException.NotFound("Course");
        c.Status = req.Status;
        c.UpdatedAt = clock.GetUtcNow();
        return await Save("course.status", "Course", id, req, ct);
    }

    // ----- Subjects / chapters / topics -----
    [HttpPost("subjects")]
    public async Task<IdDto> CreateSubject(NodeUpsert req, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == req.ParentId, ct)) throw AppException.NotFound("Course");
        var s = new Subject { CourseId = req.ParentId, TitleEn = req.TitleEn, TitleHi = req.TitleHi, SortOrder = req.SortOrder };
        db.Subjects.Add(s);
        return await Save("subject.create", "Subject", s.Id, req, ct);
    }

    [HttpPut("subjects/{id:guid}")]
    public async Task<IdDto> UpdateSubject(Guid id, NodeUpsert req, CancellationToken ct)
    {
        var s = await db.Subjects.FindAsync([id], ct) ?? throw AppException.NotFound("Subject");
        (s.TitleEn, s.TitleHi, s.SortOrder) = (req.TitleEn, req.TitleHi, req.SortOrder);
        return await Save("subject.update", "Subject", id, req, ct);
    }

    [HttpPost("chapters")]
    public async Task<IdDto> CreateChapter(NodeUpsert req, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == req.ParentId, ct)) throw AppException.NotFound("Subject");
        var c = new Chapter { SubjectId = req.ParentId, TitleEn = req.TitleEn, TitleHi = req.TitleHi, SortOrder = req.SortOrder, UnitTitleEn = req.UnitTitleEn };
        db.Chapters.Add(c);
        return await Save("chapter.create", "Chapter", c.Id, req, ct);
    }

    [HttpPut("chapters/{id:guid}")]
    public async Task<IdDto> UpdateChapter(Guid id, NodeUpsert req, CancellationToken ct)
    {
        var c = await db.Chapters.FindAsync([id], ct) ?? throw AppException.NotFound("Chapter");
        (c.TitleEn, c.TitleHi, c.SortOrder, c.UnitTitleEn) = (req.TitleEn, req.TitleHi, req.SortOrder, req.UnitTitleEn);
        return await Save("chapter.update", "Chapter", id, req, ct);
    }

    [HttpPost("topics")]
    public async Task<IdDto> CreateTopic(NodeUpsert req, CancellationToken ct)
    {
        if (!await db.Chapters.AnyAsync(c => c.Id == req.ParentId, ct)) throw AppException.NotFound("Chapter");
        var t = new Topic { ChapterId = req.ParentId, TitleEn = req.TitleEn, TitleHi = req.TitleHi, SortOrder = req.SortOrder, ObjectivesEn = req.ObjectivesEn, ObjectivesHi = req.ObjectivesHi, EstimatedMinutes = req.EstimatedMinutes };
        db.Topics.Add(t);
        return await Save("topic.create", "Topic", t.Id, req, ct);
    }

    [HttpPut("topics/{id:guid}")]
    public async Task<IdDto> UpdateTopic(Guid id, NodeUpsert req, CancellationToken ct)
    {
        var t = await db.Topics.FindAsync([id], ct) ?? throw AppException.NotFound("Topic");
        (t.TitleEn, t.TitleHi, t.SortOrder, t.ObjectivesEn, t.ObjectivesHi, t.EstimatedMinutes) = (req.TitleEn, req.TitleHi, req.SortOrder, req.ObjectivesEn, req.ObjectivesHi, req.EstimatedMinutes);
        return await Save("topic.update", "Topic", id, req, ct);
    }

    // ----- Resources -----
    [HttpPost("resources")]
    public async Task<IdDto> CreateResource(ResourceUpsert req, CancellationToken ct)
    {
        if (!await db.Topics.AnyAsync(t => t.Id == req.TopicId, ct)) throw AppException.NotFound("Topic");
        ValidateResource(req);
        var r = new LearningResource();
        ApplyResource(r, req);
        db.LearningResources.Add(r);
        return await Save("resource.create", "LearningResource", r.Id, req, ct);
    }

    [HttpPut("resources/{id:guid}")]
    public async Task<IdDto> UpdateResource(Guid id, ResourceUpsert req, CancellationToken ct)
    {
        var r = await db.LearningResources.FindAsync([id], ct) ?? throw AppException.NotFound("Resource");
        ValidateResource(req);
        ApplyResource(r, req);
        r.Version++;
        return await Save("resource.update", "LearningResource", id, req, ct);
    }

    [HttpPost("resources/{id:guid}/status")]
    [Authorize(Policy = Policies.ContentPublish)]
    public async Task<IdDto> ResourceStatus(Guid id, StatusChange req, CancellationToken ct)
    {
        var r = await db.LearningResources.FindAsync([id], ct) ?? throw AppException.NotFound("Resource");
        r.Status = req.Status;
        r.UpdatedAt = clock.GetUtcNow();
        return await Save("resource.status", "LearningResource", id, req, ct);
    }

    private async Task<IdDto> Save(string action, string entity, Guid id, object data, CancellationToken ct)
    {
        audit.Record(User.UserId(), action, entity, id, data);
        await db.SaveChangesAsync(ct);
        return new IdDto(id);
    }

    private async Task ValidateCourse(CourseUpsert req, Guid? id, CancellationToken ct)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == req.ExamId, ct)) throw AppException.NotFound("Exam");
        if (await db.Courses.AnyAsync(c => c.Slug == req.Slug && c.Id != id, ct)) throw AppException.Conflict("duplicate_slug", "Another course uses this slug.");
        if (!req.IsFree && req.PriceInPaise < 100) throw AppException.BadRequest("invalid_price", "Paid courses must cost at least ₹1.");
    }

    private static void ApplyCourse(Course c, CourseUpsert r, DateTimeOffset now)
    {
        (c.ExamId, c.Slug, c.TitleEn, c.TitleHi, c.DescriptionEn, c.DescriptionHi, c.ClassLevel, c.Board) = (r.ExamId, r.Slug, r.TitleEn, r.TitleHi, r.DescriptionEn, r.DescriptionHi, r.ClassLevel, r.Board);
        (c.IsFree, c.PriceInPaise, c.AccessDays, c.PublishAt, c.UpdatedAt) = (r.IsFree, r.IsFree ? 0 : r.PriceInPaise, r.AccessDays, r.PublishAt, now);
    }

    private static void ValidateResource(ResourceUpsert r)
    {
        if (r.Type == ResourceType.Video && (string.IsNullOrWhiteSpace(r.VideoProviderId) || r.DurationSeconds is null))
            throw AppException.BadRequest("invalid_resource", "Videos need a provider video ID and duration.");
        if (r.Type == ResourceType.Document && string.IsNullOrWhiteSpace(r.DocumentStorageKey))
            throw AppException.BadRequest("invalid_resource", "Documents need a storage key.");
    }

    private void ApplyResource(LearningResource x, ResourceUpsert r)
    {
        (x.TopicId, x.Type, x.TitleEn, x.TitleHi, x.Language, x.SortOrder, x.IsFreePreview) = (r.TopicId, r.Type, r.TitleEn, r.TitleHi, r.Language ?? "en", r.SortOrder, r.IsFreePreview);
        (x.VideoProviderId, x.DurationSeconds, x.DocumentStorageKey, x.AllowDownload, x.UpdatedAt) = (r.VideoProviderId, r.DurationSeconds, r.DocumentStorageKey, r.AllowDownload, clock.GetUtcNow());
    }
}

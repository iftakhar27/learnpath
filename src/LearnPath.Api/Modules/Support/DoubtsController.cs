using System.ComponentModel.DataAnnotations;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Support;

/// <summary>Ask a teacher. TopicId, ResourceId or QuestionId link the doubt to what the student was studying (one is enough).</summary>
public record AskDoubtRequest([Required, MinLength(5), MaxLength(DoubtRules.MaxLength)] string Text, Guid? TopicId, Guid? ResourceId, Guid? QuestionId);
public record DoubtReplyRequest([Required, MinLength(1), MaxLength(DoubtRules.MaxLength)] string Text);

/// <summary>What the doubt is about, for showing it next to the question.</summary>
public record DoubtContextDto(Guid? CourseId, string? Course, Guid? TopicId, string? Topic, Guid? ResourceId, string? Resource, Guid? QuestionId, string? Question);
public record DoubtSummaryDto(Guid Id, string Text, string Status, DoubtContextDto Context, int Replies, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public record DoubtMessageDto(Guid Id, bool FromStaff, string Author, string Text, DateTimeOffset CreatedAt);
public record DoubtDto(Guid Id, string Text, string Status, DoubtContextDto Context, List<DoubtMessageDto> Messages, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public static class DoubtRules
{
    public const int MaxLength = 2000;
    /// <summary>New doubts a student may ask per day, so a few students can't swamp the teachers.</summary>
    public const int PerDay = 10;
}

[ApiController]
[Authorize]
[Route("api/v1/doubts")]
public class DoubtsController(AppDbContext db, EntitlementService entitlements, RequestLanguage lang, TimeProvider clock) : ControllerBase
{
    [HttpPost]
    public async Task<DoubtDto> Ask(AskDoubtRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var now = clock.GetUtcNow();
        if (await db.Doubts.CountAsync(d => d.UserId == userId && d.CreatedAt > now.AddDays(-1), ct) >= DoubtRules.PerDay)
            throw new AppException(429, "doubt_limit", $"You can ask up to {DoubtRules.PerDay} doubts a day. Please try again tomorrow.");

        // Work out the topic (and course) from whichever link was given; paid topics need access to the course.
        Guid? topicId = req.TopicId, resourceId = req.ResourceId, questionId = req.QuestionId;
        if (questionId is not null)
            topicId = await db.Questions.Where(q => q.Id == questionId && q.Status == PublishStatus.Published).Select(q => (Guid?)q.TopicId).FirstOrDefaultAsync(ct)
                      ?? throw AppException.NotFound("Question");
        if (resourceId is not null)
        {
            var resTopic = await db.LearningResources.Where(r => r.Id == resourceId && r.Status == PublishStatus.Published).Select(r => (Guid?)r.TopicId).FirstOrDefaultAsync(ct)
                           ?? throw AppException.NotFound("Lesson");
            if (topicId is not null && topicId != resTopic) throw AppException.BadRequest("mismatch", "The lesson and question belong to different topics.");
            topicId = resTopic;
        }
        Guid? courseId = null;
        if (topicId is not null)
        {
            courseId = await db.Topics.Where(t => t.Id == topicId).Select(t => (Guid?)t.Chapter!.Subject!.CourseId).FirstOrDefaultAsync(ct) ?? throw AppException.NotFound("Topic");
            if (!await entitlements.HasCourseAccessAsync(userId, courseId.Value, ct))
                throw AppException.PaymentRequired("Buy this course to ask doubts about it.");
        }

        var doubt = new Doubt
        {
            UserId = userId, CourseId = courseId, TopicId = topicId, ResourceId = resourceId, QuestionId = questionId,
            Text = req.Text.Trim(), CreatedAt = now, UpdatedAt = now,
        };
        db.Doubts.Add(doubt);
        await db.SaveChangesAsync(ct);
        return await GetAsync(doubt.Id, userId, ct);
    }

    [HttpGet]
    public async Task<PagedResult<DoubtSummaryDto>> Mine([FromQuery] DoubtStatus? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, pageSize);
        var userId = User.UserId();
        var q = db.Doubts.AsNoTracking().Where(d => d.UserId == userId && (status == null || d.Status == status));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(d => d.UpdatedAt).Skip((p - 1) * size).Take(size)
            .Select(d => new { Doubt = d, Replies = d.Messages.Count(m => m.FromStaff) }).ToListAsync(ct);
        var contexts = await DoubtContexts.LoadAsync(db, lang, rows.Select(r => r.Doubt).ToList(), ct);
        return new(rows.Select(r => new DoubtSummaryDto(r.Doubt.Id, r.Doubt.Text, r.Doubt.Status.ToString(), contexts[r.Doubt.Id], r.Replies,
            r.Doubt.CreatedAt, r.Doubt.UpdatedAt)).ToList(), p, size, total);
    }

    [HttpGet("{id:guid}")]
    public Task<DoubtDto> Get(Guid id, CancellationToken ct) => GetAsync(id, User.UserId(), ct);

    /// <summary>A follow-up from the student; the doubt goes back to the teachers.</summary>
    [HttpPost("{id:guid}/messages")]
    public async Task<DoubtDto> FollowUp(Guid id, DoubtReplyRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var doubt = await db.Doubts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct) ?? throw AppException.NotFound("Doubt");
        var now = clock.GetUtcNow();
        db.DoubtMessages.Add(new DoubtMessage { DoubtId = id, AuthorId = userId, FromStaff = false, Text = req.Text.Trim(), CreatedAt = now });
        (doubt.Status, doubt.UpdatedAt) = (DoubtStatus.Open, now);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, userId, ct);
    }

    /// <summary>The student marks the doubt as solved.</summary>
    [HttpPost("{id:guid}/resolve")]
    public async Task<DoubtDto> Resolve(Guid id, CancellationToken ct)
    {
        var userId = User.UserId();
        var doubt = await db.Doubts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct) ?? throw AppException.NotFound("Doubt");
        (doubt.Status, doubt.UpdatedAt) = (DoubtStatus.Resolved, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, userId, ct);
    }

    private async Task<DoubtDto> GetAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var doubt = await db.Doubts.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, ct) ?? throw AppException.NotFound("Doubt");
        var context = (await DoubtContexts.LoadAsync(db, lang, [doubt], ct))[id];
        var messages = await DoubtContexts.MessagesAsync(db, id, ct);
        return new DoubtDto(doubt.Id, doubt.Text, doubt.Status.ToString(), context, messages, doubt.CreatedAt, doubt.UpdatedAt);
    }
}

/// <summary>Shared by the student and staff views.</summary>
public static class DoubtContexts
{
    public static async Task<Dictionary<Guid, DoubtContextDto>> LoadAsync(AppDbContext db, RequestLanguage lang, IReadOnlyCollection<Doubt> doubts, CancellationToken ct)
    {
        var courseIds = doubts.Where(d => d.CourseId != null).Select(d => d.CourseId!.Value).Distinct().ToList();
        var topicIds = doubts.Where(d => d.TopicId != null).Select(d => d.TopicId!.Value).Distinct().ToList();
        var resourceIds = doubts.Where(d => d.ResourceId != null).Select(d => d.ResourceId!.Value).Distinct().ToList();
        var questionIds = doubts.Where(d => d.QuestionId != null).Select(d => d.QuestionId!.Value).Distinct().ToList();
        var courses = await db.Courses.AsNoTracking().Where(c => courseIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => lang.Pick(c.TitleEn, c.TitleHi), ct);
        var topics = await db.Topics.AsNoTracking().Where(t => topicIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => lang.Pick(t.TitleEn, t.TitleHi), ct);
        var resources = await db.LearningResources.AsNoTracking().Where(r => resourceIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => lang.Pick(r.TitleEn, r.TitleHi), ct);
        var questions = await db.Questions.AsNoTracking().Where(q => questionIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, q => lang.Pick(q.StemEn, q.StemHi), ct);
        string? Name(Dictionary<Guid, string> names, Guid? id) => id is { } x && names.TryGetValue(x, out var n) ? n : null;
        return doubts.ToDictionary(d => d.Id, d => new DoubtContextDto(d.CourseId, Name(courses, d.CourseId), d.TopicId, Name(topics, d.TopicId),
            d.ResourceId, Name(resources, d.ResourceId), d.QuestionId, Name(questions, d.QuestionId)));
    }

    /// <summary>Replies in order, with the author's name (clients show the student's own messages as "You").</summary>
    public static async Task<List<DoubtMessageDto>> MessagesAsync(AppDbContext db, Guid doubtId, CancellationToken ct) =>
        await (from m in db.DoubtMessages.AsNoTracking()
               join u in db.Users on m.AuthorId equals u.Id
               where m.DoubtId == doubtId
               orderby m.CreatedAt
               select new DoubtMessageDto(m.Id, m.FromStaff, u.FullName, m.Text, m.CreatedAt)).ToListAsync(ct);
}

using System.ComponentModel.DataAnnotations;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Learning;

public record ProgressRequest([Required] Guid ResourceId, [Range(0, 86400)] int PositionSeconds, bool MarkCompleted = false);
public record ProgressDto(Guid ResourceId, int PositionSeconds, bool Completed);
public record BookmarkRequest([Required] BookmarkType EntityType, [Required] Guid EntityId, [MaxLength(1000)] string? Note);
public record BookmarkDto(Guid Id, string EntityType, Guid EntityId, string Title, string? Note, DateTimeOffset CreatedAt);

[ApiController]
[Authorize]
[Route("api/v1/learning")]
public class LearningController(AppDbContext db, EntitlementService entitlements, TimeProvider clock, RequestLanguage lang) : ControllerBase
{
    /// <summary>A video counts as complete once 90% has been watched; documents complete when the client marks them.</summary>
    public const double VideoCompletionRatio = 0.9;

    [HttpPost("progress")]
    public async Task<ProgressDto> SaveProgress(ProgressRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var resource = await db.LearningResources.AsNoTracking().Include(r => r.Topic!).ThenInclude(t => t.Chapter!).ThenInclude(c => c.Subject)
            .FirstOrDefaultAsync(r => r.Id == req.ResourceId && r.Status == PublishStatus.Published, ct) ?? throw AppException.NotFound("Resource");
        await entitlements.EnsureResourceAccessAsync(userId, resource, resource.Topic!.Chapter!.Subject!.CourseId, ct);

        var now = clock.GetUtcNow();
        var p = await db.ResourceProgress.FirstOrDefaultAsync(x => x.UserId == userId && x.ResourceId == req.ResourceId, ct);
        if (p is null)
        {
            p = new ResourceProgress { UserId = userId, ResourceId = resource.Id, TopicId = resource.TopicId };
            db.ResourceProgress.Add(p);
            db.LearningEvents.Add(new LearningEvent { UserId = userId, EventType = LearningEventTypes.ResourceStarted, EntityId = resource.Id, OccurredAt = now });
        }
        var position = resource.DurationSeconds is { } d ? Math.Min(req.PositionSeconds, d) : req.PositionSeconds;
        p.PositionSeconds = position;
        p.MaxPositionSeconds = Math.Max(p.MaxPositionSeconds, position);
        p.UpdatedAt = now;

        var reached = resource.Type == ResourceType.Video && resource.DurationSeconds is > 0
                      && p.MaxPositionSeconds >= resource.DurationSeconds * VideoCompletionRatio;
        if (!p.Completed && (reached || req.MarkCompleted))
        {
            p.Completed = true;
            p.CompletedAt = now;
            db.LearningEvents.Add(new LearningEvent { UserId = userId, EventType = LearningEventTypes.ResourceCompleted, EntityId = resource.Id, OccurredAt = now });
        }
        await db.SaveChangesAsync(ct);
        return new ProgressDto(p.ResourceId, p.PositionSeconds, p.Completed);
    }

    [HttpGet("history")]
    public async Task<object> History([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var (pg, size) = Paging.Normalize(page, pageSize);
        var userId = User.UserId();
        var q = from p in db.ResourceProgress.AsNoTracking()
                join r in db.LearningResources on p.ResourceId equals r.Id
                join t in db.Topics on r.TopicId equals t.Id
                where p.UserId == userId
                orderby p.UpdatedAt descending
                select new { p.ResourceId, r.TitleEn, r.TitleHi, TopicId = t.Id, TopicEn = t.TitleEn, TopicHi = t.TitleHi, Type = r.Type, p.PositionSeconds, r.DurationSeconds, p.Completed, p.UpdatedAt };
        var total = await q.CountAsync(ct);
        var rows = await q.Skip((pg - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<object>(rows.Select(x => (object)new
        {
            x.ResourceId, Title = lang.Pick(x.TitleEn, x.TitleHi), x.TopicId, Topic = lang.Pick(x.TopicEn, x.TopicHi),
            Type = x.Type.ToString(), x.PositionSeconds, x.DurationSeconds, x.Completed, x.UpdatedAt,
        }).ToList(), pg, size, total);
    }

    [HttpGet("bookmarks")]
    public async Task<List<BookmarkDto>> Bookmarks([FromQuery] BookmarkType? type, CancellationToken ct)
    {
        var userId = User.UserId();
        var list = await db.Bookmarks.AsNoTracking().Where(b => b.UserId == userId && (type == null || b.EntityType == type))
            .OrderByDescending(b => b.CreatedAt).Take(200).ToListAsync(ct);
        var qIds = list.Where(b => b.EntityType == BookmarkType.Question).Select(b => b.EntityId).ToList();
        var rIds = list.Where(b => b.EntityType == BookmarkType.Resource).Select(b => b.EntityId).ToList();
        var tIds = list.Where(b => b.EntityType == BookmarkType.Topic).Select(b => b.EntityId).ToList();
        var titles = new Dictionary<Guid, string>();
        foreach (var x in await db.Questions.Where(q => qIds.Contains(q.Id)).Select(q => new { q.Id, q.StemEn, q.StemHi }).ToListAsync(ct))
            titles[x.Id] = Truncate(lang.Pick(x.StemEn, x.StemHi));
        foreach (var x in await db.LearningResources.Where(r => rIds.Contains(r.Id)).Select(r => new { r.Id, r.TitleEn, r.TitleHi }).ToListAsync(ct))
            titles[x.Id] = lang.Pick(x.TitleEn, x.TitleHi);
        foreach (var x in await db.Topics.Where(t => tIds.Contains(t.Id)).Select(t => new { t.Id, t.TitleEn, t.TitleHi }).ToListAsync(ct))
            titles[x.Id] = lang.Pick(x.TitleEn, x.TitleHi);
        return list.Where(b => titles.ContainsKey(b.EntityId))
            .Select(b => new BookmarkDto(b.Id, b.EntityType.ToString(), b.EntityId, titles[b.EntityId], b.Note, b.CreatedAt)).ToList();
    }

    [HttpPost("bookmarks")]
    public async Task<IActionResult> AddBookmark(BookmarkRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var exists = req.EntityType switch
        {
            BookmarkType.Question => await db.Questions.AnyAsync(q => q.Id == req.EntityId && q.Status == PublishStatus.Published, ct),
            BookmarkType.Resource => await db.LearningResources.AnyAsync(r => r.Id == req.EntityId, ct),
            _ => await db.Topics.AnyAsync(t => t.Id == req.EntityId, ct),
        };
        if (!exists) throw AppException.NotFound(req.EntityType.ToString());
        var b = await db.Bookmarks.FirstOrDefaultAsync(x => x.UserId == userId && x.EntityType == req.EntityType && x.EntityId == req.EntityId, ct);
        if (b is null) db.Bookmarks.Add(new Bookmark { UserId = userId, EntityType = req.EntityType, EntityId = req.EntityId, Note = req.Note, CreatedAt = clock.GetUtcNow() });
        else b.Note = req.Note;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("bookmarks/{id:guid}")]
    public async Task<IActionResult> RemoveBookmark(Guid id, CancellationToken ct)
    {
        var userId = User.UserId();
        await db.Bookmarks.Where(b => b.Id == id && b.UserId == userId).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    private static string Truncate(string s) => s.Length <= 120 ? s : s[..117] + "...";
}

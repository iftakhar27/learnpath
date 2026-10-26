using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Search;

public record SearchHitDto(string Kind, Guid Id, string Title, string? Context);

/// <summary>Simple ILIKE search for the MVP. Swap the implementation for Postgres FTS or Meilisearch later without changing the contract.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/search")]
public class SearchController(AppDbContext db, RequestLanguage lang) : ControllerBase
{
    [HttpGet]
    public async Task<List<SearchHitDto>> Search([FromQuery] string q, [FromQuery] string? kind, CancellationToken ct)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return [];
        var pattern = "%" + q.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        var hits = new List<SearchHitDto>();
        bool Want(string k) => kind is null || kind == k;

        if (Want("course"))
            hits.AddRange((await db.Courses.AsNoTracking().Where(c => c.Status == PublishStatus.Published && (EF.Functions.ILike(c.TitleEn, pattern) || EF.Functions.ILike(c.TitleHi ?? "", pattern)))
                .Take(10).ToListAsync(ct)).Select(c => new SearchHitDto("course", c.Id, lang.Pick(c.TitleEn, c.TitleHi), null)));
        if (Want("topic"))
            hits.AddRange((await db.Topics.AsNoTracking().Include(t => t.Chapter)
                .Where(t => db.Courses.Any(c => c.Id == t.Chapter!.Subject!.CourseId && c.Status == PublishStatus.Published) && (EF.Functions.ILike(t.TitleEn, pattern) || EF.Functions.ILike(t.TitleHi ?? "", pattern)))
                .Take(15).ToListAsync(ct)).Select(t => new SearchHitDto("topic", t.Id, lang.Pick(t.TitleEn, t.TitleHi), lang.Pick(t.Chapter!.TitleEn, t.Chapter.TitleHi))));
        if (Want("chapter"))
            hits.AddRange((await db.Chapters.AsNoTracking().Include(c => c.Subject)
                .Where(ch => db.Courses.Any(c => c.Id == ch.Subject!.CourseId && c.Status == PublishStatus.Published))
                .Where(ch => EF.Functions.ILike(ch.TitleEn, pattern) || EF.Functions.ILike(ch.TitleHi ?? "", pattern)).Take(10).ToListAsync(ct))
                .Select(ch => new SearchHitDto("chapter", ch.Id, lang.Pick(ch.TitleEn, ch.TitleHi), lang.Pick(ch.Subject!.TitleEn, ch.Subject.TitleHi))));
        if (Want("resource"))
            hits.AddRange((await db.LearningResources.AsNoTracking().Include(r => r.Topic)
                .Where(r => r.Status == PublishStatus.Published && db.Courses.Any(c => c.Id == r.Topic!.Chapter!.Subject!.CourseId && c.Status == PublishStatus.Published))
                .Where(r => (EF.Functions.ILike(r.TitleEn, pattern) || EF.Functions.ILike(r.TitleHi ?? "", pattern))).Take(15).ToListAsync(ct))
                .Select(r => new SearchHitDto(r.Type == ResourceType.Video ? "video" : "notes", r.Id, lang.Pick(r.TitleEn, r.TitleHi), lang.Pick(r.Topic!.TitleEn, r.Topic.TitleHi))));
        if (Want("test"))
            hits.AddRange((await db.Tests.AsNoTracking().Where(t => t.Status == PublishStatus.Published && (EF.Functions.ILike(t.TitleEn, pattern) || EF.Functions.ILike(t.TitleHi ?? "", pattern)))
                .Take(10).ToListAsync(ct)).Select(t => new SearchHitDto("test", t.Id, lang.Pick(t.TitleEn, t.TitleHi), t.Type.ToString())));
        return hits;
    }
}

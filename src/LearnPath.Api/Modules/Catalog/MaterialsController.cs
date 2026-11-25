using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Catalog;

/// <summary><c>Format</c>: "PDF", "Image" or "Link" (opens another site).</summary>
public record MaterialDto(Guid Id, string Title, string? Description, string Type, string? Subject, string? ClassLevel, string Format, long SizeBytes, int Downloads);
/// <summary>Where to get it: a link valid for a few minutes (uploaded files; <c>Url</c> is relative to the API), or the external page.</summary>
public record MaterialDownloadDto(string Url, bool External, string? FileName, DateTimeOffset? ExpiresAt);

/// <summary>
/// Free study material: anyone can see the list; downloading needs a (free) account. A download hands out a signed
/// link valid for 10 minutes, so it opens in any browser or PDF viewer without the app's sign-in token.
/// </summary>
[ApiController]
[Route("api/v1/materials")]
public class MaterialsController(AppDbContext db, IFileStore files, SignedFiles signed, RequestLanguage lang) : ControllerBase
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(10);

    private const string Scope = "material";

    internal static string Format(StudyMaterial m) => m.ExternalUrl is not null ? "Link" : m.ContentType == "application/pdf" ? "PDF" : "Image";

    [HttpGet]
    [AllowAnonymous]
    public async Task<List<MaterialDto>> List([FromQuery] string? subject, [FromQuery] string? classLevel, [FromQuery] MaterialType? type, CancellationToken ct)
    {
        var q = db.StudyMaterials.AsNoTracking().Where(m => m.Status == PublishStatus.Published);
        if (!string.IsNullOrWhiteSpace(subject))
            q = subject.Equals("general", StringComparison.OrdinalIgnoreCase) ? q.Where(m => m.Subject == null) : q.Where(m => m.Subject == subject);
        if (!string.IsNullOrWhiteSpace(classLevel)) q = q.Where(m => m.ClassLevel == classLevel || m.ClassLevel == null);
        if (type is not null) q = q.Where(m => m.Type == type);
        var items = await q.OrderBy(m => m.SortOrder).ThenByDescending(m => m.CreatedAt).Take(500).ToListAsync(ct);
        return items.Select(m => new MaterialDto(m.Id, lang.Pick(m.TitleEn, m.TitleHi), lang.PickOpt(m.DescriptionEn, m.DescriptionHi), m.Type.ToString(),
            m.Subject, m.ClassLevel, Format(m), m.SizeBytes, m.Downloads)).ToList();
    }

    [HttpPost("{id:guid}/download")]
    [Authorize]
    public async Task<MaterialDownloadDto> Download(Guid id, CancellationToken ct)
    {
        var m = await db.StudyMaterials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == PublishStatus.Published, ct)
                ?? throw AppException.NotFound("Material");
        await db.StudyMaterials.Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Downloads, x => x.Downloads + 1), ct);
        if (m.ExternalUrl is not null) return new MaterialDownloadDto(m.ExternalUrl, true, null, null);
        if (m.FileId is null) throw AppException.NotFound("File");

        var (query, expires) = signed.Sign(Scope, id, LinkLifetime);
        return new MaterialDownloadDto($"/api/v1/materials/{id}/file?{query}", false, m.FileName, expires);
    }

    /// <summary>The file itself, for a signed link from <see cref="Download"/>. Opens in the browser's viewer, with the right name for saving.</summary>
    [HttpGet("{id:guid}/file")]
    [AllowAnonymous]
    public async Task<IActionResult> File(Guid id, [FromQuery] long expires, [FromQuery] string? sig, CancellationToken ct)
    {
        signed.EnsureValid(Scope, id, expires, sig);
        var m = await db.StudyMaterials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == PublishStatus.Published, ct)
                ?? throw AppException.NotFound("Material");
        var file = m.FileId is { } fid ? await files.GetAsync(fid, ct) : null;
        if (file is null) throw AppException.NotFound("File");

        return SignedFiles.Inline(Response, file, m.FileName ?? file.FileName, LinkLifetime);
    }
}

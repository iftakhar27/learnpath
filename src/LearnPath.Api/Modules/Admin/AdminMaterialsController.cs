using System.ComponentModel.DataAnnotations;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Admin;

public record AdminMaterialDto(Guid Id, string TitleEn, string? TitleHi, string? DescriptionEn, string? DescriptionHi, MaterialType Type, string? Subject,
    string? ClassLevel, string Format, string? ExternalUrl, string? FileName, long SizeBytes, PublishStatus Status, int SortOrder, int Downloads, DateTimeOffset UpdatedAt);
public record AdminMaterialsDto(List<AdminMaterialDto> Items, long StoredBytes, int MaxUploadMegabytes);

/// <summary>
/// One form for adding and editing (multipart, so a file can come with it). A material is either an uploaded file
/// (PDF, PNG or JPEG) or an https link. When editing, a new file replaces the old one, a link replaces the file, and
/// sending neither keeps what's there.
/// </summary>
public class MaterialForm
{
    [Required, MaxLength(200)] public string TitleEn { get; set; } = "";
    [MaxLength(200)] public string? TitleHi { get; set; }
    [MaxLength(1000)] public string? DescriptionEn { get; set; }
    [MaxLength(1000)] public string? DescriptionHi { get; set; }
    [Required] public MaterialType Type { get; set; }
    [RegularExpression("^(Physics|Chemistry|Biology)?$")] public string? Subject { get; set; }
    [RegularExpression("^(11|12)?$")] public string? ClassLevel { get; set; }
    public PublishStatus Status { get; set; } = PublishStatus.Published;
    public int SortOrder { get; set; }
    [MaxLength(1000)] public string? ExternalUrl { get; set; }
    public IFormFile? File { get; set; }
}

[ApiController]
[Authorize(Policy = Policies.ContentEdit)]
[Route("api/v1/admin/materials")]
public class AdminMaterialsController(AppDbContext db, IFileStore files, AuditService audit, IConfiguration config, TimeProvider clock) : ControllerBase
{
    /// <summary>The hard ceiling for a request; the configured limit (Materials:MaxUploadMegabytes) is checked below it.</summary>
    private const long RequestLimit = 52L * 1024 * 1024;
    private int MaxMegabytes => UploadRules.MaxMegabytes(config);

    private static AdminMaterialDto ToDto(StudyMaterial m) => new(m.Id, m.TitleEn, m.TitleHi, m.DescriptionEn, m.DescriptionHi, m.Type, m.Subject, m.ClassLevel,
        MaterialsController.Format(m), m.ExternalUrl, m.FileName, m.SizeBytes, m.Status, m.SortOrder, m.Downloads, m.UpdatedAt);

    [HttpGet]
    public async Task<AdminMaterialsDto> List(CancellationToken ct)
    {
        var items = await db.StudyMaterials.AsNoTracking().OrderBy(m => m.SortOrder).ThenByDescending(m => m.CreatedAt).ToListAsync(ct);
        return new AdminMaterialsDto(items.Select(ToDto).ToList(), await files.TotalBytesAsync(ct), MaxMegabytes);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    public async Task<AdminMaterialDto> Create([FromForm] MaterialForm form, CancellationToken ct)
    {
        if (form.File is null && string.IsNullOrWhiteSpace(form.ExternalUrl))
            throw AppException.BadRequest("file_or_link_required", "Upload a file or give a link.");
        var now = clock.GetUtcNow();
        var m = new StudyMaterial { CreatedAt = now };
        Apply(m, form, now);
        await SetSourceAsync(m, form, ct);
        db.StudyMaterials.Add(m);
        audit.Record(User.UserId(), "material.create", "StudyMaterial", m.Id, new { m.TitleEn, m.Type, Format = MaterialsController.Format(m), m.SizeBytes });
        await db.SaveChangesAsync(ct);
        return ToDto(m);
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    public async Task<AdminMaterialDto> Update(Guid id, [FromForm] MaterialForm form, CancellationToken ct)
    {
        var m = await db.StudyMaterials.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Material");
        Apply(m, form, clock.GetUtcNow());
        var replaced = form.File is not null || !string.IsNullOrWhiteSpace(form.ExternalUrl) ? await SetSourceAsync(m, form, ct) : null;
        audit.Record(User.UserId(), "material.update", "StudyMaterial", id, new { m.TitleEn, m.Status, Format = MaterialsController.Format(m) });
        await db.SaveChangesAsync(ct);
        // The old file goes only once the new source is saved.
        if (replaced is { } old) await files.DeleteAsync(old, ct);
        return ToDto(m);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var m = await db.StudyMaterials.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Material");
        var fileId = m.FileId;
        db.StudyMaterials.Remove(m);
        audit.Record(User.UserId(), "material.delete", "StudyMaterial", id, new { m.TitleEn });
        await db.SaveChangesAsync(ct);
        if (fileId is { } fid) await files.DeleteAsync(fid, ct);
        return NoContent();
    }

    private static void Apply(StudyMaterial m, MaterialForm f, DateTimeOffset now)
    {
        static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        m.TitleEn = f.TitleEn.Trim();
        m.TitleHi = Blank(f.TitleHi);
        m.DescriptionEn = Blank(f.DescriptionEn);
        m.DescriptionHi = Blank(f.DescriptionHi);
        m.Type = f.Type;
        m.Subject = Blank(f.Subject);
        m.ClassLevel = Blank(f.ClassLevel);
        m.Status = f.Status;
        m.SortOrder = f.SortOrder;
        m.UpdatedAt = now;
    }

    /// <summary>Takes the uploaded file (checked by its content, not its name) or the link. Returns the file it replaced, to delete after saving.</summary>
    private async Task<Guid?> SetSourceAsync(StudyMaterial m, MaterialForm f, CancellationToken ct)
    {
        if (f.File is not null && !string.IsNullOrWhiteSpace(f.ExternalUrl))
            throw AppException.BadRequest("file_or_link_required", "Upload a file or give a link, not both.");
        var oldFile = m.FileId;
        if (f.File is { } upload)
        {
            var (data, type) = await UploadRules.ReadAsync(upload, MaxMegabytes, ct);
            var name = UploadRules.SafeFileName(upload.FileName, m.TitleEn, type);
            var stored = await files.SaveAsync(name, type, data, ct);
            (m.FileId, m.FileName, m.ContentType, m.SizeBytes, m.ExternalUrl) = (stored.Id, name, type, data.LongLength, null);
        }
        else
        {
            var url = f.ExternalUrl!.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw AppException.BadRequest("invalid_link", "Links must start with https://.");
            (m.FileId, m.FileName, m.ContentType, m.SizeBytes, m.ExternalUrl) = (null, null, null, 0, uri.ToString());
        }
        return oldFile is { } old && old != m.FileId ? old : null;
    }
}

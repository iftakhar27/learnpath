using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Infrastructure;

/// <summary>Where uploaded files live. The database for now (works on every plan, backed up with it); an object store later.</summary>
public interface IFileStore
{
    Task<StoredFile> SaveAsync(string fileName, string contentType, byte[] data, CancellationToken ct);
    Task<StoredFile?> GetAsync(Guid id, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<long> TotalBytesAsync(CancellationToken ct);
}

/// <summary>Keeps files in Postgres (bytea). Fine for a library of PDFs up to a few hundred MB; the caller saves changes.</summary>
public class DatabaseFileStore(AppDbContext db, TimeProvider clock) : IFileStore
{
    public Task<StoredFile> SaveAsync(string fileName, string contentType, byte[] data, CancellationToken ct)
    {
        var file = new StoredFile
        {
            FileName = fileName, ContentType = contentType, SizeBytes = data.LongLength, Data = data, CreatedAt = clock.GetUtcNow(),
            Sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(),
        };
        db.StoredFiles.Add(file);
        return Task.FromResult(file);
    }

    public Task<StoredFile?> GetAsync(Guid id, CancellationToken ct) => db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct) => await db.StoredFiles.Where(f => f.Id == id).ExecuteDeleteAsync(ct);

    public async Task<long> TotalBytesAsync(CancellationToken ct) => await db.StoredFiles.SumAsync(f => (long?)f.SizeBytes, ct) ?? 0;
}

/// <summary>What may be uploaded: PDFs and images, recognised by their first bytes (not the name the browser sends).</summary>
public static class UploadRules
{
    public const int DefaultMaxMegabytes = 20;

    public static string? DetectContentType(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith("%PDF-"u8)) return "application/pdf";
        if (head.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (head.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return "image/jpeg";
        return null;
    }

    public static string Extension(string contentType) => contentType switch
    {
        "application/pdf" => ".pdf",
        "image/png" => ".png",
        _ => ".jpg",
    };

    /// <summary>A safe download name: letters and digits joined by single hyphens, with the right extension for what it really is.</summary>
    public static string SafeFileName(string? requested, string fallbackTitle, string contentType)
    {
        var name = Path.GetFileNameWithoutExtension(string.IsNullOrWhiteSpace(requested) ? fallbackTitle : requested);
        var clean = Regex.Replace(name, @"[^\p{L}\p{Nd}_]+", "-").Trim('-');
        if (clean.Length == 0) clean = "material";
        if (clean.Length > 80) clean = clean[..80];
        return clean + Extension(contentType);
    }
}

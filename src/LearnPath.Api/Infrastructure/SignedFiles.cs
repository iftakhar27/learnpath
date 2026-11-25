using LearnPath.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace LearnPath.Api.Infrastructure;

/// <summary>
/// Short-lived links to stored files that work without a sign-in token (a browser tab, a PDF viewer, the phone's
/// browser): access is checked when the link is handed out; the link carries an expiry and an HMAC over
/// "scope:id:expiry" with the server's signing key.
/// </summary>
public class SignedFiles(IConfiguration config, TimeProvider clock)
{
    private string Secret => config["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is not set.");

    private string Signature(string scope, Guid id, long expires) => Hashing.HmacSha256Hex(Secret, $"{scope}:{id:N}:{expires}");

    /// <summary>The query string to append ("expires=…&amp;sig=…") and when it stops working.</summary>
    public (string Query, DateTimeOffset ExpiresAt) Sign(string scope, Guid id, TimeSpan validFor)
    {
        var expires = clock.GetUtcNow().Add(validFor);
        var unix = expires.ToUnixTimeSeconds();
        return ($"expires={unix}&sig={Signature(scope, id, unix)}", expires);
    }

    public void EnsureValid(string scope, Guid id, long expires, string? sig)
    {
        if (string.IsNullOrEmpty(sig) || expires < clock.GetUtcNow().ToUnixTimeSeconds() || !Hashing.FixedTimeEqualsHex(sig, Signature(scope, id, expires)))
            throw new AppException(403, "link_expired", "This link has expired. Open it again from the app or website.");
    }

    /// <summary>Serves a file to open in the browser's viewer, with the right name for saving.</summary>
    public static IActionResult Inline(HttpResponse response, StoredFile file, string fileName, TimeSpan cacheFor)
    {
        var disposition = new ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(fileName);
        response.Headers.ContentDisposition = disposition.ToString();
        response.Headers.CacheControl = $"private, max-age={(int)cacheFor.TotalSeconds}";
        response.Headers.ETag = $"\"{file.Sha256}\"";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers.ContentLength = file.SizeBytes;
        return new FileContentResult(file.Data, file.ContentType) { LastModified = file.CreatedAt };
    }
}

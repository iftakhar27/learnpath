using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace LearnPath.Api.Infrastructure;

// ---------- OTP ----------

public interface IOtpSender
{
    Task SendAsync(string phone, string code, CancellationToken ct);
}

public static class OtpPolicy
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Test-server sender: writes the OTP to the log and keeps the latest codes in <see cref="TestOtpOutbox"/> so staff can
/// read them in the admin portal. Program.cs refuses to start in Production with this sender.
/// Production uses <see cref="Msg91OtpSender"/> (Otp:Provider = msg91).
/// </summary>
public class LoggingOtpSender(ILogger<LoggingOtpSender> logger, TestOtpOutbox outbox, TimeProvider clock) : IOtpSender
{
    public Task SendAsync(string phone, string code, CancellationToken ct)
    {
        logger.LogWarning("DEV OTP for {PhoneSuffix}: {Code}", phone[^4..], code);
        outbox.Add(phone, code, clock.GetUtcNow());
        return Task.CompletedTask;
    }
}

public record TestOtpCodeDto(string Phone, string Code, DateTimeOffset SentAt, DateTimeOffset ExpiresAt);

/// <summary>
/// The latest codes sent by <see cref="LoggingOtpSender"/>, newest first, kept in memory for an hour. Registered only
/// when no real SMS provider is used, so the admin endpoint that reads it doesn't exist on a production server.
/// </summary>
public class TestOtpOutbox
{
    public const int Capacity = 20;
    private static readonly TimeSpan KeepFor = TimeSpan.FromHours(1);
    private readonly LinkedList<TestOtpCodeDto> _items = new();
    private readonly object _gate = new();

    public void Add(string phone, string code, DateTimeOffset sentAt)
    {
        lock (_gate)
        {
            _items.AddFirst(new TestOtpCodeDto(Mask(phone), code, sentAt, sentAt + OtpPolicy.Lifetime));
            while (_items.Count > Capacity) _items.RemoveLast();
        }
    }

    public List<TestOtpCodeDto> Recent(DateTimeOffset now)
    {
        lock (_gate) return _items.Where(i => i.SentAt > now - KeepFor).ToList();
    }

    /// <summary>Last four digits are enough for a tester to find their own code.</summary>
    public static string Mask(string phone) => phone.Length <= 4 ? phone : new string('•', phone.Length - 4) + phone[^4..];
}

public sealed class Msg91Options
{
    public string AuthKey { get; set; } = "";
    /// <summary>OTP template from MSG91's OTP section (DLT-approved, contains ##OTP##).</summary>
    public string TemplateId { get; set; } = "";
    public string BaseUrl { get; set; } = "https://control.msg91.com";
}

/// <summary>
/// Delivers the API's own sign-in code by SMS through MSG91's SendOTP API. The API still generates, stores (hashed) and
/// verifies every code; MSG91 only sends it. The code travels in the request URL, so this client's request logging is
/// switched off (Program.cs) and failures are logged without it.
/// </summary>
public class Msg91OtpSender(HttpClient http, Microsoft.Extensions.Options.IOptions<Msg91Options> options, ILogger<Msg91OtpSender> logger) : IOtpSender
{
    public async Task SendAsync(string phone, string code, CancellationToken ct)
    {
        var o = options.Value;
        var mobile = phone.TrimStart('+'); // international format with country code, e.g. 919876543210
        var url = $"{o.BaseUrl.TrimEnd('/')}/api/v5/otp?template_id={Uri.EscapeDataString(o.TemplateId)}&mobile={Uri.EscapeDataString(mobile)}"
                  + $"&otp={Uri.EscapeDataString(code)}&otp_length={code.Length}&otp_expiry={(int)Math.Ceiling(OtpPolicy.Lifetime.TotalMinutes)}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { }) };
        req.Headers.Add("authkey", o.AuthKey);

        HttpResponseMessage res;
        try
        {
            res = await http.SendAsync(req, ct);
        }
        catch (Exception e) when (e is HttpRequestException || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogWarning("MSG91 unreachable sending a code to ...{Suffix}: {Error}", phone[^4..], e.Message);
            throw SendFailed();
        }
        using (res)
        {
            Msg91Response? body = null;
            try { body = await res.Content.ReadFromJsonAsync<Msg91Response>(ct); }
            catch (System.Text.Json.JsonException) { /* not JSON: treated as a failure below */ }
            // MSG91 reports most errors with HTTP 200 and type "error", so the body decides.
            if (res.IsSuccessStatusCode && string.Equals(body?.Type, "success", StringComparison.OrdinalIgnoreCase)) return;
            logger.LogError("MSG91 SendOTP failed ({Status}) for ...{Suffix}: {Message}", (int)res.StatusCode, phone[^4..], body?.Message ?? "no message");
            throw SendFailed();
        }
    }

    private static AppException SendFailed() => new(503, "otp_send_failed", "We couldn't send the code right now. Please try again in a minute.");

    private sealed record Msg91Response(string? Type, string? Message, [property: JsonPropertyName("request_id")] string? RequestId);
}

// ---------- Video ----------

public interface IVideoUrlSigner
{
    /// <summary>Returns a short-lived URL for the provider's embeddable web player.</summary>
    string GetPlaybackUrl(string providerVideoId, Guid userId, TimeSpan validFor);

    /// <summary>Returns a short-lived HLS stream URL for native players (mobile apps), or null when not configured.</summary>
    string? GetStreamUrl(string providerVideoId, TimeSpan validFor);
}

/// <summary>
/// Bunny Stream token-authenticated embed URL: token = SHA256(securityKey + videoId + expires).
/// Configure Video:LibraryId and Video:TokenKey. Without a key, returns an unsigned URL (dev only).
/// </summary>
public class BunnyVideoUrlSigner(IConfiguration config, TimeProvider clock) : IVideoUrlSigner
{
    public string GetPlaybackUrl(string providerVideoId, Guid userId, TimeSpan validFor)
    {
        var library = config["Video:LibraryId"] ?? "dev-library";
        var key = config["Video:TokenKey"];
        var baseUrl = $"https://iframe.mediadelivery.net/embed/{library}/{Uri.EscapeDataString(providerVideoId)}";
        if (string.IsNullOrEmpty(key)) return baseUrl;
        var expires = clock.GetUtcNow().Add(validFor).ToUnixTimeSeconds();
        var token = Hashing.Sha256(key + providerVideoId + expires);
        return $"{baseUrl}?token={token}&expires={expires}";
    }

    /// <summary>
    /// Bunny CDN token authentication for the library's pull zone (Video:CdnHost, Video:CdnTokenKey):
    /// token = base64url(SHA256_raw(key + path + expires)). A provider ID that is already an absolute
    /// https URL is returned unchanged, which lets development content point at public sample videos.
    /// </summary>
    public string? GetStreamUrl(string providerVideoId, TimeSpan validFor)
    {
        if (Uri.TryCreate(providerVideoId, UriKind.Absolute, out var direct) && direct.Scheme == Uri.UriSchemeHttps) return providerVideoId;
        var host = config["Video:CdnHost"];
        if (string.IsNullOrEmpty(host)) return null;
        var path = $"/{Uri.EscapeDataString(providerVideoId)}/playlist.m3u8";
        var key = config["Video:CdnTokenKey"];
        if (string.IsNullOrEmpty(key)) return $"https://{host}{path}";
        var expires = clock.GetUtcNow().Add(validFor).ToUnixTimeSeconds();
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key + path + expires));
        var token = Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return $"https://{host}{path}?token={token}&expires={expires}";
    }
}

public interface IDocumentUrlSigner
{
    string GetDownloadUrl(string storageKey, TimeSpan validFor);
}

/// <summary>HMAC-signed CDN URL (works with Bunny CDN / Cloudflare token auth patterns). Configure Storage:CdnBaseUrl and Storage:SigningKey.</summary>
public class HmacDocumentUrlSigner(IConfiguration config, TimeProvider clock) : IDocumentUrlSigner
{
    public string GetDownloadUrl(string storageKey, TimeSpan validFor)
    {
        var baseUrl = (config["Storage:CdnBaseUrl"] ?? "https://cdn.example.invalid").TrimEnd('/');
        var path = "/" + storageKey.TrimStart('/');
        var key = config["Storage:SigningKey"];
        if (string.IsNullOrEmpty(key)) return baseUrl + path;
        var expires = clock.GetUtcNow().Add(validFor).ToUnixTimeSeconds();
        var sig = Hashing.HmacSha256Hex(key, path + expires);
        return $"{baseUrl}{path}?expires={expires}&sig={sig}";
    }
}

// ---------- Payments ----------

public record GatewayOrder(string GatewayOrderId, long AmountInPaise, string Currency);

public interface IPaymentGateway
{
    string PublicKeyId { get; }
    Task<GatewayOrder> CreateOrderAsync(long amountInPaise, string receipt, CancellationToken ct);
    bool VerifyCheckoutSignature(string gatewayOrderId, string gatewayPaymentId, string signature);
    bool VerifyWebhookSignature(string rawBody, string signature);
}

/// <summary>
/// Razorpay: orders via REST (Basic auth key_id:key_secret); checkout signature = HMAC_SHA256(order_id|payment_id, key_secret);
/// webhook signature = HMAC_SHA256(raw_body, webhook_secret).
/// </summary>
public class RazorpayGateway(HttpClient http, IConfiguration config, ILogger<RazorpayGateway> logger) : IPaymentGateway
{
    private string KeySecret => config["Razorpay:KeySecret"] ?? throw new InvalidOperationException("Razorpay:KeySecret not configured");
    private string WebhookSecret => config["Razorpay:WebhookSecret"] ?? throw new InvalidOperationException("Razorpay:WebhookSecret not configured");
    public string PublicKeyId => config["Razorpay:KeyId"] ?? "";

    public async Task<GatewayOrder> CreateOrderAsync(long amountInPaise, string receipt, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders")
        {
            Content = JsonContent.Create(new { amount = amountInPaise, currency = "INR", receipt }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{PublicKeyId}:{KeySecret}")));
        HttpResponseMessage res;
        try
        {
            res = await http.SendAsync(req, ct);
        }
        catch (Exception e) when (e is HttpRequestException || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogWarning("Razorpay unreachable creating an order: {Error}", e.Message);
            throw new AppException(502, "payment_gateway_error", "Payment provider is unavailable. Please try again.");
        }
        using var _ = res;
        if (!res.IsSuccessStatusCode)
        {
            // Razorpay explains failures (e.g. "Authentication failed" for wrong keys) in error.description.
            var detail = await res.Content.ReadAsStringAsync(ct);
            string? description = null;
            try { description = System.Text.Json.JsonDocument.Parse(detail).RootElement.GetProperty("error").GetProperty("description").GetString(); }
            catch (Exception) { /* not Razorpay's error format */ }
            logger.LogError("Razorpay order creation failed ({Status}): {Description}", (int)res.StatusCode, description ?? "no description");
            throw new AppException(502, "payment_gateway_error", "Payment provider is unavailable. Please try again.");
        }
        var body = await res.Content.ReadFromJsonAsync<RazorpayOrderResponse>(ct)
                   ?? throw new AppException(502, "payment_gateway_error", "Invalid response from payment provider.");
        return new GatewayOrder(body.Id, body.Amount, body.Currency);
    }

    public bool VerifyCheckoutSignature(string gatewayOrderId, string gatewayPaymentId, string signature) =>
        Hashing.FixedTimeEqualsHex(Hashing.HmacSha256Hex(KeySecret, $"{gatewayOrderId}|{gatewayPaymentId}"), signature);

    public bool VerifyWebhookSignature(string rawBody, string signature) =>
        Hashing.FixedTimeEqualsHex(Hashing.HmacSha256Hex(WebhookSecret, rawBody), signature);

    private record RazorpayOrderResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("amount")] long Amount,
        [property: JsonPropertyName("currency")] string Currency);
}

/// <summary>
/// Offline gateway for local development and automated tests. Signatures are still real HMACs over configured
/// secrets so the verification code path is exercised. Program.cs refuses to use it in Production.
/// </summary>
public class FakePaymentGateway(IConfiguration config) : IPaymentGateway
{
    private string KeySecret => config["Razorpay:KeySecret"] ?? "dev_key_secret";
    private string WebhookSecret => config["Razorpay:WebhookSecret"] ?? "dev_webhook_secret";
    public string PublicKeyId => "rzp_test_fake";

    public Task<GatewayOrder> CreateOrderAsync(long amountInPaise, string receipt, CancellationToken ct) =>
        Task.FromResult(new GatewayOrder("order_fake_" + Guid.NewGuid().ToString("N")[..14], amountInPaise, "INR"));

    public bool VerifyCheckoutSignature(string gatewayOrderId, string gatewayPaymentId, string signature) =>
        Hashing.FixedTimeEqualsHex(Hashing.HmacSha256Hex(KeySecret, $"{gatewayOrderId}|{gatewayPaymentId}"), signature);

    public bool VerifyWebhookSignature(string rawBody, string signature) =>
        Hashing.FixedTimeEqualsHex(Hashing.HmacSha256Hex(WebhookSecret, rawBody), signature);
}

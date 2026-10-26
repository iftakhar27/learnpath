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

/// <summary>
/// Development-only sender: writes the OTP to the log. Program.cs refuses to start in Production with this sender.
/// Replace with an MSG91 (DLT-registered template) implementation before launch.
/// </summary>
public class LoggingOtpSender(ILogger<LoggingOtpSender> logger) : IOtpSender
{
    public Task SendAsync(string phone, string code, CancellationToken ct)
    {
        logger.LogWarning("DEV OTP for {PhoneSuffix}: {Code}", phone[^4..], code);
        return Task.CompletedTask;
    }
}

// ---------- Video ----------

public interface IVideoUrlSigner
{
    /// <summary>Returns a short-lived playback URL for a provider video ID.</summary>
    string GetPlaybackUrl(string providerVideoId, Guid userId, TimeSpan validFor);
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
public class RazorpayGateway(HttpClient http, IConfiguration config) : IPaymentGateway
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
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            throw new AppException(502, "payment_gateway_error", "Payment provider is unavailable. Please try again.");
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

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LearnPath.Tests;

public class RazorpayGatewayTests
{
    private const string KeyId = "rzp_test_ABCDEFGHIJKLMN";
    private const string KeySecret = "test_secret_value_123";
    private const string WebhookSecret = "webhook_secret_456";

    private static (RazorpayGateway Gateway, FakeMsg91 Fake, CapturingLoggerProvider Logs) Create()
    {
        var fake = new FakeMsg91 { Respond = _ => FakeMsg91.Json(HttpStatusCode.OK, new { id = "order_TEST123", entity = "order", amount = 199900, currency = "INR", status = "created" }) };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Razorpay:KeyId"] = KeyId, ["Razorpay:KeySecret"] = KeySecret, ["Razorpay:WebhookSecret"] = WebhookSecret,
        }).Build();
        var logs = new CapturingLoggerProvider();
        var gateway = new RazorpayGateway(new HttpClient(fake), config, new LoggerFactory([logs]).CreateLogger<RazorpayGateway>());
        return (gateway, fake, logs);
    }

    private static string Hmac(string key, string message) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(message))).ToLowerInvariant();

    [Fact]
    public async Task Creates_an_order_with_basic_auth_amount_in_paise_and_receipt()
    {
        var (gateway, fake, _) = Create();
        var order = await gateway.CreateOrderAsync(199900, "rcpt_0123456789abcdef0123", default);

        Assert.Equal(("order_TEST123", 199900L, "INR"), (order.GatewayOrderId, order.AmountInPaise, order.Currency));
        var req = Assert.Single(fake.Requests);
        Assert.Equal((HttpMethod.Post, "https://api.razorpay.com/v1/orders"), (req.Method, req.RequestUri!.ToString()));
        Assert.Equal("Basic", req.Headers.Authorization!.Scheme);
        Assert.Equal($"{KeyId}:{KeySecret}", Encoding.UTF8.GetString(Convert.FromBase64String(req.Headers.Authorization.Parameter!)));
        var body = System.Text.Json.JsonDocument.Parse(Assert.Single(fake.Bodies)).RootElement;
        Assert.Equal((199900L, "INR", "rcpt_0123456789abcdef0123"), (body.GetProperty("amount").GetInt64(), body.GetProperty("currency").GetString(), body.GetProperty("receipt").GetString()));
    }

    [Fact]
    public async Task Wrong_keys_are_logged_with_Razorpays_reason_but_never_the_secret()
    {
        var (gateway, fake, logs) = Create();
        fake.Respond = _ => FakeMsg91.Json(HttpStatusCode.Unauthorized, new { error = new { code = "BAD_REQUEST_ERROR", description = "Authentication failed" } });

        var e = await Assert.ThrowsAsync<AppException>(() => gateway.CreateOrderAsync(199900, "rcpt_x", default));
        Assert.Equal((502, "payment_gateway_error"), (e.Status, e.Code));
        Assert.Contains(logs.Lines, l => l.Contains("Authentication failed") && l.Contains("401"));
        Assert.DoesNotContain(logs.Lines, l => l.Contains(KeySecret));
    }

    [Fact]
    public void Checkout_and_webhook_signatures_follow_Razorpays_scheme()
    {
        var (gateway, _, _) = Create();
        var good = Hmac(KeySecret, "order_TEST123|pay_TEST456");
        Assert.True(gateway.VerifyCheckoutSignature("order_TEST123", "pay_TEST456", good));
        Assert.False(gateway.VerifyCheckoutSignature("order_TEST123", "pay_OTHER", good));
        Assert.False(gateway.VerifyCheckoutSignature("order_TEST123", "pay_TEST456", Hmac("wrong-secret", "order_TEST123|pay_TEST456")));

        const string payload = """{"event":"payment.captured","payload":{"payment":{"entity":{"id":"pay_TEST456","order_id":"order_TEST123","amount":199900}}}}""";
        Assert.True(gateway.VerifyWebhookSignature(payload, Hmac(WebhookSecret, payload)));
        Assert.False(gateway.VerifyWebhookSignature(payload + " ", Hmac(WebhookSecret, payload)));
    }
}

[Collection("api")]
public class RazorpayStartupTests(ApiFactory api)
{
    [Theory]
    [InlineData(null, "Razorpay:KeyId")]
    [InlineData("pk_live_wrongprovider", "rzp_test_")]
    public async Task Real_payments_refuse_to_start_without_proper_keys(string? keyId, string expected)
    {
        await using var host = api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Razorpay:UseFakeGateway", "false");
            b.UseSetting("Razorpay:KeyId", keyId ?? "");
        });
        var e = Assert.ThrowsAny<Exception>(() => host.CreateClient());
        Assert.Contains(expected, e.ToString());
    }
}

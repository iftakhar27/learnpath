using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LearnPath.Tests;

/// <summary>Plays MSG91: records requests and answers with a canned response.</summary>
public class FakeMsg91 : HttpMessageHandler
{
    public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new();
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => Json(HttpStatusCode.OK, new { request_id = "req-1", type = "success" });

    public static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status) { Content = JsonContent.Create(body) };

    /// <summary>Request bodies, read on arrival (callers dispose their requests afterwards).</summary>
    public ConcurrentQueue<string> Bodies { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Enqueue(request);
        Bodies.Enqueue(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
        return Respond(request);
    }

    public string? CodeSentTo(string mobile) =>
        Requests.Select(r => System.Web.HttpUtility.ParseQueryString(r.RequestUri!.Query)).LastOrDefault(q => q["mobile"] == mobile)?["otp"];
}

public class Msg91SenderTests
{
    private static (Msg91OtpSender Sender, FakeMsg91 Fake) Create()
    {
        var fake = new FakeMsg91();
        var options = Options.Create(new Msg91Options { AuthKey = "test-auth-key", TemplateId = "tmpl-123", BaseUrl = "https://msg91.test" });
        return (new Msg91OtpSender(new HttpClient(fake), options, NullLogger<Msg91OtpSender>.Instance), fake);
    }

    [Fact]
    public async Task Sends_our_code_with_the_template_and_key()
    {
        var (sender, fake) = Create();
        await sender.SendAsync("+919876543210", "482913", default);

        var req = Assert.Single(fake.Requests);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://msg91.test/api/v5/otp", req.RequestUri!.GetLeftPart(UriPartial.Path));
        var q = System.Web.HttpUtility.ParseQueryString(req.RequestUri.Query);
        Assert.Equal(("tmpl-123", "919876543210", "482913", "6", "5"), (q["template_id"], q["mobile"], q["otp"], q["otp_length"], q["otp_expiry"]));
        Assert.Equal("test-auth-key", req.Headers.GetValues("authkey").Single());
    }

    public static TheoryData<string> Failures => new() { "error-200", "401", "html-502", "network" };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Failures_become_a_clear_503(string kind)
    {
        var (sender, fake) = Create();
        fake.Respond = kind switch
        {
            // MSG91 reports most errors with HTTP 200 and type "error".
            "error-200" => _ => FakeMsg91.Json(HttpStatusCode.OK, new { message = "The provided flow ID or template ID is invalid.", type = "error" }),
            "401" => _ => FakeMsg91.Json(HttpStatusCode.Unauthorized, new { message = "Auth Key missing", type = "error" }),
            "html-502" => _ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>502</html>", System.Text.Encoding.UTF8, "text/html") },
            _ => _ => throw new HttpRequestException("connection refused"),
        };
        var e = await Assert.ThrowsAsync<AppException>(() => sender.SendAsync("+919876543210", "482913", default));
        Assert.Equal((503, "otp_send_failed"), (e.Status, e.Code));
    }
}

/// <summary>Captures every log line of a test host.</summary>
public class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Lines { get; } = new();
    public ILogger CreateLogger(string category) => new L(category, Lines);
    public void Dispose() { }

    private sealed class L(string category, ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> format) =>
            lines.Enqueue($"{category}: {format(state, ex)}");
    }
}

[Collection("api")]
public class Msg91SignInTests(ApiFactory api)
{
    private WebApplicationFactory<Program> Msg91Host(FakeMsg91 fake, CapturingLoggerProvider logs) =>
        api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Otp:Provider", "msg91");
            b.UseSetting("Otp:Msg91:AuthKey", "test-auth-key");
            b.UseSetting("Otp:Msg91:TemplateId", "tmpl-123");
            b.UseSetting("Otp:Msg91:BaseUrl", "https://msg91.test");
            b.ConfigureLogging(l => l.AddProvider(logs));
            b.ConfigureTestServices(s =>
            {
                // The test factory swaps in a capturing sender; put the real MSG91 client back, talking to the fake.
                s.RemoveAll<IOtpSender>();
                s.AddHttpClient<IOtpSender, Msg91OtpSender>().ConfigurePrimaryHttpMessageHandler(() => fake);
            });
        });

    [Fact]
    public async Task A_code_sent_by_SMS_signs_the_student_in_and_never_appears_in_the_logs()
    {
        var fake = new FakeMsg91();
        var logs = new CapturingLoggerProvider();
        await using var host = Msg91Host(fake, logs);
        var client = host.CreateClient();
        var digits = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999);

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone = digits })).StatusCode);
        var code = fake.CodeSentTo("91" + digits);
        Assert.Matches("^[0-9]{6}$", code!);

        var verify = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = digits, code, fullName = "SMS Student", acceptTerms = true });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        Assert.NotEmpty(logs.Lines);
        Assert.DoesNotContain(logs.Lines, line => line.Contains(code!));
    }

    [Fact]
    public async Task When_the_SMS_fails_the_student_can_ask_again_straight_away()
    {
        var fake = new FakeMsg91 { Respond = _ => FakeMsg91.Json(HttpStatusCode.OK, new { message = "Insufficient balance", type = "error" }) };
        await using var host = Msg91Host(fake, new CapturingLoggerProvider());
        var client = host.CreateClient();
        var digits = "8" + Random.Shared.NextInt64(100_000_000, 999_999_999);

        var failed = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone = digits });
        // A deliberate 503 (the shared ReadJson helper treats any 5xx as a crash), so parse it here.
        var problem = System.Text.Json.JsonDocument.Parse(await failed.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal((HttpStatusCode.ServiceUnavailable, "otp_send_failed"), (failed.StatusCode, problem.GetProperty("code").GetString()));

        fake.Respond = _ => FakeMsg91.Json(HttpStatusCode.OK, new { request_id = "req-2", type = "success" });
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone = digits })).StatusCode);
    }

    [Fact]
    public async Task MSG91_without_its_key_refuses_to_start()
    {
        await using var host = api.WithWebHostBuilder(b => b.UseSetting("Otp:Provider", "msg91"));
        var e = Assert.ThrowsAny<Exception>(() => host.CreateClient());
        Assert.Contains("Otp:Msg91:AuthKey", e.ToString());
    }
}

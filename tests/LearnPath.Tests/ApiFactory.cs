using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LearnPath.Api.Data;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace LearnPath.Tests;

public class CapturingOtpSender : IOtpSender
{
    public ConcurrentDictionary<string, string> Last { get; } = new();
    public Task SendAsync(string phone, string code, CancellationToken ct) { Last[phone] = code; return Task.CompletedTask; }
}

/// <summary>Runs the real API against a throwaway PostgreSQL database (env TEST_PG, default localhost postgres/postgres).</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Admin!Pass123";
    public const string KeySecret = "test_key_secret";
    public const string WebhookSecret = "test_webhook_secret";

    private readonly string _db = "learnpath_test_" + Guid.NewGuid().ToString("N")[..10];
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);
    public CapturingOtpSender Otp { get; } = new();

    private string ConnectionString =>
        $"{Environment.GetEnvironmentVariable("TEST_PG") ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres"};Database={_db};Include Error Detail=true";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-0123456789-0123456789-abcdef");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:SampleContent", "true");
        builder.UseSetting("Razorpay:UseFakeGateway", "true");
        builder.UseSetting("Razorpay:KeySecret", KeySecret);
        builder.UseSetting("Razorpay:WebhookSecret", WebhookSecret);
        builder.UseSetting("Otp:Provider", "log");
        builder.UseSetting("RateLimit:AuthPerMinute", "10000");
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Clock);
            s.RemoveAll<IOtpSender>();
            s.AddSingleton<IOtpSender>(Otp);
        });
    }

    public Task InitializeAsync()
    {
        _ = Server; // boots the host: migrates and seeds
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        await DisposeAsync();
    }

    public async Task<T> WithDb<T>(Func<AppDbContext, Task<T>> f)
    {
        await using var scope = Services.CreateAsyncScope();
        return await f(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<HttpClient> StudentAsync(string? email = null)
    {
        var c = CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/register", new { email = email ?? $"s{Guid.NewGuid():N}@test.local", password = "Passw0rd!x", fullName = "Test Student", acceptTerms = true });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return c;
    }

    public async Task<HttpClient> AdminAsync()
    {
        var c = CreateClient();
        var res = await c.PostAsJsonAsync("/api/v1/auth/login", new { email = AdminEmail, password = AdminPassword });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return c;
    }

    public async Task<Guid> CourseIdAsync(bool free) =>
        await WithDb(db => db.Courses.Where(c => c.IsFree == free).Select(c => c.Id).FirstAsync());

    public async Task<Guid> TestIdAsync(bool free) =>
        await WithDb(db => db.Tests.Where(t => t.IsFree == free).Select(t => t.Id).FirstAsync());
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;

public static class Json
{
    public static async Task<JsonElement> ReadJson(this HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode && (int)res.StatusCode >= 500) throw new Exception($"{(int)res.StatusCode}: {text}");
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public static async Task<JsonElement> Ok(this Task<HttpResponseMessage> call)
    {
        var res = await call;
        var json = await res.ReadJson();
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {json}");
        return json;
    }

    public static string Str(this JsonElement e, string name) => e.GetProperty(name).ToString();
    public static Guid Id(this JsonElement e, string name) => Guid.Parse(e.GetProperty(name).GetString()!);
    public static decimal Dec(this JsonElement e, string name) => e.GetProperty(name).GetDecimal();
}

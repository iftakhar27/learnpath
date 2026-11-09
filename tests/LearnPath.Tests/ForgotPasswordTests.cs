using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using LearnPath.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LearnPath.Tests;

/// <summary>"Forgot password?" by email: a single-use link to the website, valid 30 minutes.</summary>
[Collection("api")]
public class ForgotPasswordTests(ApiFactory api)
{
    private List<TestEmailDto> EmailsTo(string address) =>
        api.Services.GetRequiredService<TestEmailOutbox>().Recent(api.Clock.GetUtcNow()).Where(e => e.To == address).ToList();

    private static string TokenFrom(TestEmailDto email) => Regex.Match(email.Text, @"https://web\.test/reset-password\?token=([A-Za-z0-9_-]+)").Groups[1].Value;

    private async Task<(string Email, HttpClient Phone)> StudentAsync()
    {
        var email = $"s{Guid.NewGuid():N}@test.local";
        var body = await (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "Passw0rd!x", fullName = "Forgetful Student", acceptTerms = true, deviceSlot = "phone", deviceId = "phone-" + email })).ReadJson();
        var phone = api.CreateClient();
        phone.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Str("accessToken"));
        return (email, phone);
    }

    private Task<HttpResponseMessage> Forgot(string email) => api.CreateClient().PostAsJsonAsync("/api/v1/auth/password/forgot", new { email });
    private Task<HttpResponseMessage> Reset(string token, string newPassword) =>
        api.CreateClient().PostAsJsonAsync("/api/v1/auth/password/reset", new { token, newPassword });
    private async Task<HttpStatusCode> Login(string email, string password) =>
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password, deviceSlot = "browser", deviceId = "web-" + email })).StatusCode;

    [Fact]
    public async Task The_emailed_link_sets_a_new_password_once_and_signs_the_account_out()
    {
        var (email, phone) = await StudentAsync();
        Assert.Equal(HttpStatusCode.Accepted, (await Forgot("  " + email.ToUpperInvariant() + " ")).StatusCode);
        var sent = Assert.Single(EmailsTo(email));
        Assert.Equal("Reset your LearnPath password", sent.Subject);
        Assert.Contains("Forgetful Student", sent.Text);
        var token = TokenFrom(sent);
        Assert.True(token.Length >= 40);

        Assert.Equal(HttpStatusCode.BadRequest, (await Reset(token, "short")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Reset(token, "Brand-new-Passw0rd")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await Login(email, "Passw0rd!x"));
        Assert.Equal(HttpStatusCode.OK, await Login(email, "Brand-new-Passw0rd"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/v1/student/profile")).StatusCode);

        var again = await Reset(token, "Another-Passw0rd");
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("reset_invalid", (await again.ReadJson()).Str("code"));
    }

    [Fact]
    public async Task Unknown_emails_get_the_same_answer_links_expire_and_requests_are_throttled()
    {
        var unknown = $"nobody{Guid.NewGuid():N}@test.local";
        Assert.Equal(HttpStatusCode.Accepted, (await Forgot(unknown)).StatusCode);
        Assert.Empty(EmailsTo(unknown));

        var (email, _) = await StudentAsync();
        await Forgot(email);
        await Forgot(email); // within a minute: not sent again
        var token = TokenFrom(Assert.Single(EmailsTo(email)));

        // 31 minutes later (moved in the database: the shared test clock also drives other tests' tokens).
        await api.WithDb(db => db.PasswordResetTokens.Where(t => t.TokenHash == Hashing.Sha256(token))
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.ExpiresAt, api.Clock.GetUtcNow().AddSeconds(-1))));
        Assert.Equal(HttpStatusCode.BadRequest, (await Reset(token, "Brand-new-Passw0rd")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Reset("made-up-token", "Brand-new-Passw0rd")).StatusCode);

        // Staff can read the test outbox on a test server.
        var a = await api.AdminAsync();
        var outbox = await a.GetAsync("/api/v1/admin/test-emails").Ok();
        Assert.Contains(outbox.EnumerateArray(), e => e.Str("to") == email);
        var student = await api.StudentAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/v1/admin/test-emails")).StatusCode);
    }

    [Fact]
    public void The_email_names_the_brand_link_and_expiry_in_English_and_Hindi()
    {
        var m = LearnPath.Api.Modules.Identity.PasswordResetController.ResetEmail("Asha <b>", "asha@test.local", "https://web.test/reset-password?token=abc", "LearnPath");
        Assert.Contains("https://web.test/reset-password?token=abc", m.Text);
        Assert.Contains("30 minutes", m.Text);
        Assert.Contains("मिनट", m.Text);
        Assert.Contains("Asha &lt;b&gt;", m.Html); // names are escaped in HTML
        Assert.Equal("as•••@test.local", LearnPath.Api.Modules.Identity.PasswordResetController.Mask("asha@test.local"));
    }
}

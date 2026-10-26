using System.Net;
using System.Net.Http.Json;

namespace LearnPath.Tests;

[Collection("api")]
public class AuthTests(ApiFactory api)
{
    [Fact]
    public async Task Register_login_and_wrong_password()
    {
        var c = api.CreateClient();
        var email = $"a{Guid.NewGuid():N}@test.local";
        var reg = await c.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "Passw0rd!x", fullName = "A", acceptTerms = true }).Ok();
        Assert.False(reg.GetProperty("onboardingComplete").GetBoolean());

        var dup = await c.PostAsJsonAsync("/api/v1/auth/register", new { email = email.ToUpperInvariant(), password = "Passw0rd!x", fullName = "A", acceptTerms = true });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Passw0rd!x" }).Ok();
        var bad = await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "nope-nope" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal("invalid_credentials", (await bad.ReadJson()).Str("code"));
    }

    [Fact]
    public async Task Registration_requires_terms_and_valid_input()
    {
        var c = api.CreateClient();
        var noTerms = await c.PostAsJsonAsync("/api/v1/auth/register", new { email = $"t{Guid.NewGuid():N}@test.local", password = "Passw0rd!x", fullName = "A", acceptTerms = false });
        Assert.Equal(HttpStatusCode.BadRequest, noTerms.StatusCode);
        var shortPw = await c.PostAsJsonAsync("/api/v1/auth/register", new { email = "x@test.local", password = "short", fullName = "A", acceptTerms = true });
        Assert.Equal(HttpStatusCode.BadRequest, shortPw.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_rotates_and_reuse_revokes_the_family()
    {
        var c = api.CreateClient();
        var reg = await c.PostAsJsonAsync("/api/v1/auth/register", new { email = $"r{Guid.NewGuid():N}@test.local", password = "Passw0rd!x", fullName = "R", acceptTerms = true }).Ok();
        var first = reg.Str("refreshToken");
        var rotated = await c.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first }).Ok();
        var second = rotated.Str("refreshToken");
        Assert.NotEqual(first, second);

        var reuse = await c.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        // Reuse of a rotated token is treated as theft: the newer token is revoked too.
        var after = await c.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = second });
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Otp_login_creates_account_and_limits_wrong_codes()
    {
        var c = api.CreateClient();
        var phone = "9" + Random.Shared.NextInt64(100_000_000, 999_999_999);
        Assert.Equal(HttpStatusCode.Accepted, (await c.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone })).StatusCode);
        var tooSoon = await c.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone });
        Assert.Equal((HttpStatusCode)429, tooSoon.StatusCode);

        var code = api.Otp.Last["+91" + phone];
        var wrong = code == "000000" ? "111111" : "000000";
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code = wrong, acceptTerms = true })).StatusCode);
        var ok = await c.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code, fullName = "Otp User", acceptTerms = true }).Ok();
        Assert.Contains("Student", ok.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));

        // Code is single-use.
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code, acceptTerms = true })).StatusCode);
    }

    [Fact]
    public async Task Onboarding_requires_exam_and_guardian_consent_for_minors()
    {
        var c = await api.StudentAsync();
        var noExam = await c.PutAsJsonAsync("/api/v1/student/profile", new { completeOnboarding = true });
        Assert.Equal("exam_required", (await noExam.ReadJson()).Str("code"));

        var exam = (await c.GetAsync("/api/v1/exams").Ok())[0].Id("id");
        var minor = await c.PutAsJsonAsync("/api/v1/student/profile", new { targetExamId = exam, isMinor = true, completeOnboarding = true });
        Assert.Equal("guardian_consent_required", (await minor.ReadJson()).Str("code"));

        var done = await c.PutAsJsonAsync("/api/v1/student/profile", new { targetExamId = exam, isMinor = true, guardianName = "Parent", guardianConsent = true, completeOnboarding = true }).Ok();
        Assert.True(done.GetProperty("onboardingComplete").GetBoolean());
    }

    [Fact]
    public async Task Students_cannot_reach_admin_endpoints_and_anonymous_cannot_reach_student_ones()
    {
        var student = await api.StudentAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/v1/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/v1/admin/questions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/v1/student/dashboard")).StatusCode);
    }
}

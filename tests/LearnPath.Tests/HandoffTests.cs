using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

/// <summary>"Buy on website": the app gets a one-time code and the website signs the student in with it.</summary>
[Collection("api")]
public class HandoffTests(ApiFactory api)
{
    private async Task<(HttpClient Phone, string Email)> PhoneStudentAsync()
    {
        var email = $"s{Guid.NewGuid():N}@test.local";
        var res = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email, password = "Passw0rd!x", fullName = "App Student", acceptTerms = true, deviceSlot = "phone", deviceId = "phone-" + email });
        var body = await res.ReadJson();
        var c = api.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Str("accessToken"));
        return (c, email);
    }

    private Task<HttpResponseMessage> Redeem(string code, string deviceId, bool takeOver = false) =>
        api.CreateClient().PostAsJsonAsync("/api/v1/auth/handoff/redeem", new { code, deviceSlot = "browser", deviceName = "Windows · Chrome", deviceId, takeOver });

    [Fact]
    public async Task A_code_from_the_app_signs_the_student_in_on_the_website_once()
    {
        var (phone, _) = await PhoneStudentAsync();
        var handoff = await phone.PostAsync("/api/v1/auth/handoff", null).Ok();
        var code = handoff.Str("code");
        Assert.True(code.Length >= 40);

        var tokens = await (await Redeem(code, "browser-1")).ReadJson();
        var web = api.CreateClient();
        web.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Str("accessToken"));
        var profile = await web.GetAsync("/api/v1/student/profile").Ok();
        Assert.Equal("App Student", profile.Str("fullName"));

        // The phone stays signed in alongside the browser (one of each is allowed).
        await phone.GetAsync("/api/v1/student/profile").Ok();

        var reused = await Redeem(code, "browser-1");
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Equal("handoff_invalid", (await reused.ReadJson()).Str("code"));
        var userId = Guid.Parse(profile.Str("userId"));
        Assert.Equal(1, await api.WithDb(db => db.SignInHandoffs.CountAsync(h => h.UserId == userId && h.UsedAt != null)));
    }

    [Fact]
    public async Task Codes_expire_need_a_signed_in_app_and_respect_the_one_browser_rule()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().PostAsync("/api/v1/auth/handoff", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem("not-a-real-code", "browser-x")).StatusCode);

        var (phone, _) = await PhoneStudentAsync();
        var old = (await phone.PostAsync("/api/v1/auth/handoff", null).Ok()).Str("code");
        // Two minutes later (moved in the database: the shared test clock also drives other tests' tokens).
        await api.WithDb(db => db.SignInHandoffs.Where(h => h.CodeHash == LearnPath.Api.Infrastructure.Hashing.Sha256(old))
            .ExecuteUpdateAsync(x => x.SetProperty(h => h.ExpiresAt, api.Clock.GetUtcNow().AddSeconds(-1))));
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(old, "browser-1")).StatusCode);

        // Signed in on one browser already: another browser is asked first, and the code survives the question.
        await (await Redeem((await phone.PostAsync("/api/v1/auth/handoff", null).Ok()).Str("code"), "browser-1")).ReadJson();
        var code = (await phone.PostAsync("/api/v1/auth/handoff", null).Ok()).Str("code");
        var asked = await Redeem(code, "browser-2");
        Assert.Equal(HttpStatusCode.Conflict, asked.StatusCode);
        Assert.Equal("session_active", (await asked.ReadJson()).Str("code"));
        var confirmed = await Redeem(code, "browser-2", takeOver: true);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }
}

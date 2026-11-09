using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LearnPath.Tests;

/// <summary>One person per student account: one phone and one browser at a time; another device must confirm taking over.</summary>
[Collection("api")]
public class SessionTests(ApiFactory api)
{
    private const string Password = "Passw0rd!x";

    private async Task<string> NewStudentAsync()
    {
        var email = $"s{Guid.NewGuid():N}@test.local";
        var res = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new { email, password = Password, fullName = "Single User", acceptTerms = true, deviceSlot = "phone", deviceId = "setup-" + email });
        res.EnsureSuccessStatusCode();
        // Free the slot used to create the account.
        var tokens = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        await api.CreateClient().PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = tokens.GetProperty("refreshToken").GetString() });
        return email;
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Login(HttpClient c, string email, string slot, string deviceId, bool takeOver = false, string? name = null)
    {
        var res = await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password, deviceSlot = slot, deviceId, deviceName = name, takeOver });
        var text = await res.Content.ReadAsStringAsync();
        return (res.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private HttpClient As(JsonElement tokens)
    {
        var c = api.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        return c;
    }

    private static async Task<(HttpStatusCode, string?)> Profile(HttpClient c)
    {
        var res = await c.GetAsync("/api/v1/student/profile");
        var text = await res.Content.ReadAsStringAsync();
        return (res.StatusCode, res.IsSuccessStatusCode || text.Length == 0 ? null : JsonDocument.Parse(text).RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_second_phone_is_asked_first_and_taking_over_signs_the_first_phone_out_at_once()
    {
        var email = await NewStudentAsync();
        var (s1, phoneA) = await Login(api.CreateClient(), email, "phone", "phone-A", name: "Android app");
        Assert.Equal(HttpStatusCode.OK, s1);

        var (s2, conflict) = await Login(api.CreateClient(), email, "phone", "phone-B");
        Assert.Equal(HttpStatusCode.Conflict, s2);
        Assert.Equal(("session_active", "Android app", "phone"), (conflict.Str("code"), conflict.Str("deviceName"), conflict.Str("deviceSlot")));
        Assert.True(conflict.TryGetProperty("lastActiveAt", out _));
        Assert.Equal((HttpStatusCode.OK, null), await Profile(As(phoneA))); // asking changed nothing

        var (s3, phoneB) = await Login(api.CreateClient(), email, "phone", "phone-B", takeOver: true);
        Assert.Equal(HttpStatusCode.OK, s3);
        Assert.Equal((HttpStatusCode.Unauthorized, "session_replaced"), await Profile(As(phoneA))); // immediately, not in 15 minutes
        // Even public pages refuse the old token, so the device learns it was signed out.
        var publicPage = await As(phoneA).GetAsync("/api/v1/courses");
        Assert.Equal((HttpStatusCode.Unauthorized, "session_replaced"), (publicPage.StatusCode, (await publicPage.ReadJson()).Str("code")));
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClient().GetAsync("/api/v1/courses")).StatusCode); // visitors are unaffected
        var refresh = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = phoneA.Str("refreshToken") });
        Assert.Equal((HttpStatusCode.Unauthorized, "session_replaced"), (refresh.StatusCode, (await refresh.ReadJson()).Str("code")));
        Assert.Equal((HttpStatusCode.OK, null), await Profile(As(phoneB)));
    }

    [Fact]
    public async Task One_phone_and_one_browser_work_together()
    {
        var email = await NewStudentAsync();
        var (p, phone) = await Login(api.CreateClient(), email, "phone", "phone-A");
        var (b, browser) = await Login(api.CreateClient(), email, "browser", "laptop-A");
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (p, b));
        Assert.Equal((HttpStatusCode.OK, null), await Profile(As(phone)));
        Assert.Equal((HttpStatusCode.OK, null), await Profile(As(browser)));

        var (third, _) = await Login(api.CreateClient(), email, "browser", "laptop-B");
        Assert.Equal(HttpStatusCode.Conflict, third);
    }

    [Fact]
    public async Task The_same_device_signs_in_again_without_a_prompt_and_signing_out_frees_the_slot()
    {
        var email = await NewStudentAsync();
        var (_, first) = await Login(api.CreateClient(), email, "phone", "phone-A");
        var (again, second) = await Login(api.CreateClient(), email, "phone", "phone-A");
        Assert.Equal(HttpStatusCode.OK, again);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Profile(As(first))).Item1); // its own old session is replaced

        await api.CreateClient().PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = second.Str("refreshToken") });
        var (afterSignOut, _) = await Login(api.CreateClient(), email, "phone", "phone-B");
        Assert.Equal(HttpStatusCode.OK, afterSignOut);
    }

    [Fact]
    public async Task Staff_accounts_are_not_limited()
    {
        var c1 = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword, deviceSlot = "browser", deviceId = "office-1" });
        var c2 = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword, deviceSlot = "browser", deviceId = "office-2" });
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (c1.StatusCode, c2.StatusCode));
    }

    [Fact]
    public async Task Mobile_code_sign_in_asks_too_and_the_code_still_works_for_the_confirmed_retry()
    {
        var digits = "7" + Random.Shared.NextInt64(100_000_000, 999_999_999);
        var c = api.CreateClient();
        await c.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone = digits });
        var first = await c.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = digits, code = api.Otp.Last["+91" + digits], fullName = "Phone User", acceptTerms = true, deviceSlot = "phone", deviceId = "phone-A" });
        first.EnsureSuccessStatusCode();

        api.Clock.Advance(TimeSpan.FromSeconds(31));
        await c.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone = digits });
        var code = api.Otp.Last["+91" + digits];
        var asked = await c.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = digits, code, deviceSlot = "phone", deviceId = "phone-B" });
        Assert.Equal((HttpStatusCode.Conflict, "session_active"), (asked.StatusCode, (await asked.ReadJson()).Str("code")));

        var confirmed = await c.PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone = digits, code, deviceSlot = "phone", deviceId = "phone-B", takeOver = true });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_signs_out_every_device()
    {
        var email = await NewStudentAsync();
        var (_, phone) = await Login(api.CreateClient(), email, "phone", "phone-A");
        var (_, browser) = await Login(api.CreateClient(), email, "browser", "laptop-A");
        var change = await As(phone).PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = Password, newPassword = "N3w-Passw0rd!" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal((HttpStatusCode.Unauthorized, "session_ended"), await Profile(As(browser)));
        Assert.Equal((HttpStatusCode.OK, null), await Profile(As(phone))); // the device that changed it stays signed in
    }

    [Fact]
    public async Task Without_a_slot_the_device_is_read_from_the_user_agent()
    {
        var email = await NewStudentAsync();
        var iphone = api.CreateClient();
        iphone.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1");
        (await iphone.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password, deviceId = "iphone-1" })).EnsureSuccessStatusCode();

        var (status, conflict) = await Login(api.CreateClient(), email, "phone", "phone-B");
        Assert.Equal((HttpStatusCode.Conflict, "iPhone · Safari"), (status, conflict.Str("deviceName")));
    }
}

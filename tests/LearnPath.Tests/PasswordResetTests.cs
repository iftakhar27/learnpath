using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

/// <summary>Support sets a temporary password for a student who can't sign in; the student then changes it.</summary>
[Collection("api")]
public class PasswordResetTests(ApiFactory api)
{
    private async Task<(string Email, string UserId, HttpClient Client)> StudentAsync(string password = "Passw0rd!x")
    {
        var email = $"s{Guid.NewGuid():N}@test.local";
        var body = await (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, fullName = "Forgetful Student", acceptTerms = true, deviceSlot = "phone", deviceId = "phone-" + email })).ReadJson();
        return (email, body.Str("userId"), Bearer(body.Str("accessToken")));
    }

    private HttpClient Bearer(string token)
    {
        var c = api.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> Login(string email, string password, string deviceId)
    {
        var res = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password, deviceSlot = "phone", deviceId });
        return (res.StatusCode, await res.ReadJson());
    }

    [Fact]
    public async Task Support_resets_a_password_the_old_one_stops_working_and_the_student_is_signed_out()
    {
        var (email, userId, app) = await StudentAsync();
        var a = await api.AdminAsync();
        var temporary = (await a.PostAsync($"/api/v1/admin/users/{userId}/reset-password", null).Ok()).Str("temporaryPassword");
        Assert.Matches("^[A-HJ-NP-Za-hj-np-z2-9]{4}-[A-HJ-NP-Za-hj-np-z2-9]{4}-[A-HJ-NP-Za-hj-np-z2-9]{4}$", temporary);

        // The phone that was signed in is signed out with a reason.
        var old = await app.GetAsync("/api/v1/student/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        Assert.Equal("session_ended", (await old.ReadJson()).Str("code"));

        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, "Passw0rd!x", "phone-" + email)).Status);
        var (status, tokens) = await Login(email, temporary, "phone-" + email);
        Assert.Equal(HttpStatusCode.OK, status);
        var student = Bearer(tokens.Str("accessToken"));
        var note = (await student.GetAsync("/api/v1/notifications").Ok()).GetProperty("items")[0];
        Assert.Equal("Your password was reset", note.Str("title"));
        Assert.True(await api.WithDb(db => db.AuditLogs.AnyAsync(l => l.Action == "user.password_reset" && l.EntityId == userId)));

        // Changing it keeps this phone signed in and the new password works.
        var change = await student.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = temporary, newPassword = "My-own-Passw0rd" });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        await student.GetAsync("/api/v1/student/profile").Ok();
        Assert.Equal(HttpStatusCode.OK, (await Login(email, "My-own-Passw0rd", "phone-" + email)).Status);
    }

    [Fact]
    public async Task Resets_are_for_staff_and_only_a_super_admin_can_reset_staff()
    {
        var (_, studentId, student) = await StudentAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsync($"/api/v1/admin/users/{studentId}/reset-password", null)).StatusCode);

        var a = await api.AdminAsync();
        var adminId = (await a.GetAsync("/api/v1/student/profile").Ok()).Str("userId");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsync($"/api/v1/admin/users/{adminId}/reset-password", null)).StatusCode); // own account

        // A support agent can reset students but not other staff.
        var (supportEmail, supportId, _) = await StudentAsync("Supp0rt-Pass!");
        await a.PostAsJsonAsync($"/api/v1/admin/users/{supportId}/roles", new { role = "Support" }).Ok();
        var (_, supportTokens) = await Login(supportEmail, "Supp0rt-Pass!", "support-phone");
        var support = Bearer(supportTokens.Str("accessToken"));
        await support.PostAsync($"/api/v1/admin/users/{studentId}/reset-password", null).Ok();
        Assert.Equal(HttpStatusCode.Forbidden, (await support.PostAsync($"/api/v1/admin/users/{adminId}/reset-password", null)).StatusCode);

        // Mobile-number accounts have no password.
        var phone = "98" + Random.Shared.Next(10_000_000, 99_999_999);
        await api.CreateClient().PostAsJsonAsync("/api/v1/auth/otp/request", new { phone });
        var code = api.Otp.Last["+91" + phone];
        var otpUser = await (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/otp/verify", new { phone, code, fullName = "Phone Only", acceptTerms = true })).ReadJson();
        var res = await a.PostAsync($"/api/v1/admin/users/{otpUser.Str("userId")}/reset-password", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("no_email", (await res.ReadJson()).Str("code"));
    }
}

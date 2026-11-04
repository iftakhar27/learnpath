using System.Net;
using System.Net.Http.Json;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LearnPath.Tests;

public class TestOtpOutboxTests
{
    [Fact]
    public async Task Logging_sender_keeps_masked_codes_newest_first_with_expiry()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-11-04T10:00:00Z"));
        var outbox = new TestOtpOutbox();
        var sender = new LoggingOtpSender(NullLogger<LoggingOtpSender>.Instance, outbox, clock);

        await sender.SendAsync("+919876543210", "111111", default);
        clock.Advance(TimeSpan.FromMinutes(1));
        await sender.SendAsync("+919123400000", "222222", default);

        var recent = outbox.Recent(clock.GetUtcNow());
        Assert.Equal(["222222", "111111"], recent.Select(r => r.Code));
        Assert.Equal("•••••••••3210", recent[1].Phone);
        Assert.Equal(recent[1].SentAt + OtpPolicy.Lifetime, recent[1].ExpiresAt);
    }

    [Fact]
    public void Keeps_at_most_twenty_codes_for_an_hour()
    {
        var now = DateTimeOffset.Parse("2026-11-04T10:00:00Z");
        var outbox = new TestOtpOutbox();
        outbox.Add("+919000000000", "old", now.AddMinutes(-61));
        for (var i = 0; i < 25; i++) outbox.Add("+919000000000", $"c{i}", now);

        var recent = outbox.Recent(now);
        Assert.Equal(TestOtpOutbox.Capacity, recent.Count);
        Assert.Equal("c24", recent[0].Code);
        Assert.DoesNotContain(recent, r => r.Code == "old");
    }
}

[Collection("api")]
public class TestOtpCodesApiTests(ApiFactory api)
{
    [Fact]
    public async Task Support_staff_read_codes_and_students_cannot()
    {
        api.Services.GetRequiredService<TestOtpOutbox>().Add("+919876500042", "424242", api.Clock.GetUtcNow());

        var admin = await api.AdminAsync();
        var codes = await admin.GetAsync("/api/v1/admin/test-otp-codes").Ok();
        var first = codes.EnumerateArray().First();
        Assert.Equal(("424242", "•••••••••0042"), (first.Str("code"), first.Str("phone")));

        var student = await api.StudentAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/v1/admin/test-otp-codes")).StatusCode);
    }

    [Fact]
    public async Task Endpoint_does_not_exist_when_codes_are_not_logged()
    {
        await using var real = api.WithWebHostBuilder(b => b.UseSetting("Otp:Provider", "disabled"));
        Assert.Null(real.Services.GetService<TestOtpOutbox>());

        var login = await real.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword }).Ok();
        var admin = real.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new("Bearer", login.Str("accessToken"));
        var res = await admin.GetAsync("/api/v1/admin/test-otp-codes");
        Assert.Equal((HttpStatusCode.NotFound, "not_found"), (res.StatusCode, (await res.ReadJson()).Str("code")));
    }
}

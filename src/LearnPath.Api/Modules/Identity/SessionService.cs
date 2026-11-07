using System.Text.RegularExpressions;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LearnPath.Api.Modules.Identity;

/// <summary>What a sign-in says about the device it comes from.</summary>
public record DeviceInfo(string Slot, string Name, string? DeviceId);

public enum SessionState { Active, Replaced, Ended }

/// <summary>
/// One person per student account: at most one active session per device slot (one phone, one browser). A sign-in on
/// another device of the same kind is refused with 409 "session_active" (naming the other device) unless the student
/// confirms with takeOver, which ends the other session at once. The same device signing in again needs no prompt.
/// Staff accounts are not limited.
/// </summary>
public partial class SessionService(AppDbContext db, TimeProvider clock, IMemoryCache cache)
{
    /// <summary>Access tokens are checked against their session at most this stale (ended sessions are dropped at once).</summary>
    private static readonly TimeSpan StateCacheFor = TimeSpan.FromSeconds(15);

    /// <summary>Uses what the client says about itself; otherwise reads the User-Agent (phones, tablets and the app count as phones).</summary>
    public static DeviceInfo Describe(string? slot, string? name, string? deviceId, string? userAgent)
    {
        var ua = userAgent ?? "";
        var resolvedSlot = slot?.Trim().ToLowerInvariant() is DeviceSlots.Phone or DeviceSlots.Browser
            ? slot.Trim().ToLowerInvariant()
            : MobileAgent().IsMatch(ua) ? DeviceSlots.Phone : DeviceSlots.Browser;
        var resolvedName = string.IsNullOrWhiteSpace(name) ? NameFromAgent(ua) : name.Trim();
        var id = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
        return new DeviceInfo(resolvedSlot, resolvedName.Length > 100 ? resolvedName[..100] : resolvedName, id is { Length: > 200 } ? id[..200] : id);
    }

    public static string NameFromAgent(string ua)
    {
        var os = ua.Contains("iPhone") ? "iPhone" : ua.Contains("iPad") ? "iPad" : ua.Contains("Android") ? "Android"
            : ua.Contains("Windows") ? "Windows" : ua.Contains("Mac OS X") ? "Mac" : ua.Contains("Linux") ? "Linux"
            : ua.StartsWith("Dart/") ? "Phone app" : "Unknown device";
        var browser = ua.Contains("Edg/") ? "Edge" : ua.Contains("OPR/") ? "Opera" : ua.Contains("SamsungBrowser") ? "Samsung Internet"
            : ua.Contains("Chrome/") || ua.Contains("CriOS/") ? "Chrome" : ua.Contains("Firefox/") || ua.Contains("FxiOS/") ? "Firefox"
            : ua.Contains("Safari/") ? "Safari" : null;
        return browser is null ? os : $"{os} · {browser}";
    }

    [GeneratedRegex("Mobi|Android|iPhone|iPad|iPod|^Dart/", RegexOptions.IgnoreCase)]
    private static partial Regex MobileAgent();

    public static bool IsLimited(User user) => !user.Roles.Any(r => Roles.Staff.Contains(r.Role));

    private IQueryable<UserSession> Active(Guid userId, DateTimeOffset now) =>
        db.UserSessions.Where(s => s.UserId == userId && s.EndedAt == null
                                   && db.RefreshTokens.Any(t => t.SessionId == s.Id && t.RevokedAt == null && t.ExpiresAt > now));

    /// <summary>
    /// Starts a session for a sign-in (saved together with the tokens). Throws 409 "session_active" when another
    /// device of the same kind is signed in and the student hasn't confirmed taking over.
    /// </summary>
    public async Task<UserSession> StartAsync(User user, DeviceInfo device, bool takeOver, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var deviceHash = device.DeviceId is null ? null : Hashing.Sha256(device.DeviceId);
        // The same device signing in again (e.g. after clearing cookies) simply replaces its own old session.
        var sameSlot = await Active(user.Id, now).Where(s => s.DeviceSlot == device.Slot).ToListAsync(ct);
        var sameDevice = sameSlot.Where(s => deviceHash != null && s.DeviceIdHash == deviceHash).ToList();
        var otherDevices = IsLimited(user) ? sameSlot.Except(sameDevice).ToList() : [];

        if (otherDevices.Count > 0 && !takeOver)
        {
            var other = otherDevices.MaxBy(s => s.LastSeenAt)!;
            throw new AppException(409, "session_active", "This account is already signed in on another device.", new Dictionary<string, object?>
            {
                ["deviceName"] = other.DeviceName,
                ["deviceSlot"] = other.DeviceSlot,
                ["lastActiveAt"] = other.LastSeenAt,
            });
        }

        var replaced = sameDevice.Concat(otherDevices).ToList();
        if (replaced.Count > 0) await EndAsync(replaced, SessionEndReasons.Replaced, now, ct);

        var session = new UserSession
        {
            UserId = user.Id, DeviceSlot = device.Slot, DeviceName = device.Name, DeviceIdHash = deviceHash, CreatedAt = now, LastSeenAt = now,
        };
        db.UserSessions.Add(session);
        return session;
    }

    /// <summary>Ends sessions and revokes their refresh tokens; their access tokens stop working on the next request.</summary>
    public async Task EndAsync(IReadOnlyCollection<UserSession> sessions, string reason, DateTimeOffset now, CancellationToken ct)
    {
        if (sessions.Count == 0) return;
        foreach (var s in sessions)
        {
            s.EndedAt ??= now;
            s.EndReason ??= reason;
        }
        // Record why first, so the signed-out device is told the reason, then revoke its tokens.
        await db.SaveChangesAsync(ct);
        var ids = sessions.Select(s => s.Id).ToList();
        await db.RefreshTokens.Where(t => t.SessionId != null && ids.Contains(t.SessionId.Value) && t.RevokedAt == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, now), ct);
        foreach (var s in sessions) cache.Remove(CacheKey(s.Id));
    }

    /// <summary>Ends every session of a user (password change, suspected token theft).</summary>
    public async Task EndAllAsync(Guid userId, string reason, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var open = await db.UserSessions.Where(s => s.UserId == userId && s.EndedAt == null).ToListAsync(ct);
        await EndAsync(open, reason, now, ct);
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAt, now), ct);
    }

    /// <summary>Whether an access token's session may still be used (cached briefly; replaced sessions show as such).</summary>
    public async Task<SessionState> GetStateAsync(Guid sessionId, CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey(sessionId), out SessionState cached)) return cached;
        var now = clock.GetUtcNow();
        var s = await db.UserSessions.AsNoTracking().Where(x => x.Id == sessionId)
            .Select(x => new { x.EndedAt, x.EndReason, Live = db.RefreshTokens.Any(t => t.SessionId == x.Id && t.RevokedAt == null && t.ExpiresAt > now) })
            .FirstOrDefaultAsync(ct);
        var state = s is null ? SessionState.Ended
            : s.EndedAt is null && s.Live ? SessionState.Active
            : s.EndReason == SessionEndReasons.Replaced ? SessionState.Replaced
            : SessionState.Ended;
        cache.Set(CacheKey(sessionId), state, state == SessionState.Active ? StateCacheFor : TimeSpan.FromMinutes(30));
        return state;
    }

    private static string CacheKey(Guid id) => $"session-state:{id}";
}

public static class SessionResponses
{
    /// <summary>401 telling the device why its session is over ("session_replaced" or "session_ended").</summary>
    public static Task WriteEndedAsync(HttpContext ctx, SessionState state)
    {
        ctx.Response.StatusCode = 401;
        var (code, title) = state == SessionState.Replaced
            ? (TokenService.Replaced().Code, TokenService.Replaced().Message)
            : ("session_ended", "Your session ended. Please sign in again.");
        return ctx.Response.WriteAsJsonAsync(new { status = 401, code, title }, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace LearnPath.Api.Modules.Identity;

/// <summary>Claim carrying the session id in access tokens (checked on every request, so a replaced device is out at once).</summary>
public static class SessionClaims
{
    public const string SessionId = "lp_sid";
}

public record AuthTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, Guid UserId, string[] Roles, bool OnboardingComplete);

public class JwtOptions
{
    public string Issuer { get; set; } = "learnpath";
    public string Audience { get; set; } = "learnpath-clients";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public class TokenService(AppDbContext db, JwtOptions options, TimeProvider clock, SessionService sessions)
{
    public async Task<AuthTokens> IssueAsync(User user, UserSession session, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var roles = user.Roles.Select(r => r.Role).ToArray();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(SessionClaims.SessionId, session.Id.ToString()),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var expires = now.AddMinutes(options.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var jwt = new JwtSecurityToken(options.Issuer, options.Audience, claims, now.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var refresh = NewOpaqueToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hashing.Sha256(refresh),
            CreatedAt = now,
            ExpiresAt = now.AddDays(options.RefreshTokenDays),
            SessionId = session.Id,
        });
        session.LastSeenAt = now;
        user.LastLoginAt = now;
        await db.SaveChangesAsync(ct);

        var onboarded = await db.StudentProfiles.AnyAsync(p => p.UserId == user.Id && p.OnboardedAt != null, ct);
        return new AuthTokens(new JwtSecurityTokenHandler().WriteToken(jwt), expires, refresh, user.Id, roles, onboarded);
    }

    public static AppException Replaced() =>
        new(401, "session_replaced", "You were signed out because your account was signed in on another device.");

    /// <summary>
    /// Rotates a refresh token. Reuse of an already-rotated token ends every session (theft signal); a token of a
    /// session replaced by another device gets "session_replaced" so the device can say why it was signed out.
    /// </summary>
    public async Task<AuthTokens> RefreshAsync(string refreshToken, DeviceInfo deviceHint, CancellationToken ct)
    {
        var hash = Hashing.Sha256(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                     ?? throw new AppException(401, "invalid_refresh_token", "Session expired. Please log in again.");
        var now = clock.GetUtcNow();
        var session = stored.SessionId is null ? null : await db.UserSessions.FirstOrDefaultAsync(x => x.Id == stored.SessionId, ct);
        if (session?.EndReason == SessionEndReasons.Replaced) throw Replaced();
        if (stored.RevokedAt != null || session?.EndedAt != null)
        {
            if (stored.RevokedAt != null && session?.EndedAt == null)
                await sessions.EndAllAsync(stored.UserId, SessionEndReasons.Security, ct);
            throw new AppException(401, "invalid_refresh_token", "Session expired. Please log in again.");
        }
        if (stored.ExpiresAt <= now) throw new AppException(401, "invalid_refresh_token", "Session expired. Please log in again.");

        var user = await db.Users.Include(u => u.Roles).FirstAsync(u => u.Id == stored.UserId, ct);
        if (user.Status != UserStatus.Active) throw new AppException(401, "account_inactive", "This account is not active.");

        // Tokens from before sessions existed join a new session for this device.
        session ??= await sessions.StartAsync(user, deviceHint, takeOver: true, ct);
        stored.RevokedAt = now;
        var tokens = await IssueAsync(user, session, ct);
        stored.ReplacedById = (await db.RefreshTokens.FirstAsync(t => t.TokenHash == Hashing.Sha256(tokens.RefreshToken), ct)).Id;
        await db.SaveChangesAsync(ct);
        return tokens;
    }

    /// <summary>Signing out ends the device's session (its slot is free again).</summary>
    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hashing.Sha256(refreshToken);
        var stored = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return;
        var session = stored.SessionId is null ? null : await db.UserSessions.FirstOrDefaultAsync(x => x.Id == stored.SessionId && x.EndedAt == null, ct);
        if (session is not null) await sessions.EndAsync([session], SessionEndReasons.SignedOut, clock.GetUtcNow(), ct);
        await db.RefreshTokens.Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.GetUtcNow()), ct);
    }

    private static string NewOpaqueToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

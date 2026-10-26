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

public record AuthTokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, Guid UserId, string[] Roles, bool OnboardingComplete);

public class JwtOptions
{
    public string Issuer { get; set; } = "learnpath";
    public string Audience { get; set; } = "learnpath-clients";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public class TokenService(AppDbContext db, JwtOptions options, TimeProvider clock)
{
    public async Task<AuthTokens> IssueAsync(User user, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var roles = user.Roles.Select(r => r.Role).ToArray();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
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
        });
        user.LastLoginAt = now;
        await db.SaveChangesAsync(ct);

        var onboarded = await db.StudentProfiles.AnyAsync(p => p.UserId == user.Id && p.OnboardedAt != null, ct);
        return new AuthTokens(new JwtSecurityTokenHandler().WriteToken(jwt), expires, refresh, user.Id, roles, onboarded);
    }

    /// <summary>Rotates a refresh token. Reuse of an already-rotated token revokes the whole family (theft signal).</summary>
    public async Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hashing.Sha256(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                     ?? throw new AppException(401, "invalid_refresh_token", "Session expired. Please log in again.");
        var now = clock.GetUtcNow();
        if (stored.RevokedAt != null)
        {
            await db.RefreshTokens.Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            throw new AppException(401, "invalid_refresh_token", "Session expired. Please log in again.");
        }
        if (stored.ExpiresAt <= now) throw new AppException(401, "invalid_refresh_token", "Session expired. Please log in again.");

        var user = await db.Users.Include(u => u.Roles).FirstAsync(u => u.Id == stored.UserId, ct);
        if (user.Status != UserStatus.Active) throw new AppException(401, "account_inactive", "This account is not active.");

        stored.RevokedAt = now;
        var tokens = await IssueAsync(user, ct);
        stored.ReplacedById = (await db.RefreshTokens.FirstAsync(t => t.TokenHash == Hashing.Sha256(tokens.RefreshToken), ct)).Id;
        await db.SaveChangesAsync(ct);
        return tokens;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hashing.Sha256(refreshToken);
        await db.RefreshTokens.Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.GetUtcNow()), ct);
    }

    private static string NewOpaqueToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

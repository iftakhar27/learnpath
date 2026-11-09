using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Identity;

public record ForgotPasswordRequest([Required, EmailAddress, MaxLength(256)] string Email);
public record ResetPasswordRequest([Required, MaxLength(100)] string Token, [Required, MinLength(8), MaxLength(128)] string NewPassword);

/// <summary>
/// "Forgot password?": emails a link to the website's /reset-password page (single use, valid 30 minutes). The answer
/// is the same whether or not the email has an account, so it can't be used to find accounts.
/// </summary>
[ApiController]
[Route("api/v1/auth/password")]
[EnableRateLimiting("auth")]
public class PasswordResetController(AppDbContext db, SessionService sessions, IEmailSender email, TimeProvider clock, IConfiguration config,
    ILogger<PasswordResetController> logger) : ControllerBase
{
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(30);
    /// <summary>At most one email a minute and five an hour per account, so the form can't be used to flood an inbox.</summary>
    private static readonly TimeSpan MinGap = TimeSpan.FromMinutes(1);
    private const int MaxPerHour = 5;

    [HttpPost("forgot")]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest req, CancellationToken ct)
    {
        var address = req.Email.Trim().ToLowerInvariant();
        var now = clock.GetUtcNow();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address && u.Status == UserStatus.Active && u.PasswordHash != null, ct);
        if (user is null)
        {
            logger.LogInformation("Password reset asked for {Email}, which has no active account", Mask(address));
            return Accepted();
        }
        var recent = await db.PasswordResetTokens.Where(t => t.UserId == user.Id && t.CreatedAt > now.AddHours(-1)).Select(t => t.CreatedAt).ToListAsync(ct);
        if (recent.Count >= MaxPerHour || recent.Any(c => c > now - MinGap))
        {
            logger.LogInformation("Password reset for {Email} not sent again yet (limit)", Mask(address));
            return Accepted();
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        db.PasswordResetTokens.Add(new PasswordResetToken { UserId = user.Id, TokenHash = Hashing.Sha256(token), CreatedAt = now, ExpiresAt = now + LinkLifetime });
        await db.SaveChangesAsync(ct);
        var link = $"{config["Web:BaseUrl"]!.TrimEnd('/')}/reset-password?token={token}";
        try
        {
            await email.SendAsync(ResetEmail(user.FullName, user.Email!, link, config["Branding:ProductName"] ?? "LearnPath"), ct);
        }
        catch (Exception e)
        {
            // Same answer either way (it would reveal that the account exists); the reason is in the log.
            logger.LogError(e, "Password reset email to {Email} failed", Mask(address));
        }
        return Accepted();
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(ResetPasswordRequest req, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var reset = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == Hashing.Sha256(req.Token), ct);
        if (reset is null || reset.UsedAt != null || reset.ExpiresAt <= now)
            throw AppException.BadRequest("reset_invalid", "This reset link has expired or was already used. Ask for a new one.");
        var user = await db.Users.FirstAsync(u => u.Id == reset.UserId, ct);
        if (user.Status != UserStatus.Active) throw new AppException(403, "account_inactive", "This account is not active. Contact support.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 11);
        // This link and any other outstanding ones for the account stop working.
        await db.PasswordResetTokens.Where(t => t.UserId == user.Id && t.UsedAt == null).ExecuteUpdateAsync(x => x.SetProperty(t => t.UsedAt, now), ct);
        await db.SaveChangesAsync(ct);
        await sessions.EndAllAsync(user.Id, SessionEndReasons.PasswordReset, ct);
        return NoContent();
    }

    public static EmailMessage ResetEmail(string name, string to, string link, string brand)
    {
        var minutes = (int)LinkLifetime.TotalMinutes;
        var text = $"""
            Hi {name},

            We received a request to reset the password of your {brand} account. Open this link within {minutes} minutes to choose a new password:

            {link}

            If you didn't ask for this, ignore this email; your password stays the same.

            नया पासवर्ड चुनने के लिए {minutes} मिनट के अंदर ऊपर दिया लिंक खोलें। अगर आपने यह अनुरोध नहीं किया, तो इस ईमेल को अनदेखा करें।
            """;
        string H(string s) => WebUtility.HtmlEncode(s);
        var html = $"""
            <div style="font-family:Arial,sans-serif;font-size:15px;line-height:1.5;color:#1f2937;max-width:520px">
              <p>Hi {H(name)},</p>
              <p>We received a request to reset the password of your {H(brand)} account. Use the button within {minutes} minutes to choose a new password.</p>
              <p><a href="{H(link)}" style="display:inline-block;background:#4f46e5;color:#ffffff;padding:10px 18px;border-radius:8px;text-decoration:none;font-weight:600">Choose a new password</a></p>
              <p style="font-size:13px;color:#6b7280">Or open this link: <a href="{H(link)}">{H(link)}</a></p>
              <p style="font-size:13px;color:#6b7280">If you didn't ask for this, ignore this email; your password stays the same.</p>
              <p style="font-size:13px;color:#6b7280">नया पासवर्ड चुनने के लिए {minutes} मिनट के अंदर ऊपर दिया लिंक खोलें। अगर आपने यह अनुरोध नहीं किया, तो इस ईमेल को अनदेखा करें।</p>
            </div>
            """;
        return new EmailMessage(to, name, $"Reset your {brand} password", text, html);
    }

    /// <summary>"ab•••@gmail.com": enough to recognise in a log, without writing the whole address.</summary>
    public static string Mask(string address)
    {
        var at = address.IndexOf('@');
        return at <= 2 ? "•••" + address[Math.Max(0, at)..] : address[..2] + "•••" + address[at..];
    }
}

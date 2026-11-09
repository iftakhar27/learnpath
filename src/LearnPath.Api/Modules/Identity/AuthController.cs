using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Identity;

// Sign-ins may describe the device: DeviceSlot "phone" or "browser" (default: from the User-Agent), a DeviceName shown to
// the student, and a DeviceId (the device's own random id). TakeOver confirms signing out another device of the same kind.
public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required, MaxLength(200)] string FullName,
    [Required] bool AcceptTerms,
    [MaxLength(16)] string? DeviceSlot = null, [MaxLength(100)] string? DeviceName = null, [MaxLength(200)] string? DeviceId = null);

public record LoginRequest([Required] string Email, [Required] string Password,
    [MaxLength(16)] string? DeviceSlot = null, [MaxLength(100)] string? DeviceName = null, [MaxLength(200)] string? DeviceId = null, bool? TakeOver = null);
public record OtpRequest([Required] string Phone);
public record OtpVerifyRequest([Required] string Phone, [Required, StringLength(6, MinimumLength = 6)] string Code, string? FullName, bool AcceptTerms,
    [MaxLength(16)] string? DeviceSlot = null, [MaxLength(100)] string? DeviceName = null, [MaxLength(200)] string? DeviceId = null, bool? TakeOver = null);
public record RefreshRequest([Required] string RefreshToken);
public record HandoffDto(string Code, DateTimeOffset ExpiresAt);
public record HandoffRedeemRequest([Required, MaxLength(100)] string Code,
    [MaxLength(16)] string? DeviceSlot = null, [MaxLength(100)] string? DeviceName = null, [MaxLength(200)] string? DeviceId = null, bool? TakeOver = null);

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public partial class AuthController(AppDbContext db, TokenService tokens, SessionService sessions, IOtpSender otpSender, TimeProvider clock, IConfiguration config) : ControllerBase
{
    private DeviceInfo Device(string? slot, string? name, string? id) => SessionService.Describe(slot, name, id, Request.Headers.UserAgent.ToString());

    private const string TermsVersion = "2026-01";

    [HttpPost("register")]
    public async Task<ActionResult<AuthTokens>> Register(RegisterRequest req, CancellationToken ct)
    {
        if (!req.AcceptTerms) throw AppException.BadRequest("terms_required", "You must accept the terms and privacy policy.");
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw AppException.Conflict("email_taken", "An account with this email already exists.");

        var user = NewStudent(req.FullName.Trim());
        user.Email = email;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 11);
        db.Users.Add(user);
        AddTermsConsent(user.Id);
        await db.SaveChangesAsync(ct);
        var session = await sessions.StartAsync(user, Device(req.DeviceSlot, req.DeviceName, req.DeviceId), takeOver: false, ct);
        return Ok(await tokens.IssueAsync(user, session, ct));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthTokens>> Login(LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == email, ct);
        // Same error for unknown email and wrong password to avoid account enumeration.
        if (user?.PasswordHash is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            throw new AppException(401, "invalid_credentials", "Incorrect email or password.");
        if (user.Status != UserStatus.Active) throw new AppException(403, "account_inactive", "This account is not active. Contact support.");
        var session = await sessions.StartAsync(user, Device(req.DeviceSlot, req.DeviceName, req.DeviceId), req.TakeOver == true, ct);
        return Ok(await tokens.IssueAsync(user, session, ct));
    }

    [HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp(OtpRequest req, CancellationToken ct)
    {
        var phone = NormalizePhone(req.Phone);
        var now = clock.GetUtcNow();
        var recent = await db.OtpCodes.Where(o => o.Phone == phone && o.CreatedAt > now.AddMinutes(-15)).ToListAsync(ct);
        if (recent.Any(o => o.CreatedAt > now.AddSeconds(-30)))
            throw new AppException(429, "otp_too_soon", "Please wait 30 seconds before requesting another OTP.");
        if (recent.Count >= 5)
            throw new AppException(429, "otp_limit", "Too many OTP requests. Try again in 15 minutes.");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otp = new OtpCode { Phone = phone, CodeHash = Hashing.Sha256(phone + ":" + code), CreatedAt = now, ExpiresAt = now + OtpPolicy.Lifetime };
        db.OtpCodes.Add(otp);
        await db.SaveChangesAsync(ct);
        try
        {
            await otpSender.SendAsync(phone, code, ct);
        }
        catch
        {
            // Not delivered: forget the code so the student can ask again straight away (not "wait 30 seconds").
            db.OtpCodes.Remove(otp);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        return Accepted(new { expiresInSeconds = (int)OtpPolicy.Lifetime.TotalSeconds });
    }

    [HttpPost("otp/verify")]
    public async Task<ActionResult<AuthTokens>> VerifyOtp(OtpVerifyRequest req, CancellationToken ct)
    {
        var phone = NormalizePhone(req.Phone);
        var now = clock.GetUtcNow();
        var otp = await db.OtpCodes.Where(o => o.Phone == phone && o.ConsumedAt == null && o.ExpiresAt > now)
            .OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(ct);
        if (otp is null || otp.FailedAttempts >= 5)
            throw AppException.BadRequest("otp_invalid", "OTP expired or invalid. Request a new one.");
        if (!Hashing.FixedTimeEqualsHex(otp.CodeHash, Hashing.Sha256(phone + ":" + req.Code)))
        {
            otp.FailedAttempts++;
            await db.SaveChangesAsync(ct);
            throw AppException.BadRequest("otp_invalid", "Incorrect OTP.");
        }

        var device = Device(req.DeviceSlot, req.DeviceName, req.DeviceId);
        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Phone == phone, ct);
        UserSession session;
        if (user is null)
        {
            if (!req.AcceptTerms) throw AppException.BadRequest("terms_required", "You must accept the terms and privacy policy.");
            user = NewStudent(string.IsNullOrWhiteSpace(req.FullName) ? "Student" : req.FullName.Trim());
            user.Phone = phone;
            db.Users.Add(user);
            AddTermsConsent(user.Id);
            session = await sessions.StartAsync(user, device, takeOver: false, ct);
        }
        else
        {
            if (user.Status != UserStatus.Active) throw new AppException(403, "account_inactive", "This account is not active. Contact support.");
            // May ask to confirm taking over another device; the code stays valid for that retry.
            session = await sessions.StartAsync(user, device, req.TakeOver == true, ct);
        }
        otp.ConsumedAt = now;
        await db.SaveChangesAsync(ct);
        return Ok(await tokens.IssueAsync(user, session, ct));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthTokens>> Refresh(RefreshRequest req, CancellationToken ct) => Ok(await tokens.RefreshAsync(req.RefreshToken, Device(null, null, null), ct));

    /// <summary>How long a website sign-in code from the app stays valid.</summary>
    public static readonly TimeSpan HandoffLifetime = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The app asks for a one-time code, then opens the website with it (e.g. to buy a course), so the student arrives
    /// signed in. The website exchanges it with <see cref="RedeemHandoff"/> as a normal browser sign-in.
    /// </summary>
    [Authorize]
    [HttpPost("handoff")]
    public async Task<HandoffDto> CreateHandoff(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var handoff = new SignInHandoff { UserId = User.UserId(), CodeHash = Hashing.Sha256(code), CreatedAt = now, ExpiresAt = now + HandoffLifetime };
        db.SignInHandoffs.Add(handoff);
        await db.SaveChangesAsync(ct);
        return new HandoffDto(code, handoff.ExpiresAt);
    }

    /// <summary>Signs in with a code from <see cref="CreateHandoff"/>. Single use; the one-person-per-account rules apply.</summary>
    [HttpPost("handoff/redeem")]
    public async Task<ActionResult<AuthTokens>> RedeemHandoff(HandoffRedeemRequest req, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var handoff = await db.SignInHandoffs.FirstOrDefaultAsync(h => h.CodeHash == Hashing.Sha256(req.Code), ct);
        if (handoff is null || handoff.UsedAt != null || handoff.ExpiresAt <= now)
            throw AppException.BadRequest("handoff_invalid", "This sign-in link has expired. Please sign in.");
        var user = await db.Users.Include(u => u.Roles).FirstAsync(u => u.Id == handoff.UserId, ct);
        if (user.Status != UserStatus.Active) throw new AppException(403, "account_inactive", "This account is not active. Contact support.");
        // May ask to confirm taking over another browser; the code stays valid for that retry.
        var session = await sessions.StartAsync(user, Device(req.DeviceSlot, req.DeviceName, req.DeviceId), req.TakeOver == true, ct);
        handoff.UsedAt = now;
        await db.SaveChangesAsync(ct);
        return Ok(await tokens.IssueAsync(user, session, ct));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req, CancellationToken ct)
    {
        await tokens.RevokeAsync(req.RefreshToken, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == User.UserId(), ct);
        if (user.PasswordHash is null || !BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            throw new AppException(400, "invalid_credentials", "Current password is incorrect.");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 11);
        await db.SaveChangesAsync(ct);
        await sessions.EndAllAsync(user.Id, SessionEndReasons.PasswordChanged, ct);
        return NoContent();
    }

    private User NewStudent(string name)
    {
        var user = new User { FullName = name, CreatedAt = clock.GetUtcNow() };
        user.Roles.Add(new UserRole { UserId = user.Id, Role = Roles.Student });
        user.Profile = new StudentProfile { UserId = user.Id };
        return user;
    }

    private void AddTermsConsent(Guid userId)
    {
        foreach (var type in new[] { "terms", "privacy" })
            db.ConsentRecords.Add(new ConsentRecord { UserId = userId, ConsentType = type, Version = config["Legal:TermsVersion"] ?? TermsVersion, Granted = true, RecordedAt = clock.GetUtcNow() });
    }

    /// <summary>Accepts 10-digit Indian mobiles with optional +91/91/0 prefix; stores E.164.</summary>
    public static string NormalizePhone(string input)
    {
        var digits = NonDigits().Replace(input, "");
        if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
        else if (digits.Length == 11 && digits.StartsWith('0')) digits = digits[1..];
        if (digits.Length != 10 || digits[0] < '6')
            throw AppException.BadRequest("invalid_phone", "Enter a valid 10-digit Indian mobile number.");
        return "+91" + digits;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();
}

public record ChangePasswordRequest([Required] string CurrentPassword, [Required, MinLength(8), MaxLength(128)] string NewPassword);

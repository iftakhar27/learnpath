using System.ComponentModel.DataAnnotations;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Identity;

public record ProfileDto(
    Guid UserId, string FullName, string? Email, string? Phone, string? ClassLevel, string? Board,
    Guid? TargetExamId, int? TargetYear, string Language, int DailyStudyMinutes, int? TargetScore,
    bool IsMinor, string? GuardianName, bool OnboardingComplete, string[] Roles);

public record UpdateProfileRequest(
    [MaxLength(200)] string? FullName,
    [MaxLength(16)] string? ClassLevel,
    [MaxLength(32)] string? Board,
    Guid? TargetExamId,
    [Range(2024, 2040)] int? TargetYear,
    [RegularExpression("^(en|hi)$")] string? Language,
    [Range(15, 960)] int? DailyStudyMinutes,
    [Range(0, 1000)] int? TargetScore,
    bool? IsMinor,
    [MaxLength(200)] string? GuardianName,
    [MaxLength(20)] string? GuardianPhone,
    bool? GuardianConsent,
    bool CompleteOnboarding = false);

[ApiController]
[Authorize]
[Route("api/v1/student/profile")]
public class ProfileController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ProfileDto> Get(CancellationToken ct) => await Load(User.UserId(), ct);

    [HttpPut]
    public async Task<ProfileDto> Update(UpdateProfileRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var user = await db.Users.Include(u => u.Profile).FirstAsync(u => u.Id == userId, ct);
        var p = user.Profile ??= new StudentProfile { UserId = userId };

        if (req.FullName is not null) user.FullName = req.FullName.Trim();
        if (req.ClassLevel is not null) p.ClassLevel = req.ClassLevel;
        if (req.Board is not null) p.Board = req.Board;
        if (req.TargetExamId is not null)
        {
            if (!await db.Exams.AnyAsync(e => e.Id == req.TargetExamId && e.IsActive, ct)) throw AppException.NotFound("Exam");
            p.TargetExamId = req.TargetExamId;
        }
        if (req.TargetYear is not null) p.TargetYear = req.TargetYear;
        if (req.Language is not null) p.Language = req.Language;
        if (req.DailyStudyMinutes is not null) p.DailyStudyMinutes = req.DailyStudyMinutes.Value;
        if (req.TargetScore is not null) p.TargetScore = req.TargetScore;
        if (req.IsMinor is not null) p.IsMinor = req.IsMinor.Value;
        if (req.GuardianName is not null) p.GuardianName = req.GuardianName;
        if (req.GuardianPhone is not null) p.GuardianPhone = req.GuardianPhone;
        if (req.GuardianConsent == true)
        {
            if (string.IsNullOrWhiteSpace(p.GuardianName)) throw AppException.BadRequest("guardian_required", "Guardian name is required for consent.");
            db.ConsentRecords.Add(new ConsentRecord { UserId = userId, ConsentType = "guardian", Version = "2026-01", Granted = true, GrantedBy = p.GuardianName, RecordedAt = clock.GetUtcNow() });
        }

        if (req.CompleteOnboarding)
        {
            if (p.TargetExamId is null) throw AppException.BadRequest("exam_required", "Select a target exam to continue.");
            if (p.IsMinor && !await db.ConsentRecords.AnyAsync(c => c.UserId == userId && c.ConsentType == "guardian" && c.Granted, ct) && req.GuardianConsent != true)
                throw AppException.BadRequest("guardian_consent_required", "A parent or guardian must consent for students under 18.");
            p.OnboardedAt ??= clock.GetUtcNow();
        }
        await db.SaveChangesAsync(ct);
        return await Load(userId, ct);
    }

    /// <summary>Records a deletion request; support completes erasure after outstanding orders/refunds are settled.</summary>
    [HttpPost("delete-request")]
    public async Task<IActionResult> RequestDeletion(CancellationToken ct)
    {
        var userId = User.UserId();
        var now = clock.GetUtcNow();
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        user.Status = UserStatus.DeletionRequested;
        user.DeletionRequestedAt = now;
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        await db.SaveChangesAsync(ct);
        return Accepted(new { message = "Your deletion request has been recorded. You will be notified when it is complete." });
    }

    private async Task<ProfileDto> Load(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.Profile).Include(x => x.Roles).FirstAsync(x => x.Id == userId, ct);
        var p = u.Profile ?? new StudentProfile();
        return new ProfileDto(u.Id, u.FullName, u.Email, u.Phone, p.ClassLevel, p.Board, p.TargetExamId, p.TargetYear,
            p.Language, p.DailyStudyMinutes, p.TargetScore, p.IsMinor, p.GuardianName, p.OnboardedAt != null,
            u.Roles.Select(r => r.Role).ToArray());
    }
}

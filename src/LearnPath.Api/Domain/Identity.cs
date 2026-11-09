namespace LearnPath.Api.Domain;

public static class Roles
{
    public const string Student = "Student";
    public const string Teacher = "Teacher";
    public const string ContentAdmin = "ContentAdmin";
    public const string Support = "Support";
    public const string Finance = "Finance";
    public const string Analyst = "Analyst";
    public const string SuperAdmin = "SuperAdmin";

    public static readonly string[] All = [Student, Teacher, ContentAdmin, Support, Finance, Analyst, SuperAdmin];
    public static readonly string[] Staff = [Teacher, ContentAdmin, Support, Finance, Analyst, SuperAdmin];
}

public enum UserStatus { Active, Suspended, DeletionRequested, Deleted }

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = "";
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? DeletionRequestedAt { get; set; }

    public List<UserRole> Roles { get; set; } = [];
    public StudentProfile? Profile { get; set; }
}

public class UserRole
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = "";
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
    /// <summary>The sign-in this token belongs to (null for tokens issued before sessions existed).</summary>
    public Guid? SessionId { get; set; }
}

public static class DeviceSlots
{
    public const string Phone = "phone";
    public const string Browser = "browser";
}

public static class SessionEndReasons
{
    public const string SignedOut = "signed_out";
    /// <summary>The account was signed in on another device of the same kind.</summary>
    public const string Replaced = "replaced";
    public const string PasswordChanged = "password_changed";
    /// <summary>Support set a temporary password.</summary>
    public const string PasswordReset = "password_reset";
    /// <summary>A rotated refresh token was used again (possible theft): every session was ended.</summary>
    public const string Security = "security";
}

/// <summary>
/// One signed-in device. Students may have one active session per device slot (one phone, one browser); signing in on
/// another device of the same kind replaces it after the student confirms. A session is active while it has not
/// ended and still holds an unrevoked, unexpired refresh token.
/// </summary>
public class UserSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    /// <summary><see cref="DeviceSlots"/>.</summary>
    public string DeviceSlot { get; set; } = DeviceSlots.Browser;
    /// <summary>Shown to the student, e.g. "iPhone · Safari" or "Android app".</summary>
    public string DeviceName { get; set; } = "";
    /// <summary>SHA-256 of the device's own random id, so signing in again on the same device needs no prompt.</summary>
    public string? DeviceIdHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? EndReason { get; set; }
}

public class OtpCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Phone { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

/// <summary>
/// A one-time code that signs the app's student in on the website (e.g. "Buy on website"), so they don't have to sign
/// in again in the browser. Short-lived and single-use; only its hash is stored.
/// </summary>
public class SignInHandoff
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

public class StudentProfile
{
    public Guid UserId { get; set; }
    public string? ClassLevel { get; set; }      // "11", "12", "Dropper", "10"
    public string? Board { get; set; }           // "CBSE", "UP", ...
    public Guid? TargetExamId { get; set; }
    public int? TargetYear { get; set; }
    public string Language { get; set; } = "en"; // "en" | "hi"
    public int DailyStudyMinutes { get; set; } = 120;
    public int? TargetScore { get; set; }
    public bool IsMinor { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public DateTimeOffset? OnboardedAt { get; set; }
}

public class ConsentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string ConsentType { get; set; } = "";  // "terms", "privacy", "guardian", "marketing"
    public string Version { get; set; } = "";
    public bool Granted { get; set; }
    public string? GrantedBy { get; set; }          // guardian name when ConsentType == "guardian"
    public DateTimeOffset RecordedAt { get; set; }
}

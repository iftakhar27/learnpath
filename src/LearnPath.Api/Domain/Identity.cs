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

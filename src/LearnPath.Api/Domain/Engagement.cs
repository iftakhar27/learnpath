namespace LearnPath.Api.Domain;

public class ResourceProgress
{
    public Guid UserId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid TopicId { get; set; }
    public int PositionSeconds { get; set; }
    public int MaxPositionSeconds { get; set; }
    public bool Completed { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Append-only learning activity log; drives streaks, history and analytics.</summary>
public class LearningEvent
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public string EventType { get; set; } = "";   // see LearningEventTypes
    public Guid? EntityId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public static class LearningEventTypes
{
    public const string ResourceStarted = "resource_started";
    public const string ResourceCompleted = "resource_completed";
    public const string QuestionAnswered = "question_answered";
    public const string TestStarted = "test_started";
    public const string TestSubmitted = "test_submitted";
    public const string PlanTaskCompleted = "plan_task_completed";
    public const string CoursePurchased = "course_purchased";
}

public enum BookmarkType { Question, Resource, Topic }

public class Bookmark
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public BookmarkType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum PlanTaskType { Learn, Practice, Revise, Test }
public enum PlanTaskStatus { Pending, Completed, Skipped }

public class StudyPlanTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateOnly PlanDate { get; set; }
    public PlanTaskType Type { get; set; }
    public Guid EntityId { get; set; }            // resource, topic or test id
    public string Title { get; set; } = "";
    public string Reason { get; set; } = "";      // human-readable rule that produced the task
    public int EstimatedMinutes { get; set; }
    public int SortOrder { get; set; }
    public PlanTaskStatus Status { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Type { get; set; } = "";        // payment, announcement, reminder, support
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? Link { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public class SupportTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Category { get; set; } = "";    // payment, content, technical, account
    public string Priority { get; set; } = "Normal";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public string Status { get; set; } = "Open";  // Open, InProgress, Resolved, Closed
    public string? StaffResponse { get; set; }
    public Guid? RespondedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? DataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class ContentImportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UploadedBy { get; set; }
    public string Kind { get; set; } = "questions";
    public string FileName { get; set; } = "";
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int FailedRows { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<ContentImportError> Errors { get; set; } = [];
}

public class ContentImportError
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public int RowNumber { get; set; }
    public string Message { get; set; } = "";
}

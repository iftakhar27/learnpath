namespace LearnPath.Api.Domain;

public enum PublishStatus { Draft, Published, Archived }

public class Exam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";       // "NEET", "JEE_MAIN"
    public string NameEn { get; set; } = "";
    public string? NameHi { get; set; }
    public DateOnly? NextExamDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }
    public string Slug { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string? TitleHi { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionHi { get; set; }
    public string? ClassLevel { get; set; }
    public string? Board { get; set; }
    public bool IsFree { get; set; }
    public long PriceInPaise { get; set; }      // GST-inclusive
    public int? AccessDays { get; set; }        // null = lifetime
    public PublishStatus Status { get; set; } = PublishStatus.Draft;
    public DateTimeOffset? PublishAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<Subject> Subjects { get; set; } = [];
}

public class Subject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public string TitleEn { get; set; } = "";
    public string? TitleHi { get; set; }
    public int SortOrder { get; set; }
    public List<Chapter> Chapters { get; set; } = [];
}

public class Chapter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public string? UnitTitleEn { get; set; }    // optional grouping, e.g. "Mechanics"
    public string TitleEn { get; set; } = "";
    public string? TitleHi { get; set; }
    public int SortOrder { get; set; }
    public List<Topic> Topics { get; set; } = [];
}

public class Topic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
    public string TitleEn { get; set; } = "";
    public string? TitleHi { get; set; }
    public string? ObjectivesEn { get; set; }
    public string? ObjectivesHi { get; set; }
    public int EstimatedMinutes { get; set; }
    public int SortOrder { get; set; }
    public List<LearningResource> Resources { get; set; } = [];
}

public class TopicPrerequisite
{
    public Guid TopicId { get; set; }
    public Guid PrerequisiteTopicId { get; set; }
}

public enum ResourceType { Video, Document }

public class LearningResource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TopicId { get; set; }
    public Topic? Topic { get; set; }
    public ResourceType Type { get; set; }
    public string TitleEn { get; set; } = "";
    public string? TitleHi { get; set; }
    public string Language { get; set; } = "en";
    public int SortOrder { get; set; }
    public bool IsFreePreview { get; set; }
    public PublishStatus Status { get; set; } = PublishStatus.Draft;
    public string? VideoProviderId { get; set; }   // ID at the streaming provider; never a file in the DB
    public int? DurationSeconds { get; set; }
    public string? DocumentStorageKey { get; set; } // object-storage key for PDFs
    public bool AllowDownload { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum EnrollmentSource { Free, Purchase, Admin }

public class Enrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public Course? Course { get; set; }
    public EnrollmentSource Source { get; set; }
    public Guid? OrderId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

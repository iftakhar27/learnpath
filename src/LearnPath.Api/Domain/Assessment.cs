namespace LearnPath.Api.Domain;

public enum QuestionType { SingleCorrect, MultipleCorrect, Numerical, Integer, TrueFalse }
public enum Difficulty { Easy, Medium, Hard }

public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? ExternalRef { get; set; }     // unique import identifier
    public Guid TopicId { get; set; }
    public Topic? Topic { get; set; }
    public QuestionType Type { get; set; }
    public Difficulty Difficulty { get; set; } = Difficulty.Medium;
    public string StemEn { get; set; } = "";
    public string? StemHi { get; set; }
    public string? ImageUrl { get; set; }
    public string? ExplanationEn { get; set; }
    public string? ExplanationHi { get; set; }
    public string? HintEn { get; set; }
    public string? VideoSolutionId { get; set; }
    public decimal? NumericAnswer { get; set; }
    public decimal NumericTolerance { get; set; }
    public decimal DefaultMarks { get; set; } = 4;
    public decimal DefaultNegativeMarks { get; set; } = 1;
    public bool AllowPartialMarking { get; set; }  // multi-correct: +1 per correct option when no wrong option chosen
    public int? PreviousYear { get; set; }
    public PublishStatus Status { get; set; } = PublishStatus.Draft;
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<QuestionOption> Options { get; set; } = [];
}

public class QuestionOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public string TextEn { get; set; } = "";
    public string? TextHi { get; set; }
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
}

public enum TestType { Topic, Chapter, Subject, FullSyllabus, Mock, PreviousYear, Diagnostic, Custom }

public class Test
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CourseId { get; set; }
    public Guid? ExamId { get; set; }
    public TestType Type { get; set; }
    public string TitleEn { get; set; } = "";
    public string? TitleHi { get; set; }
    public string? InstructionsEn { get; set; }
    public string? InstructionsHi { get; set; }
    public int DurationMinutes { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShuffleOptions { get; set; }
    public bool IsFree { get; set; }
    public int? MaxAttempts { get; set; }
    public DateTimeOffset? AvailableFrom { get; set; }
    public DateTimeOffset? AvailableUntil { get; set; }
    public PublishStatus Status { get; set; } = PublishStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public List<TestSection> Sections { get; set; } = [];
    public List<TestQuestion> Questions { get; set; } = [];
}

public class TestSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TestId { get; set; }
    public string TitleEn { get; set; } = "";
    public string? TitleHi { get; set; }
    public int SortOrder { get; set; }
}

public class TestQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TestId { get; set; }
    public Guid? SectionId { get; set; }
    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }
    public int SortOrder { get; set; }
    public decimal Marks { get; set; }
    public decimal NegativeMarks { get; set; }
}

public enum AttemptStatus { InProgress, Submitted, AutoSubmitted }

public class TestAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TestId { get; set; }
    public Test? Test { get; set; }
    public Guid UserId { get; set; }
    public int AttemptNumber { get; set; }
    public AttemptStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public int Seed { get; set; }
    /// <summary>JSON array of <see cref="Modules.Exams.QuestionSnapshot"/> in presentation order. Immutable once created.</summary>
    public string SnapshotJson { get; set; } = "[]";
    public decimal? Score { get; set; }
    public decimal MaxScore { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public int PartialCount { get; set; }
    public int SkippedCount { get; set; }
    public uint RowVersion { get; set; }
    public List<AttemptAnswer> Answers { get; set; } = [];
}

public class AttemptAnswer
{
    public Guid AttemptId { get; set; }
    public Guid TestQuestionId { get; set; }
    public string? ResponseJson { get; set; }
    public bool MarkedForReview { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum AnswerOutcome { Correct, Incorrect, PartiallyCorrect, Skipped }

public class AttemptQuestionResult
{
    public Guid AttemptId { get; set; }
    public Guid TestQuestionId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid ChapterId { get; set; }
    public Guid TopicId { get; set; }
    public Difficulty Difficulty { get; set; }
    public AnswerOutcome Outcome { get; set; }
    public decimal MarksAwarded { get; set; }
    public int TimeSpentSeconds { get; set; }
}

public class AttemptEvent
{
    public long Id { get; set; }
    public Guid AttemptId { get; set; }
    public string EventType { get; set; } = "";   // started, resumed, answer_saved, submitted, auto_submitted
    public DateTimeOffset OccurredAt { get; set; }
}

public class PracticeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Mode { get; set; } = "topic";  // topic, chapter, mixed, incorrect, bookmarked
    public Guid? TopicId { get; set; }
    public Guid? ChapterId { get; set; }
    public string QuestionIdsJson { get; set; } = "[]";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<PracticeAnswer> Answers { get; set; } = [];
}

public class PracticeAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid TopicId { get; set; }
    public string ResponseJson { get; set; } = "{}";
    public AnswerOutcome Outcome { get; set; }
    public int TimeSpentSeconds { get; set; }
    public DateTimeOffset AnsweredAt { get; set; }
}

public class QuestionReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid QuestionId { get; set; }
    public string Reason { get; set; } = "";       // wrong_answer, typo, unclear, other
    public string? Details { get; set; }
    public string Status { get; set; } = "Open";   // Open, Fixed, Rejected
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

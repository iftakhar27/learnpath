using LearnPath.Api.Domain;

namespace LearnPath.Api.Modules.Admin;

// Read models for the admin portal. Kept separate from entities so the OpenAPI contract is explicit and stable.

public record AdminExamDto(Guid Id, string Code, string NameEn, string? NameHi, DateOnly? NextExamDate, bool IsActive, int SortOrder);

public record AdminCourseDto(Guid Id, Guid ExamId, string Slug, string TitleEn, string? TitleHi, string? DescriptionEn, string? DescriptionHi,
    string? ClassLevel, string? Board, bool IsFree, long PriceInPaise, int? AccessDays, PublishStatus Status, DateTimeOffset? PublishAt,
    DateTimeOffset UpdatedAt, int Enrollments);

public record AdminResourceDto(Guid Id, Guid TopicId, ResourceType Type, string TitleEn, string? TitleHi, string Language, int SortOrder,
    bool IsFreePreview, PublishStatus Status, string? VideoProviderId, int? DurationSeconds, string? DocumentStorageKey, bool AllowDownload, int Version);

public record AdminTopicDto(Guid Id, Guid ChapterId, string TitleEn, string? TitleHi, string? ObjectivesEn, string? ObjectivesHi,
    int EstimatedMinutes, int SortOrder, int QuestionCount, List<AdminResourceDto> Resources);

public record AdminChapterDto(Guid Id, Guid SubjectId, string? UnitTitleEn, string TitleEn, string? TitleHi, int SortOrder, List<AdminTopicDto> Topics);

public record AdminSubjectDto(Guid Id, Guid CourseId, string TitleEn, string? TitleHi, int SortOrder, List<AdminChapterDto> Chapters);

public record AdminCourseTreeDto(AdminCourseDto Course, List<AdminSubjectDto> Subjects);

public record AdminQuestionListItem(Guid Id, string? ExternalRef, Guid TopicId, string Topic, string Chapter, QuestionType Type, Difficulty Difficulty,
    string StemEn, PublishStatus Status, int Version, DateTimeOffset UpdatedAt, int OpenReports);

public record AdminOptionDto(Guid Id, string TextEn, string? TextHi, bool IsCorrect, int SortOrder);

public record AdminQuestionDto(Guid Id, string? ExternalRef, Guid TopicId, Guid ChapterId, Guid SubjectId, Guid CourseId, QuestionType Type, Difficulty Difficulty,
    string StemEn, string? StemHi, string? ImageUrl, string? ExplanationEn, string? ExplanationHi, string? HintEn, string? VideoSolutionId,
    decimal? NumericAnswer, decimal NumericTolerance, decimal DefaultMarks, decimal DefaultNegativeMarks, bool AllowPartialMarking, int? PreviousYear,
    PublishStatus Status, int Version, int UsedInTests, List<AdminOptionDto> Options);

public record AdminTestListItem(Guid Id, string TitleEn, TestType Type, Guid? CourseId, string? CourseTitle, int DurationMinutes, bool IsFree,
    PublishStatus Status, int QuestionCount, decimal MaxMarks, int Attempts, DateTimeOffset CreatedAt);

public record AdminTestQuestionDto(Guid QuestionId, int? SectionIndex, decimal Marks, decimal NegativeMarks, string StemEn, string? ExternalRef,
    QuestionType Type, Difficulty Difficulty, PublishStatus QuestionStatus, string Topic);

public record AdminTestDto(Guid Id, Guid? CourseId, Guid? ExamId, TestType Type, string TitleEn, string? TitleHi, string? InstructionsEn, string? InstructionsHi,
    int DurationMinutes, bool ShuffleQuestions, bool ShuffleOptions, bool IsFree, int? MaxAttempts, DateTimeOffset? AvailableFrom, DateTimeOffset? AvailableUntil,
    PublishStatus Status, int Attempts, List<SectionUpsert> Sections, List<AdminTestQuestionDto> Questions);

public record AdminUserListItem(Guid Id, string FullName, string? Email, string? Phone, UserStatus Status, DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt, List<string> Roles);

public record AdminEnrollmentDto(Guid CourseId, string CourseTitle, EnrollmentSource Source, DateTimeOffset StartsAt, DateTimeOffset? ExpiresAt);

public record AdminOrderDto(Guid Id, Guid UserId, string UserName, Guid CourseId, string CourseTitle, long AmountInPaise, OrderStatus Status,
    string GatewayOrderId, string Receipt, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt);

public record AdminTicketSummaryDto(Guid Id, string Subject, string Status, DateTimeOffset CreatedAt);

public record AdminUserDetailDto(AdminUserListItem User, List<AdminEnrollmentDto> Enrollments, List<AdminOrderDto> Orders, List<AdminTicketSummaryDto> Tickets);

public record AdminTicketDto(Guid Id, Guid UserId, string UserName, string? UserEmail, string? UserPhone, string Category, string Priority,
    string Subject, string Message, string Status, string? StaffResponse, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

public record AdminQuestionReportDto(Guid Id, Guid QuestionId, string QuestionStem, string? ExternalRef, string Reason, string? Details,
    string Status, DateTimeOffset CreatedAt);

public record AdminImportJobDto(Guid Id, string FileName, int TotalRows, int ImportedRows, int FailedRows, DateTimeOffset CreatedAt, List<ImportErrorDto> Errors);

public record AnnouncementResultDto(int Recipients);

public record AdminAuditLogDto(long Id, Guid? ActorUserId, string? ActorName, string Action, string EntityType, string? EntityId, string? DataJson, DateTimeOffset CreatedAt);

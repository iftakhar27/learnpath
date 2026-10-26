using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();

    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Chapter> Chapters => Set<Chapter>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<TopicPrerequisite> TopicPrerequisites => Set<TopicPrerequisite>();
    public DbSet<LearningResource> LearningResources => Set<LearningResource>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<TestSection> TestSections => Set<TestSection>();
    public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();
    public DbSet<TestAttempt> TestAttempts => Set<TestAttempt>();
    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();
    public DbSet<AttemptQuestionResult> AttemptQuestionResults => Set<AttemptQuestionResult>();
    public DbSet<AttemptEvent> AttemptEvents => Set<AttemptEvent>();
    public DbSet<PracticeSession> PracticeSessions => Set<PracticeSession>();
    public DbSet<PracticeAnswer> PracticeAnswers => Set<PracticeAnswer>();
    public DbSet<QuestionReport> QuestionReports => Set<QuestionReport>();

    public DbSet<ResourceProgress> ResourceProgress => Set<ResourceProgress>();
    public DbSet<LearningEvent> LearningEvents => Set<LearningEvent>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<StudyPlanTask> StudyPlanTasks => Set<StudyPlanTask>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ContentImportJob> ContentImportJobs => Set<ContentImportJob>();
    public DbSet<ContentImportError> ContentImportErrors => Set<ContentImportError>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Identity
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
            e.HasIndex(x => x.Phone).IsUnique().HasFilter("\"Phone\" IS NOT NULL");
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasMany(x => x.Roles).WithOne().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Profile).WithOne().HasForeignKey<StudentProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<UserRole>(e => { e.HasKey(x => new { x.UserId, x.Role }); e.Property(x => x.Role).HasMaxLength(32); });
        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<OtpCode>(e => e.HasIndex(x => new { x.Phone, x.CreatedAt }));
        b.Entity<StudentProfile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.Language).HasMaxLength(8);
            e.HasOne<Exam>().WithMany().HasForeignKey(x => x.TargetExamId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<ConsentRecord>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Catalog
        b.Entity<Exam>(e => e.HasIndex(x => x.Code).IsUnique());
        b.Entity<Course>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.ExamId, x.Status });
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Exam).WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Subjects).WithOne().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Subject>(e => e.HasMany(x => x.Chapters).WithOne(x => x.Subject).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Cascade));
        b.Entity<Chapter>(e => e.HasMany(x => x.Topics).WithOne(x => x.Chapter).HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.Cascade));
        b.Entity<Topic>(e => e.HasMany(x => x.Resources).WithOne(x => x.Topic).HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Cascade));
        b.Entity<TopicPrerequisite>(e =>
        {
            e.HasKey(x => new { x.TopicId, x.PrerequisiteTopicId });
            e.HasOne<Topic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Topic>().WithMany().HasForeignKey(x => x.PrerequisiteTopicId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LearningResource>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        });
        b.Entity<Enrollment>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique();
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Assessment
        b.Entity<Question>(e =>
        {
            e.HasIndex(x => x.ExternalRef).IsUnique().HasFilter("\"ExternalRef\" IS NOT NULL");
            e.HasIndex(x => new { x.TopicId, x.Status });
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.NumericAnswer).HasPrecision(18, 6);
            e.Property(x => x.NumericTolerance).HasPrecision(18, 6);
            e.Property(x => x.DefaultMarks).HasPrecision(8, 2);
            e.Property(x => x.DefaultNegativeMarks).HasPrecision(8, 2);
            e.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Test>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Sections).WithOne().HasForeignKey(x => x.TestId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Questions).WithOne().HasForeignKey(x => x.TestId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TestQuestion>(e =>
        {
            e.HasIndex(x => new { x.TestId, x.QuestionId }).IsUnique();
            e.Property(x => x.Marks).HasPrecision(8, 2);
            e.Property(x => x.NegativeMarks).HasPrecision(8, 2);
            e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TestSection>().WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<TestAttempt>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.TestId });
            e.HasIndex(x => new { x.TestId, x.UserId, x.AttemptNumber }).IsUnique();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.SnapshotJson).HasColumnType("jsonb");
            e.Property(x => x.Score).HasPrecision(10, 2);
            e.Property(x => x.MaxScore).HasPrecision(10, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Test).WithMany().HasForeignKey(x => x.TestId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Answers).WithOne().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AttemptAnswer>(e =>
        {
            e.HasKey(x => new { x.AttemptId, x.TestQuestionId });
            e.Property(x => x.ResponseJson).HasColumnType("jsonb");
        });
        b.Entity<AttemptQuestionResult>(e =>
        {
            e.HasKey(x => new { x.AttemptId, x.TestQuestionId });
            e.HasIndex(x => x.TopicId);
            e.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.MarksAwarded).HasPrecision(8, 2);
            e.HasOne<TestAttempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AttemptEvent>(e =>
        {
            e.HasIndex(x => x.AttemptId);
            e.HasOne<TestAttempt>().WithMany().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PracticeSession>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.Property(x => x.QuestionIdsJson).HasColumnType("jsonb");
            e.HasMany(x => x.Answers).WithOne().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PracticeAnswer>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.TopicId });
            e.HasIndex(x => new { x.UserId, x.QuestionId });
            e.Property(x => x.ResponseJson).HasColumnType("jsonb");
            e.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(20);
        });
        b.Entity<QuestionReport>(e =>
        {
            e.HasIndex(x => x.Status);
            e.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        // Engagement
        b.Entity<ResourceProgress>(e =>
        {
            e.HasKey(x => new { x.UserId, x.ResourceId });
            e.HasIndex(x => new { x.UserId, x.UpdatedAt });
            e.HasOne<LearningResource>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LearningEvent>(e => e.HasIndex(x => new { x.UserId, x.OccurredAt }));
        b.Entity<Bookmark>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.EntityType, x.EntityId }).IsUnique();
            e.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(16);
        });
        b.Entity<StudyPlanTask>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.PlanDate });
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        });
        b.Entity<Notification>(e => e.HasIndex(x => new { x.UserId, x.CreatedAt }));
        b.Entity<SupportTicket>(e => { e.HasIndex(x => x.UserId); e.HasIndex(x => x.Status); });
        b.Entity<AuditLog>(e => { e.HasIndex(x => x.CreatedAt); e.Property(x => x.DataJson).HasColumnType("jsonb"); });
        b.Entity<ContentImportJob>(e => e.HasMany(x => x.Errors).WithOne().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade));

        // Commerce
        b.Entity<Order>(e =>
        {
            e.HasIndex(x => x.GatewayOrderId).IsUnique();
            e.HasIndex(x => x.UserId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.GatewayPaymentId).IsUnique();
            e.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PaymentWebhookEvent>(e => { e.HasIndex(x => x.EventId).IsUnique(); e.Property(x => x.Payload).HasColumnType("jsonb"); });
    }
}

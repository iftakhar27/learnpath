using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Data;

/// <summary>
/// Seeds exams, an initial super-admin (from configuration) and, optionally, a small set of ORIGINAL sample content.
/// Sample content is clearly labelled and is meant for demos and QA, not for students.
/// </summary>
public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config, TimeProvider clock, ILogger logger)
    {
        var now = clock.GetUtcNow();
        if (!await db.Exams.AnyAsync())
        {
            db.Exams.AddRange(
                new Exam { Code = "NEET", NameEn = "NEET (UG)", NameHi = "नीट (यूजी)", SortOrder = 1 },
                new Exam { Code = "JEE_MAIN", NameEn = "JEE Main", NameHi = "जेईई मेन", SortOrder = 2 },
                new Exam { Code = "JEE_ADV", NameEn = "JEE Advanced", NameHi = "जेईई एडवांस्ड", SortOrder = 3 },
                new Exam { Code = "CBSE_12", NameEn = "CBSE Class 12", NameHi = "सीबीएसई कक्षा 12", SortOrder = 4 },
                new Exam { Code = "CBSE_10", NameEn = "CBSE Class 10", NameHi = "सीबीएसई कक्षा 10", SortOrder = 5 });
            await db.SaveChangesAsync();
        }

        var adminEmail = config["Seed:AdminEmail"]?.Trim().ToLowerInvariant();
        var adminPassword = config["Seed:AdminPassword"];
        if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword) && !await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var admin = new User { Email = adminEmail, FullName = "Super Admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword, 11), CreatedAt = now };
            admin.Roles.Add(new UserRole { UserId = admin.Id, Role = Roles.SuperAdmin });
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded super admin {Email}", adminEmail);
        }

        if (config.GetValue<bool>("Seed:SampleContent") && !await db.Courses.AnyAsync())
        {
            await SeedSampleContentAsync(db, now);
            logger.LogInformation("Seeded sample content");
        }
    }

    private static async Task SeedSampleContentAsync(AppDbContext db, DateTimeOffset now)
    {
        var neet = await db.Exams.FirstAsync(e => e.Code == "NEET");

        // ---------- Free course ----------
        var free = new Course
        {
            ExamId = neet.Id, Slug = "neet-physics-starter-sample", TitleEn = "NEET Physics Starter (Sample, Free)", TitleHi = "नीट भौतिकी स्टार्टर (नमूना, निःशुल्क)",
            DescriptionEn = "Sample free course for demos. Units, dimensions and measurement basics.", ClassLevel = "11", IsFree = true,
            Status = PublishStatus.Published, CreatedAt = now, UpdatedAt = now,
        };
        var fPhy = new Subject { CourseId = free.Id, TitleEn = "Physics", TitleHi = "भौतिकी" };
        var fCh = new Chapter { SubjectId = fPhy.Id, UnitTitleEn = "Physical World and Measurement", TitleEn = "Units and Measurements", TitleHi = "मात्रक और मापन" };
        var dimTopic = new Topic { ChapterId = fCh.Id, TitleEn = "Dimensional Analysis", TitleHi = "विमीय विश्लेषण", EstimatedMinutes = 40,
            ObjectivesEn = "Write dimensional formulae and check the consistency of equations." };
        fCh.Topics.Add(dimTopic);
        fPhy.Chapters.Add(fCh);
        free.Subjects.Add(fPhy);
        dimTopic.Resources.Add(new LearningResource { TopicId = dimTopic.Id, Type = ResourceType.Video, TitleEn = "Dimensions explained", TitleHi = "विमाएँ समझें", VideoProviderId = "sample-dimensions-01", DurationSeconds = 540, Status = PublishStatus.Published, UpdatedAt = now });
        dimTopic.Resources.Add(new LearningResource { TopicId = dimTopic.Id, Type = ResourceType.Document, TitleEn = "Dimensional formulae — summary notes", DocumentStorageKey = "samples/dimensions-notes.pdf", SortOrder = 1, Status = PublishStatus.Published, UpdatedAt = now });

        // ---------- Paid course ----------
        var paid = new Course
        {
            ExamId = neet.Id, Slug = "neet-physics-mechanics-sample", TitleEn = "NEET Physics: Mechanics (Sample)", TitleHi = "नीट भौतिकी: यांत्रिकी (नमूना)",
            DescriptionEn = "Sample paid course for demos and payment testing.", ClassLevel = "11", IsFree = false, PriceInPaise = 1999_00, AccessDays = 365,
            Status = PublishStatus.Published, CreatedAt = now, UpdatedAt = now,
        };
        var pPhy = new Subject { CourseId = paid.Id, TitleEn = "Physics", TitleHi = "भौतिकी" };
        var lom = new Chapter { SubjectId = pPhy.Id, UnitTitleEn = "Mechanics", TitleEn = "Laws of Motion", TitleHi = "गति के नियम" };
        var newton1 = new Topic { ChapterId = lom.Id, TitleEn = "Newton's First Law and Inertia", TitleHi = "न्यूटन का प्रथम नियम और जड़त्व", EstimatedMinutes = 45, SortOrder = 0 };
        var friction = new Topic { ChapterId = lom.Id, TitleEn = "Friction", TitleHi = "घर्षण", EstimatedMinutes = 60, SortOrder = 1,
            ObjectivesEn = "Distinguish static and kinetic friction; solve block-on-surface problems." };
        lom.Topics.AddRange([newton1, friction]);
        pPhy.Chapters.Add(lom);
        paid.Subjects.Add(pPhy);
        newton1.Resources.Add(new LearningResource { TopicId = newton1.Id, Type = ResourceType.Video, TitleEn = "Inertia and the first law", VideoProviderId = "sample-newton1-01", DurationSeconds = 720, IsFreePreview = true, Status = PublishStatus.Published, UpdatedAt = now });
        friction.Resources.Add(new LearningResource { TopicId = friction.Id, Type = ResourceType.Video, TitleEn = "Static vs kinetic friction", VideoProviderId = "sample-friction-01", DurationSeconds = 900, Status = PublishStatus.Published, UpdatedAt = now });
        friction.Resources.Add(new LearningResource { TopicId = friction.Id, Type = ResourceType.Document, TitleEn = "Friction — formula sheet", DocumentStorageKey = "samples/friction-formulas.pdf", SortOrder = 1, Status = PublishStatus.Published, UpdatedAt = now });

        db.Courses.AddRange(free, paid);

        // ---------- Original sample questions ----------
        Question Mcq(string refId, Topic t, Difficulty d, string stem, string? stemHi, string[] options, int correct, string explanation) =>
            new()
            {
                ExternalRef = refId, TopicId = t.Id, Type = QuestionType.SingleCorrect, Difficulty = d, StemEn = stem, StemHi = stemHi, ExplanationEn = explanation,
                Status = PublishStatus.Published, CreatedAt = now, UpdatedAt = now,
                Options = options.Select((o, i) => new QuestionOption { TextEn = o, IsCorrect = i == correct, SortOrder = i }).ToList(),
            };

        var questions = new List<Question>
        {
            Mcq("SAMPLE-DIM-1", dimTopic, Difficulty.Easy, "What is the dimensional formula of force?", "बल का विमीय सूत्र क्या है?",
                ["[M L T^-2]", "[M L^2 T^-2]", "[M L T^-1]", "[M^0 L T^-2]"], 0, "F = ma, so [M][L T^-2] = [M L T^-2]."),
            Mcq("SAMPLE-DIM-2", dimTopic, Difficulty.Easy, "Which pair of quantities has the same dimensions?", null,
                ["Work and torque", "Force and power", "Momentum and energy", "Pressure and force"], 0, "Both work and torque are force × distance: [M L^2 T^-2]."),
            Mcq("SAMPLE-DIM-3", dimTopic, Difficulty.Medium, "The dimensional formula of pressure is:", null,
                ["[M L^-1 T^-2]", "[M L T^-2]", "[M L^2 T^-2]", "[M L^-2 T^-1]"], 0, "Pressure = force/area = [M L T^-2]/[L^2]."),
            Mcq("SAMPLE-NL1-1", newton1, Difficulty.Easy, "A passenger lurches forward when a moving bus brakes suddenly. This is due to:", "चलती बस के अचानक रुकने पर यात्री आगे की ओर झुकता है। इसका कारण है:",
                ["Inertia of motion", "Inertia of rest", "Friction of the seat", "Air resistance"], 0, "The passenger's body tends to continue moving forward when the bus stops."),
            Mcq("SAMPLE-NL1-2", newton1, Difficulty.Medium, "If the net external force on a body is zero, the body:", null,
                ["Keeps a constant velocity", "Must be at rest", "Accelerates slowly", "Moves in a circle"], 0, "Zero net force means zero acceleration, i.e. constant velocity (which includes rest)."),
            Mcq("SAMPLE-FR-1", friction, Difficulty.Easy, "Kinetic friction compared with limiting static friction for the same surfaces is generally:", null,
                ["Slightly smaller", "Slightly larger", "Exactly equal", "Zero"], 0, "μk is usually a little smaller than μs."),
            new()
            {
                ExternalRef = "SAMPLE-FR-2", TopicId = friction.Id, Type = QuestionType.Numerical, Difficulty = Difficulty.Medium,
                StemEn = "A 5 kg block rests on a horizontal floor with μs = 0.4. Taking g = 10 m/s², what minimum horizontal force (in N) starts it moving?",
                NumericAnswer = 20, NumericTolerance = 0.01m, ExplanationEn = "F = μs·m·g = 0.4 × 5 × 10 = 20 N.", Status = PublishStatus.Published, CreatedAt = now, UpdatedAt = now,
            },
            new()
            {
                ExternalRef = "SAMPLE-FR-3", TopicId = friction.Id, Type = QuestionType.MultipleCorrect, Difficulty = Difficulty.Hard, AllowPartialMarking = true,
                DefaultMarks = 4, DefaultNegativeMarks = 2,
                StemEn = "Which statements about friction are correct? (One or more)",
                ExplanationEn = "Friction opposes relative motion and, to a good approximation, does not depend on apparent contact area. Static friction is self-adjusting up to its limit.",
                Status = PublishStatus.Published, CreatedAt = now, UpdatedAt = now,
                Options =
                [
                    new() { TextEn = "Static friction adjusts itself up to a limiting value", IsCorrect = true, SortOrder = 0 },
                    new() { TextEn = "Friction opposes relative motion between surfaces", IsCorrect = true, SortOrder = 1 },
                    new() { TextEn = "Friction is proportional to the apparent area of contact", IsCorrect = false, SortOrder = 2 },
                    new() { TextEn = "Kinetic friction always exceeds static friction", IsCorrect = false, SortOrder = 3 },
                ],
            },
        };
        db.Questions.AddRange(questions);

        var freeQuiz = new Test
        {
            CourseId = free.Id, ExamId = neet.Id, Type = TestType.Topic, TitleEn = "Dimensional Analysis Quiz (Sample)", TitleHi = "विमीय विश्लेषण प्रश्नोत्तरी (नमूना)",
            InstructionsEn = "3 questions, 10 minutes. +4 for correct, −1 for incorrect, 0 for unanswered.", DurationMinutes = 10, IsFree = true,
            Status = PublishStatus.Published, CreatedAt = now,
        };
        var i = 0;
        foreach (var q in questions.Where(q => q.TopicId == dimTopic.Id))
            freeQuiz.Questions.Add(new TestQuestion { TestId = freeQuiz.Id, QuestionId = q.Id, SortOrder = i++, Marks = 4, NegativeMarks = 1 });

        var chapterTest = new Test
        {
            CourseId = paid.Id, ExamId = neet.Id, Type = TestType.Chapter, TitleEn = "Laws of Motion — Chapter Test (Sample)",
            InstructionsEn = "5 questions, 20 minutes. Single-correct: +4/−1. Numerical: +4/−1. Multiple-correct: +4 all correct, +1 per correct option if no wrong option chosen, −2 otherwise.",
            DurationMinutes = 20, ShuffleQuestions = true, ShuffleOptions = true, MaxAttempts = 3, Status = PublishStatus.Published, CreatedAt = now,
        };
        i = 0;
        foreach (var q in questions.Where(q => q.TopicId == newton1.Id || q.TopicId == friction.Id))
            chapterTest.Questions.Add(new TestQuestion { TestId = chapterTest.Id, QuestionId = q.Id, SortOrder = i++, Marks = q.DefaultMarks, NegativeMarks = q.DefaultNegativeMarks });

        db.Tests.AddRange(freeQuiz, chapterTest);
        await db.SaveChangesAsync();
    }
}

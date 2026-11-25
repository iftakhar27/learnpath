using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Exams;

/// <summary>An NCERT chapter the generator can draw from: questions the student can use, by difficulty, and how many more are in paid courses.</summary>
public record CustomTestChapterDto(Guid Id, string ClassLevel, int Number, string Title, int Easy, int Medium, int Hard, int Locked);
public record CustomTestSubjectDto(string Subject, string Title, List<CustomTestChapterDto> Chapters);
public record CustomTestOptionsDto(List<CustomTestSubjectDto> Subjects, int MaxQuestions, int MinutesPerQuestion);
public record CreateCustomTestRequest(
    [Required, MinLength(1), MaxLength(CustomTestRules.MaxChapters)] List<Guid> ChapterIds,
    [Range(1, CustomTestRules.MaxQuestions)] int Count = 30,
    Difficulty? Difficulty = null,
    bool SkipCorrect = true);

public static class CustomTestRules
{
    public const int MaxQuestions = 180;   // a full NEET paper
    public const int MaxChapters = 100;
    public const int MinutesPerQuestion = 1; // NEET pace: 180 questions in 180 minutes
    public const int MinMinutes = 5;
    public const int PerDay = 20;
}

/// <summary>
/// Custom test generator: the student picks NCERT chapters, how many questions and the difficulty, and gets a private
/// timed test drawn from questions in courses they can open. It then runs like any other test (exam player, results,
/// review, analytics).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/tests/custom")]
public class CustomTestsController(AppDbContext db, EntitlementService entitlements, TestSummaryService tests, RequestLanguage lang, TimeProvider clock) : ControllerBase
{
    /// <summary>One published question in a published course chapter linked to an NCERT chapter.</summary>
    private sealed class Candidate
    {
        public Guid QuestionId { get; init; }
        public Guid NcertId { get; init; }
        public Guid CourseId { get; init; }
        public Difficulty Difficulty { get; init; }
        public decimal Marks { get; init; }
        public decimal NegativeMarks { get; init; }
    }

    private IQueryable<Candidate> Candidates(IReadOnlyCollection<Guid>? ncertIds)
    {
        var now = clock.GetUtcNow();
        return from q in db.Questions.AsNoTracking()
               join t in db.Topics on q.TopicId equals t.Id
               join ch in db.Chapters on t.ChapterId equals ch.Id
               join s in db.Subjects on ch.SubjectId equals s.Id
               join c in db.Courses on s.CourseId equals c.Id
               where q.Status == PublishStatus.Published && ch.NcertChapterId != null
                     && c.Status == PublishStatus.Published && (c.PublishAt == null || c.PublishAt <= now)
                     && (ncertIds == null || ncertIds.Contains(ch.NcertChapterId!.Value))
               select new Candidate
               {
                   QuestionId = q.Id, NcertId = ch.NcertChapterId!.Value, CourseId = c.Id, Difficulty = q.Difficulty,
                   Marks = q.DefaultMarks, NegativeMarks = q.DefaultNegativeMarks,
               };
    }

    private async Task<HashSet<Guid>> OpenCoursesAsync(Guid userId, CancellationToken ct)
    {
        var open = await entitlements.ActiveCourseIdsAsync(userId, ct);
        open.UnionWith(await db.Courses.Where(c => c.IsFree && c.Status == PublishStatus.Published).Select(c => c.Id).ToListAsync(ct));
        return open;
    }

    private string SubjectTitle(string subject) => subject switch
    {
        NcertSubjects.Physics => lang.Pick("Physics", "भौतिकी"),
        NcertSubjects.Chemistry => lang.Pick("Chemistry", "रसायन विज्ञान"),
        _ => lang.Pick("Biology", "जीव विज्ञान"),
    };

    [HttpGet("options")]
    public async Task<CustomTestOptionsDto> Options(CancellationToken ct)
    {
        var open = await OpenCoursesAsync(User.UserId(), ct);
        // The same question can't be in two courses, so a plain count per chapter is exact.
        var counts = await Candidates(null).GroupBy(x => new { x.NcertId, x.CourseId, x.Difficulty })
            .Select(g => new { g.Key.NcertId, g.Key.CourseId, g.Key.Difficulty, Count = g.Count() }).ToListAsync(ct);
        var ids = counts.Select(c => c.NcertId).Distinct().ToList();
        var chapters = await db.NcertChapters.AsNoTracking().Where(n => ids.Contains(n.Id)).ToListAsync(ct);

        int Sum(Guid id, Difficulty? d, bool openOnly) =>
            counts.Where(c => c.NcertId == id && (d == null || c.Difficulty == d) && open.Contains(c.CourseId) == openOnly).Sum(c => c.Count);

        var subjects = NcertSubjects.All.Select(subject => new CustomTestSubjectDto(subject, SubjectTitle(subject),
                chapters.Where(n => n.Subject == subject).OrderBy(n => n.ClassLevel).ThenBy(n => n.Number)
                    .Select(n => new CustomTestChapterDto(n.Id, n.ClassLevel, n.Number, lang.Pick(n.TitleEn, n.TitleHi),
                        Sum(n.Id, Difficulty.Easy, true), Sum(n.Id, Difficulty.Medium, true), Sum(n.Id, Difficulty.Hard, true), Sum(n.Id, null, false)))
                    .ToList()))
            .Where(s => s.Chapters.Count > 0).ToList();
        return new CustomTestOptionsDto(subjects, CustomTestRules.MaxQuestions, CustomTestRules.MinutesPerQuestion);
    }

    [HttpGet]
    public async Task<List<TestSummaryDto>> Mine(CancellationToken ct)
    {
        var userId = User.UserId();
        var mine = await db.Tests.AsNoTracking().Where(t => t.OwnerUserId == userId).OrderByDescending(t => t.CreatedAt).Take(30).ToListAsync(ct);
        return await tests.SummariesAsync(userId, mine, ct);
    }

    [HttpPost]
    public async Task<TestSummaryDto> Create(CreateCustomTestRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var now = clock.GetUtcNow();
        if (await db.Tests.CountAsync(t => t.OwnerUserId == userId && t.CreatedAt > now.AddDays(-1), ct) >= CustomTestRules.PerDay)
            throw new AppException(429, "custom_test_limit", $"You can create up to {CustomTestRules.PerDay} custom tests a day. Please try again tomorrow.");

        var chapterIds = req.ChapterIds.Distinct().ToList();
        var chapters = await db.NcertChapters.AsNoTracking().Where(n => chapterIds.Contains(n.Id)).ToListAsync(ct);
        if (chapters.Count != chapterIds.Count) throw AppException.BadRequest("invalid_chapter", "One of the chosen chapters doesn't exist.");

        var open = await OpenCoursesAsync(userId, ct);
        var matching = await Candidates(chapterIds).Where(x => req.Difficulty == null || x.Difficulty == req.Difficulty).ToListAsync(ct);
        var pool = matching.Where(x => open.Contains(x.CourseId)).ToList();
        if (pool.Count == 0)
        {
            // Questions exist, but only in courses the student hasn't bought.
            if (matching.Count > 0) throw AppException.PaymentRequired("These chapters' questions are in paid courses. Buy a course to use them in a custom test.");
            throw AppException.Conflict("no_questions", "No questions are available for this selection yet.");
        }

        // Questions answered correctly before (practice or tests) go last, so the test favours new and missed ones.
        var known = new HashSet<Guid>();
        if (req.SkipCorrect)
        {
            var poolIds = pool.Select(x => x.QuestionId).ToList();
            known.UnionWith(await db.PracticeAnswers.Where(a => a.UserId == userId && a.Outcome == AnswerOutcome.Correct && poolIds.Contains(a.QuestionId))
                .Select(a => a.QuestionId).ToListAsync(ct));
            known.UnionWith(await (from r in db.AttemptQuestionResults
                                   join a in db.TestAttempts on r.AttemptId equals a.Id
                                   where a.UserId == userId && r.Outcome == AnswerOutcome.Correct && poolIds.Contains(r.QuestionId)
                                   select r.QuestionId).ToListAsync(ct));
        }
        var picked = Pick(pool, known, req.Count);

        // NEET order: a section per subject (Physics, Chemistry, Biology), chapters in book order inside each.
        var order = chapters.OrderBy(n => Array.IndexOf(NcertSubjects.All, n.Subject)).ThenBy(n => n.ClassLevel).ThenBy(n => n.Number)
            .Select((n, i) => (n.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var bySubject = chapters.ToDictionary(n => n.Id, n => n.Subject);
        var test = new Test
        {
            OwnerUserId = userId, Type = TestType.Custom, Status = PublishStatus.Published, IsFree = true, CreatedAt = now,
            DurationMinutes = Math.Max(CustomTestRules.MinMinutes, picked.Count * CustomTestRules.MinutesPerQuestion),
            TitleEn = Title(chapters, c => c.TitleEn, "Custom test", "more"),
            TitleHi = Title(chapters, c => c.TitleHi ?? c.TitleEn, "कस्टम टेस्ट", "और"),
            InstructionsEn = "Made by you from: " + string.Join(", ", chapters.OrderBy(n => order[n.Id]).Select(n => n.TitleEn)) + ".",
            InstructionsHi = "आपके चुने अध्यायों से बना: " + string.Join(", ", chapters.OrderBy(n => order[n.Id]).Select(n => n.TitleHi ?? n.TitleEn)) + "।",
        };
        var sections = NcertSubjects.All.Where(s => picked.Any(p => bySubject[p.NcertId] == s))
            .Select((s, i) => new TestSection { TestId = test.Id, TitleEn = s, TitleHi = s switch
            {
                NcertSubjects.Physics => "भौतिकी", NcertSubjects.Chemistry => "रसायन विज्ञान", _ => "जीव विज्ञान",
            }, SortOrder = i }).ToList();
        test.Sections.AddRange(sections);
        var sort = 0;
        foreach (var q in picked.OrderBy(p => order[p.NcertId]))
            test.Questions.Add(new TestQuestion
            {
                TestId = test.Id, QuestionId = q.QuestionId, SortOrder = sort++, Marks = q.Marks, NegativeMarks = q.NegativeMarks,
                SectionId = sections.First(s => s.TitleEn == bySubject[q.NcertId]).Id,
            });
        db.Tests.Add(test);
        await db.SaveChangesAsync(ct);
        return (await tests.SummariesAsync(userId, [test], ct))[0];
    }

    /// <summary>
    /// Spreads the test across the chosen chapters (round robin over shuffled per-chapter pools), new and missed
    /// questions first, then ones already answered correctly when there aren't enough.
    /// </summary>
    private static List<Candidate> Pick(List<Candidate> pool, HashSet<Guid> known, int count)
    {
        List<Candidate> RoundRobin(IEnumerable<Candidate> items)
        {
            var queues = items.GroupBy(x => x.NcertId).Select(g => new Queue<Candidate>(g.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue))))
                .OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();
            var result = new List<Candidate>();
            while (queues.Any(q => q.Count > 0))
                foreach (var q in queues.Where(q => q.Count > 0))
                    result.Add(q.Dequeue());
            return result;
        }
        return RoundRobin(pool.Where(x => !known.Contains(x.QuestionId)))
            .Concat(RoundRobin(pool.Where(x => known.Contains(x.QuestionId))))
            .Take(count).ToList();
    }

    private static string Title(List<NcertChapter> chapters, Func<NcertChapter, string> name, string prefix, string more)
    {
        var first = chapters.OrderBy(n => Array.IndexOf(NcertSubjects.All, n.Subject)).ThenBy(n => n.ClassLevel).ThenBy(n => n.Number).First();
        var title = $"{prefix}: {name(first)}" + (chapters.Count > 1 ? $" + {chapters.Count - 1} {more}" : "");
        return title.Length <= 200 ? title : title[..200];
    }
}

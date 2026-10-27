using System.ComponentModel.DataAnnotations;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Admin;

public record SectionUpsert([Required] string TitleEn, string? TitleHi);
public record TestQuestionUpsert([Required] Guid QuestionId, int? SectionIndex, decimal? Marks, decimal? NegativeMarks);
public record TestUpsert(Guid? CourseId, Guid? ExamId, [Required] TestType Type, [Required, MaxLength(200)] string TitleEn, string? TitleHi,
    string? InstructionsEn, string? InstructionsHi, [Range(1, 600)] int DurationMinutes, bool ShuffleQuestions, bool ShuffleOptions, bool IsFree,
    [Range(1, 100)] int? MaxAttempts, DateTimeOffset? AvailableFrom, DateTimeOffset? AvailableUntil,
    List<SectionUpsert>? Sections, [Required, MinLength(1)] List<TestQuestionUpsert> Questions);

[ApiController]
[Authorize(Policy = Policies.ContentEdit)]
[Route("api/v1/admin/tests")]
public class AdminTestsController(AppDbContext db, AuditService audit, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public Task<List<AdminTestListItem>> List([FromQuery] Guid? courseId, [FromQuery] PublishStatus? status, CancellationToken ct) =>
        db.Tests.AsNoTracking().Where(t => (courseId == null || t.CourseId == courseId) && (status == null || t.Status == status))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new AdminTestListItem(t.Id, t.TitleEn, t.Type, t.CourseId, db.Courses.Where(c => c.Id == t.CourseId).Select(c => c.TitleEn).FirstOrDefault(),
                t.DurationMinutes, t.IsFree, t.Status, t.Questions.Count, t.Questions.Sum(q => (decimal?)q.Marks) ?? 0, db.TestAttempts.Count(a => a.TestId == t.Id), t.CreatedAt))
            .ToListAsync(ct);

    [HttpGet("{id:guid}")]
    public async Task<AdminTestDto> Get(Guid id, CancellationToken ct)
    {
        var t = await db.Tests.AsNoTracking().Include(x => x.Sections).Include(x => x.Questions).ThenInclude(q => q.Question!).ThenInclude(q => q.Topic)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Test");
        var sections = t.Sections.OrderBy(s => s.SortOrder).ToList();
        return new AdminTestDto(t.Id, t.CourseId, t.ExamId, t.Type, t.TitleEn, t.TitleHi, t.InstructionsEn, t.InstructionsHi, t.DurationMinutes,
            t.ShuffleQuestions, t.ShuffleOptions, t.IsFree, t.MaxAttempts, t.AvailableFrom, t.AvailableUntil, t.Status,
            await db.TestAttempts.CountAsync(a => a.TestId == id, ct),
            sections.Select(s => new SectionUpsert(s.TitleEn, s.TitleHi)).ToList(),
            t.Questions.OrderBy(q => q.SortOrder).Select(q =>
            {
                var idx = q.SectionId is { } sid ? sections.FindIndex(s => s.Id == sid) : -1;
                return new AdminTestQuestionDto(q.QuestionId, idx >= 0 ? idx : null, q.Marks, q.NegativeMarks, q.Question!.StemEn, q.Question.ExternalRef,
                    q.Question.Type, q.Question.Difficulty, q.Question.Status, q.Question.Topic!.TitleEn);
            }).ToList());
    }

    /// <summary>Copies a test (structure, marks, settings) into a new draft — the way to change a test that already has attempts.</summary>
    [HttpPost("{id:guid}/duplicate")]
    public async Task<IdDto> Duplicate(Guid id, CancellationToken ct)
    {
        var src = await Get(id, ct);
        var copy = new TestUpsert(src.CourseId, src.ExamId, src.Type, src.TitleEn + " (copy)", src.TitleHi, src.InstructionsEn, src.InstructionsHi,
            src.DurationMinutes, src.ShuffleQuestions, src.ShuffleOptions, src.IsFree, src.MaxAttempts, src.AvailableFrom, src.AvailableUntil, src.Sections,
            src.Questions.Select(q => new TestQuestionUpsert(q.QuestionId, q.SectionIndex, q.Marks, q.NegativeMarks)).ToList());
        return await Create(copy, ct);
    }

    [HttpPost]
    public async Task<IdDto> Create(TestUpsert req, CancellationToken ct)
    {
        var test = new Test { CreatedAt = clock.GetUtcNow() };
        await ApplyAsync(test, req, ct);
        db.Tests.Add(test);
        audit.Record(User.UserId(), "test.create", "Test", test.Id, new { req.TitleEn, Questions = req.Questions.Count });
        await db.SaveChangesAsync(ct);
        return new IdDto(test.Id);
    }

    /// <summary>Structure can only change while no student has attempted the test; otherwise create a new test.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IdDto> Update(Guid id, TestUpsert req, CancellationToken ct)
    {
        var test = await db.Tests.Include(t => t.Sections).Include(t => t.Questions).FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw AppException.NotFound("Test");
        if (await db.TestAttempts.AnyAsync(a => a.TestId == id, ct))
            throw AppException.Conflict("test_has_attempts", "This test already has attempts. Duplicate it to make structural changes.");
        db.TestQuestions.RemoveRange(test.Questions);
        db.TestSections.RemoveRange(test.Sections);
        test.Questions.Clear();
        test.Sections.Clear();
        await ApplyAsync(test, req, ct);
        foreach (var s in test.Sections) db.Entry(s).State = EntityState.Added;
        foreach (var q in test.Questions) db.Entry(q).State = EntityState.Added;
        audit.Record(User.UserId(), "test.update", "Test", id, new { req.TitleEn });
        await db.SaveChangesAsync(ct);
        return new IdDto(id);
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = Policies.ContentPublish)]
    public async Task<IdDto> Status(Guid id, StatusChange req, CancellationToken ct)
    {
        var test = await db.Tests.Include(t => t.Questions).ThenInclude(q => q.Question).FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw AppException.NotFound("Test");
        if (req.Status == PublishStatus.Published && test.Questions.Any(q => q.Question!.Status != PublishStatus.Published))
            throw AppException.Conflict("unpublished_questions", "Publish all questions in this test first.");
        test.Status = req.Status;
        audit.Record(User.UserId(), "test.status", "Test", id, req);
        await db.SaveChangesAsync(ct);
        return new IdDto(id);
    }

    private async Task ApplyAsync(Test t, TestUpsert r, CancellationToken ct)
    {
        if (r.CourseId is not null && !await db.Courses.AnyAsync(c => c.Id == r.CourseId, ct)) throw AppException.NotFound("Course");
        if (r.AvailableFrom is not null && r.AvailableUntil is not null && r.AvailableUntil <= r.AvailableFrom)
            throw AppException.BadRequest("invalid_window", "Availability end must be after start.");
        if (r.Questions.Select(q => q.QuestionId).Distinct().Count() != r.Questions.Count)
            throw AppException.BadRequest("duplicate_question", "A question appears more than once.");
        var ids = r.Questions.Select(q => q.QuestionId).ToList();
        var questions = await db.Questions.Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        if (questions.Count != ids.Count) throw AppException.NotFound("One or more questions");

        (t.CourseId, t.ExamId, t.Type, t.TitleEn, t.TitleHi, t.InstructionsEn, t.InstructionsHi) = (r.CourseId, r.ExamId, r.Type, r.TitleEn, r.TitleHi, r.InstructionsEn, r.InstructionsHi);
        (t.DurationMinutes, t.ShuffleQuestions, t.ShuffleOptions, t.IsFree, t.MaxAttempts, t.AvailableFrom, t.AvailableUntil) =
            (r.DurationMinutes, r.ShuffleQuestions, r.ShuffleOptions, r.IsFree, r.MaxAttempts, r.AvailableFrom, r.AvailableUntil);

        var sections = (r.Sections ?? []).Select((s, i) => new TestSection { TestId = t.Id, TitleEn = s.TitleEn, TitleHi = s.TitleHi, SortOrder = i }).ToList();
        t.Sections.AddRange(sections);
        var order = 0;
        foreach (var q in r.Questions)
        {
            if (q.SectionIndex is { } si && (si < 0 || si >= sections.Count)) throw AppException.BadRequest("invalid_section", $"Section index {si} does not exist.");
            var src = questions[q.QuestionId];
            t.Questions.Add(new TestQuestion
            {
                TestId = t.Id, QuestionId = q.QuestionId, SectionId = q.SectionIndex is { } idx ? sections[idx].Id : null, SortOrder = order++,
                Marks = q.Marks ?? src.DefaultMarks, NegativeMarks = q.NegativeMarks ?? src.DefaultNegativeMarks,
            });
        }
    }
}

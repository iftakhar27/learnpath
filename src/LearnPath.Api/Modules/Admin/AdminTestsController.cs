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
    public async Task<object> List(CancellationToken ct) =>
        await db.Tests.AsNoTracking().OrderByDescending(t => t.CreatedAt)
            .Select(t => new { t.Id, t.TitleEn, Type = t.Type.ToString(), t.CourseId, t.DurationMinutes, Status = t.Status.ToString(), Questions = t.Questions.Count, Attempts = db.TestAttempts.Count(a => a.TestId == t.Id) })
            .ToListAsync(ct);

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

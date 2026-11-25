using System.ComponentModel.DataAnnotations;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Admin;

public record AdminFlashcardDeckDto(Guid ChapterId, string ClassLevel, string Subject, int Number, string Title, int Published, int Drafts);
public record AdminFlashcardDto(Guid Id, Guid NcertChapterId, string FrontEn, string? FrontHi, string BackEn, string? BackHi, int SortOrder, PublishStatus Status);
public record FlashcardUpsert([Required] Guid NcertChapterId, [Required, MaxLength(500)] string FrontEn, [MaxLength(500)] string? FrontHi,
    [Required, MaxLength(1000)] string BackEn, [MaxLength(1000)] string? BackHi, PublishStatus Status = PublishStatus.Published);
public record FlashcardImportRequest([Required] Guid NcertChapterId, [Required, MaxLength(200_000)] string Text, PublishStatus Status = PublishStatus.Published);
public record FlashcardImportProblem(int Line, string Reason);
public record FlashcardImportResult(int Added, List<FlashcardImportProblem> Problems);

/// <summary>Flash card decks per NCERT chapter: write cards one by one, or paste many at once ("front | back" per line).</summary>
[ApiController]
[Authorize(Policy = Policies.ContentEdit)]
[Route("api/v1/admin/flashcards")]
public class AdminFlashcardsController(AppDbContext db, AuditService audit, TimeProvider clock) : ControllerBase
{
    public const int MaxImportLines = 500;

    [HttpGet("decks")]
    public async Task<List<AdminFlashcardDeckDto>> Decks(CancellationToken ct)
    {
        var counts = await db.Flashcards.GroupBy(f => new { f.NcertChapterId, f.Status }).Select(g => new { g.Key.NcertChapterId, g.Key.Status, Count = g.Count() }).ToListAsync(ct);
        var chapters = await db.NcertChapters.AsNoTracking().ToListAsync(ct);
        int Count(Guid id, PublishStatus s) => counts.Where(c => c.NcertChapterId == id && c.Status == s).Sum(c => c.Count);
        return chapters.OrderBy(n => Array.IndexOf(NcertSubjects.All, n.Subject)).ThenBy(n => n.ClassLevel).ThenBy(n => n.Number)
            .Select(n => new AdminFlashcardDeckDto(n.Id, n.ClassLevel, n.Subject, n.Number, n.TitleEn, Count(n.Id, PublishStatus.Published),
                counts.Where(c => c.NcertChapterId == n.Id && c.Status != PublishStatus.Published).Sum(c => c.Count)))
            .ToList();
    }

    [HttpGet]
    public Task<List<AdminFlashcardDto>> List([FromQuery, Required] Guid ncertChapterId, CancellationToken ct) =>
        db.Flashcards.AsNoTracking().Where(f => f.NcertChapterId == ncertChapterId).OrderBy(f => f.SortOrder).ThenBy(f => f.CreatedAt)
            .Select(f => new AdminFlashcardDto(f.Id, f.NcertChapterId, f.FrontEn, f.FrontHi, f.BackEn, f.BackHi, f.SortOrder, f.Status)).ToListAsync(ct);

    [HttpPost]
    public async Task<IdDto> Create(FlashcardUpsert req, CancellationToken ct)
    {
        await EnsureChapter(req.NcertChapterId, ct);
        var now = clock.GetUtcNow();
        var card = new Flashcard
        {
            NcertChapterId = req.NcertChapterId, FrontEn = req.FrontEn.Trim(), FrontHi = Blank(req.FrontHi), BackEn = req.BackEn.Trim(), BackHi = Blank(req.BackHi),
            Status = req.Status, SortOrder = await NextSortAsync(req.NcertChapterId, ct), CreatedAt = now, UpdatedAt = now,
        };
        db.Flashcards.Add(card);
        audit.Record(User.UserId(), "flashcard.create", "Flashcard", card.Id, new { card.NcertChapterId, card.FrontEn });
        await db.SaveChangesAsync(ct);
        return new IdDto(card.Id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IdDto> Update(Guid id, FlashcardUpsert req, CancellationToken ct)
    {
        var card = await db.Flashcards.FirstOrDefaultAsync(f => f.Id == id, ct) ?? throw AppException.NotFound("Flash card");
        if (card.NcertChapterId != req.NcertChapterId)
        {
            await EnsureChapter(req.NcertChapterId, ct);
            card.NcertChapterId = req.NcertChapterId;
            card.SortOrder = await NextSortAsync(req.NcertChapterId, ct);
        }
        card.FrontEn = req.FrontEn.Trim();
        card.FrontHi = Blank(req.FrontHi);
        card.BackEn = req.BackEn.Trim();
        card.BackHi = Blank(req.BackHi);
        card.Status = req.Status;
        card.UpdatedAt = clock.GetUtcNow();
        audit.Record(User.UserId(), "flashcard.update", "Flashcard", id, new { card.NcertChapterId, card.FrontEn, card.Status });
        await db.SaveChangesAsync(ct);
        return new IdDto(id);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var card = await db.Flashcards.FirstOrDefaultAsync(f => f.Id == id, ct) ?? throw AppException.NotFound("Flash card");
        db.Flashcards.Remove(card);
        audit.Record(User.UserId(), "flashcard.delete", "Flashcard", id, new { card.NcertChapterId, card.FrontEn });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// One card per line: <c>front | back</c>, optionally <c>| front in Hindi | back in Hindi</c>; a tab works instead of
    /// "|". Blank lines and lines starting with # are ignored; cards already in the deck (same front) are skipped.
    /// </summary>
    [HttpPost("import")]
    public async Task<FlashcardImportResult> Import(FlashcardImportRequest req, CancellationToken ct)
    {
        await EnsureChapter(req.NcertChapterId, ct);
        var lines = req.Text.Replace("\r\n", "\n").Split('\n');
        if (lines.Count(l => !string.IsNullOrWhiteSpace(l)) > MaxImportLines)
            throw AppException.BadRequest("too_many_lines", $"Paste up to {MaxImportLines} cards at a time.");
        var existing = (await db.Flashcards.Where(f => f.NcertChapterId == req.NcertChapterId).Select(f => f.FrontEn).ToListAsync(ct))
            .Select(Key).ToHashSet();
        var sort = await NextSortAsync(req.NcertChapterId, ct);
        var now = clock.GetUtcNow();
        var problems = new List<FlashcardImportProblem>();
        var added = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var parts = (line.Contains('\t') ? line.Split('\t') : line.Split('|')).Select(p => p.Trim()).ToArray();
            string? Part(int n) => parts.Length > n && parts[n].Length > 0 ? parts[n] : null;
            var (front, back, frontHi, backHi) = (Part(0), Part(1), Part(2), Part(3));
            if (front is null || back is null) { problems.Add(new(i + 1, "Needs a front and a back, separated by | or a tab.")); continue; }
            if (front.Length > 500 || (frontHi?.Length ?? 0) > 500) { problems.Add(new(i + 1, "The front is longer than 500 characters.")); continue; }
            if (back.Length > 1000 || (backHi?.Length ?? 0) > 1000) { problems.Add(new(i + 1, "The back is longer than 1000 characters.")); continue; }
            if (!existing.Add(Key(front))) { problems.Add(new(i + 1, "Already in this deck.")); continue; }
            db.Flashcards.Add(new Flashcard
            {
                NcertChapterId = req.NcertChapterId, FrontEn = front, BackEn = back, FrontHi = frontHi, BackHi = backHi,
                Status = req.Status, SortOrder = sort++, CreatedAt = now, UpdatedAt = now,
            });
            added++;
        }
        if (added > 0)
        {
            audit.Record(User.UserId(), "flashcard.import", "NcertChapter", req.NcertChapterId, new { added, skipped = problems.Count });
            await db.SaveChangesAsync(ct);
        }
        return new FlashcardImportResult(added, problems);
    }

    private static string Key(string front) => string.Join(' ', front.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<int> NextSortAsync(Guid chapterId, CancellationToken ct) =>
        (await db.Flashcards.Where(f => f.NcertChapterId == chapterId).MaxAsync(f => (int?)f.SortOrder, ct) ?? -1) + 1;

    private async Task EnsureChapter(Guid id, CancellationToken ct)
    {
        if (!await db.NcertChapters.AnyAsync(n => n.Id == id, ct)) throw AppException.BadRequest("invalid_ncert_chapter", "Choose a chapter from the NCERT list.");
    }
}

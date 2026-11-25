using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Revision;

/// <summary>A chapter's deck: how many cards, how many are due now (new ones included) and how many the student has mastered.</summary>
public record FlashcardDeckDto(Guid ChapterId, string ClassLevel, string Subject, string SubjectTitle, int Number, string Title, int Cards, int Due, int Mastered);
public record FlashcardsHomeDto(List<FlashcardDeckDto> Decks, int DueTotal);
/// <summary><c>Box</c> 0 = new or still learning; each "got it" moves it up one, up to 5.</summary>
public record FlashcardDto(Guid Id, string Front, string Back, int Box, bool IsNew, bool IsDue, DateTimeOffset? DueAt);
public record FlashcardDeckDetailDto(FlashcardDeckDto Deck, List<FlashcardDto> Cards, DateTimeOffset? NextDueAt);
public record FlashcardReviewRequest(bool Known);
public record FlashcardReviewDto(Guid Id, int Box, DateTimeOffset DueAt, bool Mastered);

/// <summary>Spaced repetition (Leitner boxes) for flash cards.</summary>
public static class FlashcardRules
{
    /// <summary>Days until a card is due again after "got it" moves it into box 1, 2, 3, 4, 5.</summary>
    public static readonly int[] IntervalDays = [1, 3, 7, 16, 35];
    public const int TopBox = 5;
    /// <summary>Known on three spaced days in a row.</summary>
    public const int MasteredBox = 3;
}

/// <summary>
/// Flash cards per NCERT chapter, free for every signed-in student. "Got it" pushes a card further out (1, 3, 7, 16, 35
/// days); "still learning" brings it back now. Due cards (and new ones) come first in a deck.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/flashcards")]
public class FlashcardsController(AppDbContext db, RequestLanguage lang, TimeProvider clock) : ControllerBase
{
    private string SubjectTitle(string subject) => subject switch
    {
        NcertSubjects.Physics => lang.Pick("Physics", "भौतिकी"),
        NcertSubjects.Chemistry => lang.Pick("Chemistry", "रसायन विज्ञान"),
        _ => lang.Pick("Biology", "जीव विज्ञान"),
    };

    private FlashcardDeckDto Deck(NcertChapter n, IReadOnlyCollection<Guid> cardIds, IReadOnlyDictionary<Guid, FlashcardReview> reviews, DateTimeOffset now) =>
        new(n.Id, n.ClassLevel, n.Subject, SubjectTitle(n.Subject), n.Number, lang.Pick(n.TitleEn, n.TitleHi), cardIds.Count,
            cardIds.Count(id => !reviews.TryGetValue(id, out var r) || r.DueAt <= now),
            cardIds.Count(id => reviews.TryGetValue(id, out var r) && r.Box >= FlashcardRules.MasteredBox));

    [HttpGet]
    public async Task<FlashcardsHomeDto> Decks(CancellationToken ct)
    {
        var userId = User.UserId();
        var now = clock.GetUtcNow();
        var cards = await db.Flashcards.AsNoTracking().Where(f => f.Status == PublishStatus.Published)
            .Select(f => new { f.Id, f.NcertChapterId }).ToListAsync(ct);
        var chapterIds = cards.Select(c => c.NcertChapterId).Distinct().ToList();
        var chapters = await db.NcertChapters.AsNoTracking().Where(n => chapterIds.Contains(n.Id)).ToListAsync(ct);
        var reviews = await db.FlashcardReviews.AsNoTracking().Where(r => r.UserId == userId).ToDictionaryAsync(r => r.FlashcardId, ct);
        var byChapter = cards.ToLookup(c => c.NcertChapterId, c => c.Id);

        var decks = chapters.OrderBy(n => Array.IndexOf(NcertSubjects.All, n.Subject)).ThenBy(n => n.ClassLevel).ThenBy(n => n.Number)
            .Select(n => Deck(n, byChapter[n.Id].ToList(), reviews, now)).ToList();
        return new FlashcardsHomeDto(decks, decks.Sum(d => d.Due));
    }

    [HttpGet("{chapterId:guid}")]
    public async Task<FlashcardDeckDetailDto> Deck(Guid chapterId, CancellationToken ct)
    {
        var userId = User.UserId();
        var now = clock.GetUtcNow();
        var n = await db.NcertChapters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == chapterId, ct) ?? throw AppException.NotFound("Chapter");
        var cards = await db.Flashcards.AsNoTracking().Where(f => f.NcertChapterId == chapterId && f.Status == PublishStatus.Published)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.CreatedAt).ToListAsync(ct);
        if (cards.Count == 0) throw AppException.NotFound("Flash card deck");
        var ids = cards.Select(c => c.Id).ToList();
        var reviews = await db.FlashcardReviews.AsNoTracking().Where(r => r.UserId == userId && ids.Contains(r.FlashcardId)).ToDictionaryAsync(r => r.FlashcardId, ct);

        var dtos = cards.Select(c =>
        {
            reviews.TryGetValue(c.Id, out var r);
            return new FlashcardDto(c.Id, lang.Pick(c.FrontEn, c.FrontHi), lang.Pick(c.BackEn, c.BackHi), r?.Box ?? 0, r is null, r is null || r.DueAt <= now, r?.DueAt);
        }).ToList();
        // Cards still being learnt first, then reviews by how overdue they are, then new cards in deck order, then the rest.
        var ordered = dtos.Where(d => d.IsDue && !d.IsNew).OrderBy(d => d.Box).ThenBy(d => d.DueAt)
            .Concat(dtos.Where(d => d.IsNew))
            .Concat(dtos.Where(d => !d.IsDue).OrderBy(d => d.DueAt)).ToList();
        var next = reviews.Values.Where(r => r.DueAt > now).Select(r => (DateTimeOffset?)r.DueAt).Min();
        return new FlashcardDeckDetailDto(Deck(n, ids, reviews, now), ordered, next);
    }

    [HttpPost("cards/{cardId:guid}/review")]
    public async Task<FlashcardReviewDto> Review(Guid cardId, FlashcardReviewRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var now = clock.GetUtcNow();
        if (!await db.Flashcards.AnyAsync(f => f.Id == cardId && f.Status == PublishStatus.Published, ct)) throw AppException.NotFound("Flash card");
        var review = await db.FlashcardReviews.FirstOrDefaultAsync(r => r.UserId == userId && r.FlashcardId == cardId, ct);
        if (review is null)
        {
            review = new FlashcardReview { UserId = userId, FlashcardId = cardId };
            db.FlashcardReviews.Add(review);
        }
        if (req.Known)
        {
            review.Box = Math.Min(review.Box + 1, FlashcardRules.TopBox);
            review.DueAt = now.AddDays(FlashcardRules.IntervalDays[review.Box - 1]);
        }
        else
        {
            review.Box = 0;
            review.DueAt = now;
        }
        review.LastReviewedAt = now;
        review.Reviews++;
        db.LearningEvents.Add(new LearningEvent { UserId = userId, EventType = LearningEventTypes.FlashcardReviewed, EntityId = cardId, OccurredAt = now });
        await db.SaveChangesAsync(ct);
        return new FlashcardReviewDto(cardId, review.Box, review.DueAt, review.Box >= FlashcardRules.MasteredBox);
    }
}

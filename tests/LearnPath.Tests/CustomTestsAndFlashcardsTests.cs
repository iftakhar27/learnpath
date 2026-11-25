using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

/// <summary>The custom test generator (private timed tests from chosen NCERT chapters) and flash cards with spaced repetition.</summary>
[Collection("api")]
public class CustomTestsAndFlashcardsTests(ApiFactory api)
{
    /// <summary>The database keeps microseconds; the test clock has finer ticks.</summary>
    private static void Near(DateTimeOffset expected, DateTimeOffset actual) => Assert.InRange(actual, expected.AddMilliseconds(-1), expected.AddMilliseconds(1));

    private Task<Guid> NcertAsync(string cls, string subject, int number) =>
        api.WithDb(db => db.NcertChapters.Where(n => n.ClassLevel == cls && n.Subject == subject && n.Number == number).Select(n => n.Id).FirstAsync());

    [Fact]
    public async Task Options_show_usable_and_paid_questions_per_NCERT_chapter()
    {
        var s = await api.StudentAsync();
        var options = await s.GetAsync("/api/v1/tests/custom/options").Ok();
        Assert.Equal(180, options.GetProperty("maxQuestions").GetInt32());
        var physics = options.GetProperty("subjects").EnumerateArray().Single(x => x.Str("subject") == "Physics");
        var chapters = physics.GetProperty("chapters").EnumerateArray().ToList();
        int Usable(JsonElement c) => c.GetProperty("easy").GetInt32() + c.GetProperty("medium").GetInt32() + c.GetProperty("hard").GetInt32();

        var units = chapters.Single(c => c.Str("title") == "Units and Measurement");
        Assert.Equal(3, Usable(units)); // free sample course
        Assert.Equal(0, units.GetProperty("locked").GetInt32());
        var laws = chapters.Single(c => c.Str("title") == "Laws of Motion");
        Assert.Equal(0, Usable(laws)); // paid sample course, not bought
        Assert.True(laws.GetProperty("locked").GetInt32() > 0);
        Assert.DoesNotContain(chapters, c => c.Str("title") == "Gravitation"); // no questions anywhere
    }

    [Fact]
    public async Task A_generated_test_is_private_runs_in_the_exam_player_and_favours_unseen_questions()
    {
        var s = await api.StudentAsync();
        var units = await NcertAsync("11", NcertSubjects.Physics, 1);

        // Get one Units question right in practice first.
        var session = await s.PostAsJsonAsync("/api/v1/practice/sessions", new { mode = "ncert", ncertChapterId = units, count = 1 }).Ok();
        var known = session.GetProperty("questions")[0].Id("questionId");
        var right = await api.WithDb(db => db.QuestionOptions.Where(o => o.QuestionId == known && o.IsCorrect).Select(o => o.Id).ToListAsync());
        await s.PostAsJsonAsync($"/api/v1/practice/sessions/{session.Str("sessionId")}/answers",
            new { questionId = known, response = new { selectedOptionIds = right }, timeSpentSeconds = 20 }).Ok();

        // Asking for 2 of the 3: the one already answered correctly is left out.
        var test = await s.PostAsJsonAsync("/api/v1/tests/custom", new { chapterIds = new[] { units }, count = 2, skipCorrect = true }).Ok();
        Assert.Equal("Custom", test.Str("type"));
        Assert.Equal("Custom test: Units and Measurement", test.Str("title"));
        Assert.Equal(2, test.GetProperty("questionCount").GetInt32());
        Assert.Equal(5, test.GetProperty("durationMinutes").GetInt32()); // at least 5 minutes
        Assert.False(test.GetProperty("locked").GetBoolean());
        var testId = test.Id("id");
        var picked = await api.WithDb(db => db.TestQuestions.Where(q => q.TestId == testId).Select(q => q.QuestionId).ToListAsync());
        Assert.DoesNotContain(known, picked);

        // More than there are: every usable question, the known one included.
        var all = await s.PostAsJsonAsync("/api/v1/tests/custom", new { chapterIds = new[] { units }, count = 30 }).Ok();
        Assert.Equal(3, all.GetProperty("questionCount").GetInt32());

        // Listed under "my custom tests", never in the shared list, the admin list or search.
        var mine = await s.GetAsync("/api/v1/tests/custom").Ok();
        Assert.Equal([all.Str("id"), test.Str("id")], mine.EnumerateArray().Select(t => t.Str("id")).ToArray());
        Assert.DoesNotContain((await s.GetAsync("/api/v1/tests").Ok()).EnumerateArray(), t => t.Str("type") == "Custom");
        var admin = await api.AdminAsync();
        Assert.DoesNotContain((await admin.GetAsync("/api/v1/admin/tests").Ok()).EnumerateArray(), t => t.Str("id") == test.Str("id"));
        Assert.Empty((await s.GetAsync("/api/v1/search?q=Custom%20test").Ok()).EnumerateArray().Where(h => h.Str("kind") == "test"));

        // It runs like any test: details, an attempt with a Physics section, results.
        var detail = await s.GetAsync($"/api/v1/tests/{testId}").Ok();
        Assert.Contains("Units and Measurement", detail.Str("instructions"));
        Assert.Equal(["Physics"], detail.GetProperty("sections").EnumerateArray().Select(x => x.GetString()).ToArray());
        var attempt = await s.PostAsync($"/api/v1/tests/{testId}/attempts", null).Ok();
        Assert.Equal(2, attempt.GetProperty("questions").GetArrayLength());
        var result = await s.PostAsync($"/api/v1/attempts/{attempt.Str("attemptId")}/submit", null).Ok();
        Assert.Equal(8m, result.Dec("maxScore"));

        // Nobody else can see or take it.
        var other = await api.StudentAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/tests/{testId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/tests/{testId}/attempts", null)).StatusCode);
        Assert.Empty((await other.GetAsync("/api/v1/tests/custom").Ok()).EnumerateArray());
    }

    [Fact]
    public async Task The_generator_explains_paid_empty_and_unknown_chapters_and_limits_tests_per_day()
    {
        var s = await api.StudentAsync();
        var laws = await NcertAsync("11", NcertSubjects.Physics, 4);
        var gravitation = await NcertAsync("11", NcertSubjects.Physics, 7);
        var units = await NcertAsync("11", NcertSubjects.Physics, 1);

        var paid = await s.PostAsJsonAsync("/api/v1/tests/custom", new { chapterIds = new[] { laws }, count = 10 });
        Assert.Equal(HttpStatusCode.PaymentRequired, paid.StatusCode);
        var empty = await s.PostAsJsonAsync("/api/v1/tests/custom", new { chapterIds = new[] { gravitation }, count = 10 });
        Assert.Equal(HttpStatusCode.Conflict, empty.StatusCode);
        Assert.Equal("no_questions", (await empty.ReadJson()).Str("code"));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.PostAsJsonAsync("/api/v1/tests/custom", new { chapterIds = new[] { Guid.NewGuid() }, count = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.PostAsJsonAsync("/api/v1/tests/custom", new { chapterIds = Array.Empty<Guid>(), count = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.PostAsJsonAsync("/api/v1/tests/custom", new { chapterIds = new[] { units }, count = 181 })).StatusCode);
        // A difficulty with nothing to offer is "no questions", not an empty test.
        var hardOnly = await api.WithDb(db => db.Questions.CountAsync(q => q.Topic!.Chapter!.NcertChapterId == units && q.Difficulty == Difficulty.Hard));
        if (hardOnly == 0)
            Assert.Equal(HttpStatusCode.Conflict, (await s.PostAsJsonAsync("/api/v1/tests/custom", new { chapterIds = new[] { units }, count = 10, difficulty = "Hard" })).StatusCode);

        for (var i = 0; i < 20; i++)
            await s.PostAsJsonAsync("/api/v1/tests/custom", new { chapterIds = new[] { units }, count = 3 }).Ok();
        var limited = await s.PostAsJsonAsync("/api/v1/tests/custom", new { chapterIds = new[] { units }, count = 3 });
        Assert.Equal((HttpStatusCode)429, limited.StatusCode);
        Assert.Equal("custom_test_limit", (await limited.ReadJson()).Str("code"));
    }

    [Fact]
    public async Task Flash_cards_come_back_by_spaced_repetition()
    {
        var s = await api.StudentAsync();
        var units = await NcertAsync("11", NcertSubjects.Physics, 1);
        var home = await s.GetAsync("/api/v1/flashcards").Ok();
        var decks = home.GetProperty("decks").EnumerateArray().ToList();
        Assert.True(decks.Count >= 4); // starter decks
        var unitsDeck = decks.Single(d => d.Id("chapterId") == units);
        var total = unitsDeck.GetProperty("cards").GetInt32();
        Assert.Equal(11, total);
        Assert.Equal(total, unitsDeck.GetProperty("due").GetInt32()); // all new
        Assert.Equal(0, unitsDeck.GetProperty("mastered").GetInt32());
        Assert.Equal(decks.Sum(d => d.GetProperty("due").GetInt32()), home.GetProperty("dueTotal").GetInt32());
        Assert.Equal(11, (await s.GetAsync($"/api/v1/library/chapters/{units}").Ok()).GetProperty("flashcardCount").GetInt32());

        var deck = await s.GetAsync($"/api/v1/flashcards/{units}").Ok();
        var cards = deck.GetProperty("cards").EnumerateArray().ToList();
        Assert.Equal("How many SI base units are there?", cards[0].Str("front"));
        Assert.All(cards, c => Assert.True(c.GetProperty("isNew").GetBoolean() && c.GetProperty("isDue").GetBoolean()));
        var (first, second) = (cards[0].Id("id"), cards[1].Id("id"));

        var gotIt = await s.PostAsJsonAsync($"/api/v1/flashcards/cards/{first}/review", new { known = true }).Ok();
        Assert.Equal(1, gotIt.GetProperty("box").GetInt32());
        Near(api.Clock.GetUtcNow().AddDays(1), gotIt.GetProperty("dueAt").GetDateTimeOffset());
        var learning = await s.PostAsJsonAsync($"/api/v1/flashcards/cards/{second}/review", new { known = false }).Ok();
        Assert.Equal(0, learning.GetProperty("box").GetInt32());

        // "Still learning" comes back first, the known card goes last until it's due tomorrow.
        deck = await s.GetAsync($"/api/v1/flashcards/{units}").Ok();
        cards = deck.GetProperty("cards").EnumerateArray().ToList();
        Assert.Equal(second, cards[0].Id("id"));
        Assert.False(cards[0].GetProperty("isNew").GetBoolean());
        Assert.Equal(first, cards[^1].Id("id"));
        Assert.False(cards[^1].GetProperty("isDue").GetBoolean());
        Assert.Equal(total - 1, deck.GetProperty("deck").GetProperty("due").GetInt32());
        Near(gotIt.GetProperty("dueAt").GetDateTimeOffset(), deck.GetProperty("nextDueAt").GetDateTimeOffset());

        // Due again (moved in the database, as the shared test clock can't go back): known twice more, it's mastered.
        var userId = await api.WithDb(db => db.FlashcardReviews.Where(r => r.FlashcardId == first).Select(r => r.UserId).FirstAsync());
        for (var box = 2; box <= 3; box++)
        {
            await api.WithDb(db => db.FlashcardReviews.Where(r => r.UserId == userId && r.FlashcardId == first)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.DueAt, api.Clock.GetUtcNow().AddMinutes(-1))));
            var again = await s.PostAsJsonAsync($"/api/v1/flashcards/cards/{first}/review", new { known = true }).Ok();
            Assert.Equal(box, again.GetProperty("box").GetInt32());
            Near(api.Clock.GetUtcNow().AddDays(box == 2 ? 3 : 7), again.GetProperty("dueAt").GetDateTimeOffset());
            Assert.Equal(box == 3, again.GetProperty("mastered").GetBoolean());
        }
        var after = (await s.GetAsync("/api/v1/flashcards").Ok()).GetProperty("decks").EnumerateArray().Single(d => d.Id("chapterId") == units);
        Assert.Equal(1, after.GetProperty("mastered").GetInt32());
        // Reviewing counts as learning for the streak.
        Assert.True(await api.WithDb(db => db.LearningEvents.AnyAsync(e => e.UserId == userId && e.EventType == LearningEventTypes.FlashcardReviewed)));

        Assert.Equal(HttpStatusCode.NotFound, (await s.PostAsJsonAsync($"/api/v1/flashcards/cards/{Guid.NewGuid()}/review", new { known = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.GetAsync($"/api/v1/flashcards/{await NcertAsync("12", NcertSubjects.Physics, 14)}")).StatusCode); // no cards in any test
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/v1/flashcards")).StatusCode);
    }

    [Fact]
    public async Task Admins_write_paste_publish_and_delete_flash_cards()
    {
        var a = await api.AdminAsync();
        var decks = await a.GetAsync("/api/v1/admin/flashcards/decks").Ok();
        Assert.Equal(79, decks.GetArrayLength());
        var gravitation = await NcertAsync("11", NcertSubjects.Physics, 7);

        // A draft card stays hidden from students until published.
        var id = (await a.PostAsJsonAsync("/api/v1/admin/flashcards", new
        {
            ncertChapterId = gravitation, frontEn = "Value of G", backEn = "6.67 × 10⁻¹¹ N m² kg⁻²", frontHi = "G का मान", status = "Draft",
        }).Ok()).Str("id");
        var s = await api.StudentAsync();
        Assert.DoesNotContain((await s.GetAsync("/api/v1/flashcards").Ok()).GetProperty("decks").EnumerateArray(), d => d.Id("chapterId") == gravitation);
        await a.PutAsJsonAsync($"/api/v1/admin/flashcards/{id}", new
        {
            ncertChapterId = gravitation, frontEn = "Value of G", backEn = "6.67 × 10⁻¹¹ N m² kg⁻²", frontHi = "G का मान", status = "Published",
        }).Ok();
        Assert.Contains((await s.GetAsync("/api/v1/flashcards").Ok()).GetProperty("decks").EnumerateArray(), d => d.Id("chapterId") == gravitation);

        // Pasting many: | or tab, optional Hindi columns, comments, blanks, problems reported by line.
        var import = await a.PostAsJsonAsync("/api/v1/admin/flashcards/import", new
        {
            ncertChapterId = gravitation,
            text = "# Gravitation\nEscape velocity from Earth | About 11.2 km/s\n\nKepler's second law\tEqual areas in equal times\tकेप्लर का दूसरा नियम\tसमान समय में समान क्षेत्रफल\nNo back here\nvalue of g | duplicate front\n",
        }).Ok();
        Assert.Equal(2, import.GetProperty("added").GetInt32());
        Assert.Equal([5, 6], import.GetProperty("problems").EnumerateArray().Select(p => p.GetProperty("line").GetInt32()).ToArray());
        var cards = await a.GetAsync($"/api/v1/admin/flashcards?ncertChapterId={gravitation}").Ok();
        Assert.Equal(["Value of G", "Escape velocity from Earth", "Kepler's second law"], cards.EnumerateArray().Select(c => c.Str("frontEn")).ToArray());
        Assert.Equal(3, (await a.GetAsync("/api/v1/admin/flashcards/decks").Ok()).EnumerateArray().Single(d => d.Id("chapterId") == gravitation).GetProperty("published").GetInt32());

        // Students see Hindi where it's written, English otherwise.
        s.DefaultRequestHeaders.AcceptLanguage.ParseAdd("hi");
        var hindi = (await s.GetAsync($"/api/v1/flashcards/{gravitation}").Ok()).GetProperty("cards").EnumerateArray().Select(c => c.Str("front")).ToList();
        Assert.Contains("G का मान", hindi);
        Assert.Contains("केप्लर का दूसरा नियम", hindi);
        Assert.Contains("Escape velocity from Earth", hindi);

        await a.DeleteAsync($"/api/v1/admin/flashcards/{id}").Ok();
        Assert.Equal(2, (await a.GetAsync($"/api/v1/admin/flashcards?ncertChapterId={gravitation}").Ok()).GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync("/api/v1/admin/flashcards", new { ncertChapterId = Guid.NewGuid(), frontEn = "x", backEn = "y" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.GetAsync("/api/v1/admin/flashcards/decks")).StatusCode);
    }
}

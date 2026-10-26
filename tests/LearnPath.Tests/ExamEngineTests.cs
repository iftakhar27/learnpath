using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

[Collection("api")]
public class ExamEngineTests(ApiFactory api)
{
    private async Task<(HttpClient C, JsonElement Attempt)> StartFreeQuiz()
    {
        var c = await api.StudentAsync();
        var attempt = await c.PostAsync($"/api/v1/tests/{await api.TestIdAsync(free: true)}/attempts", null).Ok();
        return (c, attempt);
    }

    private static object Answer(JsonElement q, int optionIndex) => new
    {
        testQuestionId = q.Str("testQuestionId"),
        response = new { selectedOptionIds = new[] { q.GetProperty("options")[optionIndex].Str("id") } },
        markedForReview = false, timeSpentSeconds = 10,
    };

    [Fact]
    public async Task Attempt_never_exposes_answer_keys_or_explanations()
    {
        var (_, attempt) = await StartFreeQuiz();
        var raw = attempt.GetRawText();
        Assert.DoesNotContain("isCorrect", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("explanation", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("numericAnswer", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Starting_twice_resumes_the_same_attempt_with_saved_answers()
    {
        var (c, attempt) = await StartFreeQuiz();
        var q = attempt.GetProperty("questions")[0];
        await c.PutAsJsonAsync($"/api/v1/attempts/{attempt.Str("attemptId")}/answers", Answer(q, 1)).Ok();

        var again = await c.PostAsync($"/api/v1/tests/{attempt.Str("testId")}/attempts", null).Ok();
        Assert.Equal(attempt.Str("attemptId"), again.Str("attemptId"));
        Assert.Equal(q.GetProperty("options")[1].Str("id"), again.GetProperty("questions")[0].GetProperty("response").GetProperty("selectedOptionIds")[0].GetString());
    }

    [Fact]
    public async Task Correct_scoring_with_negative_marking_and_result_breakdown()
    {
        var (c, attempt) = await StartFreeQuiz();
        var aid = attempt.Str("attemptId");
        var correctByQuestion = await api.WithDb(db => db.TestQuestions.Include(t => t.Question!).ThenInclude(q => q.Options)
            .ToDictionaryAsync(t => t.Id, t => t.Question!.Options.FirstOrDefault(o => o.IsCorrect)?.Id));

        var qs = attempt.GetProperty("questions").EnumerateArray().ToList();
        // Q1 right, Q2 wrong, Q3 skipped => 4 - 1 + 0 = 3 of 12
        var right = correctByQuestion[Guid.Parse(qs[0].Str("testQuestionId"))]!.Value;
        var wrong = qs[1].GetProperty("options").EnumerateArray().Select(o => Guid.Parse(o.Str("id")))
            .First(id => id != correctByQuestion[Guid.Parse(qs[1].Str("testQuestionId"))]);
        await c.PutAsJsonAsync($"/api/v1/attempts/{aid}/answers", new { testQuestionId = qs[0].Str("testQuestionId"), response = new { selectedOptionIds = new[] { right } }, markedForReview = false, timeSpentSeconds = 5 }).Ok();
        await c.PutAsJsonAsync($"/api/v1/attempts/{aid}/answers", new { testQuestionId = qs[1].Str("testQuestionId"), response = new { selectedOptionIds = new[] { wrong } }, markedForReview = true, timeSpentSeconds = 5 }).Ok();

        var result = await c.PostAsync($"/api/v1/attempts/{aid}/submit", null).Ok();
        Assert.Equal(3m, result.Dec("score"));
        Assert.Equal(12m, result.Dec("maxScore"));
        Assert.Equal(1, result.GetProperty("correct").GetInt32());
        Assert.Equal(1, result.GetProperty("incorrect").GetInt32());
        Assert.Equal(1, result.GetProperty("skipped").GetInt32());
        Assert.Equal(50, result.GetProperty("accuracy").GetDouble());
        Assert.Single(result.GetProperty("subjects").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("percentile").ValueKind); // cohort too small
        var review = result.GetProperty("questions")[1];
        Assert.Equal("Incorrect", review.Str("outcome"));
        Assert.False(string.IsNullOrEmpty(review.Str("explanation")));
    }

    [Fact]
    public async Task Concurrent_and_repeated_submits_produce_exactly_one_result()
    {
        var (c, attempt) = await StartFreeQuiz();
        var aid = attempt.Str("attemptId");
        await c.PutAsJsonAsync($"/api/v1/attempts/{aid}/answers", Answer(attempt.GetProperty("questions")[0], 0)).Ok();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => c.PostAsync($"/api/v1/attempts/{aid}/submit", null)));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var scores = await Task.WhenAll(results.Select(async r => (await r.ReadJson()).Dec("score")));
        Assert.Single(scores.Distinct());

        var rows = await api.WithDb(db => db.AttemptQuestionResults.CountAsync(r => r.AttemptId == Guid.Parse(aid)));
        Assert.Equal(3, rows);
        var events = await api.WithDb(db => db.AttemptEvents.CountAsync(e => e.AttemptId == Guid.Parse(aid) && e.EventType == "submitted"));
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task Submitted_attempt_cannot_be_changed()
    {
        var (c, attempt) = await StartFreeQuiz();
        var aid = attempt.Str("attemptId");
        await c.PostAsync($"/api/v1/attempts/{aid}/submit", null).Ok();
        var late = await c.PutAsJsonAsync($"/api/v1/attempts/{aid}/answers", Answer(attempt.GetProperty("questions")[0], 0));
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal("attempt_closed", (await late.ReadJson()).Str("code"));
    }

    [Fact]
    public async Task Invalid_answers_are_rejected()
    {
        var (c, attempt) = await StartFreeQuiz();
        var q = attempt.GetProperty("questions")[0];
        var foreign = await c.PutAsJsonAsync($"/api/v1/attempts/{attempt.Str("attemptId")}/answers",
            new { testQuestionId = q.Str("testQuestionId"), response = new { selectedOptionIds = new[] { Guid.NewGuid() } }, markedForReview = false, timeSpentSeconds = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
    }

    [Fact]
    public async Task Other_students_cannot_read_or_answer_my_attempt()
    {
        var (_, attempt) = await StartFreeQuiz();
        var other = await api.StudentAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/attempts/{attempt.Str("attemptId")}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/v1/attempts/{attempt.Str("attemptId")}/submit", null)).StatusCode);
    }

    [Fact]
    public async Task Editing_a_question_later_does_not_change_past_results()
    {
        var (c, attempt) = await StartFreeQuiz();
        var aid = attempt.Str("attemptId");
        var q = attempt.GetProperty("questions")[0];
        var tqId = Guid.Parse(q.Str("testQuestionId"));
        var question = await api.WithDb(db => db.TestQuestions.Include(t => t.Question!).ThenInclude(x => x.Options).Where(t => t.Id == tqId).Select(t => t.Question!).FirstAsync());
        var correct = question.Options.Single(o => o.IsCorrect).Id;
        await c.PutAsJsonAsync($"/api/v1/attempts/{aid}/answers", new { testQuestionId = tqId, response = new { selectedOptionIds = new[] { correct } }, markedForReview = false, timeSpentSeconds = 3 }).Ok();
        var before = await c.PostAsync($"/api/v1/attempts/{aid}/submit", null).Ok();

        var admin = await api.AdminAsync();
        await admin.PutAsJsonAsync($"/api/v1/admin/questions/{question.Id}", new
        {
            externalRef = question.ExternalRef, topicId = question.TopicId, type = "SingleCorrect", difficulty = "Easy", stemEn = question.StemEn + " (edited)",
            defaultMarks = 4, defaultNegativeMarks = 1,
            options = question.Options.OrderBy(o => o.SortOrder).Select(o => new { textEn = o.TextEn, isCorrect = o.Id != correct && o.SortOrder == 1 }).ToArray(),
        }).Ok();

        var after = await c.GetAsync($"/api/v1/attempts/{aid}/results").Ok();
        Assert.Equal(before.Dec("score"), after.Dec("score"));
        Assert.Equal("Correct", after.GetProperty("questions")[0].Str("outcome"));
        Assert.DoesNotContain("(edited)", after.GetProperty("questions")[0].Str("stem"));
        Assert.Equal(2, await api.WithDb(db => db.Questions.Where(x => x.Id == question.Id).Select(x => x.Version).FirstAsync()));
    }

    [Fact]
    public async Task Paid_test_requires_entitlement()
    {
        var c = await api.StudentAsync();
        var res = await c.PostAsync($"/api/v1/tests/{await api.TestIdAsync(free: false)}/attempts", null);
        Assert.Equal(HttpStatusCode.PaymentRequired, res.StatusCode);
    }

    [Fact]
    public async Task Max_attempts_is_enforced()
    {
        var c = await api.StudentAsync();
        var userId = (await c.GetAsync("/api/v1/student/profile").Ok()).Id("userId");
        var paid = await api.CourseIdAsync(free: false);
        await api.WithDb(async db =>
        {
            db.Enrollments.Add(new Enrollment { UserId = userId, CourseId = paid, Source = EnrollmentSource.Admin, StartsAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
            return await db.SaveChangesAsync();
        });
        var testId = await api.TestIdAsync(free: false);
        for (var i = 0; i < 3; i++)
        {
            var a = await c.PostAsync($"/api/v1/tests/{testId}/attempts", null).Ok();
            await c.PostAsync($"/api/v1/attempts/{a.Str("attemptId")}/submit", null).Ok();
        }
        var fourth = await c.PostAsync($"/api/v1/tests/{testId}/attempts", null);
        Assert.Equal("max_attempts_reached", (await fourth.ReadJson()).Str("code"));
        var history = await c.GetAsync($"/api/v1/attempts?testId={testId}").Ok();
        Assert.Equal(3, history.GetArrayLength());
    }
}

/// <summary>Uses its own API instance because it moves the clock forward.</summary>
public class ExamTimingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory api;
    public ExamTimingTests(ApiFactory api) => this.api = api;

    [Fact]
    public async Task Server_enforces_time_limit_grace_and_auto_submits()
    {
        var c = await api.StudentAsync();
        var quiz = await api.TestIdAsync(free: true);
        var attempt = await c.PostAsync($"/api/v1/tests/{quiz}/attempts", null).Ok();
        var aid = attempt.Str("attemptId");
        var q = attempt.GetProperty("questions")[0];
        Assert.InRange(attempt.GetProperty("remainingSeconds").GetInt32(), 590, 600);
        object Ans(int i) => new { testQuestionId = q.Str("testQuestionId"), response = new { selectedOptionIds = new[] { q.GetProperty("options")[i].Str("id") } }, markedForReview = false, timeSpentSeconds = 1 };

        // Within the 30s grace after the 10-minute limit, saves still succeed (slow mobile networks).
        api.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(10));
        await c.PutAsJsonAsync($"/api/v1/attempts/{aid}/answers", Ans(0)).Ok();

        // After the grace period the server rejects answers, even though the client never submitted.
        api.Clock.Advance(TimeSpan.FromSeconds(30));
        var late = await c.PutAsJsonAsync($"/api/v1/attempts/{aid}/answers", Ans(1));
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);

        // Reading the attempt finalises it as auto-submitted, scored on what was saved in time.
        var view = await c.GetAsync($"/api/v1/attempts/{aid}").Ok();
        Assert.Equal("AutoSubmitted", view.Str("status"));
        Assert.Equal(0, view.GetProperty("remainingSeconds").GetInt32());
        var result = await c.GetAsync($"/api/v1/attempts/{aid}/results").Ok();
        Assert.Equal(q.GetProperty("options")[0].Str("id"), result.GetProperty("questions")[0].GetProperty("options").EnumerateArray().First(o => o.GetProperty("selected").GetBoolean()).Str("id"));
        Assert.Equal(600, result.GetProperty("timeTakenSeconds").GetInt32());

        // Starting again creates attempt #2 rather than resuming the expired one.
        var second = await c.PostAsync($"/api/v1/tests/{quiz}/attempts", null).Ok();
        Assert.NotEqual(aid, second.Str("attemptId"));
    }
}

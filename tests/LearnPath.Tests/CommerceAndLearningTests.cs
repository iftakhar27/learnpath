using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

[Collection("api")]
public class CommerceTests(ApiFactory api)
{
    private static string Sign(string secret, string message) => Hashing.HmacSha256Hex(secret, message);

    private static string WebhookBody(string evt, string orderId, string paymentId, long amount) => JsonSerializer.Serialize(new
    {
        @event = evt,
        payload = new { payment = new { entity = new { id = paymentId, order_id = orderId, amount, status = "captured", method = "upi" } } },
    });

    private HttpRequestMessage Webhook(string body, string eventId, string? signature = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        req.Headers.Add("X-Razorpay-Signature", signature ?? Sign(ApiFactory.WebhookSecret, body));
        req.Headers.Add("X-Razorpay-Event-Id", eventId);
        return req;
    }

    [Fact]
    public async Task Checkout_signature_must_verify_before_access_is_granted()
    {
        var c = await api.StudentAsync();
        var course = await api.CourseIdAsync(free: false);
        var paidTest = await api.TestIdAsync(free: false);
        var order = await c.PostAsJsonAsync("/api/v1/payments/orders", new { courseId = course }).Ok();
        var gwOrder = order.Str("gatewayOrderId");
        Assert.Equal(199900, order.GetProperty("amountInPaise").GetInt64());

        var forged = await c.PostAsJsonAsync("/api/v1/payments/verify", new { gatewayOrderId = gwOrder, gatewayPaymentId = "pay_1", signature = "deadbeef" });
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, (await c.PostAsync($"/api/v1/tests/{paidTest}/attempts", null)).StatusCode);

        var payId = "pay_" + Guid.NewGuid().ToString("N")[..12];
        var verified = await c.PostAsJsonAsync("/api/v1/payments/verify", new { gatewayOrderId = gwOrder, gatewayPaymentId = payId, signature = Sign(ApiFactory.KeySecret, $"{gwOrder}|{payId}") }).Ok();
        Assert.Equal("Paid", verified.Str("status"));
        await c.PostAsync($"/api/v1/tests/{paidTest}/attempts", null).Ok();

        // The webhook for the same payment arriving afterwards must not double-grant or duplicate the payment.
        var body = WebhookBody("payment.captured", gwOrder, payId, 199900);
        var wh = await (await api.CreateClient().SendAsync(Webhook(body, "evt_" + Guid.NewGuid().ToString("N")))).ReadJson();
        Assert.Equal("AlreadyPaid", wh.Str("status"));
        Assert.Equal(1, await api.WithDb(db => db.Payments.CountAsync(p => p.GatewayPaymentId == payId)));

        var orders = await c.GetAsync("/api/v1/payments/orders").Ok();
        Assert.Equal("Paid", orders[0].Str("status"));
    }

    [Fact]
    public async Task Webhook_is_signature_checked_idempotent_and_amount_checked()
    {
        var c = await api.StudentAsync();
        var course = await api.CourseIdAsync(free: false);
        var gwOrder = (await c.PostAsJsonAsync("/api/v1/payments/orders", new { courseId = course }).Ok()).Str("gatewayOrderId");
        var anon = api.CreateClient();

        var bad = await anon.SendAsync(Webhook(WebhookBody("payment.captured", gwOrder, "pay_x", 199900), "evt_bad", signature: "00"));
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        var cheap = await (await anon.SendAsync(Webhook(WebhookBody("payment.captured", gwOrder, "pay_cheap", 100), "evt_" + Guid.NewGuid().ToString("N")))).ReadJson();
        Assert.Equal("AmountMismatch", cheap.Str("status"));

        var payId = "pay_" + Guid.NewGuid().ToString("N")[..12];
        var body = WebhookBody("payment.captured", gwOrder, payId, 199900);
        var eventId = "evt_" + Guid.NewGuid().ToString("N");
        var replies = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => anon.SendAsync(Webhook(body, eventId))));
        Assert.All(replies, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var statuses = await Task.WhenAll(replies.Select(async r => (await r.ReadJson()).Str("status")));
        Assert.Equal(1, statuses.Count(s => s == "Granted"));
        Assert.Equal(3, statuses.Count(s => s == "duplicate"));

        Assert.Equal(1, await api.WithDb(db => db.PaymentWebhookEvents.CountAsync(e => e.EventId == eventId)));
        var userOrder = await api.WithDb(db => db.Orders.FirstAsync(o => o.GatewayOrderId == gwOrder));
        Assert.Equal(OrderStatus.Paid, userOrder.Status);
        var enrollment = await api.WithDb(db => db.Enrollments.SingleAsync(e => e.UserId == userOrder.UserId && e.CourseId == course));
        Assert.Equal(EnrollmentSource.Purchase, enrollment.Source);
        Assert.NotNull(enrollment.ExpiresAt); // sample course grants 365 days
        var notifications = await c.GetAsync("/api/v1/notifications").Ok();
        Assert.Contains(notifications.GetProperty("items").EnumerateArray(), x => x.Str("type") == "payment");
    }

    [Fact]
    public async Task Free_courses_cannot_be_bought_and_paid_courses_cannot_be_enrolled_for_free()
    {
        var c = await api.StudentAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/payments/orders", new { courseId = await api.CourseIdAsync(free: true) })).StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, (await c.PostAsync($"/api/v1/courses/{await api.CourseIdAsync(free: false)}/enroll", null)).StatusCode);
    }
}

[Collection("api")]
public class LearningTests(ApiFactory api)
{
    [Fact]
    public async Task Paid_video_is_locked_but_free_preview_plays()
    {
        var c = await api.StudentAsync();
        var paid = await api.CourseIdAsync(free: false);
        var syllabus = await c.GetAsync($"/api/v1/courses/{paid}/syllabus").Ok();
        Assert.False(syllabus.GetProperty("hasAccess").GetBoolean());
        var topics = syllabus.GetProperty("subjects")[0].GetProperty("chapters")[0].GetProperty("topics");

        var preview = await c.GetAsync($"/api/v1/topics/{topics[0].Str("id")}").Ok();
        var previewVideo = preview.GetProperty("resources")[0];
        Assert.False(previewVideo.GetProperty("locked").GetBoolean());
        var play = await c.GetAsync($"/api/v1/resources/{previewVideo.Str("id")}/playback").Ok();
        Assert.StartsWith("https://", play.Str("url"));

        var locked = (await c.GetAsync($"/api/v1/topics/{topics[1].Str("id")}").Ok()).GetProperty("resources")[0];
        Assert.True(locked.GetProperty("locked").GetBoolean());
        Assert.Equal(HttpStatusCode.PaymentRequired, (await c.GetAsync($"/api/v1/resources/{locked.Str("id")}/playback")).StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, (await c.PostAsJsonAsync("/api/v1/learning/progress", new { resourceId = locked.Str("id"), positionSeconds = 10 })).StatusCode);
    }

    [Fact]
    public async Task Video_progress_resumes_and_completes_at_ninety_percent()
    {
        var c = await api.StudentAsync();
        var free = await api.CourseIdAsync(free: true);
        await c.PostAsync($"/api/v1/courses/{free}/enroll", null);
        var syllabus = await c.GetAsync($"/api/v1/courses/{free}/syllabus").Ok();
        var topicId = syllabus.GetProperty("subjects")[0].GetProperty("chapters")[0].GetProperty("topics")[0].Str("id");
        var video = (await c.GetAsync($"/api/v1/topics/{topicId}").Ok()).GetProperty("resources")[0]; // 540s
        var vid = video.Str("id");

        var p1 = await c.PostAsJsonAsync("/api/v1/learning/progress", new { resourceId = vid, positionSeconds = 120 }).Ok();
        Assert.False(p1.GetProperty("completed").GetBoolean());
        Assert.Equal(120, (await c.GetAsync($"/api/v1/resources/{vid}/playback").Ok()).GetProperty("resumeAtSeconds").GetInt32());

        var dash = await c.GetAsync("/api/v1/student/dashboard").Ok();
        Assert.Equal(vid, dash.GetProperty("continueLearning")[0].Str("resourceId"));

        var p2 = await c.PostAsJsonAsync("/api/v1/learning/progress", new { resourceId = vid, positionSeconds = 490 }).Ok();
        Assert.True(p2.GetProperty("completed").GetBoolean());
        // Seeking back does not un-complete.
        var p3 = await c.PostAsJsonAsync("/api/v1/learning/progress", new { resourceId = vid, positionSeconds = 10 }).Ok();
        Assert.True(p3.GetProperty("completed").GetBoolean());

        var after = await c.GetAsync($"/api/v1/courses/{free}/syllabus").Ok();
        Assert.Equal(50, after.GetProperty("completionPercent").GetInt32()); // 1 of 2 resources
    }

    [Fact]
    public async Task Practice_reveals_solution_and_wrong_answers_drive_weak_topics_and_plan()
    {
        var c = await api.StudentAsync();
        var free = await api.CourseIdAsync(free: true);
        await c.PostAsync($"/api/v1/courses/{free}/enroll", null);
        var topicId = (await c.GetAsync($"/api/v1/courses/{free}/syllabus").Ok()).GetProperty("subjects")[0].GetProperty("chapters")[0].GetProperty("topics")[0].Str("id");

        var session = await c.PostAsJsonAsync("/api/v1/practice/sessions", new { mode = "topic", topicId, count = 10 }).Ok();
        var sid = session.Str("sessionId");
        var questions = session.GetProperty("questions").EnumerateArray().ToList();
        Assert.Equal(3, questions.Count);
        Assert.DoesNotContain("isCorrect", session.GetRawText(), StringComparison.OrdinalIgnoreCase);

        foreach (var q in questions)
        {
            var key = await api.WithDb(db => db.QuestionOptions.Where(o => o.QuestionId == Guid.Parse(q.Str("questionId")) && !o.IsCorrect).Select(o => o.Id).FirstAsync());
            var fb = await c.PostAsJsonAsync($"/api/v1/practice/sessions/{sid}/answers", new { questionId = q.Str("questionId"), response = new { selectedOptionIds = new[] { key } }, timeSpentSeconds = 5 }).Ok();
            Assert.Equal("Incorrect", fb.Str("outcome"));
            Assert.Single(fb.GetProperty("correctOptionIds").EnumerateArray());
            Assert.False(string.IsNullOrEmpty(fb.Str("explanation")));
        }
        var summary = await c.PostAsync($"/api/v1/practice/sessions/{sid}/complete", null).Ok();
        Assert.Equal(0, summary.GetProperty("accuracy").GetDouble());

        var analytics = await c.GetAsync("/api/v1/student/analytics").Ok();
        Assert.Equal(topicId, analytics.GetProperty("weakTopics")[0].Str("topicId"));
        Assert.Equal(3, analytics.GetProperty("practiceQuestionsAnswered").GetInt32());

        var plan = await c.PostAsync("/api/v1/student/study-plan/regenerate", null).Ok();
        var practiceTask = plan.GetProperty("tasks").EnumerateArray().First(t => t.Str("type") == "Practice");
        Assert.Equal(topicId, practiceTask.Str("entityId"));
        Assert.Contains("Accuracy 0%", practiceTask.Str("reason"));
        var done = await c.PostAsync($"/api/v1/student/study-plan/tasks/{practiceTask.Str("id")}/complete", null).Ok();
        Assert.Equal("Completed", done.Str("status"));

        var incorrect = await c.PostAsJsonAsync("/api/v1/practice/sessions", new { mode = "incorrect", count = 10 }).Ok();
        Assert.Equal(3, incorrect.GetProperty("questions").GetArrayLength());
    }

    [Fact]
    public async Task New_student_dashboard_shows_no_fabricated_numbers()
    {
        var c = await api.StudentAsync();
        var d = await c.GetAsync("/api/v1/student/dashboard").Ok();
        Assert.Equal(0, d.GetProperty("streakDays").GetInt32());
        Assert.Equal(0, d.GetProperty("questionsAnsweredThisWeek").GetInt32());
        Assert.Empty(d.GetProperty("recentResults").EnumerateArray());
        Assert.Empty(d.GetProperty("weakTopics").EnumerateArray());
        var a = await c.GetAsync("/api/v1/student/analytics").Ok();
        Assert.Equal(JsonValueKind.Null, a.GetProperty("averagePercentage").ValueKind);
    }

    [Fact]
    public async Task Hindi_content_is_served_with_english_fallback()
    {
        var c = api.CreateClient();
        c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("hi-IN");
        var courses = await c.GetAsync("/api/v1/courses").Ok();
        Assert.Contains(courses.GetProperty("items").EnumerateArray(), x => x.Str("title").Contains("नीट"));
        var search = await api.CreateClient().GetAsync("/api/v1/search?q=friction").Ok();
        Assert.Contains(search.EnumerateArray(), h => h.Str("kind") == "topic");
    }

    [Fact]
    public async Task Bookmarks_round_trip()
    {
        var c = await api.StudentAsync();
        var qid = await api.WithDb(db => db.Questions.Select(q => q.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/v1/learning/bookmarks", new { entityType = "Question", entityId = qid })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/v1/learning/bookmarks", new { entityType = "Question", entityId = qid, note = "revise" })).StatusCode);
        var list = await c.GetAsync("/api/v1/learning/bookmarks").Ok();
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("revise", list[0].Str("note"));
    }
}

[Collection("api")]
public class AdminTests(ApiFactory api)
{
    [Fact]
    public async Task Admin_builds_course_content_and_test_end_to_end()
    {
        var a = await api.AdminAsync();
        var examId = (await a.GetAsync("/api/v1/admin/exams").Ok())[0].Str("id");
        var slug = "jee-chem-" + Guid.NewGuid().ToString("N")[..6];
        var course = (await a.PostAsJsonAsync("/api/v1/admin/courses", new { examId, slug, titleEn = "JEE Chemistry", isFree = true, priceInPaise = 0 }).Ok()).Str("id");
        var subject = (await a.PostAsJsonAsync("/api/v1/admin/subjects", new { parentId = course, titleEn = "Chemistry" }).Ok()).Str("id");
        var chapter = (await a.PostAsJsonAsync("/api/v1/admin/chapters", new { parentId = subject, titleEn = "Mole Concept" }).Ok()).Str("id");
        var topic = (await a.PostAsJsonAsync("/api/v1/admin/topics", new { parentId = chapter, titleEn = "Avogadro number", estimatedMinutes = 30 }).Ok()).Str("id");
        var badVideo = await a.PostAsJsonAsync("/api/v1/admin/resources", new { topicId = topic, type = "Video", titleEn = "No id", language = "en" });
        Assert.Equal(HttpStatusCode.BadRequest, badVideo.StatusCode);
        var video = (await a.PostAsJsonAsync("/api/v1/admin/resources", new { topicId = topic, type = "Video", titleEn = "Intro", language = "en", videoProviderId = "vid-1", durationSeconds = 300 }).Ok()).Str("id");

        var q = (await a.PostAsJsonAsync("/api/v1/admin/questions", new
        {
            topicId = topic, type = "Integer", difficulty = "Easy", stemEn = "How many atoms in one molecule of H2O?", numericAnswer = 3, defaultMarks = 4, defaultNegativeMarks = 0,
        }).Ok()).Str("id");
        var test = (await a.PostAsJsonAsync("/api/v1/admin/tests", new { courseId = course, type = "Topic", titleEn = "Mole quiz", durationMinutes = 5, isFree = true, questions = new[] { new { questionId = q } } }).Ok()).Str("id");

        // Cannot publish a test with draft questions.
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync($"/api/v1/admin/tests/{test}/status", new { status = "Published" })).StatusCode);
        await a.PostAsJsonAsync($"/api/v1/admin/questions/{q}/status", new { status = "Published" }).Ok();
        await a.PostAsJsonAsync($"/api/v1/admin/tests/{test}/status", new { status = "Published" }).Ok();
        await a.PostAsJsonAsync($"/api/v1/admin/resources/{video}/status", new { status = "Published" }).Ok();

        // Draft course is invisible to students until published.
        var s = await api.StudentAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await s.GetAsync($"/api/v1/courses/{course}/syllabus")).StatusCode);
        await a.PostAsJsonAsync($"/api/v1/admin/courses/{course}/status", new { status = "Published" }).Ok();
        await s.GetAsync($"/api/v1/courses/{course}/syllabus").Ok();

        var attempt = await s.PostAsync($"/api/v1/tests/{test}/attempts", null).Ok();
        var tq = attempt.GetProperty("questions")[0].Str("testQuestionId");
        await s.PutAsJsonAsync($"/api/v1/attempts/{attempt.Str("attemptId")}/answers", new { testQuestionId = tq, response = new { numericValue = 3 }, markedForReview = false, timeSpentSeconds = 4 }).Ok();
        var result = await s.PostAsync($"/api/v1/attempts/{attempt.Str("attemptId")}/submit", null).Ok();
        Assert.Equal(4m, result.Dec("score"));

        var logs = await a.GetAsync("/api/v1/admin/audit-logs?entityType=Test").Ok();
        Assert.Contains(logs.GetProperty("items").EnumerateArray(), l => l.Str("action") == "test.status");
        var dash = await a.GetAsync("/api/v1/admin/dashboard").Ok();
        Assert.True(dash.GetProperty("totalStudents").GetInt32() > 0);
    }

    [Fact]
    public async Task Csv_import_reports_row_errors_and_duplicates()
    {
        var a = await api.AdminAsync();
        var topic = await api.WithDb(db => db.Topics.Select(t => t.Id).FirstAsync());
        var tag = Guid.NewGuid().ToString("N")[..6];
        var csv = new StringBuilder();
        csv.AppendLine("external_ref,topic_id,type,difficulty,stem_en,stem_hi,option_a,option_b,option_c,option_d,correct,explanation_en,marks,negative_marks");
        csv.AppendLine($"IMP-{tag}-1,{topic},SingleCorrect,Easy,\"What is 2+2, exactly?\",,3,4,5,6,B,Basic addition,4,1");
        csv.AppendLine($"IMP-{tag}-2,{topic},Numerical,Medium,g on Earth (m/s^2)?,,,,,,9.8,,4,0");
        csv.AppendLine($"IMP-{tag}-1,{topic},SingleCorrect,Easy,Duplicate ref,,a,b,,,A,,4,1");
        csv.AppendLine($"IMP-{tag}-4,{Guid.NewGuid()},SingleCorrect,Easy,Unknown topic,,a,b,,,A,,4,1");
        csv.AppendLine($"IMP-{tag}-5,{topic},SingleCorrect,Easy,Two answers,,a,b,,,A;B,,4,1");
        csv.AppendLine($"IMP-{tag}-6,{topic},Essay,Easy,Bad type,,,,,,x,,4,1");

        using var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes(csv.ToString())), "file", "questions.csv" } };
        var res = await a.PostAsync("/api/v1/admin/questions/import", form).Ok();
        Assert.Equal(6, res.GetProperty("totalRows").GetInt32());
        Assert.Equal(2, res.GetProperty("imported").GetInt32());
        var errors = res.GetProperty("errors").EnumerateArray().Select(e => (Row: e.GetProperty("row").GetInt32(), Msg: e.Str("message"))).ToList();
        Assert.Equal([4, 5, 6, 7], errors.Select(e => e.Row));
        Assert.Contains("Duplicate", errors[0].Msg);

        var imported = await api.WithDb(db => db.Questions.Include(q => q.Options).SingleAsync(q => q.ExternalRef == $"IMP-{tag}-1"));
        Assert.Equal(PublishStatus.Draft, imported.Status);
        Assert.Equal("What is 2+2, exactly?", imported.StemEn);
        Assert.Equal("4", imported.Options.Single(o => o.IsCorrect).TextEn);
    }

    [Fact]
    public async Task Teacher_can_edit_but_not_publish()
    {
        var teacher = await api.StudentAsync($"teacher{Guid.NewGuid():N}@test.local");
        var userId = (await teacher.GetAsync("/api/v1/student/profile").Ok()).Id("userId");
        var admin = await api.AdminAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/v1/admin/users/{userId}/roles", new { role = "Teacher" })).StatusCode);

        // Roles live in the JWT, so log in again.
        var t = api.CreateClient();
        var login = await t.PostAsJsonAsync("/api/v1/auth/login", new { email = (await api.WithDb(db => db.Users.FirstAsync(u => u.Id == userId))).Email, password = "Passw0rd!x" }).Ok();
        t.DefaultRequestHeaders.Authorization = new("Bearer", login.Str("accessToken"));
        var qid = await api.WithDb(db => db.Questions.Select(q => q.Id).FirstAsync());
        await t.GetAsync("/api/v1/admin/questions").Ok();
        Assert.Equal(HttpStatusCode.Forbidden, (await t.PostAsJsonAsync($"/api/v1/admin/questions/{qid}/status", new { status = "Archived" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await t.GetAsync("/api/v1/admin/audit-logs")).StatusCode);
    }
}

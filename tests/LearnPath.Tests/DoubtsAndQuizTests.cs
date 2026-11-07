using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

[Collection("api")]
public class DoubtTests(ApiFactory api)
{
    private Task<Guid> TopicAsync(string title) => api.WithDb(db => db.Topics.Where(t => t.TitleEn == title).Select(t => t.Id).FirstAsync());

    [Fact]
    public async Task Student_asks_teacher_answers_student_follows_up_and_resolves()
    {
        var s = await api.StudentAsync();
        var topic = await TopicAsync("Dimensional Analysis"); // free course
        var asked = await s.PostAsJsonAsync("/api/v1/doubts", new { text = "Why is torque not measured in joules?", topicId = topic }).Ok();
        var id = asked.Str("id");
        Assert.Equal("Open", asked.Str("status"));
        Assert.Equal("Dimensional Analysis", asked.GetProperty("context").Str("topic"));
        Assert.Equal("NEET Physics Starter (Sample, Free)", asked.GetProperty("context").Str("course"));

        var a = await api.AdminAsync();
        var open = await a.GetAsync("/api/v1/admin/doubts?status=Open").Ok();
        var row = open.GetProperty("items").EnumerateArray().Single(x => x.Str("id") == id);
        Assert.Equal("Test Student", row.Str("studentName"));
        Assert.True((await a.GetAsync("/api/v1/admin/dashboard").Ok()).GetProperty("openDoubts").GetInt32() >= 1);

        var replied = await a.PostAsJsonAsync($"/api/v1/admin/doubts/{id}/reply", new { text = "Torque is a moment, not energy; we write N·m." }).Ok();
        Assert.Equal("Answered", replied.Str("status"));

        var mine = await s.GetAsync($"/api/v1/doubts/{id}").Ok();
        Assert.Equal("Answered", mine.Str("status"));
        var msg = Assert.Single(mine.GetProperty("messages").EnumerateArray());
        Assert.True(msg.GetProperty("fromStaff").GetBoolean());
        var note = (await s.GetAsync("/api/v1/notifications").Ok()).GetProperty("items").EnumerateArray().First();
        Assert.Equal("doubt", note.Str("type"));
        Assert.Equal($"/doubts/{id}", note.Str("link"));

        var followed = await s.PostAsJsonAsync($"/api/v1/doubts/{id}/messages", new { text = "So the units match but the meaning differs?" }).Ok();
        Assert.Equal("Open", followed.Str("status"));
        Assert.Equal(2, followed.GetProperty("messages").GetArrayLength());

        var resolved = await s.PostAsync($"/api/v1/doubts/{id}/resolve", null).Ok();
        Assert.Equal("Resolved", resolved.Str("status"));
        var list = await s.GetAsync("/api/v1/doubts").Ok();
        var item = list.GetProperty("items").EnumerateArray().Single(x => x.Str("id") == id);
        Assert.Equal(1, item.GetProperty("replies").GetInt32());
    }

    [Fact]
    public async Task Doubts_about_paid_topics_need_the_course_and_stay_private()
    {
        var s = await api.StudentAsync();
        var friction = await TopicAsync("Friction");
        var res = await s.PostAsJsonAsync("/api/v1/doubts", new { text = "How do I find limiting friction?", topicId = friction });
        Assert.Equal(HttpStatusCode.PaymentRequired, res.StatusCode);

        // A question links the doubt to its topic (and course) by itself.
        var dimQuestion = await api.WithDb(db => db.Questions.Where(q => q.ExternalRef == "SAMPLE-DIM-1").Select(q => q.Id).FirstAsync());
        var asked = await s.PostAsJsonAsync("/api/v1/doubts", new { text = "Why is the answer [M L T^-2]?", questionId = dimQuestion }).Ok();
        Assert.Equal("Dimensional Analysis", asked.GetProperty("context").Str("topic"));
        Assert.Contains("dimensional formula of force", asked.GetProperty("context").Str("question"));

        // General doubts need no topic.
        await s.PostAsJsonAsync("/api/v1/doubts", new { text = "How should I plan revision for NEET?" }).Ok();

        var other = await api.StudentAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/doubts/{asked.Str("id")}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/doubts/{asked.Str("id")}/messages", new { text = "hi" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/v1/admin/doubts")).StatusCode);
        Assert.Equal(0, (await other.GetAsync("/api/v1/doubts").Ok()).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Students_can_ask_a_limited_number_of_doubts_a_day()
    {
        var s = await api.StudentAsync();
        for (var i = 0; i < 10; i++) await s.PostAsJsonAsync("/api/v1/doubts", new { text = $"Question number {i}" }).Ok();
        var res = await s.PostAsJsonAsync("/api/v1/doubts", new { text = "One more question" });
        Assert.Equal((HttpStatusCode)429, res.StatusCode);
        Assert.Equal("doubt_limit", (await res.ReadJson()).Str("code"));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.PostAsJsonAsync("/api/v1/doubts", new { text = "Hi" })).StatusCode); // too short
    }
}

[Collection("api")]
public class TopicQuizTests(ApiFactory api)
{
    private Task<Guid> TopicAsync(string title) => api.WithDb(db => db.Topics.Where(t => t.TitleEn == title).Select(t => t.Id).FirstAsync());

    private Task<Dictionary<Guid, List<Guid>>> CorrectOptionsAsync(Guid topicId) => api.WithDb(db => db.Questions.Where(q => q.TopicId == topicId)
        .Include(q => q.Options).ToDictionaryAsync(q => q.Id, q => q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToList()));

    [Fact]
    public async Task Quiz_is_scored_on_submit_and_counts_on_the_topic()
    {
        var s = await api.StudentAsync();
        var topic = await TopicAsync("Dimensional Analysis");
        var detail = await s.GetAsync($"/api/v1/topics/{topic}").Ok();
        var summary = detail.GetProperty("quiz");
        Assert.Equal(3, summary.GetProperty("questionCount").GetInt32()); // 5 by default, capped by the 3 questions available
        Assert.Equal(60, summary.GetProperty("passPercent").GetInt32());
        Assert.Equal(0, summary.GetProperty("attempts").GetInt32());

        var quiz = await s.PostAsync($"/api/v1/topics/{topic}/quiz", null).Ok();
        var sessionId = quiz.Str("sessionId");
        var questions = quiz.GetProperty("questions").EnumerateArray().ToList();
        Assert.Equal(3, questions.Count);
        Assert.All(questions, q => Assert.Equal(JsonValueKind.Null, q.GetProperty("hint").ValueKind));

        // Answers stay hidden until the whole quiz is submitted.
        var first = questions[0].Id("questionId");
        var peek = await s.PostAsJsonAsync($"/api/v1/practice/sessions/{sessionId}/answers",
            new { questionId = first, response = new { selectedOptionIds = new[] { questions[0].GetProperty("options")[0].Id("id") } }, timeSpentSeconds = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, peek.StatusCode);

        var correct = await CorrectOptionsAsync(topic);
        var q0 = questions[0].Id("questionId");
        var q1 = questions[1].Id("questionId");
        var answers = new object[]
        {
            new { questionId = q0, response = new { selectedOptionIds = correct[q0] }, timeSpentSeconds = 20 },
            new { questionId = q1, response = new { selectedOptionIds = correct[q1] }, timeSpentSeconds = 25 },
            // the third is left unanswered
        };
        var result = await s.PostAsJsonAsync($"/api/v1/practice/quizzes/{sessionId}/submit", new { answers }).Ok();
        Assert.Equal(2, result.GetProperty("correct").GetInt32());
        Assert.Equal(1, result.GetProperty("skipped").GetInt32());
        Assert.Equal(67, result.GetProperty("scorePercent").GetInt32());
        Assert.True(result.GetProperty("passed").GetBoolean());
        var items = result.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal("Skipped", items[2].Str("outcome"));
        Assert.NotEmpty(items[2].GetProperty("correctOptionIds").EnumerateArray()); // the answer is shown for skipped ones too

        // Submitting again returns the same result and stores nothing twice.
        var again = await s.PostAsJsonAsync($"/api/v1/practice/quizzes/{sessionId}/submit", new { answers = Array.Empty<object>() }).Ok();
        Assert.Equal(67, again.GetProperty("scorePercent").GetInt32());
        Assert.Equal(2, again.GetProperty("correct").GetInt32());
        Assert.Equal(2, await api.WithDb(db => db.PracticeAnswers.CountAsync(a => a.SessionId == Guid.Parse(sessionId))));

        summary = (await s.GetAsync($"/api/v1/topics/{topic}").Ok()).GetProperty("quiz");
        Assert.Equal(1, summary.GetProperty("attempts").GetInt32());
        Assert.Equal(67, summary.GetProperty("bestPercent").GetInt32());
        Assert.True(summary.GetProperty("passed").GetBoolean());

        // Quiz answers count towards the student's accuracy like practice answers.
        var analytics = await s.GetAsync("/api/v1/student/analytics").Ok();
        Assert.Equal(2, analytics.GetProperty("practiceQuestionsAnswered").GetInt32());
    }

    [Fact]
    public async Task Paid_topic_quiz_needs_the_course_and_follows_the_admin_settings()
    {
        var s = await api.StudentAsync();
        var friction = await TopicAsync("Friction");
        Assert.Equal(HttpStatusCode.PaymentRequired, (await s.PostAsync($"/api/v1/topics/{friction}/quiz", null)).StatusCode);

        var userId = Guid.Parse((await s.GetAsync("/api/v1/student/profile").Ok()).Str("userId"));
        var paidCourse = await api.CourseIdAsync(free: false);
        await api.WithDb(async db =>
        {
            db.Enrollments.Add(new Enrollment { UserId = userId, CourseId = paidCourse, Source = EnrollmentSource.Admin, StartsAt = api.Clock.GetUtcNow().AddMinutes(-1) });
            return await db.SaveChangesAsync();
        });

        var a = await api.AdminAsync();
        var tree = await a.GetAsync($"/api/v1/admin/courses/{paidCourse}/tree").Ok();
        var adminTopic = tree.GetProperty("subjects").EnumerateArray().SelectMany(x => x.GetProperty("chapters").EnumerateArray())
            .SelectMany(x => x.GetProperty("topics").EnumerateArray()).Single(t => t.Str("id") == friction.ToString());
        Assert.Equal(5, adminTopic.GetProperty("quizQuestionCount").GetInt32());
        Assert.Equal(3, adminTopic.GetProperty("publishedQuestionCount").GetInt32());
        var chapterId = adminTopic.Str("chapterId");

        try
        {
            await a.PutAsJsonAsync($"/api/v1/admin/topics/{friction}", new { parentId = chapterId, titleEn = "Friction", sortOrder = 1, quizQuestionCount = 2, quizPassPercent = 50 }).Ok();
            var first = await s.PostAsync($"/api/v1/topics/{friction}/quiz", null).Ok();
            Assert.Equal(2, first.GetProperty("questions").GetArrayLength());
            Assert.Equal(50, first.GetProperty("passPercent").GetInt32());
            var sessionId = first.Str("sessionId");
            var result = await s.PostAsJsonAsync($"/api/v1/practice/quizzes/{sessionId}/submit", new { answers = Array.Empty<object>() }).Ok();
            Assert.Equal(0, result.GetProperty("scorePercent").GetInt32());
            Assert.False(result.GetProperty("passed").GetBoolean());

            // The next quiz starts with the question it hasn't asked yet.
            var seen = first.GetProperty("questions").EnumerateArray().Select(q => q.Str("questionId")).ToHashSet();
            var second = await s.PostAsync($"/api/v1/topics/{friction}/quiz", null).Ok();
            Assert.DoesNotContain(second.GetProperty("questions")[0].Str("questionId"), seen);

            // Leaving the other settings out keeps the quiz as it is; 0 turns it off.
            await a.PutAsJsonAsync($"/api/v1/admin/topics/{friction}", new { parentId = chapterId, titleEn = "Friction", sortOrder = 1 }).Ok();
            Assert.Equal(2, await api.WithDb(db => db.Topics.Where(t => t.Id == friction).Select(t => t.QuizQuestionCount).FirstAsync()));
            await a.PutAsJsonAsync($"/api/v1/admin/topics/{friction}", new { parentId = chapterId, titleEn = "Friction", sortOrder = 1, quizQuestionCount = 0 }).Ok();
            Assert.Equal(JsonValueKind.Null, (await s.GetAsync($"/api/v1/topics/{friction}").Ok()).GetProperty("quiz").ValueKind);
            var none = await s.PostAsync($"/api/v1/topics/{friction}/quiz", null);
            Assert.Equal(HttpStatusCode.Conflict, none.StatusCode);
        }
        finally
        {
            await a.PutAsJsonAsync($"/api/v1/admin/topics/{friction}", new
            {
                parentId = chapterId, titleEn = "Friction", titleHi = "घर्षण", sortOrder = 1, estimatedMinutes = 60,
                objectivesEn = "Distinguish static and kinetic friction; solve block-on-surface problems.",
                quizQuestionCount = Topic.DefaultQuizQuestions, quizPassPercent = Topic.DefaultQuizPassPercent,
            }).Ok();
        }
    }

    [Fact]
    public async Task Finishing_the_last_lesson_offers_the_topic_quiz()
    {
        var s = await api.StudentAsync();
        var topic = await TopicAsync("Dimensional Analysis");
        var lessons = await api.WithDb(db => db.LearningResources.Where(r => r.TopicId == topic).OrderBy(r => r.SortOrder).Select(r => new { r.Id, r.DurationSeconds }).ToListAsync());
        var video = await s.PostAsJsonAsync("/api/v1/learning/progress", new { resourceId = lessons[0].Id, positionSeconds = lessons[0].DurationSeconds }).Ok();
        Assert.True(video.GetProperty("completed").GetBoolean());
        Assert.False(video.GetProperty("topicCompleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, video.GetProperty("quiz").ValueKind);

        var notes = await s.PostAsJsonAsync("/api/v1/learning/progress", new { resourceId = lessons[1].Id, positionSeconds = 0, markCompleted = true }).Ok();
        Assert.True(notes.GetProperty("topicCompleted").GetBoolean());
        Assert.Equal(3, notes.GetProperty("quiz").GetProperty("questionCount").GetInt32());
    }
}

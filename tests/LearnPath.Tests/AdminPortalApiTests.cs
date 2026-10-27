using System.Net;
using System.Net.Http.Json;
using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

[Collection("api")]
public class AdminPortalApiTests(ApiFactory api)
{
    private async Task<(HttpClient A, string Course, string Subject, string Chapter, string Topic)> NewCourseAsync()
    {
        var a = await api.AdminAsync();
        var examId = (await a.GetAsync("/api/v1/admin/exams").Ok())[0].Str("id");
        var course = (await a.PostAsJsonAsync("/api/v1/admin/courses", new { examId, slug = "c-" + Guid.NewGuid().ToString("N")[..8], titleEn = "Course", isFree = true, priceInPaise = 0 }).Ok()).Str("id");
        var subject = (await a.PostAsJsonAsync("/api/v1/admin/subjects", new { parentId = course, titleEn = "Physics" }).Ok()).Str("id");
        var chapter = (await a.PostAsJsonAsync("/api/v1/admin/chapters", new { parentId = subject, titleEn = "Optics" }).Ok()).Str("id");
        var topic = (await a.PostAsJsonAsync("/api/v1/admin/topics", new { parentId = chapter, titleEn = "Reflection" }).Ok()).Str("id");
        return (a, course, subject, chapter, topic);
    }

    [Fact]
    public async Task Course_tree_is_typed_and_counts_questions()
    {
        var (a, course, _, _, topic) = await NewCourseAsync();
        await a.PostAsJsonAsync("/api/v1/admin/questions", new { topicId = topic, type = "Integer", difficulty = "Easy", stemEn = "2+2?", numericAnswer = 4, defaultMarks = 4, defaultNegativeMarks = 0 }).Ok();
        var tree = await a.GetAsync($"/api/v1/admin/courses/{course}/tree").Ok();
        Assert.Equal("Draft", tree.GetProperty("course").Str("status"));
        var t = tree.GetProperty("subjects")[0].GetProperty("chapters")[0].GetProperty("topics")[0];
        Assert.Equal(1, t.GetProperty("questionCount").GetInt32());

        var list = await a.GetAsync($"/api/v1/admin/questions?courseId={course}").Ok();
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal("Reflection", list.GetProperty("items")[0].Str("topic"));
        var q = await a.GetAsync($"/api/v1/admin/questions/{list.GetProperty("items")[0].Str("id")}").Ok();
        Assert.Equal(course, q.Str("courseId"));
        Assert.Equal(0, q.GetProperty("usedInTests").GetInt32());
    }

    [Fact]
    public async Task Deletes_only_remove_empty_or_unused_draft_content()
    {
        var (a, _, subject, chapter, topic) = await NewCourseAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await a.DeleteAsync($"/api/v1/admin/subjects/{subject}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await a.DeleteAsync($"/api/v1/admin/chapters/{chapter}")).StatusCode);

        var res = (await a.PostAsJsonAsync("/api/v1/admin/resources", new { topicId = topic, type = "Document", titleEn = "Notes", language = "en", documentStorageKey = "x.pdf" }).Ok()).Str("id");
        await a.PostAsJsonAsync($"/api/v1/admin/resources/{res}/status", new { status = "Published" }).Ok();
        var published = await a.DeleteAsync($"/api/v1/admin/resources/{res}");
        Assert.Equal("in_use", (await published.ReadJson()).Str("code"));
        await a.PostAsJsonAsync($"/api/v1/admin/resources/{res}/status", new { status = "Draft" }).Ok();
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/v1/admin/resources/{res}")).StatusCode);

        var q = (await a.PostAsJsonAsync("/api/v1/admin/questions", new { topicId = topic, type = "Integer", stemEn = "1+1?", numericAnswer = 2, defaultMarks = 4, defaultNegativeMarks = 0 }).Ok()).Str("id");
        Assert.Equal("has_questions", (await (await a.DeleteAsync($"/api/v1/admin/topics/{topic}")).ReadJson()).Str("code"));
        await api.WithDb(db => db.Questions.Where(x => x.Id == Guid.Parse(q)).ExecuteDeleteAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/v1/admin/topics/{topic}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/v1/admin/chapters/{chapter}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/v1/admin/subjects/{subject}")).StatusCode);
    }

    [Fact]
    public async Task Test_detail_round_trips_and_duplicate_creates_editable_draft()
    {
        var (a, course, _, _, topic) = await NewCourseAsync();
        var q1 = (await a.PostAsJsonAsync("/api/v1/admin/questions", new { topicId = topic, type = "Integer", stemEn = "Q1", numericAnswer = 1, defaultMarks = 4, defaultNegativeMarks = 1 }).Ok()).Str("id");
        var q2 = (await a.PostAsJsonAsync("/api/v1/admin/questions", new { topicId = topic, type = "Integer", stemEn = "Q2", numericAnswer = 2, defaultMarks = 4, defaultNegativeMarks = 1 }).Ok()).Str("id");
        var test = (await a.PostAsJsonAsync("/api/v1/admin/tests", new
        {
            courseId = course, type = "Chapter", titleEn = "Optics test", durationMinutes = 15, isFree = true,
            sections = new[] { new { titleEn = "A" }, new { titleEn = "B" } },
            questions = new object[] { new { questionId = q1, sectionIndex = 0 }, new { questionId = q2, sectionIndex = 1, marks = 3m, negativeMarks = 0m } },
        }).Ok()).Str("id");

        var detail = await a.GetAsync($"/api/v1/admin/tests/{test}").Ok();
        Assert.Equal(2, detail.GetProperty("sections").GetArrayLength());
        var qs = detail.GetProperty("questions");
        Assert.Equal(q1, qs[0].Str("questionId"));
        Assert.Equal(1, qs[1].GetProperty("sectionIndex").GetInt32());
        Assert.Equal(3m, qs[1].Dec("marks"));
        Assert.Equal("Q2", qs[1].Str("stemEn"));

        var list = await a.GetAsync($"/api/v1/admin/tests?courseId={course}").Ok();
        Assert.Equal(7m, list[0].Dec("maxMarks"));
        Assert.Equal("Course", list[0].Str("courseTitle"));

        var copy = (await a.PostAsync($"/api/v1/admin/tests/{test}/duplicate", null).Ok()).Str("id");
        var copyDetail = await a.GetAsync($"/api/v1/admin/tests/{copy}").Ok();
        Assert.Equal("Optics test (copy)", copyDetail.Str("titleEn"));
        Assert.Equal("Draft", copyDetail.Str("status"));
        Assert.Equal(2, copyDetail.GetProperty("questions").GetArrayLength());
        Assert.Equal(1, copyDetail.GetProperty("questions")[1].GetProperty("sectionIndex").GetInt32());
    }

    [Fact]
    public async Task Super_admin_cannot_lock_themselves_out()
    {
        var a = await api.AdminAsync();
        var me = (await a.GetAsync("/api/v1/student/profile").Ok()).Str("userId");
        Assert.Equal("self_change", (await (await a.DeleteAsync($"/api/v1/admin/users/{me}/roles/SuperAdmin")).ReadJson()).Str("code"));
        Assert.Equal("self_change", (await (await a.PostAsJsonAsync($"/api/v1/admin/users/{me}/status", new { status = "Suspended" })).ReadJson()).Str("code"));
    }

    [Fact]
    public async Task Support_views_include_student_identity()
    {
        var s = await api.StudentAsync();
        await s.PostAsJsonAsync("/api/v1/support/tickets", new { category = "payment", subject = "Charged twice", message = "Please check" }).Ok();
        var qid = await api.WithDb(db => db.Questions.Select(q => q.Id).FirstAsync());
        await s.PostAsJsonAsync($"/api/v1/questions/{qid}/report", new { reason = "typo", details = "spelling" });

        var a = await api.AdminAsync();
        var tickets = await a.GetAsync("/api/v1/admin/tickets?status=Open").Ok();
        var t = tickets.EnumerateArray().First(x => x.Str("subject") == "Charged twice");
        Assert.Equal("Test Student", t.Str("userName"));
        Assert.Equal("High", t.Str("priority"));

        var reports = await a.GetAsync("/api/v1/admin/question-reports").Ok();
        Assert.Contains(reports.EnumerateArray(), r => r.Str("questionId") == qid.ToString() && r.Str("questionStem").Length > 0);

        var users = await a.GetAsync("/api/v1/admin/users?role=SuperAdmin").Ok();
        Assert.All(users.GetProperty("items").EnumerateArray(), u => Assert.Contains("SuperAdmin", u.GetProperty("roles").EnumerateArray().Select(r => r.GetString())));
        var logs = await a.GetAsync("/api/v1/admin/audit-logs?action=course").Ok();
        Assert.All(logs.GetProperty("items").EnumerateArray(), l => Assert.StartsWith("course", l.Str("action")));
    }
}

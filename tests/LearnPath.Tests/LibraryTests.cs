using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

/// <summary>Browse by NCERT book: the subject grid, chapter lists and a chapter's lessons from every course that teaches it.</summary>
[Collection("api")]
public class LibraryTests(ApiFactory api)
{
    private Task<Guid> NcertAsync(string cls, string subject, int number) =>
        api.WithDb(db => db.NcertChapters.Where(n => n.ClassLevel == cls && n.Subject == subject && n.Number == number).Select(n => n.Id).FirstAsync());

    [Fact]
    public async Task The_syllabus_has_79_chapters_and_sample_chapters_are_linked_by_title()
    {
        Assert.Equal(79, await api.WithDb(db => db.NcertChapters.CountAsync()));
        Assert.Equal(new[] { 14, 9, 19, 14, 10, 13 }, await api.WithDb(async db => (await db.NcertChapters.ToListAsync())
            .GroupBy(n => (n.ClassLevel, n.Subject)).OrderBy(g => g.Key.ClassLevel).ThenBy(g => Array.IndexOf(NcertSubjects.All, g.Key.Subject)).Select(g => g.Count()).ToArray()));
        // "Units and Measurements" (sample) ~ "Units and Measurement" (NCERT).
        var units = await NcertAsync("11", NcertSubjects.Physics, 1);
        Assert.True(await api.WithDb(db => db.Chapters.AnyAsync(c => c.TitleEn == "Units and Measurements" && c.NcertChapterId == units)));
        Assert.Equal("law of motion", NcertSyllabus.Normalise("Laws of Motion"));
        Assert.Equal(NcertSyllabus.Normalise("Organic Chemistry – Some Basic Principles & Techniques"), NcertSyllabus.Normalise("Organic Chemistry: Some Basic Principles and Techniques"));
        Assert.Equal(NcertSubjects.Biology, NcertSyllabus.SubjectOf("Zoology"));
    }

    [Fact]
    public async Task The_grid_and_chapter_lists_show_what_is_ready_locked_and_done()
    {
        var anon = await api.CreateClient().GetAsync("/api/v1/library").Ok();
        var tiles = anon.GetProperty("tiles").EnumerateArray().ToList();
        Assert.Equal(6, tiles.Count);
        var physics11 = tiles.Single(t => t.Str("classLevel") == "11" && t.Str("subject") == "Physics");
        Assert.Equal(14, physics11.GetProperty("chapters").GetInt32());
        Assert.True(physics11.GetProperty("readyChapters").GetInt32() >= 2);
        Assert.Equal(JsonValueKind.Null, anon.GetProperty("resume").ValueKind);

        var s = await api.StudentAsync();
        var book = await s.GetAsync("/api/v1/library/11/physics").Ok();
        var chapters = book.GetProperty("chapters").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, 14), chapters.Select(c => c.GetProperty("number").GetInt32()));
        var units = chapters[0];
        Assert.True(units.GetProperty("ready").GetBoolean());
        Assert.False(units.GetProperty("locked").GetBoolean()); // free course
        var lawsOfMotion = chapters[3];
        Assert.True(lawsOfMotion.GetProperty("ready").GetBoolean());
        Assert.True(lawsOfMotion.GetProperty("locked").GetBoolean()); // paid course, not bought
        Assert.False(chapters[6].GetProperty("ready").GetBoolean()); // Gravitation: no lessons yet
        Assert.Equal(HttpStatusCode.NotFound, (await s.GetAsync("/api/v1/library/11/history")).StatusCode);

        // Finishing a lesson shows as progress, and "continue" points at the chapter.
        var dimNotes = await api.WithDb(db => db.LearningResources.Where(r => r.Type == ResourceType.Document && r.Topic!.TitleEn == "Dimensional Analysis").Select(r => r.Id).FirstAsync());
        await s.PostAsJsonAsync("/api/v1/learning/progress", new { resourceId = dimNotes, positionSeconds = 0, markCompleted = true }).Ok();
        var grid = await s.GetAsync("/api/v1/library").Ok();
        Assert.True(grid.GetProperty("tiles").EnumerateArray().Single(t => t.Str("classLevel") == "11" && t.Str("subject") == "Physics").GetProperty("progressPercent").GetInt32() > 0);
        Assert.Equal(units.Str("id"), grid.GetProperty("resume").Str("chapterId"));
        Assert.True((await s.GetAsync("/api/v1/library/11/physics").Ok()).GetProperty("chapters")[0].GetProperty("progressPercent").GetInt32() > 0);
    }

    [Fact]
    public async Task A_chapter_gathers_lessons_from_its_courses_with_access_and_practice()
    {
        var s = await api.StudentAsync();
        var units = await NcertAsync("11", NcertSubjects.Physics, 1);
        var open = await s.GetAsync($"/api/v1/library/chapters/{units}").Ok();
        Assert.Equal("Units and Measurement", open.Str("title"));
        var topic = open.GetProperty("topics").EnumerateArray().Single(t => t.Str("title") == "Dimensional Analysis");
        Assert.True(topic.GetProperty("hasAccess").GetBoolean());
        Assert.All(topic.GetProperty("resources").EnumerateArray(), r => Assert.False(r.GetProperty("locked").GetBoolean()));
        Assert.True(open.GetProperty("canPractise").GetBoolean());
        Assert.Equal(3, open.GetProperty("practiceQuestionCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, open.GetProperty("previousChapterId").ValueKind);
        Assert.Equal(JsonValueKind.Null, open.GetProperty("buyCourseId").ValueKind);
        var practice = await s.PostAsJsonAsync("/api/v1/practice/sessions", new { mode = "ncert", ncertChapterId = units, count = 10 }).Ok();
        Assert.Equal(3, practice.GetProperty("questions").GetArrayLength());

        // A paid chapter: lessons listed but locked (except free previews), with the course to buy.
        var laws = await NcertAsync("11", NcertSubjects.Physics, 4);
        var locked = await s.GetAsync($"/api/v1/library/chapters/{laws}").Ok();
        var lockedTopics = locked.GetProperty("topics").EnumerateArray().ToList();
        Assert.NotEmpty(lockedTopics);
        Assert.All(lockedTopics, t => Assert.False(t.GetProperty("hasAccess").GetBoolean()));
        var resources = lockedTopics.SelectMany(t => t.GetProperty("resources").EnumerateArray()).ToList();
        Assert.Contains(resources, r => r.GetProperty("isFreePreview").GetBoolean() && !r.GetProperty("locked").GetBoolean());
        Assert.False(locked.GetProperty("canPractise").GetBoolean());
        Assert.Equal((await api.CourseIdAsync(free: false)).ToString(), locked.Str("buyCourseId"));
        Assert.Equal(HttpStatusCode.PaymentRequired, (await s.PostAsJsonAsync("/api/v1/practice/sessions", new { mode = "ncert", ncertChapterId = laws, count = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.GetAsync($"/api/v1/library/chapters/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Admins_link_chapters_automatically_by_title_and_can_change_it()
    {
        var a = await api.AdminAsync();
        Assert.Equal(79, (await a.GetAsync("/api/v1/admin/ncert-chapters").Ok()).GetArrayLength());
        var exam = await api.WithDb(db => db.Exams.Select(e => e.Id).FirstAsync());
        var slug = "lib-" + Guid.NewGuid().ToString("N")[..8];
        var course = (await a.PostAsJsonAsync("/api/v1/admin/courses", new { examId = exam, slug, titleEn = "Library test", isFree = true, priceInPaise = 0 }).Ok()).Str("id");
        var subject = (await a.PostAsJsonAsync("/api/v1/admin/subjects", new { parentId = course, titleEn = "Physics" }).Ok()).Str("id");
        var chapter = (await a.PostAsJsonAsync("/api/v1/admin/chapters", new { parentId = subject, titleEn = "Gravitation" }).Ok()).Str("id");
        var gravitation = await NcertAsync("11", NcertSubjects.Physics, 7);
        Assert.Equal(gravitation, await api.WithDb(db => db.Chapters.Where(c => c.Id == Guid.Parse(chapter)).Select(c => c.NcertChapterId).FirstAsync()));

        var tree = await a.GetAsync($"/api/v1/admin/courses/{course}/tree").Ok();
        Assert.Equal(gravitation.ToString(), tree.GetProperty("subjects")[0].GetProperty("chapters")[0].Str("ncertChapterId"));

        // Leaving it out keeps the link; Guid.Empty removes it; another id moves it.
        await a.PutAsJsonAsync($"/api/v1/admin/chapters/{chapter}", new { parentId = subject, titleEn = "Gravitation (revised)" }).Ok();
        Assert.Equal(gravitation, await api.WithDb(db => db.Chapters.Where(c => c.Id == Guid.Parse(chapter)).Select(c => c.NcertChapterId).FirstAsync()));
        await a.PutAsJsonAsync($"/api/v1/admin/chapters/{chapter}", new { parentId = subject, titleEn = "Gravitation", ncertChapterId = Guid.Empty }).Ok();
        Assert.Null(await api.WithDb(db => db.Chapters.Where(c => c.Id == Guid.Parse(chapter)).Select(c => c.NcertChapterId).FirstAsync()));
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PutAsJsonAsync($"/api/v1/admin/chapters/{chapter}", new { parentId = subject, titleEn = "Gravitation", ncertChapterId = Guid.NewGuid() })).StatusCode);
    }
}

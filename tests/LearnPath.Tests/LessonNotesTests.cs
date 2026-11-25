using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

/// <summary>Lesson notes uploaded in the course editor and opened by students through short-lived signed links.</summary>
[Collection("api")]
public class LessonNotesTests(ApiFactory api)
{
    private Task<Guid> ResourceAsync(string title) => api.WithDb(db => db.LearningResources.Where(r => r.TitleEn == title).Select(r => r.Id).FirstAsync());

    private static MultipartFormDataContent File(byte[] data, string name)
    {
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new MultipartFormDataContent { { content, "file", name } };
    }

    [Fact]
    public async Task Sample_notes_open_through_a_signed_link_for_students_with_access()
    {
        var notes = await ResourceAsync("Dimensional formulae — summary notes"); // free sample course
        var s = await api.StudentAsync();
        var playback = await s.GetAsync($"/api/v1/resources/{notes}/playback").Ok();
        var url = playback.Str("url");
        Assert.StartsWith($"/api/v1/resources/{notes}/file?expires=", url);

        var file = await api.CreateClient().GetAsync(url); // no token needed
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("application/pdf", file.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Dimensional-formulae-summary-notes.pdf", file.Content.Headers.ContentDisposition!.FileName?.Trim('"'));
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(await file.Content.ReadAsByteArrayAsync())[..5]);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.CreateClient().GetAsync(url[..^4] + "0000")).StatusCode);

        // Paid course notes: locked until the course is owned.
        var friction = await ResourceAsync("Friction — formula sheet");
        Assert.Equal(HttpStatusCode.PaymentRequired, (await s.GetAsync($"/api/v1/resources/{friction}/playback")).StatusCode);
        var userId = (await s.GetAsync("/api/v1/student/profile").Ok()).Str("userId");
        var admin = await api.AdminAsync();
        await admin.PostAsJsonAsync($"/api/v1/admin/users/{userId}/enrollments", new { courseId = await api.CourseIdAsync(free: false), reason = "Test" }).Ok();
        var paid = await s.GetAsync($"/api/v1/resources/{friction}/playback").Ok();
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClient().GetAsync(paid.Str("url"))).StatusCode);
    }

    [Fact]
    public async Task Staff_upload_replace_and_remove_notes_and_cannot_publish_notes_without_a_file()
    {
        var a = await api.AdminAsync();
        var topic = await api.WithDb(db => db.Topics.Where(t => t.TitleEn == "Dimensional Analysis").Select(t => t.Id).FirstAsync());
        var title = "Upload notes " + Guid.NewGuid().ToString("N")[..6];

        // Notes can be created first and get their file next; publishing waits for the file.
        var id = (await a.PostAsJsonAsync("/api/v1/admin/resources", new { topicId = topic, type = "Document", titleEn = title, language = "en" }).Ok()).Id("id");
        var publish = await a.PostAsJsonAsync($"/api/v1/admin/resources/{id}/status", new { status = "Published" });
        Assert.Equal(HttpStatusCode.Conflict, publish.StatusCode);
        Assert.Equal("document_missing", (await publish.ReadJson()).Str("code"));

        // Only real PDFs or images, and only for notes.
        var fake = await a.PostAsync($"/api/v1/admin/resources/{id}/document", File(Encoding.UTF8.GetBytes("<html></html>"), "notes.pdf"));
        Assert.Equal("unsupported_file", (await fake.ReadJson()).Str("code"));
        var video = await ResourceAsync("Dimensions explained");
        Assert.Equal("not_a_document", (await (await a.PostAsync($"/api/v1/admin/resources/{video}/document", File(MaterialSamples.OnePagePdf("x", ["x"]), "x.pdf"))).ReadJson()).Str("code"));

        var uploaded = await a.PostAsync($"/api/v1/admin/resources/{id}/document", File(MaterialSamples.OnePagePdf("Notes", ["One", "Two"]), "Chapter notes.pdf")).Ok();
        Assert.Equal("Chapter-notes.pdf", uploaded.Str("documentFileName"));
        Assert.True(uploaded.GetProperty("documentSizeBytes").GetInt64() > 300);
        var tree = await a.GetAsync($"/api/v1/admin/courses/{await api.CourseIdAsync(free: true)}/tree").Ok();
        var inTree = tree.GetProperty("subjects").EnumerateArray().SelectMany(sub => sub.GetProperty("chapters").EnumerateArray())
            .SelectMany(ch => ch.GetProperty("topics").EnumerateArray()).SelectMany(t => t.GetProperty("resources").EnumerateArray()).Single(r => r.Id("id") == id);
        Assert.Equal("Chapter-notes.pdf", inTree.Str("documentFileName"));
        await a.PostAsJsonAsync($"/api/v1/admin/resources/{id}/status", new { status = "Published" }).Ok();

        var s = await api.StudentAsync();
        var url = (await s.GetAsync($"/api/v1/resources/{id}/playback").Ok()).Str("url");
        Assert.Equal(HttpStatusCode.OK, (await api.CreateClient().GetAsync(url)).StatusCode);

        // A new upload replaces the old file; published notes can't lose their only file.
        var firstFile = await api.WithDb(db => db.LearningResources.Where(r => r.Id == id).Select(r => r.FileId).FirstAsync());
        await a.PostAsync($"/api/v1/admin/resources/{id}/document", File(MaterialSamples.OnePagePdf("Notes v2", ["Three"]), "v2.pdf")).Ok();
        Assert.False(await api.WithDb(db => db.StoredFiles.AnyAsync(f => f.Id == firstFile)));
        var remove = await a.DeleteAsync($"/api/v1/admin/resources/{id}/document");
        Assert.Equal("document_in_use", (await remove.ReadJson()).Str("code"));
        await a.PostAsJsonAsync($"/api/v1/admin/resources/{id}/status", new { status = "Draft" }).Ok();
        var secondFile = await api.WithDb(db => db.LearningResources.Where(r => r.Id == id).Select(r => r.FileId).FirstAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/v1/admin/resources/{id}/document")).StatusCode);
        Assert.Null(await api.WithDb(db => db.LearningResources.Where(r => r.Id == id).Select(r => r.FileId).FirstAsync()));
        Assert.False(await api.WithDb(db => db.StoredFiles.AnyAsync(f => f.Id == secondFile)));
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/v1/admin/resources/{id}")).StatusCode);
    }
}

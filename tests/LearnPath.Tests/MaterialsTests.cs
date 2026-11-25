using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LearnPath.Api.Data;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

/// <summary>Free study material: public list, downloads for signed-in students through signed links, admin uploads.</summary>
[Collection("api")]
public class MaterialsTests(ApiFactory api)
{
    private const string SigningKey = "test-signing-key-0123456789-0123456789-abcdef";

    private static MultipartFormDataContent Form(string title, (byte[] Data, string Name)? file = null, string? link = null, string status = "Published",
        string type = "Notes", string? subject = "Physics", string? classLevel = "11")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(title), "titleEn" },
            { new StringContent(type), "type" },
            { new StringContent(status), "status" },
        };
        if (subject is not null) form.Add(new StringContent(subject), "subject");
        if (classLevel is not null) form.Add(new StringContent(classLevel), "classLevel");
        if (link is not null) form.Add(new StringContent(link), "externalUrl");
        if (file is { } f)
        {
            var content = new ByteArrayContent(f.Data);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(content, "file", f.Name);
        }
        return form;
    }

    private static byte[] Pdf(string text) => MaterialSamples.OnePagePdf(text, [text]);

    [Fact]
    public async Task Everyone_sees_published_material_and_filters_work()
    {
        var anon = api.CreateClient();
        var all = (await anon.GetAsync("/api/v1/materials").Ok()).EnumerateArray().ToList();
        var formula = all.Single(m => m.Str("title") == "Units and Measurement - formula sheet (Sample)");
        Assert.Equal("FormulaSheet", formula.Str("type"));
        Assert.Equal("PDF", formula.Str("format"));
        Assert.True(formula.GetProperty("sizeBytes").GetInt64() > 500);
        var ncert = all.Single(m => m.Str("title") == "NCERT textbooks (official site)");
        Assert.Equal("Link", ncert.Str("format"));
        Assert.Equal(JsonValueKind.Null, ncert.GetProperty("subject").ValueKind);

        var biology = (await anon.GetAsync("/api/v1/materials?subject=Biology").Ok()).EnumerateArray().Select(m => m.Str("title")).ToList();
        Assert.Contains("Cell: The Unit of Life - key points (Sample)", biology);
        Assert.DoesNotContain(biology, t => t.StartsWith("Laws of Motion"));
        var general = (await anon.GetAsync("/api/v1/materials?subject=general").Ok()).EnumerateArray().Select(m => m.Str("title")).ToList();
        Assert.Contains("NCERT textbooks (official site)", general);
        var class12 = (await anon.GetAsync("/api/v1/materials?classLevel=12").Ok()).EnumerateArray().Select(m => m.Str("title")).ToList();
        Assert.Contains("NCERT textbooks (official site)", class12); // material for both classes
        Assert.DoesNotContain(class12, t => t.Contains("Laws of Motion"));
        var notes = (await anon.GetAsync("/api/v1/materials?type=Notes").Ok()).EnumerateArray().Select(m => m.Str("type")).Distinct().ToList();
        Assert.Equal(["Notes"], notes);

        anon.DefaultRequestHeaders.AcceptLanguage.ParseAdd("hi");
        Assert.Contains((await anon.GetAsync("/api/v1/materials?subject=general").Ok()).EnumerateArray(), m => m.Str("title") == "एनसीईआरटी पाठ्यपुस्तकें (आधिकारिक साइट)");
    }

    [Fact]
    public async Task Downloading_needs_an_account_and_hands_out_a_short_signed_link()
    {
        var anon = api.CreateClient();
        var formula = (await anon.GetAsync("/api/v1/materials").Ok()).EnumerateArray().Single(m => m.Str("title") == "Units and Measurement - formula sheet (Sample)");
        var id = formula.Id("id");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync($"/api/v1/materials/{id}/download", null)).StatusCode);

        var s = await api.StudentAsync();
        var before = formula.GetProperty("downloads").GetInt32();
        var link = await s.PostAsync($"/api/v1/materials/{id}/download", null).Ok();
        Assert.False(link.GetProperty("external").GetBoolean());
        Assert.Equal("Units-and-Measurement-formula-sheet-Sample.pdf", link.Str("fileName"));
        var url = link.Str("url");
        Assert.StartsWith($"/api/v1/materials/{id}/file?expires=", url);

        // The link works without a sign-in token (browsers and PDF viewers open it directly).
        var file = await anon.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("application/pdf", file.Content.Headers.ContentType!.MediaType);
        Assert.Equal("inline", file.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("Units-and-Measurement-formula-sheet-Sample.pdf", file.Content.Headers.ContentDisposition.FileName?.Trim('"'));
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(await file.Content.ReadAsByteArrayAsync())[..5]);
        Assert.Equal(before + 1, (await anon.GetAsync("/api/v1/materials").Ok()).EnumerateArray().Single(m => m.Id("id") == id).GetProperty("downloads").GetInt32());

        // A changed or expired link is refused.
        Assert.Equal(HttpStatusCode.Forbidden, (await anon.GetAsync(url[..^4] + "0000")).StatusCode);
        var past = api.Clock.GetUtcNow().AddMinutes(-1).ToUnixTimeSeconds();
        var expired = $"/api/v1/materials/{id}/file?expires={past}&sig={Hashing.HmacSha256Hex(SigningKey, $"material:{id:N}:{past}")}";
        var refused = await anon.GetAsync(expired);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("link_expired", (await refused.ReadJson()).Str("code"));

        // Links to other sites are handed back as they are.
        var ncert = (await anon.GetAsync("/api/v1/materials").Ok()).EnumerateArray().Single(m => m.Str("format") == "Link" && m.Str("title").StartsWith("NCERT"));
        var external = await s.PostAsync($"/api/v1/materials/{ncert.Str("id")}/download", null).Ok();
        Assert.True(external.GetProperty("external").GetBoolean());
        Assert.Equal("https://ncert.nic.in/textbook.php", external.Str("url"));
    }

    [Fact]
    public async Task The_sample_PDFs_are_well_formed()
    {
        var pdf = Encoding.ASCII.GetString(MaterialSamples.OnePagePdf("Title (with brackets)", ["Line one", "Line two"]));
        Assert.StartsWith("%PDF-1.4", pdf);
        Assert.EndsWith("%%EOF\n", pdf);
        var startxref = int.Parse(pdf[(pdf.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref", pdf[startxref..]);
        var offsets = pdf[startxref..].Split('\n').Skip(3).Take(6).Select(l => int.Parse(l[..10])).ToList();
        for (var i = 0; i < offsets.Count; i++) Assert.StartsWith($"{i + 1} 0 obj", pdf[offsets[i]..]);
        Assert.Contains("(Title \\(with brackets\\)) Tj", pdf);
        var length = int.Parse(pdf[(pdf.IndexOf("/Length ", StringComparison.Ordinal) + 8)..].Split(' ')[0]);
        var streamStart = pdf.IndexOf("stream\n", StringComparison.Ordinal) + 7;
        Assert.Equal("endstream", pdf.Substring(streamStart + length, 9));
    }

    [Fact]
    public async Task Admins_upload_check_replace_and_delete_material()
    {
        var a = await api.AdminAsync();
        var title = "Upload test " + Guid.NewGuid().ToString("N")[..6];

        // Drafts stay hidden; only real PDFs and images are accepted; a file or a link is required.
        var created = await a.PostAsync("/api/v1/admin/materials", Form(title, (Pdf("Hello"), "my notes!.PDF"), status: "Draft")).Ok();
        Assert.Equal("PDF", created.Str("format"));
        Assert.Equal("my-notes.pdf", created.Str("fileName"));
        var id = created.Id("id");
        Assert.DoesNotContain((await api.CreateClient().GetAsync("/api/v1/materials").Ok()).EnumerateArray(), m => m.Id("id") == id);
        var fake = await a.PostAsync("/api/v1/admin/materials", Form("Fake", (Encoding.UTF8.GetBytes("<html>not a pdf</html>"), "notes.pdf")));
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);
        Assert.Equal("unsupported_file", (await fake.ReadJson()).Str("code"));
        Assert.Equal("file_or_link_required", (await (await a.PostAsync("/api/v1/admin/materials", Form("Nothing"))).ReadJson()).Str("code"));
        Assert.Equal("invalid_link", (await (await a.PostAsync("/api/v1/admin/materials", Form("Http", link: "http://example.com/x.pdf"))).ReadJson()).Str("code"));
        var png = await a.PostAsync("/api/v1/admin/materials", Form("Mind map " + title, ([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3], "map.png"), type: "MindMap")).Ok();
        Assert.Equal("Image", png.Str("format"));

        // Publishing shows it; a link replaces the file, which is then removed.
        var oldFile = await api.WithDb(db => db.StudyMaterials.Where(m => m.Id == id).Select(m => m.FileId).FirstAsync());
        var updated = await a.PutAsync($"/api/v1/admin/materials/{id}", Form(title, link: "https://drive.google.com/file/d/abc/view")).Ok();
        Assert.Equal("Link", updated.Str("format"));
        Assert.False(await api.WithDb(db => db.StoredFiles.AnyAsync(f => f.Id == oldFile)));
        Assert.Contains((await api.CreateClient().GetAsync("/api/v1/materials").Ok()).EnumerateArray(), m => m.Id("id") == id);
        // Editing without a file or link keeps the source.
        var kept = await a.PutAsync($"/api/v1/admin/materials/{id}", Form(title + " (renamed)")).Ok();
        Assert.Equal("https://drive.google.com/file/d/abc/view", kept.Str("externalUrl"));

        var list = await a.GetAsync("/api/v1/admin/materials").Ok();
        Assert.True(list.GetProperty("storedBytes").GetInt64() > 0);
        Assert.Equal(20, list.GetProperty("maxUploadMegabytes").GetInt32());

        var pngId = png.Id("id");
        var pngFile = await api.WithDb(db => db.StudyMaterials.Where(m => m.Id == pngId).Select(m => m.FileId).FirstAsync());
        await a.DeleteAsync($"/api/v1/admin/materials/{pngId}").Ok();
        Assert.False(await api.WithDb(db => db.StoredFiles.AnyAsync(f => f.Id == pngFile)));
        await a.DeleteAsync($"/api/v1/admin/materials/{id}").Ok();

        var student = await api.StudentAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/v1/admin/materials")).StatusCode);
    }

    [Fact]
    public async Task Files_over_the_configured_size_are_refused()
    {
        await using var host = api.WithWebHostBuilder(b => b.UseSetting("Materials:MaxUploadMegabytes", "1"));
        var a = host.CreateClient();
        var login = await a.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.AdminPassword });
        a.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).Str("accessToken"));
        var big = new byte[1_200_000];
        "%PDF-1.4\n"u8.CopyTo(big);
        var res = await a.PostAsync("/api/v1/admin/materials", Form("Too big", (big, "big.pdf")));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("file_too_large", (await res.ReadJson()).Str("code"));
    }
}

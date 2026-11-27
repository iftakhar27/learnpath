using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

/// <summary>The courses from the syllabus brochure: seeded once, listed by class, and not for sale until a lesson is up.</summary>
[Collection("api")]
public class CareerPathCoursesTests(ApiFactory api)
{
    private Task<Guid> CourseAsync(string slug) => api.WithDb(db => db.Courses.Where(c => c.Slug == slug).Select(c => c.Id).FirstAsync());

    [Fact]
    public async Task Every_course_is_published_with_its_monthly_fee_and_listed_by_class()
    {
        var anon = api.CreateClient();
        var list = (await anon.GetAsync("/api/v1/courses?pageSize=50").Ok()).GetProperty("items").EnumerateArray().ToList();
        var ours = list.Where(c => CareerPathCourses.Slugs.Contains(c.Str("slug"))).ToList();
        Assert.Equal(
            ["class-8-foundation", "class-9-foundation", "class-10-foundation-board", "class-11-jee", "class-11-neet", "class-12-jee", "class-12-neet", "jee-dropper", "neet-dropper"],
            ours.Select(c => c.Str("slug")));
        Assert.All(ours, c =>
        {
            Assert.False(c.GetProperty("isFree").GetBoolean());
            Assert.Equal(30, c.GetProperty("accessDays").GetInt32());
            Assert.Equal(0, c.GetProperty("lessonCount").GetInt32());
            Assert.Equal(0, c.GetProperty("testCount").GetInt32());
        });
        Assert.Equal(999_00, ours[0].GetProperty("priceInPaise").GetInt64());
        Assert.Equal(1499_00, ours[^1].GetProperty("priceInPaise").GetInt64());
        Assert.StartsWith("Goal: strong basics + Olympiad / ITSE preparation.\n10 months · 40+ tests · ₹999/month", ours[0].Str("description"));
        // Sample courses (with lessons) report how many there are.
        Assert.True(list.Single(c => c.Str("slug") == "neet-physics-mechanics-sample").GetProperty("lessonCount").GetInt32() > 0);

        // Class 8 and 9 sit under the new Foundation exam, the rest under the exam they prepare for.
        var exams = (await anon.GetAsync("/api/v1/exams").Ok()).EnumerateArray().ToDictionary(e => e.Str("code"), e => e.Str("id"));
        Assert.Equal(exams["FOUNDATION"], ours[0].Str("examId"));
        Assert.Equal(exams["CBSE_10"], ours[2].Str("examId"));
        Assert.Equal(exams["JEE_MAIN"], ours[3].Str("examId"));
        Assert.Equal(exams["NEET"], ours[^1].Str("examId"));

        var hindi = api.CreateClient();
        hindi.DefaultRequestHeaders.AcceptLanguage.ParseAdd("hi");
        var hi = (await hindi.GetAsync($"/api/v1/courses/{await CourseAsync("class-8-foundation")}/syllabus").Ok()).GetProperty("course");
        Assert.Equal("कक्षा 8 फाउंडेशन", hi.Str("title"));
        Assert.StartsWith("लक्ष्य:", hi.Str("description"));
    }

    [Fact]
    public async Task Syllabi_follow_the_brochure_and_class_11_12_chapters_link_to_ncert()
    {
        var anon = api.CreateClient();
        var neet = await anon.GetAsync($"/api/v1/courses/{await CourseAsync("class-11-neet")}/syllabus").Ok();
        var subjects = neet.GetProperty("subjects").EnumerateArray().ToList();
        Assert.Equal(["Physics", "Chemistry", "Biology"], subjects.Select(s => s.Str("title")));
        Assert.Equal(14, subjects[0].GetProperty("chapters").GetArrayLength());
        Assert.Equal(19, subjects[2].GetProperty("chapters").GetArrayLength());
        var human = subjects[2].GetProperty("chapters").EnumerateArray().Last();
        Assert.Equal("Human Physiology", human.Str("unit"));
        Assert.Equal("Chemical Coordination and Integration", human.Str("title"));

        var foundation = await anon.GetAsync($"/api/v1/courses/{await CourseAsync("class-8-foundation")}/syllabus").Ok();
        Assert.Equal(["Science", "Maths", "English + Reasoning"], foundation.GetProperty("subjects").EnumerateArray().Select(s => s.Str("title")));
        Assert.Equal("Crop Production and Management", foundation.GetProperty("subjects")[0].GetProperty("chapters")[0].Str("title"));

        var dropper = await anon.GetAsync($"/api/v1/courses/{await CourseAsync("jee-dropper")}/syllabus").Ok();
        var dropperSubjects = dropper.GetProperty("subjects").EnumerateArray().ToList();
        Assert.Equal(["Physics", "Chemistry", "Maths", "Phase 3 · Full mocks + PYQ marathon (months 7–10)"], dropperSubjects.Select(s => s.Str("title")));
        var maths = dropperSubjects[2].GetProperty("chapters").EnumerateArray().ToList();
        Assert.StartsWith("Phase 1", maths[0].Str("unit"));
        Assert.StartsWith("Phase 2", maths[^1].Str("unit"));

        // NCERT links: Class 11–12 science chapters link; Class 8–10 "Science" and brochure-only chapters don't.
        var links = await api.WithDb(db => db.Chapters.Include(c => c.Subject)
            .Where(c => db.Courses.Any(k => k.Id == c.Subject!.CourseId && CareerPathCourses.Slugs.Contains(k.Slug)))
            .Select(c => new { c.TitleEn, Subject = c.Subject!.TitleEn, Slug = db.Courses.First(k => k.Id == c.Subject!.CourseId).Slug, c.NcertChapterId }).ToListAsync());
        Assert.All(links.Where(l => l.Slug == "class-12-neet" && l.Subject == "Biology"), l => Assert.NotNull(l.NcertChapterId));
        Assert.All(links.Where(l => l.Slug == "neet-dropper" && l.Subject == "Physics"), l => Assert.NotNull(l.NcertChapterId));
        Assert.All(links.Where(l => l.Slug is "class-8-foundation" or "class-9-foundation" or "class-10-foundation-board"), l => Assert.Null(l.NcertChapterId));
        Assert.Null(links.Single(l => l.Slug == "class-12-neet" && l.TitleEn == "The Solid State").NcertChapterId);
    }

    [Fact]
    public async Task A_paid_course_cannot_be_bought_until_its_first_lesson_is_published()
    {
        var s = await api.StudentAsync();
        var seeded = await s.PostAsJsonAsync("/api/v1/payments/orders", new { courseId = await CourseAsync("class-8-foundation") });
        Assert.Equal(HttpStatusCode.Conflict, seeded.StatusCode);
        Assert.Equal("course_not_ready", (await seeded.ReadJson()).Str("code"));

        // A new paid course: on sale once the admin publishes its first lesson (a draft one doesn't count).
        var a = await api.AdminAsync();
        var exam = await api.WithDb(db => db.Exams.Where(e => e.Code == "CBSE_10").Select(e => e.Id).FirstAsync());
        var course = (await a.PostAsJsonAsync("/api/v1/admin/courses", new { examId = exam, slug = "ready-" + Guid.NewGuid().ToString("N")[..8], titleEn = "Readiness test", isFree = false, priceInPaise = 999_00, accessDays = 30 }).Ok()).Id("id");
        await a.PostAsJsonAsync($"/api/v1/admin/courses/{course}/status", new { status = "Published" }).Ok();
        var subject = (await a.PostAsJsonAsync("/api/v1/admin/subjects", new { parentId = course, titleEn = "Science" }).Ok()).Id("id");
        var chapter = (await a.PostAsJsonAsync("/api/v1/admin/chapters", new { parentId = subject, titleEn = "Motion" }).Ok()).Id("id");
        var topic = (await a.PostAsJsonAsync("/api/v1/admin/topics", new { parentId = chapter, titleEn = "Distance and displacement", estimatedMinutes = 30 }).Ok()).Id("id");
        var lesson = (await a.PostAsJsonAsync("/api/v1/admin/resources", new { topicId = topic, type = "Video", titleEn = "Displacement explained", language = "en", videoProviderId = "sample-ready-motion", durationSeconds = 600 }).Ok()).Id("id");
        Assert.Equal(0, (await s.GetAsync($"/api/v1/courses/{course}/syllabus").Ok()).GetProperty("course").GetProperty("lessonCount").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await s.PostAsJsonAsync("/api/v1/payments/orders", new { courseId = course })).StatusCode);

        await a.PostAsJsonAsync($"/api/v1/admin/resources/{lesson}/status", new { status = "Published" }).Ok();
        Assert.Equal(1, (await s.GetAsync($"/api/v1/courses/{course}/syllabus").Ok()).GetProperty("course").GetProperty("lessonCount").GetInt32());
        var order = await s.PostAsJsonAsync("/api/v1/payments/orders", new { courseId = course }).Ok();
        Assert.Equal(999_00, order.GetProperty("amountInPaise").GetInt64());

        // A test series (tests, no lessons) is on sale once a test is published; a student's own custom test doesn't count.
        var series = (await a.PostAsJsonAsync("/api/v1/admin/courses", new { examId = exam, slug = "series-" + Guid.NewGuid().ToString("N")[..8], titleEn = "Test series", isFree = false, priceInPaise = 299_00, accessDays = 30 }).Ok()).Id("id");
        await a.PostAsJsonAsync($"/api/v1/admin/courses/{series}/status", new { status = "Published" }).Ok();
        async Task AddTestAsync(PublishStatus status, Guid? owner) => await api.WithDb(async db =>
        {
            db.Tests.Add(new Test { CourseId = series, Type = TestType.Mock, TitleEn = "Mock " + status, DurationMinutes = 60, Status = status, OwnerUserId = owner, CreatedAt = DateTimeOffset.UtcNow });
            return await db.SaveChangesAsync();
        });
        await AddTestAsync(PublishStatus.Draft, null);
        await AddTestAsync(PublishStatus.Published, (await s.GetAsync("/api/v1/student/profile").Ok()).Id("userId"));
        Assert.Equal(HttpStatusCode.Conflict, (await s.PostAsJsonAsync("/api/v1/payments/orders", new { courseId = series })).StatusCode);
        await AddTestAsync(PublishStatus.Published, null);
        var summary = (await s.GetAsync($"/api/v1/courses/{series}/syllabus").Ok()).GetProperty("course");
        Assert.Equal((0, 1), (summary.GetProperty("lessonCount").GetInt32(), summary.GetProperty("testCount").GetInt32()));
        await s.PostAsJsonAsync("/api/v1/payments/orders", new { courseId = series }).Ok();
    }

    [Fact]
    public async Task Seeding_again_adds_nothing_and_keeps_admin_changes()
    {
        var id = await CourseAsync("class-12-jee");
        await api.WithDb(async db =>
        {
            var c = await db.Courses.FirstAsync(x => x.Id == id);
            c.Status = PublishStatus.Archived;
            await db.SaveChangesAsync();
            return 0;
        });
        try
        {
            Assert.Equal(0, await api.WithDb(db => CareerPathCourses.SeedAsync(db, DateTimeOffset.UtcNow)));
            Assert.Equal(1, await api.WithDb(db => db.Courses.CountAsync(c => c.Slug == "class-12-jee")));
            Assert.Equal(PublishStatus.Archived, await api.WithDb(db => db.Courses.Where(c => c.Id == id).Select(c => c.Status).FirstAsync()));
            Assert.Equal(1, await api.WithDb(db => db.Exams.CountAsync(e => e.Code == CareerPathCourses.FoundationExamCode)));
        }
        finally
        {
            await api.WithDb(async db =>
            {
                var c = await db.Courses.FirstAsync(x => x.Id == id);
                c.Status = PublishStatus.Published;
                await db.SaveChangesAsync();
                return 0;
            });
        }
    }
}

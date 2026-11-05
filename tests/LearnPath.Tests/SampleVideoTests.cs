using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Tests;

[Collection("api")]
public class SampleVideoTests(ApiFactory api)
{
    [Fact]
    public async Task Placeholder_lessons_get_the_demo_stream_and_its_length_but_real_lessons_are_untouched()
    {
        var prefix = "sample-test-" + Guid.NewGuid().ToString("N")[..8];
        var (placeholderId, realId) = await api.WithDb(async db =>
        {
            var topic = await db.Topics.FirstAsync();
            var placeholder = new LearningResource { TopicId = topic.Id, Type = ResourceType.Video, TitleEn = "Placeholder", VideoProviderId = prefix + "-01", DurationSeconds = 900, Status = PublishStatus.Published, UpdatedAt = DateTimeOffset.UtcNow };
            var real = new LearningResource { TopicId = topic.Id, Type = ResourceType.Video, TitleEn = "Real", VideoProviderId = "bunny-video-guid", DurationSeconds = 300, Status = PublishStatus.Published, UpdatedAt = DateTimeOffset.UtcNow };
            db.LearningResources.AddRange(placeholder, real);
            await db.SaveChangesAsync();
            return (placeholder.Id, real.Id);
        });

        var changed = await api.WithDb(db => Seeder.UseDemoVideoForPlaceholdersAsync(db, "https://video.test/demo.m3u8", 635, DateTimeOffset.UtcNow, prefix));
        Assert.Equal(1, changed);

        var (p, r) = await api.WithDb(async db => (await db.LearningResources.AsNoTracking().FirstAsync(x => x.Id == placeholderId), await db.LearningResources.AsNoTracking().FirstAsync(x => x.Id == realId)));
        Assert.Equal(("https://video.test/demo.m3u8", 635), (p.VideoProviderId, p.DurationSeconds));
        Assert.Equal(("bunny-video-guid", 300), (r.VideoProviderId, r.DurationSeconds));

        // Running it again (every start) changes nothing.
        Assert.Equal(0, await api.WithDb(db => Seeder.UseDemoVideoForPlaceholdersAsync(db, "https://video.test/demo.m3u8", 635, DateTimeOffset.UtcNow, prefix)));
    }
}

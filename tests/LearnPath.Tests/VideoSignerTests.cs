using System.Security.Cryptography;
using System.Text;
using LearnPath.Api.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace LearnPath.Tests;

public class VideoSignerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

    private static BunnyVideoUrlSigner Signer(Dictionary<string, string?> settings) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new FakeTimeProvider(Now));

    [Fact]
    public void Stream_url_is_null_when_no_cdn_is_configured() =>
        Assert.Null(Signer(new()).GetStreamUrl("abc-123", TimeSpan.FromHours(2)));

    [Fact]
    public void Absolute_https_ids_are_used_directly_for_development_content() =>
        Assert.Equal("https://cdn.example.com/v.m3u8", Signer(new()).GetStreamUrl("https://cdn.example.com/v.m3u8", TimeSpan.FromHours(1)));

    [Fact]
    public void Stream_url_carries_a_bunny_cdn_token_bound_to_path_and_expiry()
    {
        var url = Signer(new() { ["Video:CdnHost"] = "vz-1.b-cdn.net", ["Video:CdnTokenKey"] = "k3y" }).GetStreamUrl("vid-9", TimeSpan.FromHours(2));
        var expires = Now.AddHours(2).ToUnixTimeSeconds();
        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"k3y/vid-9/playlist.m3u8{expires}"))).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        Assert.Equal($"https://vz-1.b-cdn.net/vid-9/playlist.m3u8?token={expected}&expires={expires}", url);
    }

    [Fact]
    public void Embed_url_is_signed_when_a_token_key_is_set()
    {
        var url = Signer(new() { ["Video:LibraryId"] = "42", ["Video:TokenKey"] = "secret" }).GetPlaybackUrl("vid-9", Guid.NewGuid(), TimeSpan.FromHours(1));
        var expires = Now.AddHours(1).ToUnixTimeSeconds();
        Assert.Equal($"https://iframe.mediadelivery.net/embed/42/vid-9?token={Hashing.Sha256("secretvid-9" + expires)}&expires={expires}", url);
    }
}

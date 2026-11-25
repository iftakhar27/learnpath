using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace LearnPath.Tests;

/// <summary>Public settings for the apps: WhatsApp contact links, checked so a typo can't send students elsewhere.</summary>
[Collection("api")]
public class AppConfigTests(ApiFactory api)
{
    [Fact]
    public async Task Without_settings_there_are_no_WhatsApp_links()
    {
        var config = await api.CreateClient().GetAsync("/api/v1/app-config").Ok();
        Assert.Equal(JsonValueKind.Null, config.GetProperty("whatsAppNumber").ValueKind);
        Assert.Equal(JsonValueKind.Null, config.GetProperty("whatsAppChannelUrl").ValueKind);
    }

    [Fact]
    public async Task The_number_is_kept_as_digits_and_only_WhatsApp_links_are_accepted()
    {
        await using var host = api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Contact:WhatsAppNumber", "+91 98765-43210");
            b.UseSetting("Contact:WhatsAppChannelUrl", "https://whatsapp.com/channel/0029VaExample");
            b.UseSetting("Web:BaseUrl", "https://learnpath.example/");
        });
        var res = await host.CreateClient().GetAsync("/api/v1/app-config");
        Assert.Contains("max-age=300", res.Headers.CacheControl!.ToString());
        var config = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("919876543210", config.Str("whatsAppNumber"));
        Assert.Equal("https://whatsapp.com/channel/0029VaExample", config.Str("whatsAppChannelUrl"));
        Assert.Equal("https://learnpath.example", config.Str("websiteUrl"));

        await using var bad = api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Contact:WhatsAppNumber", "call us");
            b.UseSetting("Contact:WhatsAppChannelUrl", "https://evil.example/whatsapp.com");
        });
        var rejected = await bad.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/app-config");
        Assert.Equal(JsonValueKind.Null, rejected.GetProperty("whatsAppNumber").ValueKind);
        Assert.Equal(JsonValueKind.Null, rejected.GetProperty("whatsAppChannelUrl").ValueKind);
    }
}

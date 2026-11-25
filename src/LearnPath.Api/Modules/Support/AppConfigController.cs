using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LearnPath.Api.Modules.Support;

/// <summary>Public settings the apps and website read at start: how to reach the team on WhatsApp, and the website's address for links.</summary>
public record AppConfigDto(string? WhatsAppNumber, string? WhatsAppChannelUrl, string? WebsiteUrl);

/// <summary>
/// Set on the server (Render environment) so they change without an app release:
/// <c>Contact__WhatsAppNumber</c> (with country code, e.g. +91 98765 43210) and <c>Contact__WhatsAppChannelUrl</c>
/// (a whatsapp.com channel or chat.whatsapp.com group invite link). Anything else is ignored, so a typo can't send
/// students to another site.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/app-config")]
public partial class AppConfigController(IConfiguration config, ILogger<AppConfigController> logger) : ControllerBase
{
    private static readonly string[] ChannelHosts = ["whatsapp.com", "www.whatsapp.com", "chat.whatsapp.com", "wa.me"];

    [HttpGet]
    public AppConfigDto Get()
    {
        Response.Headers.CacheControl = "public, max-age=300";
        return new AppConfigDto(Number(config["Contact:WhatsAppNumber"]), Channel(config["Contact:WhatsAppChannelUrl"]), Website(config["Web:BaseUrl"]));
    }

    /// <summary>Digits only, country code included (what wa.me links need); 8 to 15 digits as phone numbers allow.</summary>
    internal string? Number(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = NonDigits().Replace(raw, "");
        if (digits.Length is >= 8 and <= 15) return digits;
        logger.LogWarning("Contact:WhatsAppNumber is not a phone number with country code; WhatsApp chat links are hidden");
        return null;
    }

    internal string? Channel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && ChannelHosts.Contains(uri.Host.ToLowerInvariant()))
            return uri.ToString();
        logger.LogWarning("Contact:WhatsAppChannelUrl must be an https link on whatsapp.com or chat.whatsapp.com; the channel link is hidden");
        return null;
    }

    private static string? Website(string? raw) =>
        Uri.TryCreate(raw?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri.ToString().TrimEnd('/') : null;

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();
}

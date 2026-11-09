using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace LearnPath.Api.Infrastructure;

public record EmailMessage(string To, string? ToName, string Subject, string Text, string Html);

/// <summary>Sends account emails (password reset links). Provider set by <c>Email:Provider</c>.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public sealed class EmailOptions
{
    /// <summary>disabled (default), log (test servers: written to the log and the staff outbox), or brevo.</summary>
    public string Provider { get; set; } = "disabled";
    /// <summary>Sender address; with Brevo it must be a verified sender (or on a verified domain).</summary>
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "";
    public BrevoOptions Brevo { get; set; } = new();

    public sealed class BrevoOptions
    {
        public string ApiKey { get; set; } = "";
    }
}

/// <summary>Email turned off: nothing is sent (the API still answers as if it were, so it reveals nothing).</summary>
public sealed class DisabledEmailSender(ILogger<DisabledEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogWarning("Email is not configured (Email:Provider=disabled); not sending \"{Subject}\"", message.Subject);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Test servers only (refused in Production): writes each email to the log and keeps recent ones for staff
/// (GET /api/v1/admin/test-emails), so password resets can be tried before an email service is connected.
/// </summary>
public sealed class LogEmailSender(ILogger<LogEmailSender> logger, TestEmailOutbox outbox, TimeProvider clock) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogInformation("Email (not sent, Email:Provider=log) to {To}: {Subject}\n{Text}", message.To, message.Subject, message.Text);
        outbox.Add(message, clock.GetUtcNow());
        return Task.CompletedTask;
    }
}

public record TestEmailDto(string To, string Subject, string Text, DateTimeOffset SentAt);

public sealed class TestEmailOutbox
{
    public const int Capacity = 20;
    private static readonly TimeSpan KeepFor = TimeSpan.FromHours(1);
    private readonly LinkedList<TestEmailDto> _items = new();
    private readonly object _gate = new();

    public void Add(EmailMessage m, DateTimeOffset sentAt)
    {
        lock (_gate)
        {
            _items.AddFirst(new TestEmailDto(m.To, m.Subject, m.Text, sentAt));
            while (_items.Count > Capacity) _items.RemoveLast();
        }
    }

    public List<TestEmailDto> Recent(DateTimeOffset now)
    {
        lock (_gate) return _items.Where(i => i.SentAt > now - KeepFor).ToList();
    }
}

/// <summary>Brevo's transactional email API (HTTPS, so it works where outbound SMTP is blocked, e.g. Render's free plan).</summary>
public sealed class BrevoEmailSender(HttpClient http, IOptions<EmailOptions> options, ILogger<BrevoEmailSender> logger) : IEmailSender
{
    public const string BaseUrl = "https://api.brevo.com/";

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        using var req = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = JsonContent.Create(new
            {
                sender = new { email = o.FromEmail, name = string.IsNullOrWhiteSpace(o.FromName) ? null : o.FromName },
                to = new[] { new { email = message.To, name = message.ToName } },
                subject = message.Subject,
                textContent = message.Text,
                htmlContent = message.Html,
            }),
        };
        req.Headers.Add("api-key", o.Brevo.ApiKey);
        req.Headers.Accept.ParseAdd("application/json");
        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            logger.LogError("Brevo send failed ({Status}): {Body}", (int)res.StatusCode, body.Length > 500 ? body[..500] : body);
            throw new InvalidOperationException("The email could not be sent.");
        }
    }
}

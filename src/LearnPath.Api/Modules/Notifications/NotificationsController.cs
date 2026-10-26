using System.ComponentModel.DataAnnotations;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Notifications;

public record NotificationDto(Guid Id, string Type, string Title, string Body, string? Link, DateTimeOffset CreatedAt, bool Read);
public record TicketRequest([Required, RegularExpression("^(payment|content|technical|account|other)$")] string Category,
    [Required, MaxLength(200)] string Subject, [Required, MaxLength(4000)] string Message);
public record TicketDto(Guid Id, string Category, string Priority, string Subject, string Message, string Status, string? StaffResponse, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

[ApiController]
[Authorize]
[Route("api/v1")]
public class NotificationsController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet("notifications")]
    public async Task<PagedResult<NotificationDto>> List([FromQuery] bool unreadOnly = false, [FromQuery] int? page = null, CancellationToken ct = default)
    {
        var (p, size) = Paging.Normalize(page, 30);
        var userId = User.UserId();
        var q = db.Notifications.AsNoTracking().Where(n => n.UserId == userId && (!unreadOnly || n.ReadAt == null));
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(n => n.CreatedAt).Skip((p - 1) * size).Take(size)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Body, n.Link, n.CreatedAt, n.ReadAt != null)).ToListAsync(ct);
        return new(items, p, size, total);
    }

    [HttpPost("notifications/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var userId = User.UserId();
        await db.Notifications.Where(n => n.Id == id && n.UserId == userId && n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, clock.GetUtcNow()), ct);
        return NoContent();
    }

    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = User.UserId();
        await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, clock.GetUtcNow()), ct);
        return NoContent();
    }

    [HttpPost("support/tickets")]
    public async Task<TicketDto> CreateTicket(TicketRequest req, CancellationToken ct)
    {
        var t = new SupportTicket { UserId = User.UserId(), Category = req.Category, Priority = req.Category == "payment" ? "High" : "Normal", Subject = req.Subject, Message = req.Message, CreatedAt = clock.GetUtcNow() };
        db.SupportTickets.Add(t);
        await db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    [HttpGet("support/tickets")]
    public async Task<List<TicketDto>> MyTickets(CancellationToken ct)
    {
        var userId = User.UserId();
        return (await db.SupportTickets.AsNoTracking().Where(t => t.UserId == userId).OrderByDescending(t => t.CreatedAt).Take(100).ToListAsync(ct)).Select(ToDto).ToList();
    }

    internal static TicketDto ToDto(SupportTicket t) => new(t.Id, t.Category, t.Priority, t.Subject, t.Message, t.Status, t.StaffResponse, t.CreatedAt, t.UpdatedAt);
}

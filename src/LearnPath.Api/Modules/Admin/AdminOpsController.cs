using System.ComponentModel.DataAnnotations;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Catalog;
using LearnPath.Api.Modules.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Admin;

public record AdminDashboardDto(int TotalStudents, int ActiveStudents7d, int ActiveStudents30d, int Enrollments, int PaidOrders30d, long Revenue30dInPaise,
    int FailedOrders30d, int TestAttempts30d, double? AverageTestPercentage30d, int OpenTickets, int OpenQuestionReports, int FailedImportRows30d,
    List<CourseStatDto> TopCourses);
public record CourseStatDto(Guid CourseId, string Title, int Enrollments);
public record UserStatusRequest([Required] UserStatus Status);
public record GrantEnrollmentRequest([Required] Guid CourseId, [MaxLength(500)] string? Reason);
public record TicketResponseRequest([Required, MaxLength(4000)] string Response, [RegularExpression("^(InProgress|Resolved|Closed)$")] string Status = "Resolved");
public record ReportResolveRequest([Required, RegularExpression("^(Fixed|Rejected)$")] string Status);
public record RoleRequest([Required] string Role);
public record AnnouncementRequest([Required, MaxLength(200)] string Title, [Required, MaxLength(2000)] string Body, Guid? CourseId);

[ApiController]
[Route("api/v1/admin")]
public class AdminOpsController(AppDbContext db, AuditService audit, EntitlementService entitlements, TimeProvider clock) : ControllerBase
{
    [HttpGet("dashboard")]
    [Authorize(Policy = Policies.StaffRead)]
    public async Task<AdminDashboardDto> Dashboard(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        DateTimeOffset d7 = now.AddDays(-7), d30 = now.AddDays(-30);
        var students = db.Users.Where(u => u.Roles.Any(r => r.Role == Roles.Student));
        var paid30 = db.Orders.Where(o => o.Status == OrderStatus.Paid && o.PaidAt >= d30);
        var attempts30 = await db.TestAttempts.Where(a => a.Status != AttemptStatus.InProgress && a.SubmittedAt >= d30 && a.MaxScore > 0)
            .Select(a => new { a.Score, a.MaxScore }).ToListAsync(ct);
        var top = await db.Enrollments.GroupBy(e => e.CourseId).Select(g => new { g.Key, Count = g.Count() }).OrderByDescending(x => x.Count).Take(5).ToListAsync(ct);
        var titles = await db.Courses.Where(c => top.Select(t => t.Key).Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.TitleEn, ct);

        return new AdminDashboardDto(
            await students.CountAsync(ct),
            await db.LearningEvents.Where(e => e.OccurredAt >= d7).Select(e => e.UserId).Distinct().CountAsync(ct),
            await db.LearningEvents.Where(e => e.OccurredAt >= d30).Select(e => e.UserId).Distinct().CountAsync(ct),
            await db.Enrollments.CountAsync(ct),
            await paid30.CountAsync(ct),
            await paid30.SumAsync(o => (long?)o.AmountInPaise, ct) ?? 0,
            await db.Orders.CountAsync(o => o.Status == OrderStatus.Failed && o.CreatedAt >= d30, ct),
            attempts30.Count,
            attempts30.Count == 0 ? null : Math.Round(attempts30.Average(a => (double)((a.Score ?? 0) / a.MaxScore) * 100), 1),
            await db.SupportTickets.CountAsync(t => t.Status == "Open" || t.Status == "InProgress", ct),
            await db.QuestionReports.CountAsync(r => r.Status == "Open", ct),
            await db.ContentImportJobs.Where(j => j.CreatedAt >= d30).SumAsync(j => j.FailedRows, ct),
            top.Select(t => new CourseStatDto(t.Key, titles.GetValueOrDefault(t.Key, "?"), t.Count)).ToList());
    }

    /// <summary>Support lookup. Returns only the fields needed to help a student.</summary>
    [HttpGet("users")]
    [Authorize(Policy = Policies.SupportOps)]
    public async Task<PagedResult<object>> Users([FromQuery] string? q, [FromQuery] int? page, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, 25);
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(u => (u.Email != null && EF.Functions.ILike(u.Email, $"%{q}%")) || (u.Phone != null && u.Phone.Contains(q)) || EF.Functions.ILike(u.FullName, $"%{q}%"));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(u => u.CreatedAt).Skip((p - 1) * size).Take(size)
            .Select(u => (object)new { u.Id, u.FullName, u.Email, u.Phone, Status = u.Status.ToString(), u.CreatedAt, u.LastLoginAt, Roles = u.Roles.Select(r => r.Role) })
            .ToListAsync(ct);
        return new(items, p, size, total);
    }

    [HttpGet("users/{id:guid}")]
    [Authorize(Policy = Policies.SupportOps)]
    public async Task<object> UserDetail(Guid id, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("User");
        return new
        {
            u.Id, u.FullName, u.Email, u.Phone, Status = u.Status.ToString(), u.CreatedAt, u.LastLoginAt, Roles = u.Roles.Select(r => r.Role),
            Enrollments = await db.Enrollments.Where(e => e.UserId == id).Select(e => new { e.CourseId, CourseTitle = e.Course!.TitleEn, Source = e.Source.ToString(), e.StartsAt, e.ExpiresAt }).ToListAsync(ct),
            Orders = await db.Orders.Where(o => o.UserId == id).OrderByDescending(o => o.CreatedAt).Select(o => new { o.Id, o.CourseId, o.AmountInPaise, Status = o.Status.ToString(), o.GatewayOrderId, o.CreatedAt, o.PaidAt }).ToListAsync(ct),
            Tickets = await db.SupportTickets.Where(t => t.UserId == id).OrderByDescending(t => t.CreatedAt).Select(t => new { t.Id, t.Subject, t.Status, t.CreatedAt }).ToListAsync(ct),
        };
    }

    [HttpPost("users/{id:guid}/status")]
    [Authorize(Policy = Policies.SupportOps)]
    public async Task<IActionResult> SetStatus(Guid id, UserStatusRequest req, CancellationToken ct)
    {
        var u = await db.Users.FindAsync([id], ct) ?? throw AppException.NotFound("User");
        u.Status = req.Status;
        if (req.Status != UserStatus.Active)
            await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.GetUtcNow()), ct);
        audit.Record(User.UserId(), "user.status", "User", id, req);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/roles")]
    [Authorize(Policy = Policies.SuperAdmin)]
    public async Task<IActionResult> AddRole(Guid id, RoleRequest req, CancellationToken ct)
    {
        if (!Roles.All.Contains(req.Role)) throw AppException.BadRequest("invalid_role", "Unknown role.");
        if (!await db.Users.AnyAsync(u => u.Id == id, ct)) throw AppException.NotFound("User");
        if (!await db.UserRoles.AnyAsync(r => r.UserId == id && r.Role == req.Role, ct)) db.UserRoles.Add(new UserRole { UserId = id, Role = req.Role });
        audit.Record(User.UserId(), "user.role.add", "User", id, req);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("users/{id:guid}/roles/{role}")]
    [Authorize(Policy = Policies.SuperAdmin)]
    public async Task<IActionResult> RemoveRole(Guid id, string role, CancellationToken ct)
    {
        await db.UserRoles.Where(r => r.UserId == id && r.Role == role).ExecuteDeleteAsync(ct);
        audit.Record(User.UserId(), "user.role.remove", "User", id, new { role });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("users/{id:guid}/enrollments")]
    [Authorize(Policy = Policies.SupportOps)]
    public async Task<IActionResult> Grant(Guid id, GrantEnrollmentRequest req, CancellationToken ct)
    {
        var course = await db.Courses.FindAsync([req.CourseId], ct) ?? throw AppException.NotFound("Course");
        if (!await db.Users.AnyAsync(u => u.Id == id, ct)) throw AppException.NotFound("User");
        await entitlements.GrantAsync(id, course, EnrollmentSource.Admin, null, ct);
        audit.Record(User.UserId(), "enrollment.grant", "User", id, req);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("orders")]
    [Authorize(Policy = Policies.Finance)]
    public async Task<PagedResult<object>> Orders([FromQuery] OrderStatus? status, [FromQuery] int? page, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, 50);
        var q = db.Orders.AsNoTracking().Where(o => status == null || o.Status == status);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(o => o.CreatedAt).Skip((p - 1) * size).Take(size)
            .Select(o => (object)new { o.Id, o.UserId, o.CourseId, o.AmountInPaise, Status = o.Status.ToString(), o.GatewayOrderId, o.Receipt, o.CreatedAt, o.PaidAt }).ToListAsync(ct);
        return new(items, p, size, total);
    }

    [HttpGet("tickets")]
    [Authorize(Policy = Policies.SupportOps)]
    public async Task<List<TicketDto>> Tickets([FromQuery] string? status, CancellationToken ct) =>
        (await db.SupportTickets.AsNoTracking().Where(t => status == null || t.Status == status).OrderByDescending(t => t.CreatedAt).Take(200).ToListAsync(ct))
        .Select(NotificationsController.ToDto).ToList();

    [HttpPost("tickets/{id:guid}/respond")]
    [Authorize(Policy = Policies.SupportOps)]
    public async Task<TicketDto> Respond(Guid id, TicketResponseRequest req, CancellationToken ct)
    {
        var t = await db.SupportTickets.FindAsync([id], ct) ?? throw AppException.NotFound("Ticket");
        var now = clock.GetUtcNow();
        (t.StaffResponse, t.Status, t.RespondedBy, t.UpdatedAt) = (req.Response, req.Status, User.UserId(), now);
        db.Notifications.Add(new Notification { UserId = t.UserId, Type = "support", Title = "Support replied", Body = t.Subject, Link = "/support", CreatedAt = now });
        audit.Record(User.UserId(), "ticket.respond", "SupportTicket", id, new { req.Status });
        await db.SaveChangesAsync(ct);
        return NotificationsController.ToDto(t);
    }

    [HttpGet("question-reports")]
    [Authorize(Policy = Policies.ContentEdit)]
    public async Task<object> Reports([FromQuery] string status = "Open", CancellationToken ct = default) =>
        await db.QuestionReports.AsNoTracking().Where(r => r.Status == status).OrderBy(r => r.CreatedAt).Take(200)
            .Select(r => new { r.Id, r.QuestionId, r.Reason, r.Details, r.Status, r.CreatedAt }).ToListAsync(ct);

    [HttpPost("question-reports/{id:guid}/resolve")]
    [Authorize(Policy = Policies.ContentEdit)]
    public async Task<IActionResult> Resolve(Guid id, ReportResolveRequest req, CancellationToken ct)
    {
        var r = await db.QuestionReports.FindAsync([id], ct) ?? throw AppException.NotFound("Report");
        (r.Status, r.ResolvedAt) = (req.Status, clock.GetUtcNow());
        audit.Record(User.UserId(), "question_report.resolve", "QuestionReport", id, req);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("announcements")]
    [Authorize(Policy = Policies.ContentPublish)]
    public async Task<object> Announce(AnnouncementRequest req, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var users = req.CourseId is { } cid
            ? db.Enrollments.Where(e => e.CourseId == cid && (e.ExpiresAt == null || e.ExpiresAt > now)).Select(e => e.UserId)
            : db.Users.Where(u => u.Status == UserStatus.Active && u.Roles.Any(r => r.Role == Roles.Student)).Select(u => u.Id);
        var ids = await users.Distinct().ToListAsync(ct);
        db.Notifications.AddRange(ids.Select(id => new Notification { UserId = id, Type = "announcement", Title = req.Title, Body = req.Body, CreatedAt = now }));
        audit.Record(User.UserId(), "announcement.send", "Notification", null, new { req.Title, req.CourseId, Recipients = ids.Count });
        await db.SaveChangesAsync(ct);
        return new { recipients = ids.Count };
    }

    [HttpGet("imports")]
    [Authorize(Policy = Policies.ContentEdit)]
    public async Task<object> Imports(CancellationToken ct) =>
        await db.ContentImportJobs.AsNoTracking().OrderByDescending(j => j.CreatedAt).Take(50)
            .Select(j => new { j.Id, j.FileName, j.TotalRows, j.ImportedRows, j.FailedRows, j.CreatedAt, Errors = j.Errors.OrderBy(e => e.RowNumber).Select(e => new { e.RowNumber, e.Message }) }).ToListAsync(ct);

    [HttpGet("audit-logs")]
    [Authorize(Policy = Policies.SuperAdmin)]
    public async Task<PagedResult<AuditLog>> AuditLogs([FromQuery] string? entityType, [FromQuery] int? page, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, 50);
        var q = db.AuditLogs.AsNoTracking().Where(a => entityType == null || a.EntityType == entityType);
        var total = await q.CountAsync(ct);
        return new(await q.OrderByDescending(a => a.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct), p, size, total);
    }
}

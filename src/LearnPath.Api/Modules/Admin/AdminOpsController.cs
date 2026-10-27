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
    public async Task<PagedResult<AdminUserListItem>> Users([FromQuery] string? q, [FromQuery] string? role, [FromQuery] int? page, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, 25);
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => (u.Email != null && EF.Functions.ILike(u.Email, $"%{term}%")) || (u.Phone != null && u.Phone.Contains(term)) || EF.Functions.ILike(u.FullName, $"%{term}%"));
        }
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(u => u.Roles.Any(r => r.Role == role));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id).Skip((p - 1) * size).Take(size)
            .Select(u => new AdminUserListItem(u.Id, u.FullName, u.Email, u.Phone, u.Status, u.CreatedAt, u.LastLoginAt, u.Roles.Select(r => r.Role).ToList()))
            .ToListAsync(ct);
        return new(items, p, size, total);
    }

    [HttpGet("users/{id:guid}")]
    [Authorize(Policy = Policies.SupportOps)]
    public async Task<AdminUserDetailDto> UserDetail(Guid id, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("User");
        return new AdminUserDetailDto(
            new AdminUserListItem(u.Id, u.FullName, u.Email, u.Phone, u.Status, u.CreatedAt, u.LastLoginAt, u.Roles.Select(r => r.Role).ToList()),
            await db.Enrollments.Where(e => e.UserId == id).OrderByDescending(e => e.StartsAt)
                .Select(e => new AdminEnrollmentDto(e.CourseId, e.Course!.TitleEn, e.Source, e.StartsAt, e.ExpiresAt)).ToListAsync(ct),
            await OrdersQuery(userId: id, status: null).Take(100).ToListAsync(ct),
            await db.SupportTickets.Where(t => t.UserId == id).OrderByDescending(t => t.CreatedAt)
                .Select(t => new AdminTicketSummaryDto(t.Id, t.Subject, t.Status, t.CreatedAt)).ToListAsync(ct));
    }

    [HttpPost("users/{id:guid}/status")]
    [Authorize(Policy = Policies.SupportOps)]
    public async Task<IActionResult> SetStatus(Guid id, UserStatusRequest req, CancellationToken ct)
    {
        if (id == User.UserId()) throw AppException.BadRequest("self_change", "You cannot change the status of your own account.");
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
        if (id == User.UserId() && role == Roles.SuperAdmin)
            throw AppException.BadRequest("self_change", "You cannot remove your own SuperAdmin role.");
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
    public async Task<PagedResult<AdminOrderDto>> Orders([FromQuery] OrderStatus? status, [FromQuery] int? page, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, 50);
        var q = OrdersQuery(userId: null, status);
        var total = await q.CountAsync(ct);
        return new(await q.Skip((p - 1) * size).Take(size).ToListAsync(ct), p, size, total);
    }

    /// <summary>Filters are applied before projecting, since EF cannot translate predicates on a constructed DTO.</summary>
    private IQueryable<AdminOrderDto> OrdersQuery(Guid? userId, OrderStatus? status) =>
        from o in db.Orders.AsNoTracking()
        join u in db.Users on o.UserId equals u.Id
        join c in db.Courses on o.CourseId equals c.Id
        where (userId == null || o.UserId == userId) && (status == null || o.Status == status)
        orderby o.CreatedAt descending
        select new AdminOrderDto(o.Id, o.UserId, u.FullName, o.CourseId, c.TitleEn, o.AmountInPaise, o.Status, o.GatewayOrderId, o.Receipt, o.CreatedAt, o.PaidAt);

    [HttpGet("tickets")]
    [Authorize(Policy = Policies.SupportOps)]
    public Task<List<AdminTicketDto>> Tickets([FromQuery] string? status, CancellationToken ct) =>
        (from t in db.SupportTickets.AsNoTracking()
         join u in db.Users on t.UserId equals u.Id
         where status == null || t.Status == status
         orderby t.Priority == "High" descending, t.CreatedAt descending
         select new AdminTicketDto(t.Id, t.UserId, u.FullName, u.Email, u.Phone, t.Category, t.Priority, t.Subject, t.Message, t.Status, t.StaffResponse, t.CreatedAt, t.UpdatedAt))
        .Take(200).ToListAsync(ct);

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
    public Task<List<AdminQuestionReportDto>> Reports([FromQuery] string status = "Open", CancellationToken ct = default) =>
        (from r in db.QuestionReports.AsNoTracking()
         join q in db.Questions on r.QuestionId equals q.Id
         where r.Status == status
         orderby r.CreatedAt
         select new AdminQuestionReportDto(r.Id, r.QuestionId, q.StemEn, q.ExternalRef, r.Reason, r.Details, r.Status, r.CreatedAt))
        .Take(200).ToListAsync(ct);

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
    public async Task<AnnouncementResultDto> Announce(AnnouncementRequest req, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var users = req.CourseId is { } cid
            ? db.Enrollments.Where(e => e.CourseId == cid && (e.ExpiresAt == null || e.ExpiresAt > now)).Select(e => e.UserId)
            : db.Users.Where(u => u.Status == UserStatus.Active && u.Roles.Any(r => r.Role == Roles.Student)).Select(u => u.Id);
        var ids = await users.Distinct().ToListAsync(ct);
        db.Notifications.AddRange(ids.Select(id => new Notification { UserId = id, Type = "announcement", Title = req.Title, Body = req.Body, CreatedAt = now }));
        audit.Record(User.UserId(), "announcement.send", "Notification", null, new { req.Title, req.CourseId, Recipients = ids.Count });
        await db.SaveChangesAsync(ct);
        return new AnnouncementResultDto(ids.Count);
    }

    [HttpGet("imports")]
    [Authorize(Policy = Policies.ContentEdit)]
    public async Task<List<AdminImportJobDto>> Imports(CancellationToken ct) =>
        (await db.ContentImportJobs.AsNoTracking().Include(j => j.Errors).OrderByDescending(j => j.CreatedAt).Take(50).ToListAsync(ct))
        .Select(j => new AdminImportJobDto(j.Id, j.FileName, j.TotalRows, j.ImportedRows, j.FailedRows, j.CreatedAt,
            j.Errors.OrderBy(e => e.RowNumber).Select(e => new ImportErrorDto(e.RowNumber, e.Message)).ToList())).ToList();

    [HttpGet("audit-logs")]
    [Authorize(Policy = Policies.SuperAdmin)]
    public async Task<PagedResult<AdminAuditLogDto>> AuditLogs([FromQuery] string? entityType, [FromQuery] string? action, [FromQuery] int? page, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, 50);
        var q = from a in db.AuditLogs.AsNoTracking()
                join u in db.Users on a.ActorUserId equals u.Id into actors
                from u in actors.DefaultIfEmpty()
                where (entityType == null || a.EntityType == entityType) && (action == null || a.Action.StartsWith(action))
                orderby a.Id descending
                select new AdminAuditLogDto(a.Id, a.ActorUserId, u == null ? null : u.FullName, a.Action, a.EntityType, a.EntityId, a.DataJson, a.CreatedAt);
        var total = await q.CountAsync(ct);
        return new(await q.Skip((p - 1) * size).Take(size).ToListAsync(ct), p, size, total);
    }
}

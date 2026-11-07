using System.ComponentModel.DataAnnotations;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Admin;

public record AdminDoubtListItem(Guid Id, Guid UserId, string StudentName, string Text, DoubtStatus Status, DoubtContextDto Context, int Messages,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? AnsweredAt);
public record AdminDoubtDto(Guid Id, Guid UserId, string StudentName, string? StudentEmail, string? StudentPhone, string Text, DoubtStatus Status,
    DoubtContextDto Context, List<DoubtMessageDto> Messages, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? AnsweredAt);
/// <summary>A teacher's answer. <c>Resolve</c> also closes the doubt (the student can still follow up, which re-opens it).</summary>
public record AdminDoubtReplyRequest([Required, MinLength(1), MaxLength(DoubtRules.MaxLength)] string Text, bool Resolve = false);

/// <summary>Teachers answer students' doubts. Open doubts are listed oldest first so nobody waits longest.</summary>
[ApiController]
[Authorize(Policy = Policies.Doubts)]
[Route("api/v1/admin/doubts")]
public class AdminDoubtsController(AppDbContext db, AuditService audit, RequestLanguage lang, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<AdminDoubtListItem>> List([FromQuery] DoubtStatus? status, [FromQuery] Guid? courseId, [FromQuery] string? q,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, pageSize);
        var query = from d in db.Doubts.AsNoTracking()
                    join u in db.Users on d.UserId equals u.Id
                    where (status == null || d.Status == status) && (courseId == null || d.CourseId == courseId)
                          && (q == null || q == "" || EF.Functions.ILike(d.Text, "%" + q + "%") || EF.Functions.ILike(u.FullName, "%" + q + "%"))
                    select new { Doubt = d, u.FullName, Messages = d.Messages.Count };
        query = status == DoubtStatus.Open ? query.OrderBy(x => x.Doubt.UpdatedAt) : query.OrderByDescending(x => x.Doubt.UpdatedAt);
        var total = await query.CountAsync(ct);
        var rows = await query.Skip((p - 1) * size).Take(size).ToListAsync(ct);
        var contexts = await DoubtContexts.LoadAsync(db, lang, rows.Select(r => r.Doubt).ToList(), ct);
        return new(rows.Select(r => new AdminDoubtListItem(r.Doubt.Id, r.Doubt.UserId, r.FullName, r.Doubt.Text, r.Doubt.Status, contexts[r.Doubt.Id], r.Messages,
            r.Doubt.CreatedAt, r.Doubt.UpdatedAt, r.Doubt.AnsweredAt)).ToList(), p, size, total);
    }

    [HttpGet("{id:guid}")]
    public async Task<AdminDoubtDto> Get(Guid id, CancellationToken ct)
    {
        var row = await (from d in db.Doubts.AsNoTracking()
                         join u in db.Users on d.UserId equals u.Id
                         where d.Id == id
                         select new { Doubt = d, u.FullName, u.Email, u.Phone }).FirstOrDefaultAsync(ct) ?? throw AppException.NotFound("Doubt");
        var context = (await DoubtContexts.LoadAsync(db, lang, [row.Doubt], ct))[id];
        var messages = await DoubtContexts.MessagesAsync(db, id, ct);
        var d0 = row.Doubt;
        return new AdminDoubtDto(d0.Id, d0.UserId, row.FullName, row.Email, row.Phone, d0.Text, d0.Status, context, messages, d0.CreatedAt, d0.UpdatedAt, d0.AnsweredAt);
    }

    [HttpPost("{id:guid}/reply")]
    public async Task<AdminDoubtDto> Reply(Guid id, AdminDoubtReplyRequest req, CancellationToken ct)
    {
        var doubt = await db.Doubts.FindAsync([id], ct) ?? throw AppException.NotFound("Doubt");
        var now = clock.GetUtcNow();
        var staffId = User.UserId();
        db.DoubtMessages.Add(new DoubtMessage { DoubtId = id, AuthorId = staffId, FromStaff = true, Text = req.Text.Trim(), CreatedAt = now });
        (doubt.Status, doubt.UpdatedAt) = (req.Resolve ? DoubtStatus.Resolved : DoubtStatus.Answered, now);
        doubt.AnsweredAt ??= now;
        db.Notifications.Add(new Notification
        {
            UserId = doubt.UserId, Type = "doubt", Title = "Your doubt was answered", Body = Excerpt(doubt.Text), Link = $"/doubts/{id}", CreatedAt = now,
        });
        audit.Record(staffId, "doubt.reply", "Doubt", id, new { req.Resolve });
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    /// <summary>Closes a doubt without replying (e.g. a duplicate). The student can still follow up.</summary>
    [HttpPost("{id:guid}/resolve")]
    public async Task<AdminDoubtDto> Resolve(Guid id, CancellationToken ct)
    {
        var doubt = await db.Doubts.FindAsync([id], ct) ?? throw AppException.NotFound("Doubt");
        (doubt.Status, doubt.UpdatedAt) = (DoubtStatus.Resolved, clock.GetUtcNow());
        audit.Record(User.UserId(), "doubt.resolve", "Doubt", id);
        await db.SaveChangesAsync(ct);
        return await Get(id, ct);
    }

    private static string Excerpt(string s) => s.Length <= 120 ? s : s[..117] + "...";
}

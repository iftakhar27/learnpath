using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Catalog;

/// <summary>Single place that decides whether a student may use paid content.</summary>
public class EntitlementService(AppDbContext db, TimeProvider clock)
{
    public async Task<bool> HasCourseAccessAsync(Guid userId, Guid courseId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await db.Courses.AnyAsync(c => c.Id == courseId && c.IsFree && c.Status == PublishStatus.Published, ct)
               || await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId && e.StartsAt <= now && (e.ExpiresAt == null || e.ExpiresAt > now), ct);
    }

    public async Task<HashSet<Guid>> ActiveCourseIdsAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return (await db.Enrollments.Where(e => e.UserId == userId && e.StartsAt <= now && (e.ExpiresAt == null || e.ExpiresAt > now))
            .Select(e => e.CourseId).ToListAsync(ct)).ToHashSet();
    }

    public async Task EnsureResourceAccessAsync(Guid userId, LearningResource resource, Guid courseId, CancellationToken ct)
    {
        if (resource.IsFreePreview) return;
        if (!await HasCourseAccessAsync(userId, courseId, ct)) throw AppException.PaymentRequired();
    }

    public async Task EnsureTestAccessAsync(Guid userId, Test test, CancellationToken ct)
    {
        if (test.IsFree) return;
        if (test.CourseId is null || !await HasCourseAccessAsync(userId, test.CourseId.Value, ct))
            throw AppException.PaymentRequired("Purchase the course or test series to take this test.");
    }

    /// <summary>Creates or extends an enrollment. Idempotent for the same order.</summary>
    public async Task GrantAsync(Guid userId, Course course, EnrollmentSource source, Guid? orderId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var existing = await db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == course.Id, ct);
        if (existing is not null && orderId is not null && existing.OrderId == orderId) return;
        DateTimeOffset? Expiry(DateTimeOffset from) => course.AccessDays is null ? null : from.AddDays(course.AccessDays.Value);

        if (existing is null)
        {
            db.Enrollments.Add(new Enrollment { UserId = userId, CourseId = course.Id, Source = source, OrderId = orderId, StartsAt = now, ExpiresAt = Expiry(now) });
        }
        else
        {
            var from = existing.ExpiresAt is { } e && e > now ? e : now;
            existing.ExpiresAt = existing.ExpiresAt is null && existing.Source != EnrollmentSource.Free ? null : Expiry(from);
            existing.Source = source;
            existing.OrderId = orderId ?? existing.OrderId;
        }
    }
}

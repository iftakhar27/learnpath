using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LearnPath.Api.Data;
using LearnPath.Api.Domain;
using LearnPath.Api.Infrastructure;
using LearnPath.Api.Modules.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LearnPath.Api.Modules.Commerce;

public record CreateOrderRequest([Required] Guid CourseId);
public record CheckoutDto(Guid OrderId, string GatewayOrderId, string KeyId, long AmountInPaise, string Currency, string CourseTitle, string? PrefillEmail, string? PrefillPhone);
public record VerifyPaymentRequest([Required] string GatewayOrderId, [Required] string GatewayPaymentId, [Required] string Signature);
public record OrderDto(Guid OrderId, Guid CourseId, string CourseTitle, long AmountInPaise, string Currency, string Status, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt);
public record RefundRequest([Required, MaxLength(2000)] string Reason);

public class CommerceService(AppDbContext db, EntitlementService entitlements, TimeProvider clock, ILogger<CommerceService> logger)
{
    public enum PaidResult { Granted, AlreadyPaid, AmountMismatch, UnknownOrder }

    /// <summary>
    /// Marks an order paid and grants access exactly once, regardless of how many times the checkout callback
    /// or webhook is delivered. Serialised by a row lock on the order.
    /// </summary>
    public async Task<PaidResult> MarkPaidAsync(string gatewayOrderId, string gatewayPaymentId, long amountInPaise, string? method, string verifiedVia, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Orders\" WHERE \"GatewayOrderId\" = {gatewayOrderId} FOR UPDATE", ct);
        var order = await db.Orders.FirstOrDefaultAsync(o => o.GatewayOrderId == gatewayOrderId, ct);
        if (order is null) return PaidResult.UnknownOrder;
        await db.Entry(order).ReloadAsync(ct);
        if (order.Status == OrderStatus.Paid) return PaidResult.AlreadyPaid;
        if (amountInPaise != order.AmountInPaise)
        {
            logger.LogError("Payment {PaymentId} amount {Amount} does not match order {OrderId} amount {Expected}", gatewayPaymentId, amountInPaise, order.Id, order.AmountInPaise);
            return PaidResult.AmountMismatch;
        }

        var now = clock.GetUtcNow();
        order.Status = OrderStatus.Paid;
        order.PaidAt = now;
        if (!await db.Payments.AnyAsync(p => p.GatewayPaymentId == gatewayPaymentId, ct))
            db.Payments.Add(new Payment { OrderId = order.Id, GatewayPaymentId = gatewayPaymentId, Status = "captured", AmountInPaise = amountInPaise, Method = method, VerifiedVia = verifiedVia, RecordedAt = now });
        var course = await db.Courses.FirstAsync(c => c.Id == order.CourseId, ct);
        await entitlements.GrantAsync(order.UserId, course, EnrollmentSource.Purchase, order.Id, ct);
        db.Notifications.Add(new Notification { UserId = order.UserId, Type = "payment", Title = "Payment successful", Body = $"You now have access to {course.TitleEn}.", Link = $"/courses/{course.Id}", CreatedAt = now });
        db.LearningEvents.Add(new LearningEvent { UserId = order.UserId, EventType = LearningEventTypes.CoursePurchased, EntityId = course.Id, OccurredAt = now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return PaidResult.Granted;
    }

    public async Task MarkFailedAsync(string gatewayOrderId, CancellationToken ct) =>
        await db.Orders.Where(o => o.GatewayOrderId == gatewayOrderId && o.Status == OrderStatus.Created)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Failed), ct);
}

[ApiController]
[Route("api/v1/payments")]
public class PaymentsController(AppDbContext db, IPaymentGateway gateway, CommerceService commerce, TimeProvider clock, ILogger<PaymentsController> logger) : ControllerBase
{
    [Authorize]
    [HttpPost("orders")]
    public async Task<CheckoutDto> CreateOrder(CreateOrderRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CourseId && c.Status == PublishStatus.Published, ct) ?? throw AppException.NotFound("Course");
        if (course.IsFree || course.PriceInPaise <= 0) throw AppException.BadRequest("course_free", "This course is free. Enroll directly.");
        var now = clock.GetUtcNow();
        if (await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == course.Id && e.Source != EnrollmentSource.Free && e.ExpiresAt == null, ct))
            throw AppException.Conflict("already_owned", "You already own this course.");

        var receipt = "rcpt_" + Guid.NewGuid().ToString("N")[..20];
        var go = await gateway.CreateOrderAsync(course.PriceInPaise, receipt, ct);
        var order = new Order { UserId = userId, CourseId = course.Id, AmountInPaise = course.PriceInPaise, GatewayOrderId = go.GatewayOrderId, Receipt = receipt, Status = OrderStatus.Created, CreatedAt = now };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        return new CheckoutDto(order.Id, go.GatewayOrderId, gateway.PublicKeyId, order.AmountInPaise, order.Currency, course.TitleEn, user.Email, user.Phone);
    }

    /// <summary>Called by the client after Razorpay Checkout returns. Access is granted only if the HMAC signature verifies.</summary>
    [Authorize]
    [HttpPost("verify")]
    public async Task<OrderDto> Verify(VerifyPaymentRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.GatewayOrderId == req.GatewayOrderId && o.UserId == userId, ct) ?? throw AppException.NotFound("Order");
        if (!gateway.VerifyCheckoutSignature(req.GatewayOrderId, req.GatewayPaymentId, req.Signature))
            throw AppException.BadRequest("signature_invalid", "Payment could not be verified. If money was deducted, it will be confirmed automatically or refunded.");
        await commerce.MarkPaidAsync(req.GatewayOrderId, req.GatewayPaymentId, order.AmountInPaise, null, "checkout_signature", ct);
        return await LoadOrder(order.Id, ct);
    }

    /// <summary>Razorpay webhook. Signature-verified, de-duplicated by event id, and safe to replay.</summary>
    [AllowAnonymous]
    [HttpPost("webhooks")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["X-Razorpay-Signature"].ToString();
        if (string.IsNullOrEmpty(signature) || !gateway.VerifyWebhookSignature(body, signature))
        {
            logger.LogWarning("Rejected webhook with invalid signature");
            return Unauthorized();
        }
        var eventId = Request.Headers["X-Razorpay-Event-Id"].ToString();
        if (string.IsNullOrEmpty(eventId)) eventId = "sha256:" + Hashing.Sha256(body);

        using var doc = JsonDocument.Parse(body);
        var type = doc.RootElement.TryGetProperty("event", out var ev) ? ev.GetString() ?? "" : "";
        var recordId = Guid.NewGuid();
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ""PaymentWebhookEvents"" (""Id"", ""EventId"", ""EventType"", ""Payload"", ""ReceivedAt"")
            VALUES ({recordId}, {eventId}, {type}, CAST({body} AS jsonb), {clock.GetUtcNow()})
            ON CONFLICT (""EventId"") DO NOTHING", ct);
        if (inserted == 0) return Ok(new { status = "duplicate" });

        string result;
        if (doc.RootElement.TryGetProperty("payload", out var payload) && payload.TryGetProperty("payment", out var pay) && pay.TryGetProperty("entity", out var entity))
        {
            var orderId = entity.GetProperty("order_id").GetString() ?? "";
            var paymentId = entity.GetProperty("id").GetString() ?? "";
            var amount = entity.GetProperty("amount").GetInt64();
            var method = entity.TryGetProperty("method", out var m) ? m.GetString() : null;
            switch (type)
            {
                case "payment.captured":
                case "order.paid":
                    result = (await commerce.MarkPaidAsync(orderId, paymentId, amount, method, "webhook", ct)).ToString();
                    break;
                case "payment.failed":
                    await commerce.MarkFailedAsync(orderId, ct);
                    result = "MarkedFailed";
                    break;
                default:
                    result = "Ignored";
                    break;
            }
        }
        else result = "NoPaymentEntity";

        db.ChangeTracker.Clear();
        await db.PaymentWebhookEvents.Where(e => e.Id == recordId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.ProcessedAt, clock.GetUtcNow()).SetProperty(e => e.ProcessingResult, result), ct);
        return Ok(new { status = result });
    }

    [Authorize]
    [HttpGet("orders")]
    public async Task<List<OrderDto>> Orders(CancellationToken ct)
    {
        var userId = User.UserId();
        var ids = await db.Orders.Where(o => o.UserId == userId).OrderByDescending(o => o.CreatedAt).Select(o => o.Id).Take(100).ToListAsync(ct);
        var list = new List<OrderDto>();
        foreach (var id in ids) list.Add(await LoadOrder(id, ct));
        return list;
    }

    [Authorize]
    [HttpPost("orders/{orderId:guid}/refund-request")]
    public async Task<IActionResult> RequestRefund(Guid orderId, RefundRequest req, CancellationToken ct)
    {
        var userId = User.UserId();
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct) ?? throw AppException.NotFound("Order");
        if (order.Status != OrderStatus.Paid) throw AppException.Conflict("not_refundable", "Only paid orders can be refunded.");
        db.SupportTickets.Add(new SupportTicket { UserId = userId, Category = "refund", Priority = "High", Subject = $"Refund request for order {order.Receipt}", Message = req.Reason, CreatedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct);
        return Accepted();
    }

    private async Task<OrderDto> LoadOrder(Guid id, CancellationToken ct) =>
        await (from o in db.Orders.AsNoTracking()
               join c in db.Courses on o.CourseId equals c.Id
               where o.Id == id
               select new OrderDto(o.Id, o.CourseId, c.TitleEn, o.AmountInPaise, o.Currency, o.Status.ToString(), o.CreatedAt, o.PaidAt)).FirstAsync(ct);
}

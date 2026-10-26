namespace LearnPath.Api.Domain;

public enum OrderStatus { Created, Paid, Failed, Refunded }

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CourseId { get; set; }
    public long AmountInPaise { get; set; }
    public string Currency { get; set; } = "INR";
    public OrderStatus Status { get; set; }
    public string GatewayOrderId { get; set; } = "";
    public string Receipt { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public uint RowVersion { get; set; }
}

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string GatewayPaymentId { get; set; } = "";
    public string Status { get; set; } = "";       // captured, failed
    public long AmountInPaise { get; set; }
    public string? Method { get; set; }
    public string VerifiedVia { get; set; } = "";  // checkout_signature | webhook
    public DateTimeOffset RecordedAt { get; set; }
}

public class PaymentWebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventId { get; set; } = "";     // X-Razorpay-Event-Id, unique
    public string EventType { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? ProcessingResult { get; set; }
}

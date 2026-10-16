namespace NutriCare.Domain.Entities;

/// <summary>A cancellation decision and durable work record. Completion requires confirmed provider refunds.</summary>
public sealed class CancellationRefundInstruction : BaseEntity
{
    public long AppointmentId { get; set; }
    public long CancelledByUserId { get; set; }
    public string CancellationActor { get; set; } = string.Empty;
    public DateTime CancelledAtUtc { get; set; }
    public long? PolicyId { get; set; }
    public int? PolicyVersion { get; set; }
    public string PolicySnapshotHash { get; set; } = string.Empty;
    public bool? Eligible { get; set; }
    public string DecisionReason { get; set; } = string.Empty;
    public decimal? ExpectedAmount { get; set; }
    public string Currency { get; set; } = "PKR";
    public long? PaymentId { get; set; }
    public long? PaymentRefundId { get; set; }
    public string Status { get; set; } = "Queued";
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LeaseToken { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public string? LastErrorCode { get; set; }
}

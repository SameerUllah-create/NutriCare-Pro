namespace NutriCare.Domain.Entities;

public sealed class PackageEnrollment : BaseEntity
{
    public long ServicePackageId { get; set; }
    public long PatientId { get; set; }
    public long PaymentId { get; set; }
    public int PackageVersion { get; set; }
    public string DefinitionSnapshotJson { get; set; } = string.Empty;
    public DateTime PurchasedAtUtc { get; set; }
    public DateTime FirstConsultationDueAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string Status { get; set; } = "AwaitingFirstConsultation";
}

public sealed class PackageCreditUsage : BaseEntity
{
    public long PackageEnrollmentId { get; set; }
    public string CreditKind { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public long ResourceId { get; set; }
    public string Status { get; set; } = "Reserved";
    public string? ServiceCode { get; set; }
}

public sealed class PackageCreditHistory : BaseEntity
{
    public long PackageEnrollmentId { get; set; }
    public long? PackageCreditUsageId { get; set; }
    public long ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime? PreviousExpiryUtc { get; set; }
    public DateTime? NewExpiryUtc { get; set; }
}

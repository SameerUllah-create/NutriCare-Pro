namespace NutriCare.Domain.Entities;

public class Service : BaseEntity
{
    public long? NutritionistId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameUrdu { get; set; }
    public string? Description { get; set; }
    public string? DescriptionUrdu { get; set; }
    public int DurationMinutes { get; set; }
    public int? DeliveryHours { get; set; }
    public string Mode { get; set; } = "Online";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "PKR";
    public bool IsActive { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int BookingBufferMinutes { get; set; } = 15;
    public string? PackageServiceCode { get; set; }
    public int Version { get; set; } = 1;
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class ServicePackage : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameUrdu { get; set; }
    public int DurationWeeks { get; set; }
    public int DurationDays { get; set; }
    public int InitialConsultationCount { get; set; }
    public int InitialConsultationMinutes { get; set; }
    public int FollowUpConsultationCount { get; set; }
    public int FollowUpConsultationMinutes { get; set; }
    public int DietPlanCount { get; set; }
    public int DietPlanRevisionCount { get; set; }
    public string ProgressTrackingFrequency { get; set; } = "";
    public int CreditExpiryDays { get; set; }
    public int FirstConsultationWithinDays { get; set; }
    public string[] EligibleServiceCodes { get; set; } = [];
    public string ActivationRule { get; set; } = "";
    public string NoShowCreditRule { get; set; } = "";
    public bool RestoreCreditsOnProviderCancellation { get; set; }
    public long? NutritionistApprovedById { get; set; }
    public DateTime? NutritionistApprovedAt { get; set; }
    public int? NutritionistApprovedVersion { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "PKR";
    public bool IsPublished { get; set; }
    public bool IsActive { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int Version { get; set; } = 1;
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class AvailabilityRule : BaseEntity
{
    public long NutritionistId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    public bool IsPublished { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int BookingBufferMinutes { get; set; } = 15;
    public int Version { get; set; } = 1;
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class AvailabilityException : BaseEntity
{
    public long NutritionistId { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public bool IsAvailable { get; set; }
    public string? Reason { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class CancellationPolicy : BaseEntity
{
    public string Name { get; set; } = "Proposed cancellation policy";
    public string Status { get; set; } = "Draft";
    public bool IsPublished { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int FullRefundHours { get; set; } = 24;
    public int FreeRescheduleHours { get; set; } = 6;
    public int MaxFreeReschedules { get; set; } = 1;
    public bool ProviderCancellationRefund { get; set; } = true;
    public bool NoShowAutomaticRefund { get; set; }
    public string PackageNoShowCreditAction { get; set; } = "ReviewRequired";
    public string PackagePatientCancellationCreditAction { get; set; } = "ReviewRequired";
    public int Version { get; set; } = 1;
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class ClinicalAssignment : BaseEntity
{
    public long PatientId { get; set; }
    public long NutritionistId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
}

public class AppointmentHistory : BaseEntity
{
    public long AppointmentId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public DateTime? PreviousStartAt { get; set; }
    public DateTime? NewStartAt { get; set; }
    public long ChangedByUserId { get; set; }
    public string? Reason { get; set; }
}

public class ConsentRecord : BaseEntity
{
    public string? PolicyTitle { get; set; }
    public string? ConsentTextUrdu { get; set; }
    public string PresentedLanguage { get; set; } = "English";
    public long UserId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = string.Empty;
    public string ConsentText { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? WithdrawnAt { get; set; }
    public string? IPAddress { get; set; }
}

public class PolicyDocument : BaseEntity
{
    public string Purpose { get; set; } = string.Empty;
    public string Version { get; set; } = "1";
    public string Title { get; set; } = string.Empty;
    public string? TitleUrdu { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ContentUrdu { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class IntakeForm : BaseEntity
{
    public long PatientId { get; set; }
    public string Goals { get; set; } = string.Empty;
    public string? MedicalHistory { get; set; }
    public string? Allergies { get; set; }
    public string? CurrentMedications { get; set; }
    public string? Lifestyle { get; set; }
    public long ConsentRecordId { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
}

public class DietPlanVersion : BaseEntity
{
    public long DietPlanId { get; set; }
    public int VersionNumber { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public long CreatedByUserId { get; set; }
    public string? ChangeReason { get; set; }
}

public class ConsultationNote : BaseEntity
{
    public long AppointmentId { get; set; }
    public long AuthorUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public long? SupersedesId { get; set; }
}

public class MedicalReportNote : BaseEntity
{
    public long MedicalReportId { get; set; }
    public long AuthorUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public long? SupersedesId { get; set; }
}

public class ProgressRevision : BaseEntity
{
    public long WeightTrackingId { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public long ChangedByUserId { get; set; }
    public string? Reason { get; set; }
}

public class ConfigurationRevision : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public int Version { get; set; }
    public string Action { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public long ChangedByUserId { get; set; }
}

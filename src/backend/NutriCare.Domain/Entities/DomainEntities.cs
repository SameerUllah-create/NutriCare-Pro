namespace NutriCare.Domain.Entities;

using Microsoft.AspNetCore.Identity;

/// <summary>
/// User entity representing all system users
/// </summary>
public class User : IdentityUser<long>
{
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool SmsNotificationsEnabled { get; set; }
    public bool InAppNotificationsEnabled { get; set; } = true;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    // Refresh tokens are stored as SHA-256 hashes, never as raw tokens.
    public string? RefreshTokenHash { get; set; }
    public DateTime? RefreshTokenExpiryDate { get; set; }

    // Navigation properties
    public virtual Patient? Patient { get; set; }
    public virtual Nutritionist? Nutritionist { get; set; }

    public bool IsDeleted => DeletedAt.HasValue;
}

/// <summary>
/// Role entity representing user roles
/// </summary>
public class Role : IdentityRole<long>
{
    public Role() { }

    public Role(string roleName) : base(roleName) { }

    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public bool IsDeleted => DeletedAt.HasValue;
}

/// <summary>
/// Patient entity
/// </summary>
public class Patient : BaseEntity
{
    public long UserId { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? BloodGroup { get; set; }
    public decimal Height { get; set; } // in cm
    public decimal CurrentWeight { get; set; } // in kg
    public decimal? TargetWeight { get; set; }
    public string? MedicalHistory { get; set; }
    public string? Allergies { get; set; }
    public string? CurrentMedications { get; set; }
    public string? EmergencyContact { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public string PreferredLanguage { get; set; } = "English";
    public string? NotificationPreference { get; set; }
    public string? SelectedMetricsJson { get; set; }

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public virtual ICollection<DietPlan> DietPlans { get; set; } = new List<DietPlan>();
    public virtual ICollection<WeightTracking> WeightTrackings { get; set; } = new List<WeightTracking>();
    public virtual ICollection<MedicalReport> MedicalReports { get; set; } = new List<MedicalReport>();
}

/// <summary>
/// Nutritionist entity
/// </summary>
public class Nutritionist : BaseEntity
{
    public long UserId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string? Qualification { get; set; }
    public string? Specializations { get; set; } // JSON array
    public int Experience { get; set; } // years
    public string? Bio { get; set; }
    public decimal ConsultationFee { get; set; }
    public int ConsultationDuration { get; set; } = 30; // minutes
    public bool IsVerified { get; set; }
    public DateTime? VerificationDate { get; set; }
    public string? VerificationDocumentUrl { get; set; }
    public int MaxPatientsPerDay { get; set; } = 10;
    public string? AvailableHours { get; set; } // JSON
    public string Timezone { get; set; } = "Asia/Karachi";
    public decimal RatingAverage { get; set; } = 0;
    public int TotalRatings { get; set; } = 0;
    public int TotalPatientsServed { get; set; } = 0;

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public virtual ICollection<DietPlan> DietPlans { get; set; } = new List<DietPlan>();
}

/// <summary>
/// Appointment entity
/// </summary>
public class Appointment : BaseEntity
{
    public long? ServiceId { get; set; }
    public long? IntakeFormId { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    public DateTime? PreviousStartAt { get; set; }
    public int RescheduleCount { get; set; }
    public string? IdempotencyKey { get; set; }
    public decimal? PriceSnapshot { get; set; }
    // Preserve the service/calendar buffer agreed at booking. Null identifies legacy records.
    public int? BookingBufferMinutesSnapshot { get; set; }
    public long? PackageEnrollmentId { get; set; }
    public string? PackageCreditKind { get; set; }
    public string Currency { get; set; } = "PKR";
    public bool IsPaid { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";
    public long? CancellationPolicyId { get; set; }
    public int? CancellationPolicyVersion { get; set; }
    public string? CancellationPolicySnapshotJson { get; set; }
    public long PatientId { get; set; }
    public long NutritionistId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public int Duration { get; set; } = 30; // minutes
    public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed, Cancelled, NoShow
    public string AppointmentType { get; set; } = "Initial"; // Initial, Follow-up, Video, In-person
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public string? ConsultationNotes { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancelledBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public long? ApprovedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public long? RejectedByUserId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? CompletedByUserId { get; set; }
    public long? RescheduledFromId { get; set; }
    public DateTime? RescheduledAt { get; set; }
    public long? RescheduledByUserId { get; set; }

    // Navigation properties
    public virtual Patient? Patient { get; set; }
    public virtual Nutritionist? Nutritionist { get; set; }
    public virtual VideoConsultation? VideoConsultation { get; set; }
}

/// <summary>
/// Diet Plan entity
/// </summary>
public class DietPlan : BaseEntity
{
    public DateTime? ReviewDate { get; set; }
    public DateTime? PublishedAt { get; set; }
    public long? PublishedVersionId { get; set; }
    public long PatientId { get; set; }
    public long NutritionistId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Objective { get; set; } // Weight Loss, Muscle Gain, etc.
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Duration { get; set; } // days
    public string Status { get; set; } = "Active"; // Active, Completed, Archived, OnHold
    public int DailyCalories { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatsGrams { get; set; }
    public decimal? FiberGrams { get; set; }
    public int? SodiumMg { get; set; }
    public decimal? WaterIntakeLiters { get; set; }
    public string? Supplements { get; set; }
    public string? Instructions { get; set; }
    public int? MealFrequency { get; set; }
    public bool IsTemplate { get; set; }
    public int Version { get; set; } = 1;
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Patient? Patient { get; set; }
    public virtual Nutritionist? Nutritionist { get; set; }
    public virtual ICollection<DietPlanItem> Items { get; set; } = new List<DietPlanItem>();
    public virtual ICollection<DietPlanCompliance> ComplianceRecords { get; set; } = new List<DietPlanCompliance>();
}

/// <summary>
/// Diet Plan Item entity
/// </summary>
public class DietPlanItem : BaseEntity
{
    public long DietPlanId { get; set; }
    public int DayNumber { get; set; }
    public string MealType { get; set; } = string.Empty; // Breakfast, Lunch, Dinner, Snack1, Snack2
    public TimeOnly? MealTime { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "grams"; // grams, ml, cup, tbsp
    public int Calories { get; set; }
    public decimal? Protein { get; set; } // grams
    public decimal? Carbs { get; set; }
    public decimal? Fats { get; set; }
    public decimal? Fiber { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public virtual DietPlan? DietPlan { get; set; }
}

/// <summary>
/// Diet plan compliance tracking entry.
/// </summary>
public class DietPlanCompliance : BaseEntity
{
    public long DietPlanId { get; set; }
    public long PatientId { get; set; }
    public DateTime RecordDate { get; set; }
    public int MealsCompleted { get; set; }
    public int TotalMeals { get; set; }
    public decimal WaterIntakeLiters { get; set; }
    public bool TookSupplements { get; set; }
    public string? Notes { get; set; }

    public decimal CompliancePercentage => TotalMeals <= 0
        ? 0
        : Math.Round((decimal)MealsCompleted / TotalMeals * 100, 2);

    public virtual DietPlan? DietPlan { get; set; }
    public virtual Patient? Patient { get; set; }
}

/// <summary>
/// Weight Tracking entity
/// </summary>
public class WeightTracking : BaseEntity
{
    public bool BMIIsCalculated { get; set; }
    public long PatientId { get; set; }
    public DateTime RecordDate { get; set; }
    public decimal Weight { get; set; } // kg
    public decimal? BMI { get; set; }
    public decimal? WaistCircumference { get; set; } // cm
    public decimal? ChestCircumference { get; set; }
    public decimal? HipCircumference { get; set; }
    public decimal? ArmCircumference { get; set; }
    public decimal? ThighCircumference { get; set; }
    public decimal? BodyFatPercentage { get; set; }
    public decimal? MusclePercentage { get; set; }
    public decimal? WaterPercentage { get; set; }
    public decimal? WaterIntakeLiters { get; set; }
    public int? DailyCalories { get; set; }
    public int? ExerciseMinutes { get; set; }
    public string? ExerciseType { get; set; }
    public decimal? BoneMassKg { get; set; }
    public int? MetabolicRate { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Patient? Patient { get; set; }
}

/// <summary>
/// Medical report metadata stored in SQL while file bytes live in Azure Blob Storage.
/// </summary>
public class MedicalReport : BaseEntity
{
    public long PatientId { get; set; }
    public long UploadedByUserId { get; set; }
    public string ReportType { get; set; } = "LabReport";
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string BlobContainer { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string? PatientNotes { get; set; }
    public string? NutritionistNotes { get; set; }
    public long? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Uploaded";

    public virtual Patient? Patient { get; set; }
}

/// <summary>
/// Payment entity
/// </summary>
public class Payment : BaseEntity
{
    public string? ProviderSessionId { get; set; }
    public string? IdempotencyKey { get; set; }
    public long PatientId { get; set; }
    public long? NutritionistId { get; set; }
    public long? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentMethod { get; set; } = string.Empty; // Stripe, JazzCash, EasyPaisa
    public string TransactionId { get; set; } = string.Empty;
    public string? ProviderPaymentId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Completed, Failed, Refunded
    public string? Description { get; set; }
    public string? RelatedToType { get; set; } // Appointment, DietPlan
    public long? RelatedToId { get; set; }
    public string? GatewayResponse { get; set; } // JSON
    public string? FailureReason { get; set; }
    public DateTime? PaidAt { get; set; }
    public decimal RefundedAmount { get; set; } = 0;
    public string? RefundReason { get; set; }
    public DateTime? RefundedAt { get; set; }

    // Navigation properties
    public virtual Patient? Patient { get; set; }
    public virtual Nutritionist? Nutritionist { get; set; }
    public virtual Invoice? Invoice { get; set; }
    public virtual ICollection<PaymentRefund> Refunds { get; set; } = new List<PaymentRefund>();
}

public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public long PatientId { get; set; }
    public long? NutritionistId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Issued";
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Description { get; set; }
    public string? RelatedToType { get; set; }
    public long? RelatedToId { get; set; }

    public virtual Patient? Patient { get; set; }
    public virtual Nutritionist? Nutritionist { get; set; }
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class PaymentRefund : BaseEntity
{
    public DateTime? ProviderRequestStartedAt { get; set; }
    public string? ProcessingToken { get; set; }
    public DateTime? ProcessingUntil { get; set; }
    public string? LastErrorCode { get; set; }
    public string? IdempotencyKey { get; set; }
    public long PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Pending";
    public string? Reason { get; set; }
    public string? ProviderRefundId { get; set; }
    public string? GatewayResponse { get; set; }
    public DateTime? RefundedAt { get; set; }

    public virtual Payment? Payment { get; set; }
}

/// <summary>
/// Video Consultation entity
/// </summary>
public class VideoConsultation : BaseEntity
{
    public string ProviderSyncStatus { get; set; } = "Current";
    public int ProviderSyncAttempts { get; set; }
    public DateTime? ProviderSyncNextAttemptAt { get; set; }
    public long AppointmentId { get; set; }
    public string Provider { get; set; } = "GoogleMeet";
    public string? ProviderMeetingId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string? MeetingUrl { get; set; }
    public string? HostUrl { get; set; }
    public string? JoinToken { get; set; }
    public DateTime ScheduledStartTime { get; set; }
    public DateTime ScheduledEndTime { get; set; }
    public DateTime? CallStartTime { get; set; }
    public DateTime? CallEndTime { get; set; }
    public int? Duration { get; set; } // seconds
    public string? RecordingUrl { get; set; }
    public string? RecordingStatus { get; set; } // NotRecorded, Recording, Completed, Failed
    public string Status { get; set; } = "Scheduled";
    public long CreatedByUserId { get; set; }
    public string? ProviderResponse { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Appointment? Appointment { get; set; }
}

/// <summary>
/// Message entity for user communication
/// </summary>
public class Message : BaseEntity
{
    public string? ClientMessageId { get; set; }
    public long SenderId { get; set; }
    public long ReceiverId { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string? Attachments { get; set; } // JSON array
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }
    public long? AttachmentFileSizeBytes { get; set; }
    public string? AttachmentBlobContainer { get; set; }
    public string? AttachmentBlobName { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public bool IsArchived { get; set; }
    public string MessageType { get; set; } = "Text"; // Text, Notification, Prescription
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent

    public virtual User? Sender { get; set; }
    public virtual User? Receiver { get; set; }
}

/// <summary>
/// Notification entity
/// </summary>
public class Notification : BaseEntity
{
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public string? ProviderMessageId { get; set; }
    public string DeliveryStatus { get; set; } = "Pending";
    public string? EventKey { get; set; }
    public long UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? RelatedToType { get; set; }
    public long? RelatedToId { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string Channel { get; set; } = "InApp"; // InApp, Email, SMS, Push
    public string Status { get; set; } = "Pending"; // Pending, Sent, Failed
    public DateTime? SentAt { get; set; }

    // Navigation properties
    public virtual User? User { get; set; }
}

/// <summary>
/// Blog article managed by administrators and nutritionists.
/// </summary>
public class Blog : BaseEntity
{
    public long AuthorUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string Category { get; set; } = "General";
    public string Tags { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime? PublishedAt { get; set; }

    public virtual User? Author { get; set; }
}

/// <summary>
/// Review entity for nutritionists
/// </summary>
public class Review : BaseEntity
{
    public long NutritionistId { get; set; }
    public long PatientId { get; set; }
    public long? AppointmentId { get; set; }
    public int Rating { get; set; } // 1-5
    public string? Title { get; set; }
    public string? ReviewText { get; set; }
    public int? Professionalism { get; set; }
    public int? Knowledge { get; set; }
    public int? CommunicationSkills { get; set; }
    public int? TimelinessRating { get; set; }
    public int? ValueForMoney { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsVerifiedPurchase { get; set; } = true;
    public int HelpfulCount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

    // Navigation properties
    public virtual Nutritionist? Nutritionist { get; set; }
    public virtual Patient? Patient { get; set; }
}

/// <summary>
/// Audit log entity for compliance
/// </summary>
public class AuditLog : BaseEntity
{
    public long? UserId { get; set; }
    public string Action { get; set; } = string.Empty; // Create, Read, Update, Delete, Login, Logout
    public string EntityType { get; set; } = string.Empty; // Users, Appointments, etc.
    public long EntityId { get; set; }
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public int? StatusCode { get; set; }
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public virtual User? User { get; set; }
}

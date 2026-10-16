namespace NutriCare.Application.DTOs;

/// <summary>
/// Base DTO response
/// </summary>
public class BaseDto
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// User DTO
/// </summary>
public class UserDto : BaseDto
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// Login request DTO
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Login response DTO
/// </summary>
public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public UserDto? User { get; set; }
}

/// <summary>
/// Register request DTO
/// </summary>
public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string Role { get; set; } = "Patient";
}

/// <summary>
/// Refresh token request DTO
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// Refresh token response DTO
/// </summary>
public class RefreshTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class VerifyEmailRequest
{
    public long UserId { get; set; }
    public string Token { get; set; } = string.Empty;
}

public class ResendEmailVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}

public class RoleRequest
{
    public string Role { get; set; } = string.Empty;
}

/// <summary>
/// Patient DTO
/// </summary>
public class PatientDto : BaseDto
{
    public long UserId { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? BloodGroup { get; set; }
    public decimal Height { get; set; }
    public decimal CurrentWeight { get; set; }
    public decimal? TargetWeight { get; set; }
    public string? MedicalHistory { get; set; }
    public string? Allergies { get; set; }
    public UserDto? User { get; set; }
}

/// <summary>
/// Create patient DTO
/// </summary>
public class CreatePatientRequest
{
    public DateTime DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? BloodGroup { get; set; }
    public decimal Height { get; set; }
    public decimal CurrentWeight { get; set; }
    public decimal? TargetWeight { get; set; }
}

/// <summary>
/// Update patient DTO
/// </summary>
public class UpdatePatientRequest
{
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? BloodGroup { get; set; }
    public decimal? Height { get; set; }
    public decimal? CurrentWeight { get; set; }
    public decimal? TargetWeight { get; set; }
    public string? MedicalHistory { get; set; }
    public string? Allergies { get; set; }
}

/// <summary>
/// Nutritionist DTO
/// </summary>
public class NutritionistDto : BaseDto
{
    public long UserId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string? Qualification { get; set; }
    public string? Specializations { get; set; }
    public int Experience { get; set; }
    public string? Bio { get; set; }
    public decimal ConsultationFee { get; set; }
    public bool IsVerified { get; set; }
    public decimal RatingAverage { get; set; }
    public int TotalRatings { get; set; }
    public UserDto? User { get; set; }
}

/// <summary>
/// Appointment DTO
/// </summary>
public class AppointmentDto : BaseDto
{
    public long? PackageEnrollmentId { get; set; }
    public string? PackageCreditKind { get; set; }
    public decimal? PriceSnapshot { get; set; }
    public string Currency { get; set; } = "PKR";
    public bool IsPaid { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";
    public long? CancellationPolicyId { get; set; }
    public int? CancellationPolicyVersion { get; set; }
    public string? CancellationPolicySnapshotJson { get; set; }
    public long? ServiceId { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    public int RescheduleCount { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public long PatientId { get; set; }
    public long NutritionistId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public int Duration { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AppointmentType { get; set; } = string.Empty;
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
    public PatientDto? Patient { get; set; }
    public NutritionistDto? Nutritionist { get; set; }
}

/// <summary>
/// Create appointment DTO
/// </summary>
public class CreateAppointmentRequest
{
    public long? PackageEnrollmentId { get; set; }
    public string? PackageCreditKind { get; set; }
    public long? ServiceId { get; set; }
    public long? IntakeFormId { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    public string? IdempotencyKey { get; set; }
    public long NutritionistId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public string AppointmentType { get; set; } = "Video";
    public string? Notes { get; set; }
}

public class RescheduleAppointmentRequest
{
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    public DateTime AppointmentDateTime { get; set; }
    public string? Notes { get; set; }
}

public class CancelAppointmentRequest
{
    public string? CancellationReason { get; set; }
}

public class RejectAppointmentRequest
{
    public string RejectionReason { get; set; } = string.Empty;
}

public class CompleteAppointmentRequest
{
    public string? ConsultationNotes { get; set; }
}

public class ConsultationNotesRequest
{
    public string ConsultationNotes { get; set; } = string.Empty;
}

public class DietPlanDto : BaseDto
{
    public int Version { get; set; }
    public DateTime? ReviewDate { get; set; }
    public DateTime? PublishedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public long PatientId { get; set; }
    public long NutritionistId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Objective { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Duration { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DailyCalories { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatsGrams { get; set; }
    public decimal? FiberGrams { get; set; }
    public decimal? WaterIntakeLiters { get; set; }
    public string? Supplements { get; set; }
    public string? Instructions { get; set; }
    public string? Notes { get; set; }
    public IEnumerable<DietPlanItemDto> Meals { get; set; } = new List<DietPlanItemDto>();
}

public class DietPlanItemDto : BaseDto
{
    public long DietPlanId { get; set; }
    public int DayNumber { get; set; }
    public string MealType { get; set; } = string.Empty;
    public TimeOnly? MealTime { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "grams";
    public int Calories { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Carbs { get; set; }
    public decimal? Fats { get; set; }
    public decimal? Fiber { get; set; }
    public string? Notes { get; set; }
}

public class UpsertDietPlanRequest
{
    public DateTime? ReviewDate { get; set; }
    public int? ExpectedVersion { get; set; }
    public string? ChangeReason { get; set; }
    public long PatientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Objective { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DailyCalories { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatsGrams { get; set; }
    public decimal? FiberGrams { get; set; }
    public decimal? WaterIntakeLiters { get; set; }
    public string? Supplements { get; set; }
    public string? Instructions { get; set; }
    public string? Notes { get; set; }
    public IEnumerable<UpsertDietPlanItemRequest> Meals { get; set; } = new List<UpsertDietPlanItemRequest>();
}

public class UpsertDietPlanItemRequest
{
    public int DayNumber { get; set; }
    public string MealType { get; set; } = string.Empty;
    public TimeOnly? MealTime { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "grams";
    public int Calories { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Carbs { get; set; }
    public decimal? Fats { get; set; }
    public decimal? Fiber { get; set; }
    public string? Notes { get; set; }
}

public class DietPlanComplianceDto : BaseDto
{
    public long DietPlanId { get; set; }
    public long PatientId { get; set; }
    public DateTime RecordDate { get; set; }
    public int MealsCompleted { get; set; }
    public int TotalMeals { get; set; }
    public decimal WaterIntakeLiters { get; set; }
    public bool TookSupplements { get; set; }
    public string? Notes { get; set; }
    public decimal CompliancePercentage { get; set; }
}

public class TrackDietPlanComplianceRequest
{
    public DateTime RecordDate { get; set; }
    public int MealsCompleted { get; set; }
    public int TotalMeals { get; set; }
    public decimal WaterIntakeLiters { get; set; }
    public bool TookSupplements { get; set; }
    public string? Notes { get; set; }
}

public class ProgressRecordDto : BaseDto
{
    public decimal? WaistCircumference { get; set; }
    public decimal? ChestCircumference { get; set; }
    public decimal? HipCircumference { get; set; }
    public decimal? ArmCircumference { get; set; }
    public decimal? ThighCircumference { get; set; }
    public bool BMIIsCalculated { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public long PatientId { get; set; }
    public DateTime RecordDate { get; set; }
    public decimal Weight { get; set; }
    public decimal? BMI { get; set; }
    public decimal? BodyFatPercentage { get; set; }
    public decimal? WaterIntakeLiters { get; set; }
    public int? DailyCalories { get; set; }
    public int? ExerciseMinutes { get; set; }
    public string? ExerciseType { get; set; }
    public string? Notes { get; set; }
}

public class UpsertProgressRecordRequest
{
    public decimal? WaistCircumference { get; set; }
    public decimal? ChestCircumference { get; set; }
    public decimal? HipCircumference { get; set; }
    public decimal? ArmCircumference { get; set; }
    public decimal? ThighCircumference { get; set; }
    public string? ChangeReason { get; set; }
    public byte[]? ExpectedVersion { get; set; }
    public DateTime RecordDate { get; set; } = DateTime.UtcNow.Date;
    public decimal Weight { get; set; }
    public decimal? BMI { get; set; }
    public decimal? BodyFatPercentage { get; set; }
    public decimal? WaterIntakeLiters { get; set; }
    public int? DailyCalories { get; set; }
    public int? ExerciseMinutes { get; set; }
    public string? ExerciseType { get; set; }
    public string? Notes { get; set; }
}

public class ProgressChartPointDto
{
    public DateTime Date { get; set; }
    public string Metric { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class ProgressAnalyticsDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int RecordCount { get; set; }
    public decimal? StartWeight { get; set; }
    public decimal? CurrentWeight { get; set; }
    public decimal? WeightChange { get; set; }
    public decimal? AverageBMI { get; set; }
    public decimal? AverageBodyFatPercentage { get; set; }
    public decimal? AverageWaterIntakeLiters { get; set; }
    public decimal? AverageDailyCalories { get; set; }
    public int TotalExerciseMinutes { get; set; }
}

public class MedicalReportDto : BaseDto
{
    public long PatientId { get; set; }
    public long UploadedByUserId { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? PatientNotes { get; set; }
    public string? NutritionistNotes { get; set; }
    public long? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime UploadedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class UploadMedicalReportRequest
{
    public string ReportType { get; set; } = "LabReport";
    public string? PatientNotes { get; set; }
}

public class AddMedicalReportNotesRequest
{
    public string Notes { get; set; } = string.Empty;
}

public class MedicalReportFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public Stream Content { get; set; } = Stream.Null;
}

public class PaymentDto : BaseDto
{
    public long PatientId { get; set; }
    public long? NutritionistId { get; set; }
    public long? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string? ProviderPaymentId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RelatedToType { get; set; }
    public long? RelatedToId { get; set; }
    public decimal RefundedAmount { get; set; }
    public string? RefundReason { get; set; }
    public DateTime? RefundedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public InvoiceDto? Invoice { get; set; }
}

public class InvoiceDto : BaseDto
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public long PatientId { get; set; }
    public long? NutritionistId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Description { get; set; }
    public string? RelatedToType { get; set; }
    public long? RelatedToId { get; set; }
}

public class PaymentRefundDto : BaseDto
{
    public long PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? ProviderRefundId { get; set; }
    public DateTime? RefundedAt { get; set; }
}

public class CreatePaymentRequest
{
    public long? NutritionistId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentMethod { get; set; } = "Stripe";
    public string? Description { get; set; }
    public string? RelatedToType { get; set; }
    public long? RelatedToId { get; set; }
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
}

public class RefundPaymentRequest
{
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}

public class ChatMessageDto : BaseDto
{
    public long SenderId { get; set; }
    public long ReceiverId { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string MessageType { get; set; } = "Text";
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }
    public long? AttachmentFileSizeBytes { get; set; }
}

public class SendChatMessageRequest
{
    public long ReceiverId { get; set; }
    public string MessageBody { get; set; } = string.Empty;
}

public class ChatFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public Stream Content { get; set; } = Stream.Null;
}

public class UnreadMessageSummaryDto
{
    public int TotalUnread { get; set; }
    public IEnumerable<UnreadConversationDto> Conversations { get; set; } = new List<UnreadConversationDto>();
}

public class UnreadConversationDto
{
    public string ConversationId { get; set; } = string.Empty;
    public long SenderId { get; set; }
    public int UnreadCount { get; set; }
}

public class VideoConsultationDto : BaseDto
{
    public long AppointmentId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderMeetingId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string? MeetingUrl { get; set; }
    public string? HostUrl { get; set; }
    public DateTime ScheduledStartTime { get; set; }
    public DateTime ScheduledEndTime { get; set; }
    public DateTime? CallStartTime { get; set; }
    public DateTime? CallEndTime { get; set; }
    public int? Duration { get; set; }
    public string? RecordingUrl { get; set; }
    public string? RecordingStatus { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ScheduleVideoConsultationRequest
{
    public long AppointmentId { get; set; }
    public string Provider { get; set; } = "GoogleMeet";
    public string? Notes { get; set; }
}

public class JoinVideoConsultationDto
{
    public long ConsultationId { get; set; }
    public long AppointmentId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string MeetingUrl { get; set; } = string.Empty;
    public string? HostUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsHost { get; set; }
    public DateTime ScheduledStartTime { get; set; }
    public DateTime ScheduledEndTime { get; set; }
}

public class CompleteVideoConsultationRequest
{
    public string? RecordingUrl { get; set; }
    public string? Notes { get; set; }
}

public class AdminDashboardSummaryDto
{
    public string Currency { get; set; } = "PKR";
    public int ExcludedCurrencyCount { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalPatients { get; set; }
    public int TotalNutritionists { get; set; }
    public int VerifiedNutritionists { get; set; }
    public int TotalAppointments { get; set; }
    public int UpcomingAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int PublishedBlogs { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal RefundedRevenue { get; set; }
    public decimal NetRevenue { get; set; }
}

public class AdminChartPointDto
{
    public string Label { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public decimal Value { get; set; }
}

public class AdminRevenueAnalyticsDto
{
    public string Currency { get; set; } = "PKR";
    public int ExcludedCurrencyCount { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal GrossRevenue { get; set; }
    public decimal RefundedRevenue { get; set; }
    public decimal NetRevenue { get; set; }
    public IEnumerable<AdminChartPointDto> RevenueByDay { get; set; } = new List<AdminChartPointDto>();
    public IEnumerable<AdminChartPointDto> RevenueByProvider { get; set; } = new List<AdminChartPointDto>();
}

public class AdminAppointmentAnalyticsDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalAppointments { get; set; }
    public IEnumerable<AdminChartPointDto> AppointmentsByDay { get; set; } = new List<AdminChartPointDto>();
    public IEnumerable<AdminChartPointDto> AppointmentsByStatus { get; set; } = new List<AdminChartPointDto>();
    public IEnumerable<AdminChartPointDto> AppointmentsByType { get; set; } = new List<AdminChartPointDto>();
}

public class AdminUserListItemDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class AdminPatientListItemDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal CurrentWeight { get; set; }
    public decimal? TargetWeight { get; set; }
    public string? City { get; set; }
    public int AppointmentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminNutritionistListItemDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public string? Qualification { get; set; }
    public decimal ConsultationFee { get; set; }
    public bool IsVerified { get; set; }
    public decimal RatingAverage { get; set; }
    public int AppointmentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminAuditLogDto
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public int? StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BlogDto : BaseDto
{
    public byte[] RowVersion { get; set; } = [];
    public long AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
}

public class UpsertBlogRequest
{
    public byte[]? ExpectedVersion { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string Category { get; set; } = "General";
    public string Tags { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
}

public class UpdateUserStatusRequest
{
    public bool IsActive { get; set; }
}

public class UpdateNutritionistVerificationRequest
{
    public bool IsVerified { get; set; }
}

public class PatientDashboardDto
{
    public PatientDashboardOverviewDto Overview { get; set; } = new();
    public IEnumerable<PatientDashboardAppointmentDto> UpcomingAppointments { get; set; } = new List<PatientDashboardAppointmentDto>();
    public PatientDashboardDietPlanDto? DietPlanSummary { get; set; }
    public PatientDashboardWeightProgressDto WeightProgress { get; set; } = new();
    public IEnumerable<PatientDashboardReportDto> RecentReports { get; set; } = new List<PatientDashboardReportDto>();
    public IEnumerable<PatientDashboardNotificationDto> Notifications { get; set; } = new List<PatientDashboardNotificationDto>();
    public PatientDashboardMessageSummaryDto Messages { get; set; } = new();
}

public class PatientDashboardOverviewDto
{
    public string PatientName { get; set; } = string.Empty;
    public decimal CurrentWeight { get; set; }
    public decimal? TargetWeight { get; set; }
    public int UpcomingAppointmentCount { get; set; }
    public int UnreadNotificationCount { get; set; }
    public int UnreadMessageCount { get; set; }
    public decimal? LatestBMI { get; set; }
}

public class PatientDashboardAppointmentDto
{
    public long Id { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public int Duration { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AppointmentType { get; set; } = string.Empty;
    public string NutritionistName { get; set; } = string.Empty;
}

public class PatientDashboardDietPlanDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DailyCalories { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbsGrams { get; set; }
    public decimal FatsGrams { get; set; }
    public decimal? WaterIntakeLiters { get; set; }
    public string? Supplements { get; set; }
    public string? Instructions { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DaysRemaining { get; set; }
    public decimal AverageCompliancePercentage { get; set; }
}

public class PatientDashboardWeightProgressDto
{
    public decimal? StartWeight { get; set; }
    public decimal? CurrentWeight { get; set; }
    public decimal? WeightChange { get; set; }
    public decimal? LatestBMI { get; set; }
    public IEnumerable<PatientDashboardWeightPointDto> Points { get; set; } = new List<PatientDashboardWeightPointDto>();
}

public class PatientDashboardWeightPointDto
{
    public DateTime Date { get; set; }
    public decimal Weight { get; set; }
    public decimal? BMI { get; set; }
}

public class PatientDashboardReportDto
{
    public long Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public class PatientDashboardNotificationDto
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class PatientDashboardMessageSummaryDto
{
    public int TotalUnread { get; set; }
    public IEnumerable<PatientDashboardMessageDto> RecentMessages { get; set; } = new List<PatientDashboardMessageDto>();
}

public class PatientDashboardMessageDto
{
    public long Id { get; set; }
    public long SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NutritionistDashboardDto
{
    public NutritionistDashboardOverviewDto Overview { get; set; } = new();
    public IEnumerable<NutritionistDashboardAppointmentDto> TodaysAppointments { get; set; } = new List<NutritionistDashboardAppointmentDto>();
    public IEnumerable<NutritionistDashboardAppointmentDto> PendingConsultations { get; set; } = new List<NutritionistDashboardAppointmentDto>();
    public IEnumerable<NutritionistPatientProgressDto> PatientProgressOverview { get; set; } = new List<NutritionistPatientProgressDto>();
    public NutritionistRevenueSummaryDto RevenueSummary { get; set; } = new();
    public NutritionistMessageSummaryDto RecentMessages { get; set; } = new();
}

public class NutritionistDashboardOverviewDto
{
    public string NutritionistName { get; set; } = string.Empty;
    public int TodaysAppointmentCount { get; set; }
    public int PendingConsultationCount { get; set; }
    public int ActivePatientCount { get; set; }
    public int UnreadMessageCount { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal RatingAverage { get; set; }
}

public class NutritionistDashboardAppointmentDto
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public DateTime AppointmentDateTime { get; set; }
    public int Duration { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AppointmentType { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class NutritionistPatientProgressDto
{
    public long PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public decimal? StartWeight { get; set; }
    public decimal? CurrentWeight { get; set; }
    public decimal? WeightChange { get; set; }
    public decimal? LatestBMI { get; set; }
    public DateTime? LastRecordDate { get; set; }
    public int ProgressRecordCount { get; set; }
}

public class NutritionistRevenueSummaryDto
{
    public string Currency { get; set; } = "PKR";
    public int ExcludedCurrencyCount { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal WeekRevenue { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal RefundedAmount { get; set; }
    public IEnumerable<NutritionistRevenuePointDto> RevenueByDay { get; set; } = new List<NutritionistRevenuePointDto>();
}

public class NutritionistRevenuePointDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
}

public class NutritionistMessageSummaryDto
{
    public int TotalUnread { get; set; }
    public IEnumerable<NutritionistMessageDto> Messages { get; set; } = new List<NutritionistMessageDto>();
}

public class NutritionistMessageDto
{
    public long Id { get; set; }
    public long SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string MessageBody { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationDto : BaseDto
{
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
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
}

public class TriggerAppointmentReminderRequest
{
    public long AppointmentId { get; set; }
}

/// <summary>
/// API Response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
    public int StatusCode { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Operation successful", int statusCode = 200)
        => new() { Success = true, Data = data, Message = message, StatusCode = statusCode };

    public static ApiResponse<T> FailureResponse(string message, int statusCode = 400)
        => new() { Success = false, Message = message, StatusCode = statusCode };

    public static ApiResponse<T> FailureResponse(IDictionary<string, string[]> errors, int statusCode = 400)
        => new() { Success = false, Message = "Validation failed", Errors = errors, StatusCode = statusCode };
}

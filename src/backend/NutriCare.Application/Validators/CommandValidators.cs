namespace NutriCare.Application.Validators;

using FluentValidation;
using NutriCare.Application.Commands.Authentication;
using NutriCare.Application.Commands.Appointments;
using NutriCare.Application.Commands.Chat;
using NutriCare.Application.Commands.DietPlans;
using NutriCare.Application.Commands.MedicalReports;
using NutriCare.Application.Commands.Payments;
using NutriCare.Application.Commands.Progress;
using NutriCare.Application.Commands.VideoConsultations;
using NutriCare.Application.DTOs;
using NutriCare.Application.Services;

/// <summary>
/// Validator for login command
/// </summary>
public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");
    }
}

/// <summary>
/// Validator for register command
/// </summary>
public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required")
            .Equal(x => x.Password).WithMessage("Passwords do not match");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required")
            .Equal("Patient").WithMessage("Public registration is available for patients only");
    }
}

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]")
            .Matches("[a-z]")
            .Matches("[0-9]");
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword);
    }
}

public class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.Token).NotEmpty();
    }
}

public class AssignRoleCommandValidator : AbstractValidator<AssignRoleCommand>
{
    public AssignRoleCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(x => new[] { "Patient", "Nutritionist", "Admin" }.Contains(x));
    }
}

/// <summary>
/// Validator for create appointment command
/// </summary>
public class CreateAppointmentCommandValidator : AbstractValidator<CreateAppointmentCommand>
{
    private static readonly string[] AppointmentTypes = { "Video", "Online", "InPerson" };

    public CreateAppointmentCommandValidator()
    {
        RuleFor(x => x.PatientId)
            .GreaterThan(0).WithMessage("Patient ID is required");

        RuleFor(x => x.NutritionistId)
            .GreaterThan(0).WithMessage("Nutritionist ID is required");

        RuleFor(x => x.AppointmentDateTime)
            .NotEmpty().WithMessage("Appointment date and time is required")
            .Must((command, value) => SchedulingValidation.IsFuture(value, command.TimeZoneId)).WithMessage("Appointment must be in the future in a valid timezone");

        RuleFor(x => x.AppointmentType)
            .NotEmpty().WithMessage("Appointment type is required")
            .Must(x => AppointmentTypes.Contains(x))
            .WithMessage("Invalid appointment type");
    }
}

public class RescheduleAppointmentCommandValidator : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0);
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.RequestedByUserId).GreaterThan(0);
        RuleFor(x => x.AppointmentDateTime)
            .NotEmpty()
            .Must((command, value) => SchedulingValidation.IsFuture(value, command.TimeZoneId)).WithMessage("Appointment must be in the future in a valid timezone");
    }
}

/// <summary>
/// Validator for cancel appointment command
/// </summary>
public class CancelAppointmentCommandValidator : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId)
            .GreaterThan(0).WithMessage("Appointment ID is required");

        RuleFor(x => x.CancelledBy)
            .NotEmpty().WithMessage("Cancelled by is required")
            .Must(x => new[] { "Patient", "Nutritionist", "Admin" }.Contains(x))
            .WithMessage("Invalid cancellation source");
    }
}

public class ApproveAppointmentCommandValidator : AbstractValidator<ApproveAppointmentCommand>
{
    public ApproveAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0);
        RuleFor(x => x.NutritionistId).GreaterThan(0);
        RuleFor(x => x.ApprovedByUserId).GreaterThan(0);
    }
}

public class RejectAppointmentCommandValidator : AbstractValidator<RejectAppointmentCommand>
{
    public RejectAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0);
        RuleFor(x => x.NutritionistId).GreaterThan(0);
        RuleFor(x => x.RejectedByUserId).GreaterThan(0);
        RuleFor(x => x.RejectionReason)
            .NotEmpty()
            .MaximumLength(1000);
    }
}

public class CompleteAppointmentCommandValidator : AbstractValidator<CompleteAppointmentCommand>
{
    public CompleteAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0);
        RuleFor(x => x.NutritionistId).GreaterThan(0);
        RuleFor(x => x.CompletedByUserId).GreaterThan(0);
        RuleFor(x => x.ConsultationNotes).MaximumLength(5000);
    }
}

public class AddConsultationNotesCommandValidator : AbstractValidator<AddConsultationNotesCommand>
{
    public AddConsultationNotesCommandValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0);
        RuleFor(x => x.NutritionistId).GreaterThan(0);
        RuleFor(x => x.ConsultationNotes)
            .NotEmpty()
            .MaximumLength(5000);
    }
}

public class CreateDietPlanCommandValidator : AbstractValidator<CreateDietPlanCommand>
{
    public CreateDietPlanCommandValidator()
    {
        RuleFor(x => x.NutritionistId).GreaterThan(0);
        RuleFor(x => x.PatientId).GreaterThan(0);
        Include(new DietPlanFieldsValidator<CreateDietPlanCommand>());
    }
}

public class UpdateDietPlanCommandValidator : AbstractValidator<UpdateDietPlanCommand>
{
    public UpdateDietPlanCommandValidator()
    {
        RuleFor(x => x.DietPlanId).GreaterThan(0);
        RuleFor(x => x.NutritionistId).GreaterThan(0);
        RuleFor(x => x.PatientId).GreaterThan(0);
        Include(new DietPlanFieldsValidator<UpdateDietPlanCommand>());
    }
}

public class TrackDietPlanComplianceCommandValidator : AbstractValidator<TrackDietPlanComplianceCommand>
{
    public TrackDietPlanComplianceCommandValidator()
    {
        RuleFor(x => x.DietPlanId).GreaterThan(0);
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.RecordDate).NotEmpty().Must(value => value.Date <= ClinicalTime.LocalToday());
        RuleFor(x => x.TotalMeals).GreaterThan(0);
        RuleFor(x => x.MealsCompleted).GreaterThanOrEqualTo(0).LessThanOrEqualTo(x => x.TotalMeals);
        RuleFor(x => x.WaterIntakeLiters).GreaterThanOrEqualTo(0).LessThanOrEqualTo(15);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

internal class DietPlanFieldsValidator<T> : AbstractValidator<T>
    where T : class
{
    public DietPlanFieldsValidator()
    {
        RuleFor(x => GetTitle(x)).NotEmpty().MaximumLength(255).WithName("Title");
        RuleFor(x => GetStartDate(x)).NotEmpty().WithName("StartDate");
        RuleFor(x => GetEndDate(x))
            .NotEmpty()
            .GreaterThanOrEqualTo(x => GetStartDate(x))
            .WithName("EndDate");
        RuleFor(x => GetDailyCalories(x)).GreaterThan(0).LessThanOrEqualTo(10000).WithName("DailyCalories");
        RuleFor(x => GetProtein(x)).GreaterThanOrEqualTo(0).WithName("ProteinGrams");
        RuleFor(x => GetCarbs(x)).GreaterThanOrEqualTo(0).WithName("CarbsGrams");
        RuleFor(x => GetFats(x)).GreaterThanOrEqualTo(0).WithName("FatsGrams");
        RuleFor(x => GetWater(x)).GreaterThanOrEqualTo(0).LessThanOrEqualTo(15).When(x => GetWater(x).HasValue).WithName("WaterIntakeLiters");
        RuleFor(x => GetMeals(x)).NotEmpty().WithName("Meals");
        RuleForEach(x => GetMeals(x)).SetValidator(new UpsertDietPlanItemRequestValidator());
    }

    private static string GetTitle(T value) => value switch
    {
        CreateDietPlanCommand command => command.Title,
        UpdateDietPlanCommand command => command.Title,
        _ => string.Empty
    };

    private static DateTime GetStartDate(T value) => value switch
    {
        CreateDietPlanCommand command => command.StartDate,
        UpdateDietPlanCommand command => command.StartDate,
        _ => default
    };

    private static DateTime GetEndDate(T value) => value switch
    {
        CreateDietPlanCommand command => command.EndDate,
        UpdateDietPlanCommand command => command.EndDate,
        _ => default
    };

    private static int GetDailyCalories(T value) => value switch
    {
        CreateDietPlanCommand command => command.DailyCalories,
        UpdateDietPlanCommand command => command.DailyCalories,
        _ => 0
    };

    private static decimal GetProtein(T value) => value switch
    {
        CreateDietPlanCommand command => command.ProteinGrams,
        UpdateDietPlanCommand command => command.ProteinGrams,
        _ => 0
    };

    private static decimal GetCarbs(T value) => value switch
    {
        CreateDietPlanCommand command => command.CarbsGrams,
        UpdateDietPlanCommand command => command.CarbsGrams,
        _ => 0
    };

    private static decimal GetFats(T value) => value switch
    {
        CreateDietPlanCommand command => command.FatsGrams,
        UpdateDietPlanCommand command => command.FatsGrams,
        _ => 0
    };

    private static decimal? GetWater(T value) => value switch
    {
        CreateDietPlanCommand command => command.WaterIntakeLiters,
        UpdateDietPlanCommand command => command.WaterIntakeLiters,
        _ => null
    };

    private static IEnumerable<UpsertDietPlanItemRequest> GetMeals(T value) => value switch
    {
        CreateDietPlanCommand command => command.Meals,
        UpdateDietPlanCommand command => command.Meals,
        _ => Array.Empty<UpsertDietPlanItemRequest>()
    };
}

public class UpsertDietPlanItemRequestValidator : AbstractValidator<UpsertDietPlanItemRequest>
{
    public UpsertDietPlanItemRequestValidator()
    {
        RuleFor(x => x.DayNumber).InclusiveBetween(1, 365);
        RuleFor(x => x.MealType)
            .NotEmpty()
            .Must(x => new[] { "Breakfast", "Lunch", "Dinner", "Snack1", "Snack2", "Other" }.Contains(x))
            .WithMessage("Invalid meal type");
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Calories).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Protein).GreaterThanOrEqualTo(0).When(x => x.Protein.HasValue);
        RuleFor(x => x.Carbs).GreaterThanOrEqualTo(0).When(x => x.Carbs.HasValue);
        RuleFor(x => x.Fats).GreaterThanOrEqualTo(0).When(x => x.Fats.HasValue);
        RuleFor(x => x.Fiber).GreaterThanOrEqualTo(0).When(x => x.Fiber.HasValue);
    }
}

public class TrackProgressCommandValidator : AbstractValidator<TrackProgressCommand>
{
    public TrackProgressCommandValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.RecordDate).NotEmpty().Must(value => value.Date <= ClinicalTime.LocalToday());
        RuleFor(x => x.Weight).GreaterThan(0).LessThanOrEqualTo(500);
        RuleFor(x => x.BMI).InclusiveBetween(10, 80).When(x => x.BMI.HasValue);
        RuleFor(x => x.BodyFatPercentage).InclusiveBetween(0, 100).When(x => x.BodyFatPercentage.HasValue);
        RuleFor(x => x.WaterIntakeLiters).InclusiveBetween(0, 15).When(x => x.WaterIntakeLiters.HasValue);
        RuleFor(x => x.DailyCalories).InclusiveBetween(0, 10000).When(x => x.DailyCalories.HasValue);
        RuleFor(x => x.ExerciseMinutes).InclusiveBetween(0, 1440).When(x => x.ExerciseMinutes.HasValue);
        RuleFor(x => x.ExerciseType).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class UploadMedicalReportCommandValidator : AbstractValidator<UploadMedicalReportCommand>
{
    private static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] AllowedReportTypes = { "MedicalReport", "LabReport", "Prescription", "Imaging", "Other" };

    public UploadMedicalReportCommandValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.UploadedByUserId).GreaterThan(0);
        RuleFor(x => x.ReportType)
            .NotEmpty()
            .Must(x => AllowedReportTypes.Contains(x))
            .WithMessage("Invalid report type");
        RuleFor(x => x.OriginalFileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(x => AllowedContentTypes.Contains(x))
            .WithMessage("Only PDF, JPEG, PNG, and WEBP files are allowed");
        RuleFor(x => x.OriginalFileName)
            .Must(x => AllowedExtensions.Contains(Path.GetExtension(x).ToLowerInvariant()))
            .WithMessage("Only .pdf, .jpg, .jpeg, .png, and .webp files are allowed");
        RuleFor(x => x.FileSizeBytes).GreaterThan(0).LessThanOrEqualTo(10 * 1024 * 1024);
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.PatientNotes).MaximumLength(2000);
    }
}

public class AddMedicalReportNotesCommandValidator : AbstractValidator<AddMedicalReportNotesCommand>
{
    public AddMedicalReportNotesCommandValidator()
    {
        RuleFor(x => x.ReportId).GreaterThan(0);
        RuleFor(x => x.RequestedByUserId).GreaterThan(0);
        RuleFor(x => x.RequestedByRole).NotEmpty();
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(5000);
    }
}

public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    private static readonly string[] Providers = { "Stripe", "JazzCash", "EasyPaisa" };
    private static readonly string[] RelatedTypes = { "Appointment", "DietPlan", "Consultation", "Subscription", "Other" };

    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.PatientId).GreaterThan(0);
        RuleFor(x => x.NutritionistId).GreaterThan(0).When(x => x.NutritionistId.HasValue);
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.PaymentMethod)
            .NotEmpty()
            .Must(x => Providers.Contains(x))
            .WithMessage("Payment provider must be Stripe, JazzCash, or EasyPaisa");
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.RelatedToType)
            .Must(x => string.IsNullOrWhiteSpace(x) || RelatedTypes.Contains(x))
            .WithMessage("Invalid related payment type");
        RuleFor(x => x.RelatedToId).GreaterThan(0).When(x => x.RelatedToId.HasValue);
    }
}

public class RefundPaymentCommandValidator : AbstractValidator<RefundPaymentCommand>
{
    public RefundPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}

public class HandlePaymentWebhookCommandValidator : AbstractValidator<HandlePaymentWebhookCommand>
{
    private static readonly string[] Providers = { "Stripe", "JazzCash", "EasyPaisa" };

    public HandlePaymentWebhookCommandValidator()
    {
        RuleFor(x => x.Provider)
            .NotEmpty()
            .Must(x => Providers.Contains(x))
            .WithMessage("Invalid payment provider");
        RuleFor(x => x.Payload).NotEmpty();
    }
}

public class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageCommandValidator()
    {
        RuleFor(x => x.SenderId).GreaterThan(0);
        RuleFor(x => x.ReceiverId).GreaterThan(0).NotEqual(x => x.SenderId);
        RuleFor(x => x.MessageBody)
            .MaximumLength(5000)
            .NotEmpty()
            .When(x => string.IsNullOrWhiteSpace(x.AttachmentBlobName));
        RuleFor(x => x.MessageType)
            .NotEmpty()
            .Must(x => new[] { "Text", "File", "Image" }.Contains(x));
        RuleFor(x => x.AttachmentFileSizeBytes)
            .LessThanOrEqualTo(10 * 1024 * 1024)
            .When(x => x.AttachmentFileSizeBytes.HasValue);
    }
}

public class MarkConversationReadCommandValidator : AbstractValidator<MarkConversationReadCommand>
{
    public MarkConversationReadCommandValidator()
    {
        RuleFor(x => x.CurrentUserId).GreaterThan(0);
        RuleFor(x => x.OtherUserId).GreaterThan(0).NotEqual(x => x.CurrentUserId);
    }
}

public class ScheduleVideoConsultationCommandValidator : AbstractValidator<ScheduleVideoConsultationCommand>
{
    public ScheduleVideoConsultationCommandValidator()
    {
        RuleFor(x => x.AppointmentId).GreaterThan(0);
        RuleFor(x => x.RequestedByUserId).GreaterThan(0);
        RuleFor(x => x.RequestedByRole).NotEmpty();
        RuleFor(x => x.Provider)
            .NotEmpty()
            .Must(x => new[] { "GoogleMeet", "Google Meet", "Zoom" }.Contains(x))
            .WithMessage("Provider must be GoogleMeet or Zoom");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class JoinVideoConsultationCommandValidator : AbstractValidator<JoinVideoConsultationCommand>
{
    public JoinVideoConsultationCommandValidator()
    {
        RuleFor(x => x.ConsultationId).GreaterThan(0);
        RuleFor(x => x.RequestedByUserId).GreaterThan(0);
        RuleFor(x => x.RequestedByRole).NotEmpty();
    }
}

public class CompleteVideoConsultationCommandValidator : AbstractValidator<CompleteVideoConsultationCommand>
{
    public CompleteVideoConsultationCommandValidator()
    {
        RuleFor(x => x.ConsultationId).GreaterThan(0);
        RuleFor(x => x.RequestedByUserId).GreaterThan(0);
        RuleFor(x => x.RequestedByRole).NotEmpty();
        RuleFor(x => x.RecordingUrl).MaximumLength(2000);
        RuleFor(x => x.Notes).MaximumLength(5000);
    }
}

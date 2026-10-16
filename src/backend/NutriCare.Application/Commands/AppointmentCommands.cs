namespace NutriCare.Application.Commands.Appointments;

using MediatR;
using NutriCare.Application.DTOs;

/// <summary>
/// Command to create an appointment
/// </summary>
public class CreateAppointmentCommand : IRequest<AppointmentDto>
{
    public long PatientId { get; set; }
    public long NutritionistId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public string AppointmentType { get; set; } = "Video";
    public string? Notes { get; set; }
    public long? ServiceId { get; set; }
    public long? IntakeFormId { get; set; }
    public long? PackageEnrollmentId { get; set; }
    public string? PackageCreditKind { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Karachi";
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// Command to update appointment
/// </summary>
public class UpdateAppointmentCommand : IRequest<AppointmentDto>
{
    public long AppointmentId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public string? Notes { get; set; }
}

public class RescheduleAppointmentCommand : IRequest<AppointmentDto>
{
    public long AppointmentId { get; set; }
    public long PatientId { get; set; }
    public long RequestedByUserId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public string? Notes { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Karachi";
}

/// <summary>
/// Command to cancel appointment
/// </summary>
public class CancelAppointmentCommand : IRequest<bool>
{
    public long AppointmentId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public string CancelledBy { get; set; } = string.Empty;
}

public class ApproveAppointmentCommand : IRequest<AppointmentDto>
{
    public long AppointmentId { get; set; }
    public long NutritionistId { get; set; }
    public long ApprovedByUserId { get; set; }
}

public class RejectAppointmentCommand : IRequest<AppointmentDto>
{
    public long AppointmentId { get; set; }
    public long NutritionistId { get; set; }
    public long RejectedByUserId { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
}

/// <summary>
/// Command to complete appointment
/// </summary>
public class CompleteAppointmentCommand : IRequest<AppointmentDto>
{
    public long AppointmentId { get; set; }
    public long NutritionistId { get; set; }
    public long CompletedByUserId { get; set; }
    public string? ConsultationNotes { get; set; }
}

public class AddConsultationNotesCommand : IRequest<AppointmentDto>
{
    public long AppointmentId { get; set; }
    public long NutritionistId { get; set; }
    public string ConsultationNotes { get; set; } = string.Empty;
}

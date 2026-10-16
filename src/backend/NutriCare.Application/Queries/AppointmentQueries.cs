namespace NutriCare.Application.Queries.Appointments;

using MediatR;
using NutriCare.Application.DTOs;

/// <summary>
/// Query to get appointment by id
/// </summary>
public class GetAppointmentByIdQuery : IRequest<AppointmentDto>
{
    public long AppointmentId { get; set; }
}

/// <summary>
/// Query to get appointments by patient
/// </summary>
public class GetPatientAppointmentsQuery : IRequest<IEnumerable<AppointmentDto>>
{
    public long PatientId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// Query to get appointments by nutritionist
/// </summary>
public class GetNutritionistAppointmentsQuery : IRequest<IEnumerable<AppointmentDto>>
{
    public long NutritionistId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
}

public class GetAllAppointmentsQuery : IRequest<IEnumerable<AppointmentDto>>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
    public long? PatientId { get; set; }
    public long? NutritionistId { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
}

/// <summary>
/// Query to get available slots
/// </summary>
public class GetAvailableSlotsQuery : IRequest<IEnumerable<DateTime>>
{
    public long NutritionistId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public long? ServiceId { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Karachi";
}

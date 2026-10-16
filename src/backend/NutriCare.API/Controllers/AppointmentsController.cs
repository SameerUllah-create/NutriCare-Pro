namespace NutriCare.API.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using NutriCare.Application.Commands.Appointments;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.Appointments;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppointmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly NutriCareDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IClinicalAccessService _clinicalAccess;

    public AppointmentsController(
        IMediator mediator,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository,
        NutriCareDbContext context,
        INotificationService notificationService,
        IClinicalAccessService clinicalAccess)
    {
        _mediator = mediator;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _context = context;
        _notificationService = notificationService;
        _clinicalAccess = clinicalAccess;
    }

    [HttpPost]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> BookAppointment([FromBody] CreateAppointmentRequest request)
    {
        var patient = await GetCurrentPatientAsync();
        var command = new CreateAppointmentCommand
        {
            PatientId = patient.Id,
            NutritionistId = request.NutritionistId,
            AppointmentDateTime = request.AppointmentDateTime,
            AppointmentType = request.AppointmentType,
            Notes = request.Notes,
            ServiceId = request.ServiceId,
            IntakeFormId = request.IntakeFormId,
            PackageEnrollmentId = request.PackageEnrollmentId,
            PackageCreditKind = request.PackageCreditKind,
            TimeZoneId = request.TimeZoneId,
            IdempotencyKey = request.IdempotencyKey
        };

        var result = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetAppointment), new { id = result.Id },
            ApiResponse<AppointmentDto>.SuccessResponse(result, "Appointment booked successfully", 201));
    }

    [HttpPut("{id:long}/reschedule")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> RescheduleAppointment(long id, [FromBody] RescheduleAppointmentRequest request)
    {
        var patient = await GetCurrentPatientAsync();
        var result = await _mediator.Send(new RescheduleAppointmentCommand
        {
            AppointmentId = id,
            PatientId = patient.Id,
            RequestedByUserId = GetCurrentUserId(),
            AppointmentDateTime = request.AppointmentDateTime,
            Notes = request.Notes,
            TimeZoneId = request.TimeZoneId
        });

        return Ok(ApiResponse<AppointmentDto>.SuccessResponse(result, "Appointment rescheduled successfully"));
    }

    [HttpDelete("{id:long}/cancel")]
    [Authorize(Roles = "Patient,Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<bool>>> CancelAppointment(long id, [FromBody] CancelAppointmentRequest request)
    {
        var role = GetCurrentRole();
        var result = await _mediator.Send(new CancelAppointmentCommand
        {
            AppointmentId = id,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = role,
            CancellationReason = request.CancellationReason,
            CancelledBy = role
        });

        return Ok(ApiResponse<bool>.SuccessResponse(result, "Appointment cancelled successfully"));
    }

    [HttpGet("history")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AppointmentDto>>>> GetMyAppointmentHistory(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null)
    {
        var patient = await GetCurrentPatientAsync();
        var result = await _mediator.Send(new GetPatientAppointmentsQuery
        {
            PatientId = patient.Id,
            FromDate = fromDate,
            ToDate = toDate,
            Status = status
        });

        return Ok(ApiResponse<IEnumerable<AppointmentDto>>.SuccessResponse(result));
    }

    [HttpGet("patient/{patientId:long}")]
    [Authorize(Roles = "Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AppointmentDto>>>> GetPatientAppointments(
        long patientId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null)
    {
        await _clinicalAccess.EnsurePatientAccessAsync(patientId, allowPatient: false);
        var result = await _mediator.Send(new GetPatientAppointmentsQuery
        {
            PatientId = patientId,
            FromDate = fromDate,
            ToDate = toDate,
            Status = status
        });

        return Ok(ApiResponse<IEnumerable<AppointmentDto>>.SuccessResponse(result));
    }

    [HttpGet("nutritionist")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AppointmentDto>>>> GetMyNutritionistAppointments(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null)
    {
        var nutritionist = await GetCurrentNutritionistAsync();
        var result = await _mediator.Send(new GetNutritionistAppointmentsQuery
        {
            NutritionistId = nutritionist.Id,
            FromDate = fromDate,
            ToDate = toDate,
            Status = status
        });

        return Ok(ApiResponse<IEnumerable<AppointmentDto>>.SuccessResponse(result));
    }

    [HttpPut("{id:long}/approve")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> ApproveAppointment(long id)
    {
        var nutritionist = await GetCurrentNutritionistAsync();
        var result = await _mediator.Send(new ApproveAppointmentCommand
        {
            AppointmentId = id,
            NutritionistId = nutritionist.Id,
            ApprovedByUserId = GetCurrentUserId()
        });

        return Ok(ApiResponse<AppointmentDto>.SuccessResponse(result, "Appointment approved successfully"));
    }

    [HttpPut("{id:long}/reject")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> RejectAppointment(long id, [FromBody] RejectAppointmentRequest request)
    {
        var nutritionist = await GetCurrentNutritionistAsync();
        var result = await _mediator.Send(new RejectAppointmentCommand
        {
            AppointmentId = id,
            NutritionistId = nutritionist.Id,
            RejectedByUserId = GetCurrentUserId(),
            RejectionReason = request.RejectionReason
        });

        return Ok(ApiResponse<AppointmentDto>.SuccessResponse(result, "Appointment rejected successfully"));
    }

    [HttpPut("{id:long}/complete")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> MarkCompleted(long id, [FromBody] CompleteAppointmentRequest request)
    {
        var nutritionist = await GetCurrentNutritionistAsync();
        var result = await _mediator.Send(new CompleteAppointmentCommand
        {
            AppointmentId = id,
            NutritionistId = nutritionist.Id,
            CompletedByUserId = GetCurrentUserId(),
            ConsultationNotes = request.ConsultationNotes
        });

        return Ok(ApiResponse<AppointmentDto>.SuccessResponse(result, "Appointment completed successfully"));
    }

    [HttpPut("{id:long}/consultation-notes")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> AddConsultationNotes(long id, [FromBody] ConsultationNotesRequest request)
    {
        var nutritionist = await GetCurrentNutritionistAsync();
        var result = await _mediator.Send(new AddConsultationNotesCommand
        {
            AppointmentId = id,
            NutritionistId = nutritionist.Id,
            ConsultationNotes = request.ConsultationNotes
        });

        return Ok(ApiResponse<AppointmentDto>.SuccessResponse(result, "Consultation notes saved successfully"));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AppointmentDto>>>> GetAllAppointments(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null,
        [FromQuery] long? patientId = null,
        [FromQuery] long? nutritionistId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var result = await _mediator.Send(new GetAllAppointmentsQuery
        {
            FromDate = fromDate,
            ToDate = toDate,
            Status = status,
            PatientId = patientId,
            NutritionistId = nutritionistId,
            Skip = skip,
            Take = take
        });

        return Ok(ApiResponse<IEnumerable<AppointmentDto>>.SuccessResponse(result));
    }

    [HttpGet("{id:long}")]
    [Authorize(Roles = "Patient,Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<AppointmentDto>>> GetAppointment(long id)
    {
        await _clinicalAccess.EnsureAppointmentAccessAsync(id);
        var result = await _mediator.Send(new GetAppointmentByIdQuery { AppointmentId = id });
        return Ok(ApiResponse<AppointmentDto>.SuccessResponse(result));
    }

    [HttpGet("available-slots")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<IEnumerable<DateTime>>>> GetAvailableSlots(
        [FromQuery] long nutritionistId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] int durationMinutes = 30,
        [FromQuery] long? serviceId = null,
        [FromQuery] string timeZoneId = "Asia/Karachi")
    {
        var result = await _mediator.Send(new GetAvailableSlotsQuery
        {
            NutritionistId = nutritionistId,
            FromDate = fromDate,
            ToDate = toDate,
            DurationMinutes = durationMinutes,
            ServiceId = serviceId,
            TimeZoneId = timeZoneId
        });

        return Ok(ApiResponse<IEnumerable<DateTime>>.SuccessResponse(result));
    }

    private async Task<Patient> GetCurrentPatientAsync()
    {
        var userId = GetCurrentUserId();
        var patients = await _patientRepository.GetAsync(query => query.Where(p => p.UserId == userId && p.DeletedAt == null));
        return patients.FirstOrDefault()
            ?? throw new InvalidOperationException("Patient profile was not found for the current user");
    }

    private async Task<Nutritionist> GetCurrentNutritionistAsync()
    {
        var userId = GetCurrentUserId();
        var nutritionists = await _nutritionistRepository.GetAsync(query => query.Where(n => n.UserId == userId && n.DeletedAt == null));
        return nutritionists.FirstOrDefault()
            ?? throw new InvalidOperationException("Nutritionist profile was not found for the current user");
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst("uid")?.Value;
        return long.TryParse(claim, out var userId) ? userId : 0;
    }

    private string GetCurrentRole()
    {
        return User.FindFirst("role")?.Value ?? string.Empty;
    }

    private async Task NotifyAppointmentBookedAsync(AppointmentDto appointment)
    {
        var patient = await _context.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == appointment.PatientId);
        var nutritionist = await _context.Nutritionists
            .Include(n => n.User)
            .FirstOrDefaultAsync(n => n.Id == appointment.NutritionistId);

        var patientRecipient = ToRecipient(patient?.User);
        var nutritionistRecipient = ToRecipient(nutritionist?.User);
        if (patientRecipient == null || nutritionistRecipient == null)
        {
            return;
        }

        await _notificationService.SendAppointmentBookedAsync(
            patientRecipient,
            nutritionistRecipient,
            appointment.Id,
            appointment.AppointmentDateTime);
    }

    private static NotificationRecipient? ToRecipient(User? user)
    {
        return user == null
            ? null
            : new NotificationRecipient(
                user.Id,
                user.Email ?? string.Empty,
                user.PhoneNumber,
                $"{user.FirstName} {user.LastName}".Trim());
    }
}

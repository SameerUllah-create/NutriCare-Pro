namespace NutriCare.Application.Handlers.Appointments;

using MediatR;
using NutriCare.Application.Commands.Appointments;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.Appointments;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public sealed class CreateAppointmentCommandHandler(ClinicalAppointmentWorkflow workflow) : IRequestHandler<CreateAppointmentCommand, AppointmentDto>
{
    public Task<AppointmentDto> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken) => workflow.BookAsync(request, cancellationToken);
}
public sealed class RescheduleAppointmentCommandHandler(ClinicalAppointmentWorkflow workflow) : IRequestHandler<RescheduleAppointmentCommand, AppointmentDto>
{
    public Task<AppointmentDto> Handle(RescheduleAppointmentCommand request, CancellationToken cancellationToken) => workflow.RescheduleAsync(request, cancellationToken);
}
public sealed class CancelAppointmentCommandHandler(ClinicalAppointmentWorkflow workflow) : IRequestHandler<CancelAppointmentCommand, bool>
{
    public Task<bool> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken) => workflow.CancelAsync(request, cancellationToken);
}
public sealed class ApproveAppointmentCommandHandler(ClinicalAppointmentWorkflow workflow) : IRequestHandler<ApproveAppointmentCommand, AppointmentDto>
{
    public Task<AppointmentDto> Handle(ApproveAppointmentCommand request, CancellationToken cancellationToken) => workflow.ChangeStatusAsync(request.AppointmentId, "Confirmed", null, null, cancellationToken);
}
public sealed class RejectAppointmentCommandHandler(ClinicalAppointmentWorkflow workflow) : IRequestHandler<RejectAppointmentCommand, AppointmentDto>
{
    public Task<AppointmentDto> Handle(RejectAppointmentCommand request, CancellationToken cancellationToken) => workflow.ChangeStatusAsync(request.AppointmentId, "Rejected", request.RejectionReason, null, cancellationToken);
}
public sealed class CompleteAppointmentCommandHandler(ClinicalAppointmentWorkflow workflow) : IRequestHandler<CompleteAppointmentCommand, AppointmentDto>
{
    public Task<AppointmentDto> Handle(CompleteAppointmentCommand request, CancellationToken cancellationToken) => workflow.ChangeStatusAsync(request.AppointmentId, "Completed", null, request.ConsultationNotes, cancellationToken);
}
public sealed class AddConsultationNotesCommandHandler(ClinicalAppointmentWorkflow workflow) : IRequestHandler<AddConsultationNotesCommand, AppointmentDto>
{
    public Task<AppointmentDto> Handle(AddConsultationNotesCommand request, CancellationToken cancellationToken) => workflow.AddNoteAsync(request.AppointmentId, request.ConsultationNotes, cancellationToken);
}
public sealed class GetAppointmentByIdQueryHandler(IRepository<Appointment> appointments, IClinicalAccessService access) : IRequestHandler<GetAppointmentByIdQuery, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
    {
        await access.EnsureAppointmentAccessAsync(request.AppointmentId, cancellationToken);
        return ClinicalAppointmentWorkflow.ToDto((await appointments.GetByIdAsync(request.AppointmentId))!);
    }
}
public sealed class GetPatientAppointmentsQueryHandler(IRepository<Appointment> appointments, IClinicalAccessService access, ICurrentUserService user) : IRequestHandler<GetPatientAppointmentsQuery, IEnumerable<AppointmentDto>>
{
    public async Task<IEnumerable<AppointmentDto>> Handle(GetPatientAppointmentsQuery request, CancellationToken cancellationToken)
    {
        await access.EnsurePatientAccessAsync(request.PatientId, cancellationToken: cancellationToken);
        long? clinicianId = user.Role == "Nutritionist" ? await access.GetNutritionistIdAsync(cancellationToken) : null;
        var values = await appointments.GetAsync(q => AppointmentHandlerHelpers.Filter(q.Where(a => a.PatientId == request.PatientId && a.DeletedAt == null &&
            (!clinicianId.HasValue || a.NutritionistId == clinicianId)), request.FromDate, request.ToDate, request.Status).OrderByDescending(a => a.AppointmentDateTime));
        return values.Select(ClinicalAppointmentWorkflow.ToDto).ToArray();
    }
}
public sealed class GetNutritionistAppointmentsQueryHandler(IRepository<Appointment> appointments, IClinicalAccessService access) : IRequestHandler<GetNutritionistAppointmentsQuery, IEnumerable<AppointmentDto>>
{
    public async Task<IEnumerable<AppointmentDto>> Handle(GetNutritionistAppointmentsQuery request, CancellationToken cancellationToken)
    {
        if (request.NutritionistId != await access.GetNutritionistIdAsync(cancellationToken)) throw new ForbiddenAccessException("You can only view your own schedule");
        var values = await appointments.GetAsync(q => AppointmentHandlerHelpers.Filter(q.Where(a => a.NutritionistId == request.NutritionistId && a.DeletedAt == null), request.FromDate, request.ToDate, request.Status).OrderBy(a => a.AppointmentDateTime));
        var permitted = new List<AppointmentDto>();
        foreach (var appointment in values)
            if (await access.CanAccessPatientAsync(appointment.PatientId, cancellationToken)) permitted.Add(ClinicalAppointmentWorkflow.ToDto(appointment));
        return permitted;
    }
}
public sealed class GetAllAppointmentsQueryHandler(IRepository<Appointment> appointments, ICurrentUserService user) : IRequestHandler<GetAllAppointmentsQuery, IEnumerable<AppointmentDto>>
{
    public async Task<IEnumerable<AppointmentDto>> Handle(GetAllAppointmentsQuery request, CancellationToken cancellationToken)
    {
        if (user.Role != "Admin" || !user.CanAccessClinicalData) throw new ForbiddenAccessException("Clinical access permission is required");
        var values = await appointments.GetAsync(q => AppointmentHandlerHelpers.Filter(q.Where(a => a.DeletedAt == null &&
            (!request.PatientId.HasValue || a.PatientId == request.PatientId.Value) && (!request.NutritionistId.HasValue || a.NutritionistId == request.NutritionistId.Value)),
            request.FromDate, request.ToDate, request.Status).OrderByDescending(a => a.AppointmentDateTime).Skip(Math.Max(0, request.Skip)).Take(Math.Clamp(request.Take, 1, 200)));
        return values.Select(ClinicalAppointmentWorkflow.ToDto).ToArray();
    }
}
public sealed class GetAvailableSlotsQueryHandler(IClinicalSchedulingService scheduling) : IRequestHandler<GetAvailableSlotsQuery, IEnumerable<DateTime>>
{
    public async Task<IEnumerable<DateTime>> Handle(GetAvailableSlotsQuery request, CancellationToken cancellationToken)
        => await scheduling.GetAvailableSlotsAsync(request.NutritionistId, request.ServiceId, ClinicalTime.ToUtc(request.FromDate, request.TimeZoneId), ClinicalTime.ToUtc(request.ToDate, request.TimeZoneId), cancellationToken);
}
internal static class AppointmentHandlerHelpers
{
    internal static IQueryable<Appointment> Filter(IQueryable<Appointment> q, DateTime? from, DateTime? to, string? status)
    {
        if (from.HasValue) q = q.Where(a => a.AppointmentDateTime >= from.Value);
        if (to.HasValue) q = q.Where(a => a.AppointmentDateTime <= to.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.Status == status);
        return q;
    }
}

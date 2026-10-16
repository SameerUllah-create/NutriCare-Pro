namespace NutriCare.Application.Services;

using NutriCare.Application.Commands.Appointments;
using System.Text.Json;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

/// <summary>All reservation changes share the same database lock, history and outbox commit.</summary>
public sealed class ClinicalAppointmentWorkflow(
    IRepository<Appointment> appointments,
    IRepository<Patient> patients,
    IRepository<Nutritionist> nutritionists,
    IRepository<IntakeForm> intakes,
    IRepository<ConsentRecord> consents,
    IRepository<PolicyDocument> policies,
    IRepository<ClinicalAssignment> assignments,
    IRepository<AppointmentHistory> history,
    IRepository<ConsultationNote> notes,
    IRepository<CancellationPolicy> cancellationPolicies,
    IRepository<VideoConsultation> videos,
    IRepository<User> users,
    IClinicalAccessService access,
    IClinicalSchedulingService scheduling,
    IAppointmentReservationLock reservationLock,
    INotificationOutbox outbox,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    ICancellationRefundOutbox? cancellationRefunds = null,
    IPackageCreditService? packageCredits = null)
{
    public async Task<AppointmentDto> BookAsync(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        if (request.PatientId != await access.GetPatientIdAsync(cancellationToken)) throw Denied();
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 128)
            throw new ArgumentException("A booking request key of at most 128 characters is required");
        if (request.PackageEnrollmentId.HasValue != !string.IsNullOrWhiteSpace(request.PackageCreditKind) ||
            request.PackageEnrollmentId is <= 0 || (request.PackageCreditKind != null && request.PackageCreditKind is not ("InitialConsultation" or "FollowUpConsultation")))
            throw new ArgumentException("Select a package enrollment and a supported consultation credit together");
        if (request.PackageEnrollmentId.HasValue && packageCredits == null)
            throw new ConflictException("Package credit booking is not available");
        var start = ClinicalTime.ToUtc(request.AppointmentDateTime, request.TimeZoneId);
        var selectedMode = ResolveAppointmentMode("Either", request.AppointmentType);
        return await reservationLock.ExecuteAsync(request.NutritionistId, async () =>
        {
            var duplicate = (await appointments.GetAsync(q => q.Where(a => a.PatientId == request.PatientId &&
                a.IdempotencyKey == request.IdempotencyKey && a.DeletedAt == null))).FirstOrDefault();
            if (duplicate != null)
            {
                if (duplicate.NutritionistId != request.NutritionistId || duplicate.ServiceId != request.ServiceId || duplicate.AppointmentDateTime != start ||
                    ResolveAppointmentMode("Either", duplicate.AppointmentType) != selectedMode ||
                    duplicate.PackageEnrollmentId != request.PackageEnrollmentId || duplicate.PackageCreditKind != request.PackageCreditKind)
                    throw new ConflictException("This booking request key was already used for different details");
                return ToDto(duplicate);
            }
            var clinician = await nutritionists.GetByIdAsync(request.NutritionistId);
            if (clinician == null || clinician.DeletedAt != null || !clinician.IsVerified)
                throw new ArgumentException("The selected nutritionist is not available for booking");
            var clinicianAccount = await users.GetByIdAsync(clinician.UserId);
            if (clinicianAccount == null || clinicianAccount.DeletedAt != null || !clinicianAccount.IsActive || !clinicianAccount.EmailConfirmed)
                throw new ArgumentException("The selected nutritionist is not available for booking");
            var service = await scheduling.ValidateSlotAsync(request.NutritionistId, request.ServiceId, start, cancellationToken: cancellationToken);
            var bookingBuffer = await scheduling.GetBookingBufferAsync(request.NutritionistId, request.ServiceId, start, cancellationToken);
            var appointmentMode = ResolveAppointmentMode(service.Mode, selectedMode);
            var bookingPolicy = await CurrentCancellationPolicyAsync();
            var intake = request.IntakeFormId.HasValue ? await intakes.GetByIdAsync(request.IntakeFormId.Value) : null;
            if (intake == null || intake.PatientId != request.PatientId || intake.DeletedAt != null)
                throw new ArgumentException("Complete your patient intake before booking");
            await RequireConsentAsync(intake.ConsentRecordId, "HealthData", cancellationToken);
            if (appointmentMode == "Online")
            {
                var grants = await consents.GetAsync(q => q.Where(c => c.UserId == currentUser.UserId && c.Purpose == "Teleconsultation" && c.WithdrawnAt == null && c.DeletedAt == null));
                var grant = grants.OrderByDescending(c => c.GrantedAt).FirstOrDefault();
                if (grant == null) throw new ArgumentException("Teleconsultation consent is required for a video appointment");
                await RequireConsentAsync(grant.Id, "Teleconsultation", cancellationToken);
            }
            var appointment = new Appointment
            {
                PatientId = request.PatientId, NutritionistId = request.NutritionistId, ServiceId = service.Id,
                IntakeFormId = intake.Id, TimeZoneId = ClinicalTime.Zone(request.TimeZoneId).Id,
                AppointmentDateTime = start, Duration = service.DurationMinutes, AppointmentType = appointmentMode,
                Notes = request.Notes, Status = "Pending", IdempotencyKey = request.IdempotencyKey,
                PriceSnapshot = service.Price, Currency = service.Currency, BookingBufferMinutesSnapshot = bookingBuffer,
                CancellationPolicyId = bookingPolicy.Id, CancellationPolicyVersion = bookingPolicy.Version,
                CancellationPolicySnapshotJson = JsonSerializer.Serialize(bookingPolicy)
            };
            await appointments.AddAsync(appointment);
            await unitOfWork.SaveChangesAsync();
            if (request.PackageEnrollmentId.HasValue)
                await packageCredits!.ReserveAppointmentAsync(request.PackageEnrollmentId.Value, appointment.Id, request.PackageCreditKind!, currentUser.UserId, cancellationToken);
            var activeAssignment = await assignments.GetAsync(q => q.Where(a => a.PatientId == appointment.PatientId && a.NutritionistId == appointment.NutritionistId &&
                a.DeletedAt == null && a.StartedAt <= DateTime.UtcNow && (!a.EndedAt.HasValue || a.EndedAt > DateTime.UtcNow)));
            if (!activeAssignment.Any()) await assignments.AddAsync(new ClinicalAssignment { PatientId = appointment.PatientId, NutritionistId = appointment.NutritionistId, StartedAt = DateTime.UtcNow });
            await AddHistoryAsync(appointment, null, "Pending", "Patient booking");
            await NotifyAsync(appointment, "AppointmentBooked", "Appointment requested", "Your appointment request has been received. Review the appointment in your portal.", "booked");
            await unitOfWork.SaveChangesAsync();
            return ToDto(appointment);
        }, cancellationToken);
    }

    public async Task<AppointmentDto> RescheduleAsync(RescheduleAppointmentCommand request, CancellationToken cancellationToken)
    {
        var existing = await GetRequiredAsync(request.AppointmentId);
        await access.EnsureAppointmentAccessAsync(existing.Id, cancellationToken);
        if (currentUser.Role != "Patient" || existing.PatientId != request.PatientId) throw Denied();
        return await reservationLock.ExecuteAsync(existing.NutritionistId, async () =>
        {
            var appointment = await GetRequiredAsync(request.AppointmentId);
            EnsureMutable(appointment);
            var policy = await BookedCancellationPolicyAsync(appointment);
            var now = DateTime.UtcNow;
            var notice = appointment.AppointmentDateTime - now;
            if (notice < TimeSpan.FromHours(policy.FreeRescheduleHours))
                throw new ConflictException("The self-service rescheduling cutoff has passed. Contact the clinic.");
            if (notice < TimeSpan.FromHours(policy.FullRefundHours))
            {
                // The quota applies only to short-notice changes. Earlier changes remain
                // available, and the total counter still records every reschedule.
                var previousChanges = await history.GetAsync(q => q.Where(h => h.AppointmentId == appointment.Id &&
                    h.PreviousStartAt != null && h.NewStartAt != null && h.PreviousStartAt != h.NewStartAt &&
                    h.DeletedAt == null && h.ChangedByUserId == currentUser.UserId));
                var lateUses = previousChanges.Count(h => h.PreviousStartAt!.Value - h.CreatedAt >= TimeSpan.FromHours(policy.FreeRescheduleHours) &&
                    h.PreviousStartAt.Value - h.CreatedAt < TimeSpan.FromHours(policy.FullRefundHours));
                if (lateUses >= policy.MaxFreeReschedules)
                    throw new ConflictException("The permitted short-notice self-service reschedules have been used. Contact the clinic.");
            }
            var start = ClinicalTime.ToUtc(request.AppointmentDateTime, request.TimeZoneId);
            var service = await scheduling.ValidateSlotAsync(appointment.NutritionistId, appointment.ServiceId, start, appointment.Id, cancellationToken);
            var bookingBuffer = await scheduling.GetBookingBufferAsync(appointment.NutritionistId, appointment.ServiceId, start, cancellationToken);
            ResolveAppointmentMode(service.Mode, appointment.AppointmentType);
            var previous = appointment.AppointmentDateTime;
            var fromStatus = appointment.Status;
            appointment.PreviousStartAt = previous;
            appointment.AppointmentDateTime = start;
            appointment.Duration = service.DurationMinutes;
            appointment.BookingBufferMinutesSnapshot = Math.Max(appointment.BookingBufferMinutesSnapshot ?? 0, bookingBuffer);
            appointment.Notes = request.Notes ?? appointment.Notes;
            appointment.TimeZoneId = ClinicalTime.Zone(request.TimeZoneId).Id;
            appointment.Status = "Pending";
            appointment.ApprovedAt = null;
            appointment.ApprovedByUserId = null;
            appointment.RejectedAt = null;
            appointment.RejectedByUserId = null;
            appointment.RejectionReason = null;
            appointment.RescheduledFromId = null;
            appointment.RescheduledAt = DateTime.UtcNow;
            appointment.RescheduledByUserId = currentUser.UserId;
            appointment.RescheduleCount++;
            if (appointment.PackageEnrollmentId.HasValue)
                await (packageCredits ?? throw new ConflictException("Package credit validation is not available")).ValidateRescheduleAsync(appointment, cancellationToken);
            await CancelLinkedVideoAsync(appointment);
            await appointments.UpdateAsync(appointment);
            await AddHistoryAsync(appointment, fromStatus, appointment.Status, "Patient reschedule", previous);
            await NotifyAsync(appointment, "AppointmentRescheduled", "Appointment rescheduled", "Your appointment time changed. Review the updated time in your portal.", $"rescheduled:{appointment.RescheduleCount}");
            await unitOfWork.SaveChangesAsync();
            return ToDto(appointment);
        }, cancellationToken);
    }

    public async Task<bool> CancelAsync(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        var existing = await GetRequiredAsync(request.AppointmentId);
        await access.EnsureAppointmentAccessAsync(existing.Id, cancellationToken);
        return await reservationLock.ExecuteAsync(existing.NutritionistId, async () =>
        {
            var appointment = await GetRequiredAsync(existing.Id);
            if (appointment.Status == "Cancelled") return true;
            EnsureMutable(appointment);
            var policy = await BookedCancellationPolicyAsync(appointment);
            if (currentUser.Role == "Patient" && appointment.AppointmentDateTime <= DateTime.UtcNow)
                throw new ConflictException("This appointment has started; contact the clinic to resolve its status");
            var previousStatus = appointment.Status;
            appointment.Status = "Cancelled";
            appointment.CancellationReason = request.CancellationReason;
            appointment.CancelledBy = currentUser.Role;
            appointment.CancelledAt = DateTime.UtcNow;
            if (appointment.PackageEnrollmentId.HasValue)
                await (packageCredits ?? throw new ConflictException("Package credit reconciliation is not available")).ApplyAppointmentStatusAsync(appointment, currentUser.UserId, currentUser.Role, cancellationToken);
            if (cancellationRefunds != null)
                await cancellationRefunds.StageAsync(appointment, currentUser.UserId, currentUser.Role, appointment.CancelledAt.Value, cancellationToken);
            await CancelLinkedVideoAsync(appointment);
            await appointments.UpdateAsync(appointment);
            var eligible = !appointment.PackageEnrollmentId.HasValue && (currentUser.Role == "Nutritionist" ? policy.ProviderCancellationRefund :
                appointment.AppointmentDateTime >= DateTime.UtcNow.AddHours(policy.FullRefundHours));
            await AddHistoryAsync(appointment, previousStatus, "Cancelled", $"{request.CancellationReason ?? "Cancelled"}; policy v{policy.Version}; refund eligible: {eligible}");
            await NotifyAsync(appointment, "AppointmentCancelled", "Appointment cancelled", "Your appointment was cancelled. Review the cancellation policy and any payment adjustment in your portal.", "cancelled");
            await unitOfWork.SaveChangesAsync();
            return true;
        }, cancellationToken);
    }

    public async Task<AppointmentDto> ChangeStatusAsync(long appointmentId, string status, string? reason, string? consultationNotes, CancellationToken cancellationToken)
    {
        var existing = await GetRequiredAsync(appointmentId);
        await access.EnsureAppointmentAccessAsync(appointmentId, cancellationToken);
        if (currentUser.Role != "Nutritionist") throw Denied();
        return await reservationLock.ExecuteAsync(existing.NutritionistId, async () =>
        {
            var appointment = await GetRequiredAsync(appointmentId);
            var from = appointment.Status;
            if (from == status) return ToDto(appointment);
            var allowed = status switch
            {
                "Confirmed" or "Rejected" => from == "Pending",
                "Completed" => (from is "Confirmed" or "Approved" or "InProgress") && appointment.AppointmentDateTime <= DateTime.UtcNow,
                "No-show" => (from is "Confirmed" or "Approved") && appointment.AppointmentDateTime.AddMinutes(appointment.Duration) <= DateTime.UtcNow,
                _ => false
            };
            if (!allowed) throw new ConflictException("The requested appointment state transition is not permitted");
            appointment.Status = status;
            if (status == "Confirmed") { appointment.ApprovedAt = DateTime.UtcNow; appointment.ApprovedByUserId = currentUser.UserId; }
            if (status == "Rejected") { appointment.RejectedAt = DateTime.UtcNow; appointment.RejectedByUserId = currentUser.UserId; appointment.RejectionReason = reason; await CancelLinkedVideoAsync(appointment); }
            if (cancellationRefunds != null && (status is "Rejected" or "No-show"))
                await cancellationRefunds.StageAsync(appointment, currentUser.UserId, currentUser.Role, appointment.RejectedAt ?? DateTime.UtcNow, cancellationToken);
            if (status == "Completed") { appointment.CompletedAt = DateTime.UtcNow; appointment.CompletedByUserId = currentUser.UserId; }
            if (appointment.PackageEnrollmentId.HasValue)
                await (packageCredits ?? throw new ConflictException("Package credit reconciliation is not available")).ApplyAppointmentStatusAsync(appointment, currentUser.UserId, currentUser.Role, cancellationToken);
            if (!string.IsNullOrWhiteSpace(consultationNotes)) await AppendNoteAsync(appointment, consultationNotes);
            await appointments.UpdateAsync(appointment);
            await AddHistoryAsync(appointment, from, status, reason);
            await NotifyAsync(appointment, "Appointment" + status.Replace("-", ""), "Appointment updated", "Your appointment status changed. Review it in your secure portal.", "state:" + status);
            await unitOfWork.SaveChangesAsync();
            return ToDto(appointment);
        }, cancellationToken);
    }

    public async Task<AppointmentDto> AddNoteAsync(long appointmentId, string content, CancellationToken cancellationToken)
    {
        await access.EnsureAppointmentAccessAsync(appointmentId, cancellationToken);
        if (currentUser.Role != "Nutritionist") throw Denied();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 5000) throw new ArgumentException("Consultation notes must contain between 1 and 5000 characters");
        var appointment = await GetRequiredAsync(appointmentId);
        if (appointment.Status is "Cancelled" or "Rejected") throw new ConflictException("Notes cannot be added to a cancelled appointment");
        await AppendNoteAsync(appointment, content);
        await appointments.UpdateAsync(appointment);
        await unitOfWork.SaveChangesAsync();
        return ToDto(appointment);
    }

    private async Task AppendNoteAsync(Appointment appointment, string content)
    {
        var previous = (await notes.GetAsync(q => q.Where(n => n.AppointmentId == appointment.Id && n.DeletedAt == null).OrderByDescending(n => n.CreatedAt))).FirstOrDefault();
        await notes.AddAsync(new ConsultationNote { AppointmentId = appointment.Id, AuthorUserId = currentUser.UserId, Content = content, SupersedesId = previous?.Id });
        appointment.ConsultationNotes = content;
    }

    private async Task RequireConsentAsync(long consentId, string purpose, CancellationToken cancellationToken)
    {
        var consent = await consents.GetByIdAsync(consentId);
        if (consent == null || consent.DeletedAt != null || consent.UserId != currentUser.UserId || consent.WithdrawnAt != null || consent.Purpose != purpose)
            throw new ArgumentException("Current consent is required before this action");
        var now = DateTime.UtcNow;
        var documents = await policies.GetAsync(q => q.Where(p => p.Purpose == purpose && p.Version == consent.PolicyVersion && p.IsPublished && p.DeletedAt == null &&
            (!p.EffectiveFrom.HasValue || p.EffectiveFrom <= now) && (!p.EffectiveTo.HasValue || p.EffectiveTo > now)));
        if (!documents.Any(p => p.Content == consent.ConsentText &&
            (consent.PresentedLanguage != "Urdu" || (!string.IsNullOrWhiteSpace(p.ContentUrdu) && p.ContentUrdu == consent.ConsentTextUrdu))))
            throw new ArgumentException("Review and accept the current published consent policy");
    }

    private async Task<CancellationPolicy> CurrentCancellationPolicyAsync()
    {
        var now = DateTime.UtcNow;
        return (await cancellationPolicies.GetAsync(q => q.Where(p => p.IsPublished && p.Status == "Published" && p.DeletedAt == null &&
            (!p.EffectiveFrom.HasValue || p.EffectiveFrom <= now) && (!p.EffectiveTo.HasValue || p.EffectiveTo > now)).OrderByDescending(p => p.Version))).FirstOrDefault()
            ?? throw new ConflictException("The clinic cancellation policy has not been published. Contact the clinic.");
    }

    private Task<CancellationPolicy> BookedCancellationPolicyAsync(Appointment appointment)
    {
        var policy = string.IsNullOrWhiteSpace(appointment.CancellationPolicySnapshotJson) ? null :
            JsonSerializer.Deserialize<CancellationPolicy>(appointment.CancellationPolicySnapshotJson);
        if (policy == null || policy.Id != appointment.CancellationPolicyId || policy.Version != appointment.CancellationPolicyVersion)
            throw new ConflictException("This booking has no traceable cancellation policy. Contact the clinic.");
        return Task.FromResult(policy);
    }

    private async Task CancelLinkedVideoAsync(Appointment appointment)
    {
        var linked = await videos.GetAsync(q => q.Where(v => v.AppointmentId == appointment.Id && v.DeletedAt == null));
        foreach (var video in linked)
        {
            video.Status = "Cancelled"; video.MeetingUrl = null; video.HostUrl = null; video.JoinToken = null;
            video.ProviderSyncStatus = "Pending";
            video.ScheduledStartTime = appointment.AppointmentDateTime;
            video.ScheduledEndTime = appointment.AppointmentDateTime.AddMinutes(appointment.Duration);
            await videos.UpdateAsync(video);
        }
    }

    private Task<AppointmentHistory> AddHistoryAsync(Appointment appointment, string? from, string to, string? reason, DateTime? previousStart = null)
        => history.AddAsync(new AppointmentHistory { AppointmentId = appointment.Id, FromStatus = from, ToStatus = to,
            PreviousStartAt = previousStart, NewStartAt = appointment.AppointmentDateTime, ChangedByUserId = currentUser.UserId, Reason = reason });

    private async Task NotifyAsync(Appointment appointment, string type, string title, string message, string key)
    {
        var patient = await patients.GetByIdAsync(appointment.PatientId);
        var clinician = await nutritionists.GetByIdAsync(appointment.NutritionistId);
        foreach (var userId in new[] { patient?.UserId, clinician?.UserId }.Where(id => id.HasValue).Select(id => id!.Value).Distinct())
        {
            var user = await users.GetByIdAsync(userId);
            if (user == null) continue;
            await outbox.EnqueueAsync(new NotificationRecipient(user.Id, user.Email ?? "", user.PhoneNumber, $"{user.FirstName} {user.LastName}"),
                new NotificationRequest(type, title, message, "Appointment", appointment.Id, "/appointments", ["InApp", "Email"]),
                $"appointment:{appointment.Id}:{key}:user:{user.Id}");
        }
    }

    private async Task<Appointment> GetRequiredAsync(long id)
    {
        var appointment = await appointments.GetByIdAsync(id);
        if (appointment != null) await appointments.ReloadAsync(appointment);
        return appointment is { DeletedAt: null } ? appointment : throw new EntityNotFoundException(nameof(Appointment), id);
    }

    private static void EnsureMutable(Appointment appointment)
    {
        if (appointment.Status is not ("Pending" or "Confirmed" or "Approved")) throw new ConflictException("This appointment can no longer be changed through self service");
    }

    private static string ResolveAppointmentMode(string serviceMode, string requestedMode)
    {
        var selected = requestedMode switch
        {
            "Video" or "Online" => "Online",
            "InPerson" => "InPerson",
            _ => throw new ArgumentException("Choose Online or InPerson as the appointment mode")
        };
        var offered = serviceMode switch
        {
            "Video" or "Online" => "Online",
            "InPerson" => "InPerson",
            "Either" => "Either",
            _ => throw new ArgumentException("This service does not support an appointment")
        };
        if (offered != "Either" && selected != offered)
            throw new ArgumentException("The selected appointment mode is not offered by this service");
        return selected;
    }

    private static ForbiddenAccessException Denied() => new("You can only manage your own appointment");

    public static AppointmentDto ToDto(Appointment a) => new()
    {
        Id = a.Id, PatientId = a.PatientId, NutritionistId = a.NutritionistId, AppointmentDateTime = DateTime.SpecifyKind(a.AppointmentDateTime, DateTimeKind.Utc),
        ServiceId = a.ServiceId, TimeZoneId = a.TimeZoneId, Duration = a.Duration, AppointmentType = a.AppointmentType, Status = a.Status,
        PriceSnapshot = a.PriceSnapshot, Currency = a.Currency, IsPaid = a.IsPaid, PaymentStatus = a.PaymentStatus,
        PackageEnrollmentId = a.PackageEnrollmentId, PackageCreditKind = a.PackageCreditKind,
        CancellationPolicyId = a.CancellationPolicyId, CancellationPolicyVersion = a.CancellationPolicyVersion, CancellationPolicySnapshotJson = a.CancellationPolicySnapshotJson,
        Location = a.Location, Notes = a.Notes, ConsultationNotes = a.ConsultationNotes, CancellationReason = a.CancellationReason,
        CancelledBy = a.CancelledBy, CancelledAt = a.CancelledAt, ApprovedAt = a.ApprovedAt, ApprovedByUserId = a.ApprovedByUserId,
        RejectedAt = a.RejectedAt, RejectedByUserId = a.RejectedByUserId, RejectionReason = a.RejectionReason, CompletedAt = a.CompletedAt,
        CompletedByUserId = a.CompletedByUserId, RescheduledFromId = a.RescheduledFromId, RescheduledAt = a.RescheduledAt,
        RescheduledByUserId = a.RescheduledByUserId, RescheduleCount = a.RescheduleCount, RowVersion = a.RowVersion, CreatedAt = a.CreatedAt, UpdatedAt = a.UpdatedAt
    };
}

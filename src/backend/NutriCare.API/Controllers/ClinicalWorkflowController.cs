namespace NutriCare.API.Controllers;

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

[ApiController]
[Authorize]
[Route("api/clinical")]
public sealed class ClinicalWorkflowController(NutriCareDbContext context, IClinicalAccessService access,
    ICurrentUserService user, ClinicalAppointmentWorkflow appointments, ClinicalPlanWorkflow plans, IAppointmentReservationLock reservationLock) : ControllerBase
{
    [HttpGet("patients")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<IActionResult> Patients(CancellationToken cancellationToken)
    {
        var clinicianId = await access.GetNutritionistIdAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var ids = await context.ClinicalAssignments.Where(a => a.NutritionistId == clinicianId && a.DeletedAt == null && a.StartedAt <= now &&
            (!a.EndedAt.HasValue || a.EndedAt > now)).Select(a => a.PatientId).Distinct().ToListAsync(cancellationToken);
        var values = await context.Patients.Where(p => ids.Contains(p.Id) && p.DeletedAt == null)
            .Select(p => new { p.Id, p.UserId, Name = p.User!.FirstName + " " + p.User.LastName, p.CurrentWeight, p.TargetWeight, p.Height, p.SelectedMetricsJson })
            .OrderBy(p => p.Name).Take(200).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(values));
    }

    [HttpGet("consents")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> Consents(CancellationToken cancellationToken)
    {
        var values = await context.ConsentRecords.Where(c => c.UserId == user.UserId && c.DeletedAt == null)
            .OrderByDescending(c => c.GrantedAt).Select(c => new { c.Id, c.Purpose, c.PolicyVersion, c.PolicyTitle, c.ConsentText,
                c.ConsentTextUrdu, c.PresentedLanguage, c.GrantedAt, c.WithdrawnAt }).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(values));
    }

    [HttpPost("consents")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> GrantConsent(GrantClinicalConsentRequest request, CancellationToken cancellationToken)
    {
        if (!request.Accepted) throw new ArgumentException("Explicit acceptance is required");
        var language = request.Language.Trim().ToLowerInvariant() switch
        {
            "en" or "english" => "English",
            "ur" or "urdu" => "Urdu",
            _ => throw new ArgumentException("Choose English or Urdu for the consent presentation language")
        };
        var now = DateTime.UtcNow;
        var policy = await context.PolicyDocuments.SingleOrDefaultAsync(p => p.Purpose == request.Purpose && p.Version == request.PolicyVersion &&
            p.DeletedAt == null && p.IsPublished && (!p.EffectiveFrom.HasValue || p.EffectiveFrom <= now) && (!p.EffectiveTo.HasValue || p.EffectiveTo > now), cancellationToken)
            ?? throw new ConflictException("An approved effective consent policy is not available for this purpose/version");
        if (string.IsNullOrWhiteSpace(policy.Content)) throw new ConflictException("The policy has no approved consent text");
        if (language == "Urdu" && string.IsNullOrWhiteSpace(policy.ContentUrdu))
            throw new ConflictException("An approved Urdu translation is required before accepting this policy in Urdu");
        var existing = await context.ConsentRecords.FirstOrDefaultAsync(c => c.UserId == user.UserId && c.Purpose == policy.Purpose &&
            c.PolicyVersion == policy.Version && c.ConsentText == policy.Content && c.ConsentTextUrdu == policy.ContentUrdu &&
            c.PresentedLanguage == language && c.WithdrawnAt == null && c.DeletedAt == null, cancellationToken);
        if (existing != null) return Ok(ApiResponse<object>.SuccessResponse(new { existing.Id, existing.Purpose, existing.PolicyVersion, existing.PresentedLanguage, existing.GrantedAt }));
        var record = new ConsentRecord { UserId = user.UserId, Purpose = policy.Purpose, PolicyVersion = policy.Version,
            PolicyTitle = policy.Title, ConsentText = policy.Content, ConsentTextUrdu = policy.ContentUrdu, PresentedLanguage = language,
            GrantedAt = now, IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString() };
        context.ConsentRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(new { record.Id, record.Purpose, record.PolicyVersion, record.PresentedLanguage, record.GrantedAt }));
    }

    [HttpPost("consents/{id:long}/withdraw")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> WithdrawConsent(long id, CancellationToken cancellationToken)
    {
        var patientId = await access.GetPatientIdAsync(cancellationToken);
        return await reservationLock.ExecuteAsync(-patientId, async () =>
        {
            var consent = await context.ConsentRecords.SingleOrDefaultAsync(c => c.Id == id && c.UserId == user.UserId && c.DeletedAt == null, cancellationToken)
                ?? throw new EntityNotFoundException(nameof(ConsentRecord), id);
            if (consent.WithdrawnAt.HasValue)
                return (IActionResult)Ok(ApiResponse<bool>.SuccessResponse(true, "Consent was already withdrawn."));
            consent.WithdrawnAt = DateTime.UtcNow;
            if (consent.Purpose == "HealthData")
            {
                var withdrawnAt = consent.WithdrawnAt.Value;
                // Include all assignment history, including soft-deleted records:
                // its existence deliberately suppresses legacy inferred access.
                var assignments = await context.ClinicalAssignments.Where(a => a.PatientId == patientId).ToListAsync(cancellationToken);
                foreach (var assignment in assignments.Where(a => a.DeletedAt == null && (!a.EndedAt.HasValue || a.EndedAt > withdrawnAt)))
                    assignment.EndedAt = withdrawnAt;

                var legacyClinicians = await context.Appointments.Where(a => a.PatientId == patientId && a.DeletedAt == null &&
                    (a.Status == "Confirmed" || a.Status == "Approved" || a.Status == "Completed"))
                    .Select(a => a.NutritionistId)
                    .Union(context.DietPlans.Where(p => p.PatientId == patientId && p.DeletedAt == null && p.PublishedAt != null)
                        .Select(p => p.NutritionistId)).Distinct().ToListAsync(cancellationToken);
                var recordedClinicians = assignments.Select(a => a.NutritionistId).ToHashSet();
                foreach (var clinicianId in legacyClinicians.Where(clinicianId => !recordedClinicians.Contains(clinicianId)))
                    context.ClinicalAssignments.Add(new ClinicalAssignment { PatientId = patientId, NutritionistId = clinicianId,
                        StartedAt = withdrawnAt, EndedAt = withdrawnAt });
            }
            await context.SaveChangesAsync(cancellationToken);
            return (IActionResult)Ok(ApiResponse<bool>.SuccessResponse(true, "Consent withdrawn. Required record retention and existing care obligations follow the published privacy policy."));
        }, cancellationToken);
    }

    [HttpGet("intake")]
    public async Task<IActionResult> Intake([FromQuery] long? patientId, CancellationToken cancellationToken)
    {
        var id = patientId ?? await access.GetPatientIdAsync(cancellationToken);
        await access.EnsurePatientAccessAsync(id, cancellationToken: cancellationToken);
        var values = await context.IntakeForms.Where(i => i.PatientId == id && i.DeletedAt == null)
            .OrderByDescending(i => i.Version).Select(i => new { i.Id, i.PatientId, i.Goals, i.MedicalHistory, i.Allergies, i.CurrentMedications,
                i.Lifestyle, i.ConsentRecordId, i.SubmittedAt, i.Version }).Take(50).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(values));
    }

    [HttpPost("intake")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> SubmitIntake(ClinicalIntakeRequest request, CancellationToken cancellationToken)
    {
        var patientId = await access.GetPatientIdAsync(cancellationToken);
        return await reservationLock.ExecuteAsync(-patientId, async () =>
        {
            var consent = await context.ConsentRecords.SingleOrDefaultAsync(c => c.Id == request.ConsentRecordId && c.UserId == user.UserId &&
                c.Purpose == "HealthData" && c.WithdrawnAt == null && c.DeletedAt == null, cancellationToken)
                ?? throw new ArgumentException("Accept the published health-data consent before submitting intake");
            var now = DateTime.UtcNow;
            var effective = await context.PolicyDocuments.AnyAsync(p => p.Purpose == consent.Purpose && p.Version == consent.PolicyVersion && p.Content == consent.ConsentText &&
                (consent.PresentedLanguage != "Urdu" || (!string.IsNullOrWhiteSpace(p.ContentUrdu) && p.ContentUrdu == consent.ConsentTextUrdu)) &&
                p.IsPublished && p.DeletedAt == null && (!p.EffectiveFrom.HasValue || p.EffectiveFrom <= now) && (!p.EffectiveTo.HasValue || p.EffectiveTo > now), cancellationToken);
            if (!effective) throw new ConflictException("Review the currently effective health-data policy");
            var version = await context.IntakeForms.Where(i => i.PatientId == patientId).Select(i => (int?)i.Version).MaxAsync(cancellationToken) ?? 0;
            var intake = new IntakeForm { PatientId = patientId, Goals = request.Goals.Trim(), MedicalHistory = request.MedicalHistory,
                Allergies = request.Allergies, CurrentMedications = request.CurrentMedications, Lifestyle = request.Lifestyle,
                ConsentRecordId = consent.Id, SubmittedAt = now, Version = version + 1 };
            context.IntakeForms.Add(intake);
            await context.SaveChangesAsync(cancellationToken);
            return (IActionResult)Ok(ApiResponse<object>.SuccessResponse(new { intake.Id, intake.PatientId, intake.Version, intake.SubmittedAt }));
        }, cancellationToken);
    }

    [HttpGet("appointments/{id:long}/history")]
    public async Task<IActionResult> AppointmentHistory(long id, CancellationToken cancellationToken)
    {
        await access.EnsureAppointmentAccessAsync(id, cancellationToken);
        var values = await context.AppointmentHistories.Where(h => h.AppointmentId == id && h.DeletedAt == null)
            .OrderBy(h => h.CreatedAt).Select(h => new { h.Id, h.FromStatus, h.ToStatus, h.PreviousStartAt, h.NewStartAt, h.ChangedByUserId, h.Reason, h.CreatedAt }).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(values));
    }

    [HttpGet("appointments/{id:long}/notes")]
    public async Task<IActionResult> ConsultationNotes(long id, CancellationToken cancellationToken)
    {
        await access.EnsureAppointmentAccessAsync(id, cancellationToken);
        var values = await context.ConsultationNotes.Where(n => n.AppointmentId == id && n.DeletedAt == null)
            .OrderBy(n => n.CreatedAt).Select(n => new { n.Id, n.AuthorUserId, n.Content, n.SupersedesId, n.CreatedAt }).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(values));
    }

    [HttpPost("appointments/{id:long}/no-show")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<IActionResult> NoShow(long id, CancellationToken cancellationToken)
        => Ok(ApiResponse<AppointmentDto>.SuccessResponse(await appointments.ChangeStatusAsync(id, "No-show", "Patient did not attend", null, cancellationToken)));

    [HttpPut("patients/{patientId:long}/metrics")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<IActionResult> SelectMetrics(long patientId, SelectClinicalMetricsRequest request, CancellationToken cancellationToken)
    {
        await access.EnsureAssignedClinicianAsync(patientId, cancellationToken);
        var allowed = new[] { "weight", "bmi", "bodyFatPercentage", "waistCircumference", "chestCircumference", "hipCircumference", "armCircumference", "thighCircumference", "waterIntake", "dailyCalories", "exerciseMinutes" };
        if (request.Metrics.Length == 0 || request.Metrics.Any(m => !allowed.Contains(m))) throw new ArgumentException("Choose at least one supported metric");
        var patient = await context.Patients.SingleAsync(p => p.Id == patientId && p.DeletedAt == null, cancellationToken);
        patient.SelectedMetricsJson = JsonSerializer.Serialize(request.Metrics.Distinct().ToArray());
        await context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(new { patientId, metrics = request.Metrics.Distinct() }));
    }

    [HttpGet("plans/{id:long}/versions")]
    public async Task<IActionResult> PlanVersions(long id, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.SuccessResponse(await plans.VersionsAsync(id, cancellationToken)));

    [HttpPost("plans/{id:long}/publish")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<IActionResult> PublishPlan(long id, PlanVersionRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<DietPlanDto>.SuccessResponse(await plans.PublishAsync(id, request.ExpectedVersion, cancellationToken)));

    [HttpPost("plans/{id:long}/archive")]
    [Authorize(Roles = "Nutritionist")]
    public async Task<IActionResult> ArchivePlan(long id, PlanVersionRequest request, CancellationToken cancellationToken)
    {
        await plans.ArchiveAsync(id, request.ExpectedVersion, cancellationToken);
        return Ok(ApiResponse<bool>.SuccessResponse(true));
    }

    [HttpGet("plans/{id:long}/adherence")]
    public async Task<IActionResult> PlanAdherence(long id, CancellationToken cancellationToken)
    {
        var plan = await plans.VisibleAsync(id, cancellationToken);
        var entries = await context.DietPlanCompliances.Where(c => c.DietPlanId == plan.Id && c.DeletedAt == null)
            .OrderByDescending(c => c.RecordDate).Take(180).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(entries.Select(c => new { c.Id, c.RecordDate, c.MealsCompleted, c.TotalMeals,
            c.CompliancePercentage, c.WaterIntakeLiters, c.TookSupplements, c.Notes, c.CreatedAt, c.UpdatedAt })));
    }

    [HttpGet("progress/{id:long}/history")]
    public async Task<IActionResult> ProgressHistory(long id, CancellationToken cancellationToken)
    {
        var progress = await context.WeightTrackings.SingleOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(WeightTracking), id);
        await access.EnsurePatientAccessAsync(progress.PatientId, cancellationToken: cancellationToken);
        var revisions = await context.ProgressRevisions.Where(r => r.WeightTrackingId == id && r.DeletedAt == null)
            .OrderByDescending(r => r.CreatedAt).Select(r => new { r.Id, r.SnapshotJson, r.ChangedByUserId, r.Reason, r.CreatedAt }).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(revisions));
    }

    [HttpGet("reports/{id:long}/notes")]
    public async Task<IActionResult> MedicalReportNotes(long id, CancellationToken cancellationToken)
    {
        var report = await context.MedicalReports.SingleOrDefaultAsync(r => r.Id == id && r.DeletedAt == null, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(MedicalReport), id);
        await access.EnsurePatientAccessAsync(report.PatientId, cancellationToken: cancellationToken);
        var entries = await context.MedicalReportNotes.Where(n => n.MedicalReportId == id && n.DeletedAt == null)
            .OrderBy(n => n.CreatedAt).Select(n => new { n.Id, n.Content, n.AuthorUserId, n.SupersedesId, n.CreatedAt }).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(entries));
    }
}

public sealed class GrantClinicalConsentRequest
{
    [Required, MaxLength(10)] public string Language { get; set; } = "en";
    [Required, MaxLength(100)] public string Purpose { get; set; } = "HealthData";
    [Required, MaxLength(100)] public string PolicyVersion { get; set; } = string.Empty;
    public bool Accepted { get; set; }
}
public sealed class ClinicalIntakeRequest
{
    [Required, StringLength(5000, MinimumLength = 1)] public string Goals { get; set; } = string.Empty;
    [MaxLength(10000)] public string? MedicalHistory { get; set; }
    [MaxLength(5000)] public string? Allergies { get; set; }
    [MaxLength(5000)] public string? CurrentMedications { get; set; }
    [MaxLength(10000)] public string? Lifestyle { get; set; }
    [Range(1, long.MaxValue)] public long ConsentRecordId { get; set; }
}
public sealed class SelectClinicalMetricsRequest
{
    [Required, MaxLength(20)] public string[] Metrics { get; set; } = [];
}
public sealed class PlanVersionRequest
{
    [Range(1, int.MaxValue)] public int ExpectedVersion { get; set; }
}

namespace NutriCare.Application.Services;

using System.Text.Json;
using NutriCare.Application.Commands.DietPlans;
using NutriCare.Application.DTOs;
using NutriCare.Application.Handlers.DietPlans;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public sealed class ClinicalPlanWorkflow(
    IRepository<DietPlan> plans, IRepository<DietPlanItem> items, IRepository<DietPlanVersion> versions,
    IRepository<Patient> patients, IRepository<User> users, IClinicalAccessService access, ICurrentUserService user,
    IAppointmentReservationLock reservationLock, INotificationOutbox outbox, IUnitOfWork unitOfWork)
{
    public async Task<DietPlanDto> CreateAsync(CreateDietPlanCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAssignedClinicianAsync(request.PatientId, cancellationToken);
        if (request.NutritionistId != await access.GetNutritionistIdAsync(cancellationToken)) throw Denied();
        ValidateReviewDate(request.ReviewDate, request.StartDate, request.EndDate);
        return await reservationLock.ExecuteAsync(request.NutritionistId, async () =>
        {
            var plan = new DietPlan { Status = "Draft", Version = 1 };
            DietPlanHandlerHelpers.ApplyPlanFields(plan, request);
            plan.ReviewDate = request.ReviewDate?.Date;
            await plans.AddAsync(plan);
            await unitOfWork.SaveChangesAsync();
            var dto = DietPlanHandlerHelpers.ToDto(plan, plan.Items.Where(i => i.DeletedAt == null));
            await versions.AddAsync(Snapshot(plan, dto, request.ChangeReason ?? "Initial draft"));
            await unitOfWork.SaveChangesAsync();
            return dto;
        }, cancellationToken);
    }

    public async Task<DietPlanDto> UpdateAsync(UpdateDietPlanCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAssignedClinicianAsync(request.PatientId, cancellationToken);
        if (request.NutritionistId != await access.GetNutritionistIdAsync(cancellationToken)) throw Denied();
        return await reservationLock.ExecuteAsync(request.NutritionistId, async () =>
        {
            var plan = await RequiredAsync(request.DietPlanId);
            if (plan.NutritionistId != request.NutritionistId || plan.PatientId != request.PatientId) throw Denied();
            if (plan.Status == "Archived") throw new ConflictException("Create a new plan to resume care after archiving");
            if (!request.ExpectedVersion.HasValue || request.ExpectedVersion != plan.Version) throw new ConflictException("The plan changed. Reload it before saving a revision");
            ValidateReviewDate(request.ReviewDate, request.StartDate, request.EndDate);
            var oldItems = (await items.GetAsync(q => q.Where(i => i.DietPlanId == plan.Id && i.DeletedAt == null))).ToArray();
            foreach (var item in oldItems) { item.DeletedAt = DateTime.UtcNow; await items.UpdateAsync(item); }
            DietPlanHandlerHelpers.ApplyPlanFields(plan, request);
            plan.ReviewDate = request.ReviewDate?.Date;
            plan.Status = "Draft";
            await plans.UpdateAsync(plan);
            await unitOfWork.SaveChangesAsync();
            var dto = DietPlanHandlerHelpers.ToDto(plan, plan.Items.Where(i => i.DeletedAt == null));
            await versions.AddAsync(Snapshot(plan, dto, request.ChangeReason));
            await unitOfWork.SaveChangesAsync();
            return dto;
        }, cancellationToken);
    }

    public async Task<DietPlanDto> PublishAsync(long planId, int expectedVersion, CancellationToken cancellationToken)
    {
        var existing = await RequiredAsync(planId);
        await access.EnsureAssignedClinicianAsync(existing.PatientId, cancellationToken);
        if (existing.NutritionistId != await access.GetNutritionistIdAsync(cancellationToken)) throw Denied();
        return await reservationLock.ExecuteAsync(existing.NutritionistId, async () =>
        {
            var plan = await RequiredAsync(planId);
            if (plan.Version != expectedVersion) throw new ConflictException("The plan changed; review the current draft before publication");
            if (plan.Status == "Archived") throw new ConflictException("An archived plan cannot be published");
            var version = (await versions.GetAsync(q => q.Where(v => v.DietPlanId == planId && v.VersionNumber == expectedVersion && v.DeletedAt == null))).SingleOrDefault()
                ?? throw new ConflictException("The plan revision snapshot is missing");
            if (version.PublishedAt.HasValue) return await VisibleAsync(planId, cancellationToken);
            version.PublishedAt = DateTime.UtcNow;
            plan.PublishedAt = version.PublishedAt;
            plan.PublishedVersionId = version.Id;
            plan.Status = "Active";
            await versions.UpdateAsync(version);
            await plans.UpdateAsync(plan);
            var patient = await patients.GetByIdAsync(plan.PatientId);
            var recipient = patient == null ? null : await users.GetByIdAsync(patient.UserId);
            if (recipient != null)
                await outbox.EnqueueAsync(new NotificationRecipient(recipient.Id, recipient.Email ?? "", recipient.PhoneNumber, recipient.FirstName),
                    new NotificationRequest("DietPlanAssigned", "Diet plan ready", "Your nutritionist published an updated diet plan. Review it in your secure portal.", "DietPlan", plan.Id, "/plans", ["InApp", "Email"]),
                    $"plan:{plan.Id}:published:v{plan.Version}:user:{recipient.Id}", cancellationToken);
            await unitOfWork.SaveChangesAsync();
            return await VisibleAsync(planId, cancellationToken);
        }, cancellationToken);
    }

    public async Task ArchiveAsync(long planId, int expectedVersion, CancellationToken cancellationToken)
    {
        var plan = await RequiredAsync(planId);
        await access.EnsureAssignedClinicianAsync(plan.PatientId, cancellationToken);
        if (plan.NutritionistId != await access.GetNutritionistIdAsync(cancellationToken)) throw Denied();
        if (plan.Version != expectedVersion) throw new ConflictException("The plan changed. Reload it before archiving");
        plan.Status = "Archived";
        await plans.UpdateAsync(plan);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task<DietPlanDto> VisibleAsync(long planId, CancellationToken cancellationToken)
    {
        var plan = await RequiredAsync(planId);
        await access.EnsurePatientAccessAsync(plan.PatientId, cancellationToken: cancellationToken);
        if (user.Role == "Nutritionist" && plan.NutritionistId != await access.GetNutritionistIdAsync(cancellationToken)) throw Denied();
        if (user.Role != "Patient") return DietPlanHandlerHelpers.ToDto(plan, await DietPlanHandlerHelpers.GetPlanItemsAsync(items, plan.Id));
        if (!plan.PublishedVersionId.HasValue) throw new EntityNotFoundException(nameof(DietPlan), planId);
        var version = await versions.GetByIdAsync(plan.PublishedVersionId.Value);
        if (version == null || !version.PublishedAt.HasValue || version.DeletedAt != null) throw new EntityNotFoundException(nameof(DietPlanVersion), plan.PublishedVersionId.Value);
        return Deserialize(version, plan.Status == "Archived" ? "Archived" : "Active");
    }

    public async Task<IReadOnlyCollection<DietPlanDto>> PatientPlansAsync(long patientId, string? status, CancellationToken cancellationToken)
    {
        await access.EnsurePatientAccessAsync(patientId, cancellationToken: cancellationToken);
        long? clinicianId = user.Role == "Nutritionist" ? await access.GetNutritionistIdAsync(cancellationToken) : null;
        var patientView = user.Role == "Patient";
        var values = await plans.GetAsync(q => q.Where(p => p.PatientId == patientId && p.DeletedAt == null &&
            (!clinicianId.HasValue || p.NutritionistId == clinicianId) && (!patientView || p.PublishedVersionId != null)).OrderByDescending(p => p.StartDate));
        var results = new List<DietPlanDto>();
        foreach (var plan in values)
        {
            var dto = await VisibleAsync(plan.Id, cancellationToken);
            if (string.IsNullOrEmpty(status) || dto.Status == status) results.Add(dto);
        }
        return results.OrderByDescending(p => p.StartDate).ToArray();
    }

    public async Task<IReadOnlyCollection<object>> VersionsAsync(long planId, CancellationToken cancellationToken)
    {
        var plan = await RequiredAsync(planId);
        await access.EnsurePatientAccessAsync(plan.PatientId, cancellationToken: cancellationToken);
        if (user.Role == "Nutritionist" && plan.NutritionistId != await access.GetNutritionistIdAsync(cancellationToken)) throw Denied();
        var patientView = user.Role == "Patient";
        var history = await versions.GetAsync(q => q.Where(v => v.DietPlanId == planId && v.DeletedAt == null &&
            (!patientView || v.PublishedAt != null)).OrderByDescending(v => v.VersionNumber));
        return history.Select(v => (object)new { v.Id, v.VersionNumber, v.PublishedAt, v.CreatedAt, v.CreatedByUserId, v.ChangeReason,
            Plan = Deserialize(v, v.PublishedAt.HasValue ? "Published" : "Draft") }).ToArray();
    }

    private DietPlanVersion Snapshot(DietPlan plan, DietPlanDto dto, string? reason) => new()
    {
        DietPlanId = plan.Id, VersionNumber = plan.Version, SnapshotJson = JsonSerializer.Serialize(dto), CreatedByUserId = user.UserId, ChangeReason = reason
    };
    private static DietPlanDto Deserialize(DietPlanVersion version, string status)
    {
        var dto = JsonSerializer.Deserialize<DietPlanDto>(version.SnapshotJson) ?? throw new ConflictException("The plan revision is not readable");
        dto.Version = version.VersionNumber; dto.PublishedAt = version.PublishedAt; dto.Status = status;
        return dto;
    }
    private async Task<DietPlan> RequiredAsync(long id)
    {
        var plan = await plans.GetByIdAsync(id);
        return plan is { DeletedAt: null } ? plan : throw new EntityNotFoundException(nameof(DietPlan), id);
    }
    private static void ValidateReviewDate(DateTime? review, DateTime start, DateTime end)
    {
        if (!review.HasValue || review.Value.Date < start.Date || review.Value.Date > end.Date)
            throw new ArgumentException("Choose a review date within the plan dates");
    }
    private static ForbiddenAccessException Denied() => new("Only the assigned author can manage this clinical plan");
}

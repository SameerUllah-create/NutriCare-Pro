namespace NutriCare.Persistence.Configuration;

using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Application.Services;
using NutriCare.Persistence.Contexts;

/// <summary>Internal entitlement engine. It neither sells packages nor creates charges.</summary>
public sealed class PackageCreditStore(NutriCareDbContext db) : IPackageCreditService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public Task<PackageEnrollment> IssueFromVerifiedPaymentAsync(long paymentId, long actorUserId, CancellationToken ct = default) =>
        LockedAsync($"purchase:{paymentId}", async () =>
        {
            var existing = await db.PackageEnrollments.SingleOrDefaultAsync(e => e.PaymentId == paymentId, ct);
            if (existing != null) return existing;
            var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == paymentId && p.DeletedAt == null, ct)
                ?? throw new EntityNotFoundException("Payment", paymentId);
            await db.Entry(payment).ReloadAsync(ct);
            if (payment.RelatedToType != "Package" || payment.RelatedToId == null || payment.Status != "Completed" || payment.PaidAt == null || payment.RefundedAmount != 0 ||
                await db.PaymentRefunds.AnyAsync(r => r.PaymentId == paymentId && r.Status != "Failed" && r.Status != "Cancelled", ct))
                throw new ConflictException("A verified, unrefunded package payment is required");
            await VerifyPaymentAsync(payment, ct);
            var package = await db.ServicePackages.SingleOrDefaultAsync(p => p.Id == payment.RelatedToId && p.DeletedAt == null, ct)
                ?? throw new EntityNotFoundException("Package", payment.RelatedToId.Value);
            await db.Entry(package).ReloadAsync(ct);
            if (!package.IsPublished || !package.IsActive || package.NutritionistApprovedById == null || package.NutritionistApprovedAt == null ||
                package.NutritionistApprovedVersion != package.Version || package.NutritionistApprovedAt > payment.CreatedAt ||
                (package.EffectiveFrom.HasValue && package.EffectiveFrom > payment.CreatedAt) || (package.EffectiveTo.HasValue && package.EffectiveTo <= payment.CreatedAt) ||
                package.Price != payment.Amount || package.Currency != payment.Currency)
                throw new ConflictException("The paid package must match an effective, nutritionist-reviewed offer; request reconciliation");
            PackageConfigurationRules.ValidateForPublication(package);
            var enrollment = new PackageEnrollment
            {
                ServicePackageId = package.Id, PatientId = payment.PatientId, PaymentId = payment.Id, PackageVersion = package.Version,
                DefinitionSnapshotJson = JsonSerializer.Serialize(package), PurchasedAtUtc = payment.PaidAt.Value,
                FirstConsultationDueAtUtc = payment.PaidAt.Value.AddDays(package.FirstConsultationWithinDays)
            };
            db.PackageEnrollments.Add(enrollment);
            await db.SaveChangesAsync(ct);
            History(enrollment, null, actorUserId, "Purchased", "Verified package payment; no credits consumed");
            return enrollment;
        }, ct);

    public Task<PackageCreditUsage> ReserveAppointmentAsync(long enrollmentId, long appointmentId, string kind, long actorUserId, CancellationToken ct = default) =>
        LockedAsync($"enrollment:{enrollmentId}", async () =>
        {
            if (kind is not ("InitialConsultation" or "FollowUpConsultation")) throw new ArgumentException("Select a supported consultation credit");
            var enrollment = await EnrollmentAsync(enrollmentId, ct);
            var appointment = await db.Appointments.SingleOrDefaultAsync(a => a.Id == appointmentId && a.DeletedAt == null, ct)
                ?? throw new EntityNotFoundException("Appointment", appointmentId);
            await db.Entry(appointment).ReloadAsync(ct);
            if (appointment.PatientId != enrollment.PatientId || (appointment.PackageEnrollmentId.HasValue && appointment.PackageEnrollmentId != enrollment.Id))
                throw new ForbiddenAccessException("The appointment does not belong to this package patient");
            var patient = await db.Patients.SingleAsync(p => p.Id == appointment.PatientId && p.DeletedAt == null, ct);
            if (actorUserId != patient.UserId || !await db.Users.AnyAsync(u => u.Id == actorUserId && u.IsActive && u.EmailConfirmed && u.DeletedAt == null, ct))
                throw new ForbiddenAccessException("Only the active package patient may reserve this credit");
            var prior = await db.PackageCreditUsages.SingleOrDefaultAsync(u => u.ResourceType == "Appointment" && u.ResourceId == appointmentId, ct);
            if (prior != null)
            {
                if (prior.PackageEnrollmentId != enrollmentId || prior.CreditKind != kind) throw new ConflictException("The appointment already uses another package credit");
                if (prior.Status is "Released") throw new ConflictException("A released appointment credit cannot be reused");
                return prior;
            }
            if (appointment.Status is not ("Pending" or "Confirmed" or "Approved")) throw new ConflictException("Only a live reservation may reserve a credit");
            var definition = Definition(enrollment);
            await EnsureSpendableAsync(enrollment, ct);
            await EnsureClinicianAsync(appointment.NutritionistId, definition, ct);
            var service = await db.Services.SingleOrDefaultAsync(s => s.Id == appointment.ServiceId && s.DeletedAt == null, ct)
                ?? throw new ConflictException("A configured service is required for package use");
            await db.Entry(service).ReloadAsync(ct);
            if (!service.IsPublished || !service.IsActive || service.NutritionistId != appointment.NutritionistId)
                throw new ConflictException("An active service from the package clinician is required");
            if (kind == "InitialConsultation" && (!definition.EligibleServiceCodes.Contains(service.PackageServiceCode ?? "", StringComparer.Ordinal) || appointment.Duration != definition.InitialConsultationMinutes) ||
                kind == "FollowUpConsultation" && (service.PackageServiceCode != "FollowUp" || appointment.Duration != definition.FollowUpConsultationMinutes))
                throw new ConflictException("This service or duration is excluded from the package");
            ValidateDates(enrollment, appointment, kind);
            await EnsureCapacityAsync(enrollmentId, kind, definition, ct);
            var usage = new PackageCreditUsage { PackageEnrollmentId = enrollmentId, CreditKind = kind, ResourceType = "Appointment", ResourceId = appointmentId, ServiceCode = service.PackageServiceCode };
            db.PackageCreditUsages.Add(usage);
            appointment.PackageEnrollmentId = enrollment.Id; appointment.PackageCreditKind = kind;
            appointment.IsPaid = true; appointment.PaymentStatus = "PackageCreditReserved";
            await db.SaveChangesAsync(ct);
            History(enrollment, usage, actorUserId, "Reserved", "Reserved consultation credit; completion consumes it");
            return usage;
        }, ct);

    public async Task ValidateRescheduleAsync(Appointment appointment, CancellationToken ct = default)
    {
        if (!appointment.PackageEnrollmentId.HasValue) return;
        await LockedAsync($"enrollment:{appointment.PackageEnrollmentId.Value}", async () =>
        {
            var enrollment = await EnrollmentAsync(appointment.PackageEnrollmentId.Value, ct);
            await EnsureSpendableAsync(enrollment, ct);
            await EnsureClinicianAsync(appointment.NutritionistId, Definition(enrollment), ct);
            ValidateDates(enrollment, appointment, appointment.PackageCreditKind ?? "");
            var usage = await db.PackageCreditUsages.SingleOrDefaultAsync(u => u.ResourceType == "Appointment" && u.ResourceId == appointment.Id, ct);
            if (usage == null || usage.PackageEnrollmentId != enrollment.Id || usage.CreditKind != appointment.PackageCreditKind || usage.Status != "Reserved") throw new ConflictException("An active reserved package credit is required");
            return true;
        }, ct);
    }

    public async Task ApplyAppointmentStatusAsync(Appointment appointment, long actorUserId, string actor, CancellationToken ct = default)
    {
        if (!appointment.PackageEnrollmentId.HasValue) return;
        await LockedAsync($"enrollment:{appointment.PackageEnrollmentId.Value}", async () =>
        {
            var enrollment = await EnrollmentAsync(appointment.PackageEnrollmentId.Value, ct);
            var usage = await db.PackageCreditUsages.SingleOrDefaultAsync(u => u.ResourceType == "Appointment" && u.ResourceId == appointment.Id && u.PackageEnrollmentId == enrollment.Id, ct)
                ?? throw new ConflictException("The package credit reservation is missing");
            await db.Entry(usage).ReloadAsync(ct);
            var definition = Definition(enrollment);
            await EnsureActorAsync(appointment, actorUserId, actor, ct);
            await EnsureClinicianAsync(appointment.NutritionistId, definition, ct);
            if (usage.CreditKind != appointment.PackageCreditKind) throw new ConflictException("The appointment credit kind changed");
            if (usage.Status is "Consumed" or "Released" or "ReviewRequired") return true;
            if (usage.Status != "Reserved") throw new ConflictException("The package credit needs review");
            string? decision = null;
            if (appointment.Status == "Completed")
            {
                if (actor != "Nutritionist") throw new ForbiddenAccessException("Only the package clinician may complete a consultation");
                await EnsureSpendableAsync(enrollment, ct);
                if (appointment.AppointmentDateTime > DateTime.UtcNow) throw new ConflictException("A future consultation cannot activate or consume package credits");
                ValidateDates(enrollment, appointment, usage.CreditKind);
                if (enrollment.ActivatedAtUtc == null)
                {
                    if (usage.CreditKind != "InitialConsultation") throw new ConflictException("Complete the initial consultation before follow-ups");
                    enrollment.ActivatedAtUtc = DateTime.UtcNow;
                    enrollment.ExpiresAtUtc = enrollment.ActivatedAtUtc.Value.AddDays(definition.CreditExpiryDays);
                    enrollment.Status = "Active";
                    History(enrollment, usage, actorUserId, "Activated", "Activated by first completed consultation");
                }
                decision = "Consume";
            }
            else if (appointment.Status is "Cancelled" or "Rejected")
                decision = actor == "Nutritionist" && definition.RestoreCreditsOnProviderCancellation ? "Release" : actor == "Patient" ? PolicyCreditAction(appointment, false) : "ReviewRequired";
            else if (appointment.Status is "No-show" or "NoShow") decision = PolicyCreditAction(appointment, true);
            if (decision == null) return true;
            usage.Status = decision switch { "Consume" => "Consumed", "Release" => "Released", _ => "ReviewRequired" };
            appointment.PaymentStatus = "PackageCredit" + usage.Status;
            History(enrollment, usage, actorUserId, usage.Status, "Appointment " + appointment.Status + "; booked policy decision: " + decision);
            return true;
        }, ct);
    }

    public Task<PackageCreditUsage> ConsumePlanVersionAsync(long enrollmentId, long versionId, bool isRevision, long actorUserId, CancellationToken ct = default) =>
        LockedAsync($"enrollment:{enrollmentId}", async () =>
        {
            var enrollment = await EnrollmentAsync(enrollmentId, ct);
            var version = await db.DietPlanVersions.SingleOrDefaultAsync(v => v.Id == versionId && v.DeletedAt == null && v.PublishedAt != null, ct)
                ?? throw new ConflictException("A published plan version is required");
            var plan = await db.DietPlans.SingleAsync(p => p.Id == version.DietPlanId, ct);
            await db.Entry(version).ReloadAsync(ct); await db.Entry(plan).ReloadAsync(ct);
            if (plan.DeletedAt != null || version.PublishedAt == null) throw new ConflictException("A retained published plan version is required");
            if (plan.PatientId != enrollment.PatientId) throw new ForbiddenAccessException("The plan belongs to another patient");
            var definition = Definition(enrollment);
            var clinician = await EnsureClinicianAsync(plan.NutritionistId, definition, ct);
            if (actorUserId != clinician.UserId) throw new ForbiddenAccessException("Only the package clinician may assign a plan credit");
            var kind = isRevision ? "DietPlanRevision" : "DietPlan";
            var prior = await db.PackageCreditUsages.SingleOrDefaultAsync(u => u.ResourceType == "PlanVersion" && u.ResourceId == versionId, ct);
            if (prior != null)
            {
                if (prior.PackageEnrollmentId != enrollmentId || prior.CreditKind != kind) throw new ConflictException("This plan version already used another credit");
                return prior;
            }
            await EnsureSpendableAsync(enrollment, ct);
            if (enrollment.ActivatedAtUtc == null || enrollment.ExpiresAtUtc == null || enrollment.ExpiresAtUtc <= DateTime.UtcNow ||
                version.PublishedAt < enrollment.ActivatedAtUtc || version.PublishedAt > enrollment.ExpiresAtUtc || version.PublishedAt > DateTime.UtcNow)
                throw new ConflictException("A plan published within an active unexpired package is required");
            var previous = await db.DietPlanVersions.Where(v => v.DietPlanId == plan.Id && v.Id != versionId && v.PublishedAt != null && v.VersionNumber < version.VersionNumber).AnyAsync(ct);
            if (previous != isRevision) throw new ConflictException("The plan credit kind must match its published version history");
            await EnsureCapacityAsync(enrollmentId, kind, definition, ct);
            var usage = new PackageCreditUsage { PackageEnrollmentId = enrollmentId, CreditKind = kind, ResourceType = "PlanVersion", ResourceId = versionId, Status = "Consumed" };
            db.PackageCreditUsages.Add(usage); await db.SaveChangesAsync(ct);
            History(enrollment, usage, actorUserId, "Consumed", kind + " assigned; retained plan access follows the retention policy");
            return usage;
        }, ct);

    public Task<PackageEnrollment> ExtendAsync(long enrollmentId, int days, string reason, byte[] expectedRowVersion, long actorUserId, CancellationToken ct = default) =>
        LockedAsync($"enrollment:{enrollmentId}", async () =>
        {
            if (days is < 1 or > 3660 || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000) throw new ArgumentException("Supply 1–3660 extension days and a reason of at most 1000 characters");
            var enrollment = await EnrollmentAsync(enrollmentId, ct);
            if (!await db.Users.AnyAsync(u => u.Id == actorUserId && u.IsActive && u.EmailConfirmed && u.DeletedAt == null, ct) ||
                !await db.UserRoles.AnyAsync(ur => ur.UserId == actorUserId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin"), ct))
                throw new ForbiddenAccessException("Only an active administrator may extend package validity");
            if (expectedRowVersion.Length == 0 || !enrollment.RowVersion.SequenceEqual(expectedRowVersion)) throw new ConflictException("The package changed; reload before extending");
            var previous = enrollment.ExpiresAtUtc ?? enrollment.FirstConsultationDueAtUtc;
            var next = previous.AddDays(days);
            if (enrollment.ActivatedAtUtc == null) enrollment.FirstConsultationDueAtUtc = next;
            else { enrollment.ExpiresAtUtc = next; enrollment.Status = "Active"; }
            db.PackageCreditHistory.Add(new PackageCreditHistory { PackageEnrollmentId = enrollment.Id, ActorUserId = actorUserId, Action = "ValidityExtended", Reason = reason.Trim(), PreviousExpiryUtc = previous, NewExpiryUtc = next });
            return enrollment;
        }, ct);

    private async Task<PackageEnrollment> EnrollmentAsync(long id, CancellationToken ct)
    {
        var enrollment = await db.PackageEnrollments.SingleOrDefaultAsync(e => e.Id == id && e.DeletedAt == null, ct) ?? throw new EntityNotFoundException("Package enrollment", id);
        await db.Entry(enrollment).ReloadAsync(ct);
        if (enrollment.DeletedAt != null) throw new EntityNotFoundException("Package enrollment", id);
        if (enrollment.Status == "Cancelled") throw new ConflictException("The package has been cancelled");
        return enrollment;
    }
    private static ServicePackage Definition(PackageEnrollment enrollment)
    {
        try
        {
            var definition = JsonSerializer.Deserialize<ServicePackage>(enrollment.DefinitionSnapshotJson, JsonOptions) ?? throw new ConflictException("The purchased package definition requires review");
            if (definition.Id != enrollment.ServicePackageId || definition.Version != enrollment.PackageVersion || !definition.IsPublished || !definition.IsActive ||
                definition.NutritionistApprovedById == null || definition.NutritionistApprovedAt == null || definition.NutritionistApprovedVersion != definition.Version)
                throw new ConflictException("The purchased package definition requires review");
            PackageConfigurationRules.ValidateForPublication(definition);
            return definition;
        }
        catch (JsonException) { throw new ConflictException("The purchased package definition requires review"); }
    }
    private static void ValidateDates(PackageEnrollment enrollment, Appointment appointment, string kind)
    {
        if (enrollment.ActivatedAtUtc == null)
        {
            if (kind != "InitialConsultation" || appointment.AppointmentDateTime < enrollment.PurchasedAtUtc || appointment.AppointmentDateTime > enrollment.FirstConsultationDueAtUtc) throw new ConflictException("Schedule the initial consultation within the package purchase window");
        }
        else if (enrollment.ExpiresAtUtc == null || appointment.AppointmentDateTime < enrollment.ActivatedAtUtc || appointment.AppointmentDateTime.AddMinutes(appointment.Duration) > enrollment.ExpiresAtUtc)
            throw new ConflictException("The consultation must fit inside the package credit validity period");
    }
    private async Task EnsureCapacityAsync(long id, string kind, ServicePackage definition, CancellationToken ct)
    {
        var limit = kind switch { "InitialConsultation" => definition.InitialConsultationCount, "FollowUpConsultation" => definition.FollowUpConsultationCount, "DietPlan" => definition.DietPlanCount, "DietPlanRevision" => definition.DietPlanRevisionCount, _ => 0 };
        if (await db.PackageCreditUsages.CountAsync(u => u.PackageEnrollmentId == id && u.CreditKind == kind && u.Status != "Released", ct) >= limit) throw new ConflictException("No unreserved package credits remain for this item");
    }
    private static string PolicyCreditAction(Appointment appointment, bool noShow)
    {
        try
        {
            var policy = JsonSerializer.Deserialize<CancellationPolicy>(appointment.CancellationPolicySnapshotJson ?? "", JsonOptions);
            if (policy == null || !policy.IsPublished || policy.Status != "Published" || policy.ApprovedAt == null || policy.ApprovedByUserId == null ||
                policy.ApprovedAt > appointment.CreatedAt || (policy.EffectiveFrom.HasValue && policy.EffectiveFrom > appointment.CreatedAt) ||
                (policy.EffectiveTo.HasValue && policy.EffectiveTo <= appointment.CreatedAt) || policy.Id != appointment.CancellationPolicyId || policy.Version != appointment.CancellationPolicyVersion) return "ReviewRequired";
            var value = noShow ? policy.PackageNoShowCreditAction : policy.PackagePatientCancellationCreditAction;
            return value is "Consume" or "Release" ? value : "ReviewRequired";
        }
        catch (JsonException) { return "ReviewRequired"; }
    }

    private async Task VerifyPaymentAsync(Payment payment, CancellationToken ct)
    {
        if (payment.DeletedAt != null || payment.Status != "Completed" || payment.PaidAt == null || payment.RefundedAmount != 0 ||
            payment.PaymentMethod != "Stripe" || payment.ProviderPaymentId?.StartsWith("pi_", StringComparison.Ordinal) != true ||
            !await db.ProviderWebhookEvents.AnyAsync(e => e.PaymentId == payment.Id && e.Provider == "Stripe", ct) ||
            await db.PaymentRefunds.AnyAsync(r => r.PaymentId == payment.Id && r.Status != "Failed" && r.Status != "Cancelled", ct))
            throw new ConflictException("The package payment or refund status requires reconciliation before spending credits");
    }

    private async Task EnsureSpendableAsync(PackageEnrollment enrollment, CancellationToken ct)
    {
        if (enrollment.Status is not ("AwaitingFirstConsultation" or "Active")) throw new ConflictException("The package is not available for new credit spending");
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == enrollment.PaymentId, ct) ?? throw new ConflictException("The package purchase requires review");
        await db.Entry(payment).ReloadAsync(ct);
        await VerifyPaymentAsync(payment, ct);
        var definition = Definition(enrollment);
        if (payment.PatientId != enrollment.PatientId || payment.RelatedToType != "Package" || payment.RelatedToId != enrollment.ServicePackageId ||
            payment.Amount != definition.Price || payment.Currency != definition.Currency)
            throw new ConflictException("The verified package purchase no longer matches its recorded entitlement");
    }

    private async Task<Nutritionist> EnsureClinicianAsync(long nutritionistId, ServicePackage definition, CancellationToken ct)
    {
        var clinician = await db.Nutritionists.SingleOrDefaultAsync(n => n.Id == nutritionistId && n.DeletedAt == null, ct);
        if (clinician != null) await db.Entry(clinician).ReloadAsync(ct);
        if (clinician == null || !clinician.IsVerified || clinician.UserId != definition.NutritionistApprovedById ||
            !await db.Users.AnyAsync(u => u.Id == clinician.UserId && u.DeletedAt == null && u.IsActive && u.EmailConfirmed, ct))
            throw new ForbiddenAccessException("This package is available only with its approved active nutritionist");
        return clinician;
    }

    private async Task EnsureActorAsync(Appointment appointment, long userId, string role, CancellationToken ct)
    {
        var allowed = role switch
        {
            "Patient" => appointment.Status == "Cancelled" && await db.Patients.AnyAsync(p => p.Id == appointment.PatientId && p.UserId == userId && p.DeletedAt == null, ct),
            "Nutritionist" => await db.Nutritionists.AnyAsync(n => n.Id == appointment.NutritionistId && n.UserId == userId && n.DeletedAt == null, ct),
            "Admin" => appointment.Status == "Cancelled" && await db.UserRoles.AnyAsync(ur => ur.UserId == userId && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin"), ct),
            _ => false
        };
        if (!allowed || !await db.Users.AnyAsync(u => u.Id == userId && u.DeletedAt == null && u.IsActive && u.EmailConfirmed, ct))
            throw new ForbiddenAccessException("The credit action does not match the authenticated appointment participant");
    }

    private void History(PackageEnrollment enrollment, PackageCreditUsage? usage, long actor, string action, string reason) => db.PackageCreditHistory.Add(new PackageCreditHistory { PackageEnrollmentId = enrollment.Id, PackageCreditUsageId = usage?.Id, ActorUserId = actor, Action = action, Reason = reason });
    private async Task<T> LockedAsync<T>(string key, Func<Task<T>> action, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction != null) { await AcquireAsync(key, ct); var nested = await action(); await db.SaveChangesAsync(ct); return nested; }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try { await AcquireAsync(key, ct); var result = await action(); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result; }
        catch { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); throw; }
    }
    private Task AcquireAsync(string key, CancellationToken ct) => db.Database.IsSqlServer()
        ? db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={"NutriCare:package:" + key},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Package credits are busy; retry.',1;", ct)
        : Task.CompletedTask;
}

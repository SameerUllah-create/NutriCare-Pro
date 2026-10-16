namespace NutriCare.Tests.Integrations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Persistence.Configuration;
using NutriCare.Persistence.Contexts;
using NutriCare.Persistence.Repositories;
using NutriCare.Tests.Configuration;

[Trait("Category", "SqlIntegration")]
public sealed class SqlPackageCreditTests(IsolatedSqlDatabase fixture) : IClassFixture<IsolatedSqlDatabase>
{
    [Fact]
    public async Task Eight_concurrent_reservations_share_one_credit_without_over_allocation()
    {
        var seed = await SeedAsync(); var ids = new List<long>();
        for (var i = 0; i < 8; i++) ids.Add(await AppointmentAsync(seed, DateTime.UtcNow.AddDays(i + 1)));
        var attempts = ids.Select(id => Task.Run(async () =>
        {
            await using var db = fixture.Open();
            try { await new PackageCreditStore(db).ReserveAppointmentAsync(seed.EnrollmentId, id, "InitialConsultation", seed.PatientUserId); return true; }
            catch (ConflictException) { return false; }
        }));
        Assert.Equal(1, (await Task.WhenAll(attempts)).Count(success => success));
        await using var check = fixture.Open();
        Assert.Equal(1, await check.PackageCreditUsages.CountAsync(u => u.PackageEnrollmentId == seed.EnrollmentId && u.Status == "Reserved"));
        Assert.Equal(1, await check.PackageCreditHistory.CountAsync(h => h.PackageEnrollmentId == seed.EnrollmentId && h.Action == "Reserved"));
        Assert.Null((await check.PackageEnrollments.SingleAsync(e => e.Id == seed.EnrollmentId)).ActivatedAtUtc);
    }

    [Fact]
    public async Task Retrying_the_same_resource_does_not_spend_twice_or_change_its_credit_kind()
    {
        var seed = await SeedAsync(); var id = await AppointmentAsync(seed, DateTime.UtcNow.AddDays(1));
        await using var db = fixture.Open(); var store = new PackageCreditStore(db);
        var first = await store.ReserveAppointmentAsync(seed.EnrollmentId, id, "InitialConsultation", seed.PatientUserId);
        var retry = await store.ReserveAppointmentAsync(seed.EnrollmentId, id, "InitialConsultation", seed.PatientUserId);
        Assert.Equal(first.Id, retry.Id);
        await Assert.ThrowsAsync<ConflictException>(() => store.ReserveAppointmentAsync(seed.EnrollmentId, id, "FollowUpConsultation", seed.PatientUserId));
        Assert.Equal(1, await db.PackageCreditUsages.CountAsync(u => u.PackageEnrollmentId == seed.EnrollmentId));
    }

    [Fact]
    public async Task Purchase_window_and_full_session_expiry_are_enforced_on_reservation_and_reschedule()
    {
        var seed = await SeedAsync(); DateTime purchased, due;
        await using (var db = fixture.Open()) { var e = await db.PackageEnrollments.SingleAsync(e => e.Id == seed.EnrollmentId); purchased = e.PurchasedAtUtc; due = e.FirstConsultationDueAtUtc; }
        foreach (var start in new[] { purchased.AddSeconds(-1), due.AddSeconds(1) })
        {
            var id = await AppointmentAsync(seed, start);
            await using var db = fixture.Open();
            await Assert.ThrowsAsync<ConflictException>(() => new PackageCreditStore(db).ReserveAppointmentAsync(seed.EnrollmentId, id, "InitialConsultation", seed.PatientUserId));
        }
        await ActivateAsync(seed);
        DateTime expiry;
        await using (var db = fixture.Open()) expiry = (await db.PackageEnrollments.SingleAsync(e => e.Id == seed.EnrollmentId)).ExpiresAtUtc!.Value;
        var follow = await AppointmentAsync(seed, expiry.AddMinutes(-20), follow: true);
        await using (var db = fixture.Open()) await new PackageCreditStore(db).ReserveAppointmentAsync(seed.EnrollmentId, follow, "FollowUpConsultation", seed.PatientUserId);
        await using (var db = fixture.Open())
        {
            var appointment = await db.Appointments.SingleAsync(a => a.Id == follow); appointment.AppointmentDateTime = expiry.AddMinutes(-19);
            await Assert.ThrowsAsync<ConflictException>(() => new PackageCreditStore(db).ValidateRescheduleAsync(appointment));
        }
        await using var verify = fixture.Open();
        Assert.Equal(expiry.AddMinutes(-20), (await verify.Appointments.SingleAsync(a => a.Id == follow)).AppointmentDateTime);
    }

    [Fact]
    public async Task Only_completed_initial_consultation_activates_the_purchased_snapshot()
    {
        var seed = await SeedAsync();
        var id = await AppointmentAsync(seed, DateTime.UtcNow.AddMinutes(-1));
        await using (var db = fixture.Open())
        {
            var store = new PackageCreditStore(db); await store.ReserveAppointmentAsync(seed.EnrollmentId, id, "InitialConsultation", seed.PatientUserId);
            var appointment = await db.Appointments.SingleAsync(a => a.Id == id); appointment.Status = "Confirmed";
            await store.ApplyAppointmentStatusAsync(appointment, seed.ClinicianUserId, "Nutritionist");
            Assert.Null((await db.PackageEnrollments.SingleAsync(e => e.Id == seed.EnrollmentId)).ActivatedAtUtc);
            var offer = await db.ServicePackages.SingleAsync(p => p.Id == seed.PackageId); offer.CreditExpiryDays = 90; await db.SaveChangesAsync();
            appointment.Status = "Completed"; appointment.CompletedAt = DateTime.UtcNow;
            await store.ApplyAppointmentStatusAsync(appointment, seed.ClinicianUserId, "Nutritionist");
        }
        await using var check = fixture.Open(); var enrollment = await check.PackageEnrollments.SingleAsync(e => e.Id == seed.EnrollmentId);
        Assert.NotNull(enrollment.ActivatedAtUtc); Assert.Equal(enrollment.ActivatedAtUtc.Value.AddDays(14), enrollment.ExpiresAtUtc);
        Assert.Equal("Consumed", (await check.PackageCreditUsages.SingleAsync(u => u.ResourceId == id && u.ResourceType == "Appointment")).Status);
    }

    [Theory]
    [InlineData("ReviewRequired", "ReviewRequired")]
    [InlineData("Release", "Released")]
    [InlineData("Consume", "Consumed")]
    public async Task No_show_uses_only_the_explicit_booked_credit_policy_without_activating(string rule, string expected)
    {
        var seed = await SeedAsync(); var id = await AppointmentAsync(seed, DateTime.UtcNow.AddMinutes(-1), noShowRule: rule);
        await using var db = fixture.Open(); var store = new PackageCreditStore(db);
        await store.ReserveAppointmentAsync(seed.EnrollmentId, id, "InitialConsultation", seed.PatientUserId);
        var appointment = await db.Appointments.SingleAsync(a => a.Id == id); appointment.Status = "No-show";
        await store.ApplyAppointmentStatusAsync(appointment, seed.ClinicianUserId, "Nutritionist");
        Assert.Equal(expected, (await db.PackageCreditUsages.SingleAsync(u => u.ResourceId == id && u.ResourceType == "Appointment")).Status);
        Assert.Null((await db.PackageEnrollments.SingleAsync(e => e.Id == seed.EnrollmentId)).ActivatedAtUtc);
    }

    [Fact]
    public async Task Provider_cancellation_restores_a_credit_without_refunding_the_whole_package()
    {
        var seed = await SeedAsync(); var id = await AppointmentAsync(seed, DateTime.UtcNow.AddDays(1));
        await using (var db = fixture.Open())
        {
            var store = new PackageCreditStore(db); await store.ReserveAppointmentAsync(seed.EnrollmentId, id, "InitialConsultation", seed.PatientUserId);
            var appointment = await db.Appointments.SingleAsync(a => a.Id == id); appointment.Status = "Cancelled";
            await store.ApplyAppointmentStatusAsync(appointment, seed.ClinicianUserId, "Nutritionist");
            var cash = NutriCare.Application.Services.CancellationRefundOutbox.Evaluate(appointment, "Nutritionist", DateTime.UtcNow);
            Assert.False(cash.Eligible); Assert.Equal("PackageCreditNonMonetaryAdjustment", cash.Reason);
        }
        var replacement = await AppointmentAsync(seed, DateTime.UtcNow.AddDays(2));
        await using var verify = fixture.Open(); await new PackageCreditStore(verify).ReserveAppointmentAsync(seed.EnrollmentId, replacement, "InitialConsultation", seed.PatientUserId);
        Assert.Equal(1, await verify.PackageCreditUsages.CountAsync(u => u.PackageEnrollmentId == seed.EnrollmentId && u.Status == "Released"));
        Assert.Equal(1, await verify.PackageCreditUsages.CountAsync(u => u.PackageEnrollmentId == seed.EnrollmentId && u.Status == "Reserved"));
        Assert.Empty(await verify.PaymentRefunds.Where(r => r.PaymentId == seed.PaymentId).ToArrayAsync());
    }

    [Fact]
    public async Task Ownership_service_exclusions_and_actor_spoofing_cannot_spend_or_release_credits()
    {
        var seed = await SeedAsync(); var excluded = await AppointmentAsync(seed, DateTime.UtcNow.AddDays(1), excluded: true);
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<ConflictException>(() => new PackageCreditStore(db).ReserveAppointmentAsync(seed.EnrollmentId, excluded, "InitialConsultation", seed.PatientUserId));
        var valid = await AppointmentAsync(seed, DateTime.UtcNow.AddDays(1));
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<ForbiddenAccessException>(() => new PackageCreditStore(db).ReserveAppointmentAsync(seed.EnrollmentId, valid, "InitialConsultation", seed.ClinicianUserId));
        await using (var db = fixture.Open())
        {
            var store = new PackageCreditStore(db); await store.ReserveAppointmentAsync(seed.EnrollmentId, valid, "InitialConsultation", seed.PatientUserId);
            var appointment = await db.Appointments.SingleAsync(a => a.Id == valid); appointment.Status = "Cancelled";
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => store.ApplyAppointmentStatusAsync(appointment, seed.PatientUserId, "Nutritionist"));
        }
        await using var verify = fixture.Open(); Assert.Equal("Reserved", (await verify.PackageCreditUsages.SingleAsync(u => u.PackageEnrollmentId == seed.EnrollmentId)).Status);
    }

    [Fact]
    public async Task Pending_package_refund_freezes_new_spending_and_retains_existing_plan_access()
    {
        var seed = await SeedAsync(); await ActivateAsync(seed); var version = await PlanAsync(seed, 1);
        await using (var db = fixture.Open()) await new PackageCreditStore(db).ConsumePlanVersionAsync(seed.EnrollmentId, version, false, seed.ClinicianUserId);
        await using (var db = fixture.Open()) { db.PaymentRefunds.Add(new PaymentRefund { PaymentId = seed.PaymentId, Amount = 1, Currency = "PKR", Status = "Pending", IdempotencyKey = "pending-package-refund" + seed.PaymentId }); await db.SaveChangesAsync(); }
        var follow = await AppointmentAsync(seed, DateTime.UtcNow.AddDays(1), follow: true);
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<ConflictException>(() => new PackageCreditStore(db).ReserveAppointmentAsync(seed.EnrollmentId, follow, "FollowUpConsultation", seed.PatientUserId));
        await using var check = fixture.Open();
        Assert.True(await check.DietPlanVersions.AnyAsync(v => v.Id == version && v.PublishedAt != null && v.DeletedAt == null));
        Assert.Equal(1, await check.PackageCreditUsages.CountAsync(u => u.PackageEnrollmentId == seed.EnrollmentId && u.ResourceType == "PlanVersion"));
    }

    [Fact]
    public async Task Published_plan_and_revision_quotas_are_distinct_and_idempotent()
    {
        var seed = await SeedAsync(); await ActivateAsync(seed); var first = await PlanAsync(seed, 1);
        await using (var db = fixture.Open())
        {
            var store = new PackageCreditStore(db); var usage = await store.ConsumePlanVersionAsync(seed.EnrollmentId, first, false, seed.ClinicianUserId);
            Assert.Equal(usage.Id, (await store.ConsumePlanVersionAsync(seed.EnrollmentId, first, false, seed.ClinicianUserId)).Id);
            await Assert.ThrowsAsync<ConflictException>(() => store.ConsumePlanVersionAsync(seed.EnrollmentId, first, true, seed.ClinicianUserId));
        }
        long planId; await using (var db = fixture.Open()) planId = (await db.DietPlanVersions.SingleAsync(v => v.Id == first)).DietPlanId;
        var second = await PlanAsync(seed, 2, planId); var third = await PlanAsync(seed, 3, planId); var otherPlan = await PlanAsync(seed, 1);
        await using (var db = fixture.Open()) await new PackageCreditStore(db).ConsumePlanVersionAsync(seed.EnrollmentId, second, true, seed.ClinicianUserId);
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<ConflictException>(() => new PackageCreditStore(db).ConsumePlanVersionAsync(seed.EnrollmentId, third, true, seed.ClinicianUserId));
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<ConflictException>(() => new PackageCreditStore(db).ConsumePlanVersionAsync(seed.EnrollmentId, otherPlan, false, seed.ClinicianUserId));
    }

    [Fact]
    public async Task Failed_credit_reservation_rolls_back_the_enclosing_booking_transaction()
    {
        var seed = await SeedAsync(); var used = await AppointmentAsync(seed, DateTime.UtcNow.AddDays(1));
        await using (var db = fixture.Open()) await new PackageCreditStore(db).ReserveAppointmentAsync(seed.EnrollmentId, used, "InitialConsultation", seed.PatientUserId);
        long failedId = 0;
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<ConflictException>(() => new AppointmentReservationLock(db).ExecuteAsync(seed.NutritionistId, async () =>
        {
            var appointment = NewAppointment(seed, DateTime.UtcNow.AddDays(2)); db.Appointments.Add(appointment); await db.SaveChangesAsync(); failedId = appointment.Id;
            await new PackageCreditStore(db).ReserveAppointmentAsync(seed.EnrollmentId, appointment.Id, "InitialConsultation", seed.PatientUserId); return true;
        }));
        await using var check = fixture.Open(); Assert.False(await check.Appointments.AnyAsync(a => a.Id == failedId));
        Assert.Equal(1, await check.PackageCreditUsages.CountAsync(u => u.PackageEnrollmentId == seed.EnrollmentId));
    }

    private async Task<Seed> SeedAsync()
    {
        await using var db = fixture.Open(); var token = Guid.NewGuid().ToString("N"); var now = DateTime.UtcNow;
        var patientUser = new User { UserName = token + "@test.invalid", Email = token + "@test.invalid", EmailConfirmed = true };
        var clinicianUser = new User { UserName = "clinician-" + token + "@test.invalid", Email = "clinician-" + token + "@test.invalid", EmailConfirmed = true };
        db.Users.AddRange(patientUser, clinicianUser); await db.SaveChangesAsync();
        var patient = new Patient { UserId = patientUser.Id }; var clinician = new Nutritionist { UserId = clinicianUser.Id, IsVerified = true, LicenseNumber = token };
        db.Patients.Add(patient); db.Nutritionists.Add(clinician); await db.SaveChangesAsync();
        var package = new ServicePackage { Name = "Isolated ledger package", NameUrdu = "آزمائشی پیکیج", Version = 1, DurationDays = 14, DurationWeeks = 2,
            InitialConsultationCount = 1, InitialConsultationMinutes = 45, FollowUpConsultationCount = 1, FollowUpConsultationMinutes = 20, DietPlanCount = 1, DietPlanRevisionCount = 1,
            ProgressTrackingFrequency = "Basic", CreditExpiryDays = 14, FirstConsultationWithinDays = 30, EligibleServiceCodes = ["GeneralNutrition", "WeightLoss"],
            ActivationRule = "FirstCompletedConsultation", NoShowCreditRule = "CancellationPolicy", RestoreCreditsOnProviderCancellation = true,
            NutritionistApprovedById = clinicianUser.Id, NutritionistApprovedAt = now.AddDays(-1), NutritionistApprovedVersion = 1,
            Price = 4000, Currency = "PKR", IsActive = true, IsPublished = true, ApprovedByUserId = clinicianUser.Id, ApprovedAt = now.AddDays(-1), EffectiveFrom = now.AddDays(-1) };
        var initial = new Service { Name = "Initial ledger service", NameUrdu = "ابتدائی مشاورت", NutritionistId = clinician.Id, PackageServiceCode = "GeneralNutrition", DurationMinutes = 45, Price = 2500, Currency = "PKR", Mode = "InPerson", IsActive = true, IsPublished = true };
        var follow = new Service { Name = "Follow ledger service", NameUrdu = "فالو اپ", NutritionistId = clinician.Id, PackageServiceCode = "FollowUp", DurationMinutes = 20, Price = 1500, Currency = "PKR", Mode = "InPerson", IsActive = true, IsPublished = true };
        var excluded = new Service { Name = "Excluded pregnancy", NameUrdu = "حمل", NutritionistId = clinician.Id, PackageServiceCode = "Pregnancy", DurationMinutes = 45, Price = 3000, Currency = "PKR", Mode = "InPerson", IsActive = true, IsPublished = true };
        var policy = new CancellationPolicy { Name = "Ledger cancellation", Version = 1, Status = "Published", IsPublished = true, ApprovedByUserId = clinicianUser.Id, ApprovedAt = now.AddDays(-1), EffectiveFrom = now.AddDays(-1) };
        db.ServicePackages.Add(package); db.Services.AddRange(initial, follow, excluded); db.CancellationPolicies.Add(policy); await db.SaveChangesAsync();
        var payment = new Payment { PatientId = patient.Id, NutritionistId = clinician.Id, Amount = package.Price, Currency = package.Currency, Status = "Completed", PaidAt = now.AddMinutes(-5),
            PaymentMethod = "Stripe", ProviderPaymentId = "pi_" + token, TransactionId = token, RelatedToType = "Package", RelatedToId = package.Id };
        db.Payments.Add(payment); await db.SaveChangesAsync();
        db.ProviderWebhookEvents.Add(new ProviderWebhookEvent { Provider = "Stripe", EventId = "evt_" + token, PaymentId = payment.Id, PayloadHash = new string('A', 64), ProcessedAt = now }); await db.SaveChangesAsync();
        var enrollment = await new PackageCreditStore(db).IssueFromVerifiedPaymentAsync(payment.Id, patientUser.Id);
        return new(enrollment.Id, package.Id, payment.Id, patient.Id, patientUser.Id, clinician.Id, clinicianUser.Id, initial.Id, follow.Id, excluded.Id, policy.Id, JsonSerializer.Serialize(policy));
    }

    private async Task<long> AppointmentAsync(Seed seed, DateTime start, bool follow = false, bool excluded = false, string noShowRule = "ReviewRequired")
    {
        await using var db = fixture.Open(); var appointment = NewAppointment(seed, start);
        if (follow) { appointment.ServiceId = seed.FollowServiceId; appointment.Duration = 20; }
        if (excluded) appointment.ServiceId = seed.ExcludedServiceId;
        var policy = JsonSerializer.Deserialize<CancellationPolicy>(seed.PolicyJson)!; policy.PackageNoShowCreditAction = noShowRule;
        appointment.CancellationPolicySnapshotJson = JsonSerializer.Serialize(policy);
        db.Appointments.Add(appointment); await db.SaveChangesAsync(); return appointment.Id;
    }
    private static Appointment NewAppointment(Seed seed, DateTime start) => new() { PatientId = seed.PatientId, NutritionistId = seed.NutritionistId, ServiceId = seed.InitialServiceId,
        AppointmentDateTime = start, Duration = 45, Status = "Pending", AppointmentType = "InPerson", PriceSnapshot = 2500, Currency = "PKR",
        CancellationPolicyId = seed.PolicyId, CancellationPolicyVersion = 1, CancellationPolicySnapshotJson = seed.PolicyJson };
    private async Task ActivateAsync(Seed seed)
    {
        var id = await AppointmentAsync(seed, DateTime.UtcNow.AddMinutes(-1)); await using var db = fixture.Open(); var store = new PackageCreditStore(db);
        await store.ReserveAppointmentAsync(seed.EnrollmentId, id, "InitialConsultation", seed.PatientUserId);
        var appointment = await db.Appointments.SingleAsync(a => a.Id == id); appointment.Status = "Completed";
        await store.ApplyAppointmentStatusAsync(appointment, seed.ClinicianUserId, "Nutritionist");
    }
    private async Task<long> PlanAsync(Seed seed, int number, long? planId = null)
    {
        await using var db = fixture.Open();
        if (!planId.HasValue)
        {
            var plan = new DietPlan { PatientId = seed.PatientId, NutritionistId = seed.NutritionistId, Title = "Ledger retained plan", StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(14), Status = "Published" };
            db.DietPlans.Add(plan); await db.SaveChangesAsync(); planId = plan.Id;
        }
        var version = new DietPlanVersion { DietPlanId = planId.Value, VersionNumber = number, PublishedAt = DateTime.UtcNow, CreatedByUserId = seed.ClinicianUserId, SnapshotJson = "{}" };
        db.DietPlanVersions.Add(version); await db.SaveChangesAsync(); return version.Id;
    }
    private sealed record Seed(long EnrollmentId, long PackageId, long PaymentId, long PatientId, long PatientUserId, long NutritionistId, long ClinicianUserId,
        long InitialServiceId, long FollowServiceId, long ExcludedServiceId, long PolicyId, string PolicyJson);
}

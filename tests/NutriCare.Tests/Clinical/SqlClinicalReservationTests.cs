namespace NutriCare.Tests.Clinical;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NutriCare.Application.Commands.Appointments;
using NutriCare.Application.Commands.DietPlans;
using NutriCare.Application.Commands.Progress;
using NutriCare.Application.DTOs;
using NutriCare.Application.Handlers.Progress;
using NutriCare.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NutriCare.Application.Handlers.Appointments;
using NutriCare.Application.Queries.Appointments;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Infrastructure.Notifications;
using NutriCare.Persistence.Configuration;
using NutriCare.Persistence.Contexts;
using NutriCare.Persistence.Repositories;
using NutriCare.Tests.Configuration;

/// <summary>Runs only against a fixture-generated SQL database; never a configured application database.</summary>
[Trait("Category", "SqlIntegration")]
public sealed partial class SqlClinicalReservationTests(IsolatedSqlDatabase fixture) : IClassFixture<IsolatedSqlDatabase>
{
    [Fact]
    public async Task Concurrent_same_slot_has_one_reservation_with_one_history_and_outbox_set()
    {
        var seed = await SeedAsync();
        var attempts = Enumerable.Range(0, 8).Select(async i =>
        {
            await using var db = fixture.Open();
            try { await Workflow(db, seed).BookAsync(Request(seed, "race-" + i), default); return true; }
            catch (ConflictException) { return false; }
        });
        var results = await Task.WhenAll(attempts);
        Assert.Single(results, success => success);
        await using var verification = fixture.Open();
        var reservation = await verification.Appointments.SingleAsync(a => a.NutritionistId == seed.NutritionistId);
        Assert.Equal(5000m, reservation.PriceSnapshot);
        Assert.Equal("PKR", reservation.Currency);
        Assert.NotEmpty(reservation.RowVersion);
        Assert.NotNull(reservation.CancellationPolicySnapshotJson);
        Assert.Equal(1, await verification.AppointmentHistories.CountAsync(h => h.AppointmentId == reservation.Id));
        Assert.Equal(4, await verification.Notifications.CountAsync(n => n.RelatedToType == "Appointment" && n.RelatedToId == reservation.Id));
    }

    [Fact]
    public async Task Retried_booking_returns_same_id_without_duplicate_notifications()
    {
        var seed = await SeedAsync(); long firstId;
        await using (var first = fixture.Open()) firstId = (await Workflow(first, seed).BookAsync(Request(seed, "same-request"), default)).Id;
        await using (var retry = fixture.Open()) Assert.Equal(firstId, (await Workflow(retry, seed).BookAsync(Request(seed, "same-request"), default)).Id);
        await using var verification = fixture.Open();
        Assert.Equal(1, await verification.Appointments.CountAsync(a => a.NutritionistId == seed.NutritionistId));
        Assert.Equal(4, await verification.Notifications.CountAsync(n => n.RelatedToType == "Appointment" && n.RelatedToId == firstId));
    }

    [Fact]
    public async Task Full_interval_and_fifteen_minute_buffer_are_checked_in_sql()
    {
        var seed = await SeedAsync();
        await using (var setup = fixture.Open())
        {
            setup.Appointments.Add(new Appointment { PatientId = seed.PatientId, NutritionistId = seed.NutritionistId,
                AppointmentDateTime = seed.Start.AddMinutes(-15), Duration = 30, Status = "Confirmed" });
            await setup.SaveChangesAsync();
        }
        await using (var blocked = fixture.Open())
            await Assert.ThrowsAsync<ConflictException>(() => Workflow(blocked, seed).BookAsync(Request(seed, "overlap"), default));
        await using var allowed = fixture.Open();
        var next = Request(seed, "adjacent"); next.AppointmentDateTime = seed.Start.AddMinutes(45);
        Assert.Equal(seed.Start.AddMinutes(45), (await Workflow(allowed, seed).BookAsync(next, default)).AppointmentDateTime);
    }

    [Fact]
    public async Task Calendar_closure_publication_and_booking_cannot_both_win()
    {
        var seed = await SeedAsync(); long closureId;
        await using (var setup = fixture.Open())
        {
            var closure = new AvailabilityException { NutritionistId = seed.NutritionistId, StartAtUtc = seed.Start,
                EndAtUtc = seed.Start.AddMinutes(30), IsAvailable = false, IsPublished = false, Reason = "Isolated test closure", EffectiveFrom = DateTime.UtcNow.AddDays(-1) };
            setup.AvailabilityExceptions.Add(closure); await setup.SaveChangesAsync(); closureId = closure.Id;
        }
        var booking = Task.Run(async () =>
        {
            await using var db = fixture.Open();
            try { await Workflow(db, seed).BookAsync(Request(seed, "calendar-race"), default); return true; }
            catch (ConflictException) { return false; }
        });
        var closing = Task.Run(async () =>
        {
            await using var db = fixture.Open();
            var closure = await db.AvailabilityExceptions.SingleAsync(e => e.Id == closureId);
            var store = new ConfigurationStore(db, new SqlActor(seed.ClinicianUserId, "Admin"), new AppointmentReservationLock(db));
            try
            {
                await store.PublishAsync("exceptions", closureId, JsonSerializer.SerializeToElement(new { approvalConfirmed = true, rowVersion = closure.RowVersion }), true);
                return true;
            }
            catch (ConflictException) { return false; }
        });
        var results = await Task.WhenAll(booking, closing);
        Assert.Equal(1, results.Count(success => success));
        await using var verification = fixture.Open();
        var booked = await verification.Appointments.AnyAsync(a => a.NutritionistId == seed.NutritionistId);
        var closed = (await verification.AvailabilityExceptions.SingleAsync(e => e.Id == closureId)).IsPublished;
        Assert.NotEqual(booked, closed);
    }

    [Fact]
    public async Task Existing_transaction_still_acquires_the_transaction_owned_application_lock()
    {
        var seed = await SeedAsync();
        await using var db = fixture.Open();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var mode = await new AppointmentReservationLock(db).ExecuteAsync(seed.NutritionistId, () =>
            db.Database.SqlQueryRaw<string>("SELECT APPLOCK_MODE('public', {0}, 'Transaction') AS [Value]", $"NutriCare:booking:{seed.NutritionistId}").SingleAsync());
        Assert.Equal("Exclusive", mode);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Booked_policy_snapshot_survives_later_configuration_changes()
    {
        var seed = await SeedAsync(); long appointmentId;
        await using (var first = fixture.Open()) appointmentId = (await Workflow(first, seed).BookAsync(Request(seed, "policy-snapshot"), default)).Id;
        await using (var edit = fixture.Open())
        {
            var appointment = await edit.Appointments.SingleAsync(a => a.Id == appointmentId);
            var policy = await edit.CancellationPolicies.SingleAsync(p => p.Id == appointment.CancellationPolicyId);
            policy.IsPublished = false; policy.Status = "Draft"; policy.FreeRescheduleHours = 10000;
            await edit.SaveChangesAsync();
        }
        await using var reschedule = fixture.Open();
        var result = await Workflow(reschedule, seed).RescheduleAsync(new RescheduleAppointmentCommand { AppointmentId = appointmentId,
            PatientId = seed.PatientId, RequestedByUserId = seed.PatientUserId, AppointmentDateTime = seed.Start.AddDays(7), TimeZoneId = "Asia/Karachi" }, default);
        Assert.Equal(1, result.RescheduleCount);
        Assert.Equal(seed.Start.AddDays(7), result.AppointmentDateTime);
        Assert.Equal(2, await reschedule.AppointmentHistories.CountAsync(h => h.AppointmentId == appointmentId));
    }

    [Fact]
    public async Task Sql_appointment_reads_deny_another_patient_and_revoked_clinician()
    {
        var seed = await SeedAsync(); long appointmentId;
        await using (var first = fixture.Open()) appointmentId = (await Workflow(first, seed).BookAsync(Request(seed, "ownership"), default)).Id;
        await using (var foreign = fixture.Open())
        {
            var unrelated = new User { FirstName = "Other", LastName = "Patient", Email = Guid.NewGuid() + "@example.invalid" };
            foreign.Users.Add(unrelated); await foreign.SaveChangesAsync();
            foreign.Patients.Add(new Patient { UserId = unrelated.Id }); await foreign.SaveChangesAsync();
            var access = Access(foreign, new SqlActor(unrelated.Id, "Patient"));
            var handler = new GetAppointmentByIdQueryHandler(new Repository<Appointment>(foreign), access);
            await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new GetAppointmentByIdQuery { AppointmentId = appointmentId }, default));
        }
        await using (var revoke = fixture.Open())
        {
            var assignment = await revoke.ClinicalAssignments.SingleAsync(a => a.PatientId == seed.PatientId && a.NutritionistId == seed.NutritionistId);
            assignment.EndedAt = DateTime.UtcNow.AddSeconds(-1); await revoke.SaveChangesAsync();
        }
        await using var clinician = fixture.Open();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Access(clinician, new SqlActor(seed.ClinicianUserId, "Nutritionist")).EnsureAppointmentAccessAsync(appointmentId));
    }

    [Fact]
    public async Task Urdu_acceptance_records_both_exact_texts_and_does_not_reuse_tampered_evidence()
    {
        var seed = await SeedAsync();
        await using var db = fixture.Open();
        db.ConsentRecords.Add(new ConsentRecord { UserId = seed.PatientUserId, Purpose = "HealthData", PolicyVersion = "sql-clinical-test",
            PresentedLanguage = "Urdu", ConsentText = "Incorrect old text", ConsentTextUrdu = "Incorrect old translation" });
        await db.SaveChangesAsync();
        var actor = new SqlActor(seed.PatientUserId, "Patient");
        var controller = new ClinicalWorkflowController(db, Access(db, actor), actor, null!, null!, new AppointmentReservationLock(db))
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var response = await controller.GrantConsent(new GrantClinicalConsentRequest { Purpose = "HealthData", PolicyVersion = "sql-clinical-test", Accepted = true, Language = "ur" }, default);
        var receipt = JsonSerializer.SerializeToElement(Assert.IsType<ApiResponse<object>>(Assert.IsType<OkObjectResult>(response).Value).Data);
        var record = await db.ConsentRecords.SingleAsync(c => c.Id == receipt.GetProperty("Id").GetInt64());
        var policy = await db.PolicyDocuments.SingleAsync(p => p.Purpose == "HealthData" && p.Version == "sql-clinical-test");
        Assert.Equal("Urdu", record.PresentedLanguage);
        Assert.Equal(policy.Content, record.ConsentText);
        Assert.Equal(policy.ContentUrdu, record.ConsentTextUrdu);
        Assert.Equal(policy.Title, record.PolicyTitle);
        await Assert.ThrowsAsync<ArgumentException>(() => controller.GrantConsent(new GrantClinicalConsentRequest { Accepted = true, Language = "de" }, default));
    }

    [Theory]
    [InlineData("Appointment")]
    [InlineData("DietPlan")]
    public async Task Health_data_withdrawal_revokes_legacy_relationships_without_assignment_history(string legacySource)
    {
        var seed = await SeedAsync(); long consentId;
        await using (var setup = fixture.Open())
        {
            consentId = await setup.ConsentRecords.Where(c => c.UserId == seed.PatientUserId).Select(c => c.Id).SingleAsync();
            if (legacySource == "Appointment")
                setup.Appointments.Add(new Appointment { PatientId = seed.PatientId, NutritionistId = seed.NutritionistId,
                    AppointmentDateTime = DateTime.UtcNow.AddDays(-2), Duration = 30, Status = "Completed" });
            else
                setup.DietPlans.Add(new DietPlan { PatientId = seed.PatientId, NutritionistId = seed.NutritionistId,
                    Title = "Legacy published plan", PublishedAt = DateTime.UtcNow.AddDays(-2), Status = "Published",
                    StartDate = ClinicalTime.LocalToday().AddDays(-2), EndDate = ClinicalTime.LocalToday().AddDays(5) });
            await setup.SaveChangesAsync();
            Assert.False(await setup.ClinicalAssignments.AnyAsync(a => a.PatientId == seed.PatientId));
            Assert.True(await Access(setup, new SqlActor(seed.ClinicianUserId, "Nutritionist")).CanAccessPatientAsync(seed.PatientId));
        }
        await using (var withdrawal = fixture.Open())
        {
            var actor = new SqlActor(seed.PatientUserId, "Patient");
            var controller = new ClinicalWorkflowController(withdrawal, Access(withdrawal, actor), actor, null!, null!, new AppointmentReservationLock(withdrawal));
            await controller.WithdrawConsent(consentId, default);
            await controller.WithdrawConsent(consentId, default);
        }
        await using var verification = fixture.Open();
        var marker = await verification.ClinicalAssignments.SingleAsync(a => a.PatientId == seed.PatientId && a.NutritionistId == seed.NutritionistId);
        var consent = await verification.ConsentRecords.SingleAsync(c => c.Id == consentId);
        Assert.Equal(consent.WithdrawnAt, marker.StartedAt);
        Assert.Equal(consent.WithdrawnAt, marker.EndedAt);
        Assert.False(await Access(verification, new SqlActor(seed.ClinicianUserId, "Nutritionist")).CanAccessPatientAsync(seed.PatientId));
    }

    [Fact]
    public async Task Withdrawal_and_intake_share_the_patient_lock_and_reject_collection_after_withdrawal()
    {
        var seed = await SeedAsync();
        await using var withdrawalDb = fixture.Open();
        await using var intakeDb = fixture.Open();
        var consentId = await withdrawalDb.ConsentRecords.Where(c => c.UserId == seed.PatientUserId).Select(c => c.Id).SingleAsync();
        var actor = new SqlActor(seed.PatientUserId, "Patient");
        var holdingLock = new HoldingPatientLock(withdrawalDb);
        var withdrawal = new ClinicalWorkflowController(withdrawalDb, Access(withdrawalDb, actor), actor, null!, null!, holdingLock);
        var intake = new ClinicalWorkflowController(intakeDb, Access(intakeDb, actor), actor, null!, null!, new AppointmentReservationLock(intakeDb));
        var withdrawing = withdrawal.WithdrawConsent(consentId, default);
        Task<IActionResult>? collecting = null;
        try
        {
            await holdingLock.Acquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(-seed.PatientId, holdingLock.ResourceId);
            collecting = intake.SubmitIntake(new ClinicalIntakeRequest { ConsentRecordId = consentId, Goals = "Must not persist after withdrawal" }, default);
            await Task.Delay(200);
            Assert.False(collecting.IsCompleted);
        }
        finally { holdingLock.Release.TrySetResult(); }
        await withdrawing;
        Assert.NotNull(collecting);
        await Assert.ThrowsAsync<ArgumentException>(async () => await collecting);
        await using var verification = fixture.Open();
        Assert.Equal(1, await verification.IntakeForms.CountAsync(i => i.PatientId == seed.PatientId));
        Assert.NotNull((await verification.ConsentRecords.SingleAsync(c => c.Id == consentId)).WithdrawnAt);
    }

    [Fact]
    public async Task Retrying_an_old_withdrawal_does_not_revoke_newly_consented_care()
    {
        var seed = await SeedAsync(); long oldConsentId;
        await using (var initial = fixture.Open())
        {
            await Workflow(initial, seed).BookAsync(Request(seed, "care-before-withdrawal"), default);
            oldConsentId = await initial.ConsentRecords.Where(c => c.UserId == seed.PatientUserId).Select(c => c.Id).SingleAsync();
        }
        await using (var withdrawal = fixture.Open())
        {
            var actor = new SqlActor(seed.PatientUserId, "Patient");
            var controller = new ClinicalWorkflowController(withdrawal, Access(withdrawal, actor), actor, null!, null!, new AppointmentReservationLock(withdrawal));
            await controller.WithdrawConsent(oldConsentId, default);
        }
        await using (var renewed = fixture.Open())
        {
            var oldConsent = await renewed.ConsentRecords.SingleAsync(c => c.Id == oldConsentId);
            var newConsent = new ConsentRecord { UserId = seed.PatientUserId, Purpose = oldConsent.Purpose, PolicyVersion = oldConsent.PolicyVersion,
                ConsentText = oldConsent.ConsentText, ConsentTextUrdu = oldConsent.ConsentTextUrdu, PolicyTitle = oldConsent.PolicyTitle,
                PresentedLanguage = oldConsent.PresentedLanguage };
            renewed.ConsentRecords.Add(newConsent); await renewed.SaveChangesAsync();
            var intake = new IntakeForm { PatientId = seed.PatientId, ConsentRecordId = newConsent.Id, Goals = "Freshly authorized care", Version = 2 };
            renewed.IntakeForms.Add(intake); await renewed.SaveChangesAsync();
            var request = Request(seed with { IntakeId = intake.Id }, "care-after-fresh-consent");
            request.AppointmentDateTime = seed.Start.AddMinutes(45);
            await Workflow(renewed, seed).BookAsync(request, default);
        }
        await using (var retry = fixture.Open())
        {
            var actor = new SqlActor(seed.PatientUserId, "Patient");
            var controller = new ClinicalWorkflowController(retry, Access(retry, actor), actor, null!, null!, new AppointmentReservationLock(retry));
            await controller.WithdrawConsent(oldConsentId, default);
        }
        await using var verification = fixture.Open();
        var assignments = await verification.ClinicalAssignments.Where(a => a.PatientId == seed.PatientId && a.NutritionistId == seed.NutritionistId).ToListAsync();
        Assert.Equal(2, assignments.Count);
        Assert.Single(assignments, a => a.EndedAt == null);
        Assert.True(await Access(verification, new SqlActor(seed.ClinicianUserId, "Nutritionist")).CanAccessPatientAsync(seed.PatientId));
        Assert.Equal(1, await verification.ConsentRecords.CountAsync(c => c.UserId == seed.PatientUserId && c.WithdrawnAt == null));
    }

    [Fact]
    public async Task Published_plan_snapshot_survives_a_draft_edit_with_prior_meals_retained()
    {
        var seed = await SeedAsync();
        await using (var booking = fixture.Open()) await Workflow(booking, seed).BookAsync(Request(seed, "plan-care-relationship"), default);
        var start = ClinicalTime.LocalToday(); long planId;
        await using (var author = fixture.Open())
        {
            var actor = new SqlActor(seed.ClinicianUserId, "Nutritionist"); var unit = new UnitOfWork(author);
            var plans = new ClinicalPlanWorkflow(new Repository<DietPlan>(author), new Repository<DietPlanItem>(author), new Repository<DietPlanVersion>(author),
                new Repository<Patient>(author), new Repository<User>(author), Access(author, actor), actor, new AppointmentReservationLock(author),
                new NotificationService(new Repository<Notification>(author), new Repository<User>(author), unit, new ConfigurationBuilder().Build()), unit);
            var original = await plans.CreateAsync(new CreateDietPlanCommand { NutritionistId = seed.NutritionistId, PatientId = seed.PatientId, Title = "Reviewed original", StartDate = start,
                EndDate = start.AddDays(7), ReviewDate = start.AddDays(3), DailyCalories = 1800,
                Meals = [new UpsertDietPlanItemRequest { DayNumber = 1, MealType = "Breakfast", ItemName = "Original oats", Quantity = 1, Unit = "cup" }] }, default);
            planId = original.Id;
            await plans.PublishAsync(planId, 1, default);
            await plans.UpdateAsync(new UpdateDietPlanCommand { DietPlanId = planId, NutritionistId = seed.NutritionistId, PatientId = seed.PatientId, ExpectedVersion = 1,
                Title = "Unpublished revision", StartDate = start, EndDate = start.AddDays(7), ReviewDate = start.AddDays(3), DailyCalories = 1900, ChangeReason = "Clinician adjustment",
                Meals = [new UpsertDietPlanItemRequest { DayNumber = 1, MealType = "Breakfast", ItemName = "Revised oats", Quantity = 2, Unit = "cup" }] }, default);
        }
        await using var patient = fixture.Open();
        var user = new SqlActor(seed.PatientUserId, "Patient"); var patientUnit = new UnitOfWork(patient);
        var reader = new ClinicalPlanWorkflow(new Repository<DietPlan>(patient), new Repository<DietPlanItem>(patient), new Repository<DietPlanVersion>(patient),
            new Repository<Patient>(patient), new Repository<User>(patient), Access(patient, user), user, new AppointmentReservationLock(patient),
            new NotificationService(new Repository<Notification>(patient), new Repository<User>(patient), patientUnit, new ConfigurationBuilder().Build()), patientUnit);
        var visible = await reader.VisibleAsync(planId, default);
        Assert.Equal("Reviewed original", visible.Title);
        Assert.Equal("Original oats", visible.Meals.Single().ItemName);
        Assert.Equal(2, await patient.DietPlanVersions.CountAsync(v => v.DietPlanId == planId));
        Assert.True(await patient.DietPlanItems.AnyAsync(i => i.DietPlanId == planId && i.ItemName == "Original oats" && i.DeletedAt != null));
    }

    [Fact]
    public async Task Sql_progress_backfill_and_correction_preserve_current_weight_and_revision()
    {
        var seed = await SeedAsync();
        await using var db = fixture.Open();
        var patient = await db.Patients.SingleAsync(p => p.Id == seed.PatientId);
        patient.CurrentWeight = 70;
        db.WeightTrackings.Add(new WeightTracking { PatientId = seed.PatientId, RecordDate = ClinicalTime.LocalToday(), Weight = 70 });
        await db.SaveChangesAsync();
        var actor = new SqlActor(seed.PatientUserId, "Patient"); var unit = new UnitOfWork(db);
        var handler = new TrackProgressCommandHandler(new Repository<Patient>(db), new Repository<WeightTracking>(db), unit, new Repository<ProgressRevision>(db),
            Access(db, actor), actor, new AppointmentReservationLock(db), new ClinicalConsentService(new Repository<ConsentRecord>(db), new Repository<PolicyDocument>(db)));
        var backfill = await handler.Handle(new TrackProgressCommand { PatientId = seed.PatientId, RecordDate = ClinicalTime.LocalToday().AddDays(-7), Weight = 75 }, default);
        await handler.Handle(new TrackProgressCommand { PatientId = seed.PatientId, RecordDate = backfill.RecordDate, Weight = 74,
            ExpectedVersion = backfill.RowVersion, ChangeReason = "Corrected scale transcription" }, default);
        Assert.Equal(70, patient.CurrentWeight);
        var revision = await db.ProgressRevisions.SingleAsync(r => r.WeightTrackingId == backfill.Id);
        Assert.Equal(75, JsonSerializer.Deserialize<ProgressRecordDto>(revision.SnapshotJson)!.Weight);
        Assert.Equal(2, await db.WeightTrackings.CountAsync(w => w.PatientId == seed.PatientId));
    }

    private async Task<BookingSeed> SeedAsync()
    {
        await using var db = fixture.Open();
        var patientUser = new User { FirstName = "SQL", LastName = "Patient", Email = Guid.NewGuid() + "@example.invalid", IsActive = true };
        var clinicianUser = new User { FirstName = "SQL", LastName = "Clinician", Email = Guid.NewGuid() + "@example.invalid", IsActive = true, EmailConfirmed = true };
        db.Users.AddRange(patientUser, clinicianUser); await db.SaveChangesAsync();
        var patient = new Patient { UserId = patientUser.Id, Height = 170 };
        var clinician = new Nutritionist { UserId = clinicianUser.Id, LicenseNumber = "TEST-" + Guid.NewGuid().ToString("N"), IsVerified = true, Timezone = "Asia/Karachi" };
        db.Patients.Add(patient); db.Nutritionists.Add(clinician); await db.SaveChangesAsync();
        var policy = await db.PolicyDocuments.SingleOrDefaultAsync(p => p.Purpose == "HealthData" && p.Version == "sql-clinical-test");
        if (policy == null)
        {
            policy = new PolicyDocument { Purpose = "HealthData", Version = "sql-clinical-test", Title = "Isolated SQL test policy", Content = "Test-only consent evidence",
                TitleUrdu = "آزمائشی پالیسی", ContentUrdu = "صرف آزمائش کے لیے", IsPublished = true, EffectiveFrom = DateTime.UtcNow.AddDays(-1) };
            db.PolicyDocuments.Add(policy);
        }
        if (!await db.CancellationPolicies.AnyAsync(p => p.IsPublished))
            db.CancellationPolicies.Add(new CancellationPolicy { Name = "Isolated SQL test cancellation", IsPublished = true, Status = "Published", EffectiveFrom = DateTime.UtcNow.AddDays(-1), FreeRescheduleHours = 6, MaxFreeReschedules = 1, FullRefundHours = 24 });
        var consent = new ConsentRecord { UserId = patientUser.Id, Purpose = "HealthData", PolicyVersion = policy.Version, ConsentText = policy.Content,
            ConsentTextUrdu = policy.ContentUrdu, PolicyTitle = policy.Title, PresentedLanguage = "English" };
        db.ConsentRecords.Add(consent); await db.SaveChangesAsync();
        var intake = new IntakeForm { PatientId = patient.Id, Goals = "Isolated clinical regression", ConsentRecordId = consent.Id };
        var service = new Service { NutritionistId = clinician.Id, Name = "SQL test consultation", NameUrdu = "آزمائشی مشاورت", Mode = "InPerson", DurationMinutes = 30,
            BookingBufferMinutes = 15, Price = 5000, Currency = "PKR", IsActive = true, IsPublished = true, EffectiveFrom = DateTime.UtcNow.AddDays(-1) };
        var start = DateTime.UtcNow.Date.AddDays(7).AddHours(5);
        var rule = new AvailabilityRule { NutritionistId = clinician.Id, DayOfWeek = start.DayOfWeek, StartTime = new(10, 0), EndTime = new(18, 0), BookingBufferMinutes = 15,
            TimeZoneId = "Asia/Karachi", IsPublished = true, EffectiveFrom = DateTime.UtcNow.AddDays(-1) };
        db.IntakeForms.Add(intake); db.Services.Add(service); db.AvailabilityRules.Add(rule); await db.SaveChangesAsync();
        return new(patientUser.Id, clinicianUser.Id, patient.Id, clinician.Id, service.Id, intake.Id, start);
    }

    private static CreateAppointmentCommand Request(BookingSeed seed, string key) => new() { PatientId = seed.PatientId, NutritionistId = seed.NutritionistId,
        ServiceId = seed.ServiceId, IntakeFormId = seed.IntakeId, AppointmentDateTime = seed.Start, AppointmentType = "InPerson", TimeZoneId = "Asia/Karachi", IdempotencyKey = key };

    private static ClinicalAccessService Access(NutriCareDbContext db, ICurrentUserService actor) => new(actor, new Repository<Patient>(db), new Repository<Nutritionist>(db),
        new Repository<ClinicalAssignment>(db), new Repository<Appointment>(db), new Repository<DietPlan>(db));

    private static ClinicalAppointmentWorkflow Workflow(NutriCareDbContext db, BookingSeed seed)
    {
        var actor = new SqlActor(seed.PatientUserId, "Patient");
        var unit = new UnitOfWork(db);
        var scheduling = new ClinicalSchedulingService(new Repository<Service>(db), new Repository<AvailabilityRule>(db), new Repository<AvailabilityException>(db), new Repository<Appointment>(db));
        var outbox = new NotificationService(new Repository<Notification>(db), new Repository<User>(db), unit, new ConfigurationBuilder().Build());
        return new(new Repository<Appointment>(db), new Repository<Patient>(db), new Repository<Nutritionist>(db), new Repository<IntakeForm>(db), new Repository<ConsentRecord>(db),
            new Repository<PolicyDocument>(db), new Repository<ClinicalAssignment>(db), new Repository<AppointmentHistory>(db), new Repository<ConsultationNote>(db), new Repository<CancellationPolicy>(db),
            new Repository<VideoConsultation>(db), new Repository<User>(db), Access(db, actor), scheduling, new AppointmentReservationLock(db), outbox, actor, unit);
    }

    private sealed record BookingSeed(long PatientUserId, long ClinicianUserId, long PatientId, long NutritionistId, long ServiceId, long IntakeId, DateTime Start);
    private sealed class HoldingPatientLock(NutriCareDbContext db) : IAppointmentReservationLock
    {
        public TaskCompletionSource Acquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public long ResourceId { get; private set; }
        public Task<T> ExecuteAsync<T>(long nutritionistId, Func<Task<T>> action, CancellationToken cancellationToken = default)
            => new AppointmentReservationLock(db).ExecuteAsync(nutritionistId, async () =>
            {
                ResourceId = nutritionistId;
                Acquired.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                return await action();
            }, cancellationToken);
    }
    private sealed class SqlActor(long id, string role) : ICurrentUserService
    { public long UserId => id; public string Role => role; public string Email => "test@example.invalid"; public bool IsAuthenticated => true; public bool CanAccessClinicalData => false; }
}

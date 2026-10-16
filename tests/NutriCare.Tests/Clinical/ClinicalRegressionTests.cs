namespace NutriCare.Tests.Clinical;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NutriCare.Application.Commands.DietPlans;
using NutriCare.Application.Commands.Progress;
using NutriCare.Application.DTOs;
using NutriCare.Application.Handlers.Appointments;
using NutriCare.Application.Handlers.Progress;
using NutriCare.Application.Queries.Appointments;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Infrastructure;
using NutriCare.Tests.Integrations;

public sealed class ClinicalRegressionTests
{
    [Fact]
    public async Task DraftCalendarNeverAdvertisesSlots()
    {
        var fixture = Scheduling();
        fixture.Rules.Items[0].IsPublished = false;
        Assert.Empty(await fixture.Service.GetAvailableSlotsAsync(20, 1, fixture.Day.AddHours(5), fixture.Day.AddHours(8)));
    }

    [Fact]
    public async Task IntervalStartingBeforeQueryBlocksOverlappingSlotAndOneBuffer()
    {
        var f = Scheduling();
        f.Appointments.Items.Add(new Appointment { Id = 1, NutritionistId = 20, AppointmentDateTime = f.Day.AddHours(4).AddMinutes(45), Duration = 30, Status = "Confirmed" });
        var slots = await f.Service.GetAvailableSlotsAsync(20, 1, f.Day.AddHours(5), f.Day.AddHours(8));
        Assert.DoesNotContain(f.Day.AddHours(5), slots);
        Assert.Contains(f.Day.AddHours(5).AddMinutes(45), slots);
        Assert.Equal(DateTimeKind.Utc, slots.First().Kind);
    }

    [Fact]
    public async Task ClosureWinsOverPublishedHoursAndExceptionalOpening()
    {
        var f = Scheduling();
        f.Exceptions.Items.AddRange([
            new AvailabilityException { NutritionistId = 20, IsPublished = true, IsAvailable = true, StartAtUtc = f.Day.AddHours(5), EndAtUtc = f.Day.AddHours(8) },
            new AvailabilityException { NutritionistId = 20, IsPublished = true, IsAvailable = false, StartAtUtc = f.Day.AddHours(5), EndAtUtc = f.Day.AddHours(8) }
        ]);
        Assert.Empty(await f.Service.GetAvailableSlotsAsync(20, 1, f.Day.AddHours(5), f.Day.AddHours(7)));
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ValidateSlotAsync(20, 1, f.Day.AddHours(5)));
    }

    [Fact]
    public async Task FullBufferIsEnforcedAndCancelledReservationsReleaseIt()
    {
        var f = Scheduling();
        var visit = new Appointment { Id = 1, NutritionistId = 20, AppointmentDateTime = f.Day.AddHours(5).AddMinutes(14), Duration = 30, Status = "Confirmed" };
        f.Appointments.Items.Add(visit);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ValidateSlotAsync(20, 1, f.Day.AddHours(5).AddMinutes(45)));
        visit.Status = "Cancelled";
        Assert.Equal(1, (await f.Service.ValidateSlotAsync(20, 1, f.Day.AddHours(5).AddMinutes(45))).Id);
    }

    [Fact]
    public async Task ServiceExpiryAndQueryBoundsAreEnforced()
    {
        var f = Scheduling();
        f.Services.Items[0].EffectiveTo = f.Day.AddHours(5).AddMinutes(29);
        Assert.Empty(await f.Service.GetAvailableSlotsAsync(20, 1, f.Day.AddHours(5), f.Day.AddHours(8)));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GetAvailableSlotsAsync(20, 1, f.Day, f.Day.AddDays(32)));
    }

    [Fact]
    public async Task PakistanMidnightRuleUsesUtcEffectiveInstantsAcrossDateBoundary()
    {
        var f = Scheduling();
        var start = f.Day.AddDays(-1).AddHours(19); // 00:00 on the Pakistan calendar date.
        var rule = f.Rules.Items[0];
        rule.StartTime = new(0, 0); rule.EndTime = new(2, 0);
        rule.EffectiveFrom = start; rule.EffectiveTo = start.AddHours(2);
        var slots = await f.Service.GetAvailableSlotsAsync(20, 1, start, start.AddHours(2));
        Assert.Equal(new[] { start, start.AddMinutes(45), start.AddMinutes(90) }, slots);
        Assert.Equal(1, (await f.Service.ValidateSlotAsync(20, 1, start.AddMinutes(90))).Id);
    }

    [Fact]
    public async Task MiddayRuleActivationAndExpiryContainTheWholeAppointment()
    {
        var f = Scheduling();
        var rule = f.Rules.Items[0];
        rule.EffectiveFrom = f.Day.AddHours(5).AddMinutes(15);
        rule.EffectiveTo = f.Day.AddHours(6).AddMinutes(15);
        var slots = await f.Service.GetAvailableSlotsAsync(20, 1, f.Day.AddHours(5), f.Day.AddHours(8));
        Assert.Equal(new[] { f.Day.AddHours(5).AddMinutes(45) }, slots);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ValidateSlotAsync(20, 1, f.Day.AddHours(5)));
        Assert.Equal(1, (await f.Service.ValidateSlotAsync(20, 1, f.Day.AddHours(5).AddMinutes(45))).Id);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ValidateSlotAsync(20, 1, f.Day.AddHours(6).AddMinutes(30)));
    }

    [Fact]
    public async Task ExceptionalOpeningRequiresTheWholeAppointmentInsideItsEffectiveWindow()
    {
        var f = Scheduling();
        f.Rules.Items[0].DayOfWeek = f.Day.AddDays(1).DayOfWeek;
        f.Exceptions.Items.Add(new AvailabilityException { NutritionistId = 20, IsPublished = true, IsAvailable = true,
            StartAtUtc = f.Day.AddHours(5), EndAtUtc = f.Day.AddHours(8),
            EffectiveFrom = f.Day.AddHours(5).AddMinutes(15), EffectiveTo = f.Day.AddHours(6).AddMinutes(15) });
        Assert.Equal(new[] { f.Day.AddHours(5).AddMinutes(45) }, await f.Service.GetAvailableSlotsAsync(20, 1, f.Day.AddHours(5), f.Day.AddHours(8)));
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ValidateSlotAsync(20, 1, f.Day.AddHours(5)));
        Assert.Equal(1, (await f.Service.ValidateSlotAsync(20, 1, f.Day.AddHours(5).AddMinutes(45))).Id);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ValidateSlotAsync(20, 1, f.Day.AddHours(6).AddMinutes(30)));
    }

    [Fact]
    public async Task ClosureBecomingEffectiveDuringAppointmentBlocksTheInterval()
    {
        var f = Scheduling();
        f.Exceptions.Items.Add(new AvailabilityException { NutritionistId = 20, IsPublished = true, IsAvailable = false,
            StartAtUtc = f.Day.AddHours(5), EndAtUtc = f.Day.AddHours(8),
            EffectiveFrom = f.Day.AddHours(5).AddMinutes(15), EffectiveTo = f.Day.AddHours(5).AddMinutes(30) });
        var slots = await f.Service.GetAvailableSlotsAsync(20, 1, f.Day.AddHours(5), f.Day.AddHours(8));
        Assert.DoesNotContain(f.Day.AddHours(5), slots);
        Assert.Contains(f.Day.AddHours(5).AddMinutes(45), slots);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ValidateSlotAsync(20, 1, f.Day.AddHours(5)));
        Assert.Equal(1, (await f.Service.ValidateSlotAsync(20, 1, f.Day.AddHours(5).AddMinutes(45))).Id);
    }

    [Fact]
    public void PakistanInputResolvesToSameUtcInstant()
    {
        var local = new DateTime(2030, 1, 2, 10, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(new DateTime(2030, 1, 2, 5, 0, 0, DateTimeKind.Utc), ClinicalTime.ToUtc(local, "Asia/Karachi"));
        Assert.False(ClinicalTime.Overlaps(local, local.AddMinutes(30), local.AddMinutes(45), local.AddMinutes(75), 15));
    }

    [Fact]
    public async Task AppointmentByIdCannotCrossPatientOwnership()
    {
        var f = Access();
        f.User.Role = "Patient"; f.User.UserId = 1;
        f.Appointments.Items.Add(new Appointment { Id = 5, PatientId = 11, NutritionistId = 20, ConsultationNotes = "Private" });
        var handler = new GetAppointmentByIdQueryHandler(f.Appointments, f.Service);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new GetAppointmentByIdQuery { AppointmentId = 5 }, default));
    }

    [Fact]
    public async Task ExplicitRevocationOverridesHistoricalAppointment()
    {
        var f = Access();
        f.User.Role = "Nutritionist"; f.User.UserId = 2;
        f.Appointments.Items.Add(new Appointment { Id = 5, PatientId = 10, NutritionistId = 20, Status = "Completed" });
        f.Assignments.Items.Add(new ClinicalAssignment { PatientId = 10, NutritionistId = 20, StartedAt = DateTime.UtcNow.AddDays(-2), EndedAt = DateTime.UtcNow.AddDays(-1) });
        Assert.False(await f.Service.CanAccessPatientAsync(10));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => f.Service.EnsureAppointmentAccessAsync(5));
    }

    [Fact]
    public async Task AdminNeedsExplicitClinicalPermission()
    {
        var f = Access(); f.User.Role = "Admin";
        Assert.False(await f.Service.CanAccessPatientAsync(10));
        f.User.CanAccessClinicalData = true;
        Assert.True(await f.Service.CanAccessPatientAsync(10));
    }

    [Fact]
    public async Task ConsentRequiresApprovedCurrentTextAndActiveGrant()
    {
        var consents = new MemoryRepository<ConsentRecord>();
        var policies = new MemoryRepository<PolicyDocument>();
        var service = new ClinicalConsentService(consents, policies);
        await Assert.ThrowsAsync<ConflictException>(() => service.RequireAsync(1, "HealthData"));
        policies.Items.Add(new PolicyDocument { Purpose = "HealthData", Version = "1", Content = "Approved policy", IsPublished = true });
        consents.Items.Add(new ConsentRecord { Id = 1, UserId = 1, Purpose = "HealthData", PolicyVersion = "1", ConsentText = "Tampered text" });
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.RequireAsync(1, "HealthData"));
        consents.Items[0].ConsentText = "Approved policy";
        Assert.Equal(1, (await service.RequireAsync(1, "HealthData")).Id);
        consents.Items[0].WithdrawnAt = DateTime.UtcNow;
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.RequireAsync(1, "HealthData"));
    }

    [Fact]
    public async Task UrduConsentRequiresTheExactCurrentlyApprovedTranslation()
    {
        var consents = new MemoryRepository<ConsentRecord>();
        var policies = new MemoryRepository<PolicyDocument>();
        var policy = new PolicyDocument { Purpose = "HealthData", Version = "1", Content = "Approved English text",
            ContentUrdu = "Approved Urdu text", IsPublished = true };
        var consent = new ConsentRecord { Id = 1, UserId = 1, Purpose = policy.Purpose, PolicyVersion = policy.Version,
            ConsentText = policy.Content, ConsentTextUrdu = "An older Urdu translation", PresentedLanguage = "Urdu" };
        policies.Items.Add(policy); consents.Items.Add(consent);
        var service = new ClinicalConsentService(consents, policies);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.RequireAsync(1, "HealthData"));
        consent.ConsentTextUrdu = policy.ContentUrdu;
        Assert.Equal(1, (await service.RequireAsync(1, "HealthData")).Id);
        policy.ContentUrdu = null; consent.ConsentTextUrdu = null;
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.RequireAsync(1, "HealthData"));
    }

    [Fact]
    public async Task BackdatedMeasurementDoesNotReplaceCurrentWeightAndLabelsDerivedBmi()
    {
        var patients = new MemoryRepository<Patient>(); patients.Items.Add(new Patient { Id = 10, UserId = 1, Height = 170, CurrentWeight = 70 });
        var records = new MemoryRepository<WeightTracking>(); records.Items.Add(new WeightTracking { Id = 1, PatientId = 10, RecordDate = DateTime.UtcNow.Date, Weight = 70 });
        var handler = new TrackProgressCommandHandler(patients, records, new MemoryUnitOfWork(), new MemoryRepository<ProgressRevision>(), new TestAccess(), new TestUser(), new SerialClinicalLock(), new TestConsent());
        var result = await handler.Handle(new TrackProgressCommand { PatientId = 10, RecordDate = DateTime.UtcNow.Date.AddDays(-7), Weight = 75, WaistCircumference = 80 }, default);
        Assert.Equal(70, patients.Items[0].CurrentWeight);
        Assert.True(result.BMIIsCalculated);
        Assert.Equal(80, result.WaistCircumference);
    }

    [Fact]
    public async Task MeasurementCorrectionsRequireVersionAndPreservePreviousValue()
    {
        var patients = new MemoryRepository<Patient>(); patients.Items.Add(new Patient { Id = 10, UserId = 1, Height = 170 });
        var records = new MemoryRepository<WeightTracking>(); records.Items.Add(new WeightTracking { Id = 1, PatientId = 10, RecordDate = DateTime.UtcNow.Date, Weight = 70, RowVersion = [1] });
        var revisions = new MemoryRepository<ProgressRevision>();
        var handler = new TrackProgressCommandHandler(patients, records, new MemoryUnitOfWork(), revisions, new TestAccess(), new TestUser(), new SerialClinicalLock(), new TestConsent());
        var command = new TrackProgressCommand { PatientId = 10, RecordDate = DateTime.UtcNow.Date, Weight = 71, ChangeReason = "Corrected scale entry" };
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(command, default));
        command.ExpectedVersion = [1];
        await handler.Handle(command, default);
        Assert.Single(revisions.Items);
        Assert.Equal(70, JsonSerializer.Deserialize<ProgressRecordDto>(revisions.Items[0].SnapshotJson)!.Weight);
    }

    [Fact]
    public async Task DraftEditsCannotReplaceThePatientsPublishedRevision()
    {
        var user = new TestUser { Role = "Nutritionist", UserId = 2 };
        var plans = new MemoryRepository<DietPlan>(); var versions = new MemoryRepository<DietPlanVersion>(); var items = new MemoryRepository<DietPlanItem>();
        var patients = new MemoryRepository<Patient>(); patients.Items.Add(new Patient { Id = 10, UserId = 1 });
        var users = new MemoryRepository<User>(); users.Items.Add(new User { Id = 1, Email = "patient@example.test" });
        var events = new RecordingOutbox();
        var workflow = new ClinicalPlanWorkflow(plans, items, versions, patients, users, new TestAccess(), user, new SerialClinicalLock(), events, new MemoryUnitOfWork());
        var start = DateTime.UtcNow.Date;
        var original = await workflow.CreateAsync(new CreateDietPlanCommand { NutritionistId = 20, PatientId = 10, Title = "Original", StartDate = start, EndDate = start.AddDays(7), ReviewDate = start.AddDays(3), DailyCalories = 1800,
            Meals = [new UpsertDietPlanItemRequest { DayNumber = 1, MealType = "Breakfast", ItemName = "Original meal", Quantity = 1, Unit = "cup" }] }, default);
        await workflow.PublishAsync(original.Id, 1, default);
        await workflow.PublishAsync(original.Id, 1, default);
        Assert.Single(events.Events);
        await workflow.UpdateAsync(new UpdateDietPlanCommand { DietPlanId = original.Id, NutritionistId = 20, PatientId = 10, ExpectedVersion = 1, Title = "Unpublished edit", StartDate = start, EndDate = start.AddDays(7), ReviewDate = start.AddDays(3), DailyCalories = 1900,
            Meals = [new UpsertDietPlanItemRequest { DayNumber = 1, MealType = "Breakfast", ItemName = "Draft meal", Quantity = 1, Unit = "cup" }] }, default);
        user.Role = "Patient"; user.UserId = 1;
        var visible = await workflow.VisibleAsync(original.Id, default);
        Assert.Equal("Original", visible.Title);
        Assert.Equal("Original meal", visible.Meals.Single().ItemName);
        Assert.Equal(1, visible.Version);
        Assert.Equal(2, versions.Items.Count);
        Assert.Single(await workflow.VersionsAsync(original.Id, default));
    }

    [Fact]
    public void PrintablePlanPreservesAllMealsAndEscapesClinicalText()
    {
        var plan = LongPlan(); plan.Instructions = "<script>fetch('https://example.test')</script>";
        var html = ClinicalPlanHtml.Render(plan);
        Assert.Contains("meal-119", html);
        Assert.Contains("غذائی منصوبہ", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("scope='col'", html);
    }

    [PdfBrowserFact]
    public async Task LongUrduPlanProducesPagedTaggedPdfWithUnicodeMappings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ClinicalPdf:ExecutablePath"] = Environment.GetEnvironmentVariable("NUTRICARE_PDF_BROWSER") }).Build();
        await using var renderer = new ClinicalPdfRenderer(configuration);
        var bytes = await renderer.RenderAsync(ClinicalPlanHtml.Render(LongPlan()));
        var source = Encoding.Latin1.GetString(bytes);
        Assert.StartsWith("%PDF-", source);
        Assert.Contains("/StructTreeRoot", source);
        Assert.Contains("/ToUnicode", source);
        Assert.True(System.Text.RegularExpressions.Regex.Matches(source, @"/Type\s*/Page(?:\s|/)").Count > 1);
        var artifact = Environment.GetEnvironmentVariable("NUTRICARE_PDF_ARTIFACT");
        if (!string.IsNullOrWhiteSpace(artifact)) await File.WriteAllBytesAsync(artifact, bytes);
    }

    private static DietPlanDto LongPlan() => new() { Id = 1, Version = 2, Title = "غذائی منصوبہ — Clinical test", StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(30), ReviewDate = DateTime.UtcNow.Date.AddDays(14),
        Instructions = "مناسب مقدار میں پانی پیئیں۔", Meals = Enumerable.Range(0, 120).Select(i => new DietPlanItemDto { DayNumber = i / 4 + 1, MealType = "Breakfast", MealTime = new TimeOnly(8, 30), ItemName = $"meal-{i} دلیہ", Quantity = 1, Unit = "cup", Calories = 300 }).ToArray() };

    private static (ClinicalSchedulingService Service, MemoryRepository<Service> Services, MemoryRepository<AvailabilityRule> Rules, MemoryRepository<AvailabilityException> Exceptions, MemoryRepository<Appointment> Appointments, DateTime Day) Scheduling()
    {
        var day = DateTime.UtcNow.Date.AddDays(14);
        var services = new MemoryRepository<Service>(); services.Items.Add(new Service { Id = 1, NutritionistId = 20, IsActive = true, IsPublished = true, DurationMinutes = 30, BookingBufferMinutes = 15 });
        var rules = new MemoryRepository<AvailabilityRule>(); rules.Items.Add(new AvailabilityRule { NutritionistId = 20, DayOfWeek = day.DayOfWeek, StartTime = new(10, 0), EndTime = new(13, 0), IsPublished = true, TimeZoneId = "Asia/Karachi", BookingBufferMinutes = 15 });
        var exceptions = new MemoryRepository<AvailabilityException>(); var visits = new MemoryRepository<Appointment>();
        return (new(services, rules, exceptions, visits), services, rules, exceptions, visits, day);
    }

    private static (ClinicalAccessService Service, TestUser User, MemoryRepository<Appointment> Appointments, MemoryRepository<ClinicalAssignment> Assignments) Access()
    {
        var user = new TestUser(); var patients = new MemoryRepository<Patient>(); patients.Items.AddRange([new Patient { Id = 10, UserId = 1 }, new Patient { Id = 11, UserId = 3 }]);
        var clinicians = new MemoryRepository<Nutritionist>(); clinicians.Items.Add(new Nutritionist { Id = 20, UserId = 2 });
        var appointments = new MemoryRepository<Appointment>(); var assignments = new MemoryRepository<ClinicalAssignment>();
        return (new(user, patients, clinicians, assignments, appointments, new MemoryRepository<DietPlan>()), user, appointments, assignments);
    }
}

internal sealed class TestUser : ICurrentUserService
{
    public long UserId { get; set; } = 1;
    public string Email => "patient@example.test";
    public string Role { get; set; } = "Patient";
    public bool IsAuthenticated => true;
    public bool CanAccessClinicalData { get; set; }
}
internal sealed class TestAccess : IClinicalAccessService
{
    public Task<bool> CanAccessPatientAsync(long patientId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task EnsurePatientAccessAsync(long patientId, bool allowPatient = true, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task EnsureAssignedClinicianAsync(long patientId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task EnsureAppointmentAccessAsync(long appointmentId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<long> GetNutritionistIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(20L);
    public Task<long> GetPatientIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(10L);
}
internal sealed class SerialClinicalLock : IAppointmentReservationLock
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<T> ExecuteAsync<T>(long nutritionistId, Func<Task<T>> action, CancellationToken cancellationToken = default)
    { await _gate.WaitAsync(cancellationToken); try { return await action(); } finally { _gate.Release(); } }
}
internal sealed class TestConsent : IClinicalConsentService
{
    public Task<ConsentRecord> RequireAsync(long userId, string purpose, long? consentRecordId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new ConsentRecord { Id = 1, UserId = userId, Purpose = purpose });
}
internal sealed class PdfBrowserFactAttribute : FactAttribute
{
    public PdfBrowserFactAttribute()
    { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NUTRICARE_PDF_BROWSER"))) Skip = "Set NUTRICARE_PDF_BROWSER to the installed local headless Chromium executable"; }
}

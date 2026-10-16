namespace NutriCare.Tests.Configuration;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Configuration;
using NutriCare.Persistence.Contexts;
using NutriCare.Persistence.Repositories;

[Trait("Category", "SqlIntegration")]
public class SqlCalendarConfigurationTests(IsolatedSqlDatabase fixture) : IClassFixture<IsolatedSqlDatabase>
{
    [Theory]
    [InlineData(60)] // Before the exception becomes effective.
    [InlineData(420)] // At the exclusive effective end.
    public async Task Closure_publication_ignores_bookings_outside_its_effective_interval(int appointmentOffset)
    {
        await using var db = fixture.Open();
        var exception = await SeedAsync(db, false, false, appointmentOffset);
        await Store(db, exception).PublishAsync("exceptions", exception.Id, Input(exception), true);
        Assert.True(exception.IsPublished);
        Assert.True(await db.ConfigurationRevisions.AnyAsync(h => h.EntityType == "exceptions" && h.EntityId == exception.Id && h.Action == "Approved and published"));
    }

    [Theory]
    [InlineData(105)] // Appointment crosses the effective start.
    [InlineData(390)] // Appointment crosses the effective end.
    public async Task Closure_publication_blocks_any_overlap_with_the_effective_interval(int appointmentOffset)
    {
        await using var db = fixture.Open();
        var exception = await SeedAsync(db, false, false, appointmentOffset);
        await Assert.ThrowsAsync<ConflictException>(() => Store(db, exception).PublishAsync("exceptions", exception.Id, Input(exception), true));
        Assert.False((await db.AvailabilityExceptions.AsNoTracking().SingleAsync(e => e.Id == exception.Id)).IsPublished);
        Assert.False(await db.ConfigurationRevisions.AnyAsync(h => h.EntityType == "exceptions" && h.EntityId == exception.Id));
    }

    [Fact]
    public async Task Closure_with_an_empty_effective_intersection_does_not_block_an_unrelated_booking()
    {
        await using var db = fixture.Open();
        var exception = await SeedAsync(db, false, false, 180);
        exception.EffectiveFrom = exception.EndAtUtc.AddHours(1);
        exception.EffectiveTo = exception.EndAtUtc.AddHours(2);
        await db.SaveChangesAsync();
        await Store(db, exception).PublishAsync("exceptions", exception.Id, Input(exception), true);
        Assert.True(exception.IsPublished);
    }

    [Fact]
    public async Task Exceptional_opening_cannot_be_unpublished_while_a_future_booking_depends_on_it()
    {
        await using var db = fixture.Open();
        var exception = await SeedAsync(db, true, true, 180);
        await Assert.ThrowsAsync<ConflictException>(() => Store(db, exception).PublishAsync("exceptions", exception.Id, Input(exception), false));
        Assert.True((await db.AvailabilityExceptions.AsNoTracking().SingleAsync(e => e.Id == exception.Id)).IsPublished);
        Assert.False(await db.ConfigurationRevisions.AnyAsync(h => h.EntityType == "exceptions" && h.EntityId == exception.Id));
    }

    [Theory]
    [InlineData(60)]
    [InlineData(420)]
    public async Task Exceptional_opening_can_be_unpublished_when_bookings_are_outside_its_effective_interval(int appointmentOffset)
    {
        await using var db = fixture.Open();
        var exception = await SeedAsync(db, true, true, appointmentOffset);
        await Store(db, exception).PublishAsync("exceptions", exception.Id, Input(exception), false);
        Assert.False(exception.IsPublished);
        Assert.True(await db.ConfigurationRevisions.AnyAsync(h => h.EntityType == "exceptions" && h.EntityId == exception.Id && h.Action.StartsWith("Unpublished")));
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Rejected")]
    [InlineData("Completed")]
    [InlineData("No-show")]
    [InlineData("NoShow")]
    public async Task Terminal_appointments_do_not_block_closure_publication_or_opening_unpublication(string status)
    {
        await using var db = fixture.Open();
        var closure = await SeedAsync(db, false, false, 180, status);
        await Store(db, closure).PublishAsync("exceptions", closure.Id, Input(closure), true);
        var opening = await SeedAsync(db, true, true, 180, status);
        await Store(db, opening).PublishAsync("exceptions", opening.Id, Input(opening), false);
        Assert.True(closure.IsPublished);
        Assert.False(opening.IsPublished);
    }

    private static async Task<AvailabilityException> SeedAsync(NutriCareDbContext db, bool isAvailable, bool isPublished, int appointmentOffset, string status = "Confirmed")
    {
        var suffix = Guid.NewGuid().ToString("N");
        var clinician = new Nutritionist
        {
            LicenseNumber = suffix, IsVerified = true,
            User = new User { UserName = suffix + "@example.test", Email = suffix + "@example.test", FirstName = "Test", LastName = "Clinician", EmailConfirmed = true, IsActive = true }
        };
        var patient = new Patient
        {
            User = new User { UserName = "patient-" + suffix + "@example.test", Email = "patient-" + suffix + "@example.test", FirstName = "Test", LastName = "Patient", EmailConfirmed = true, IsActive = true }
        };
        db.AddRange(clinician, patient);
        await db.SaveChangesAsync();
        var start = DateTime.UtcNow.Date.AddDays(14).AddHours(5);
        var exception = new AvailabilityException
        {
            NutritionistId = clinician.Id, StartAtUtc = start, EndAtUtc = start.AddHours(8),
            EffectiveFrom = start.AddHours(2), EffectiveTo = start.AddHours(7), IsAvailable = isAvailable, IsPublished = isPublished
        };
        db.Add(exception);
        db.Appointments.Add(new Appointment
        {
            NutritionistId = clinician.Id, PatientId = patient.Id, AppointmentDateTime = start.AddMinutes(appointmentOffset), Duration = 45, Status = status
        });
        await db.SaveChangesAsync();
        return exception;
    }

    private static JsonElement Input(AvailabilityException exception) => JsonSerializer.SerializeToElement(new
    {
        rowVersion = exception.RowVersion, approvalConfirmed = true, reason = "Reviewed isolated calendar configuration"
    });
    private static ConfigurationStore Store(NutriCareDbContext db, AvailabilityException exception) =>
        new(db, new Staff(exception.NutritionistId), new AppointmentReservationLock(db));

    private sealed class Staff(long id) : ICurrentUserService
    {
        public long UserId => id;
        public string Email => "staff@example.test";
        public string Role => "Admin";
        public bool IsAuthenticated => true;
        public bool CanAccessClinicalData => false;
    }
}

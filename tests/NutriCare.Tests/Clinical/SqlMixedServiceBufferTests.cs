namespace NutriCare.Tests.Clinical;

using Microsoft.EntityFrameworkCore;
using NutriCare.Application.Commands.Appointments;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Persistence.Repositories;

public sealed partial class SqlClinicalReservationTests
{
    [Theory]
    [InlineData(0, 60, 120)]
    [InlineData(210, 120, 60)]
    public async Task Persisted_long_service_buffer_survives_unpublication_and_edit_on_either_side(int existingMinutes, int blockedMinutes, int allowedMinutes)
    {
        var seed = await SeedAsync(); long shortServiceId; long originalId;
        await using (var setup = fixture.Open())
        {
            var originalService = await setup.Services.SingleAsync(s => s.Id == seed.ServiceId);
            originalService.DurationMinutes = 45; originalService.BookingBufferMinutes = 60;
            var shortService = new Service { NutritionistId = seed.NutritionistId, Name = "Short buffer consultation", Mode = "InPerson",
                DurationMinutes = 45, BookingBufferMinutes = 15, Price = 2500, Currency = "PKR", IsActive = true, IsPublished = true };
            setup.Services.Add(shortService); await setup.SaveChangesAsync(); shortServiceId = shortService.Id;
            var request = Request(seed, "long-buffer-original"); request.AppointmentDateTime = seed.Start.AddMinutes(existingMinutes);
            originalId = (await Workflow(setup, seed).BookAsync(request, default)).Id;
            Assert.Equal(60, (await setup.Appointments.SingleAsync(a => a.Id == originalId)).BookingBufferMinutesSnapshot);
            originalService.IsPublished = false; originalService.IsActive = false;
            await setup.SaveChangesAsync();
            originalService.BookingBufferMinutes = 15;
            await setup.SaveChangesAsync();
        }
        await using (var blocked = fixture.Open())
        {
            var scheduling = Scheduling(blocked);
            var slots = await scheduling.GetAvailableSlotsAsync(seed.NutritionistId, shortServiceId, seed.Start, seed.Start.AddHours(7));
            Assert.DoesNotContain(seed.Start.AddMinutes(blockedMinutes), slots);
            Assert.Contains(seed.Start.AddMinutes(allowedMinutes), slots);
            await Assert.ThrowsAsync<ConflictException>(() => scheduling.ValidateSlotAsync(seed.NutritionistId, shortServiceId, seed.Start.AddMinutes(blockedMinutes)));
            var request = Request(seed, "short-buffer-blocked"); request.ServiceId = shortServiceId; request.AppointmentDateTime = seed.Start.AddMinutes(blockedMinutes);
            await Assert.ThrowsAsync<ConflictException>(() => Workflow(blocked, seed).BookAsync(request, default));
        }
        await using (var allowed = fixture.Open())
        {
            var request = Request(seed, "short-buffer-exact-or-longer-gap"); request.ServiceId = shortServiceId; request.AppointmentDateTime = seed.Start.AddMinutes(allowedMinutes);
            var booked = await Workflow(allowed, seed).BookAsync(request, default);
            Assert.Equal(15, (await allowed.Appointments.SingleAsync(a => a.Id == booked.Id)).BookingBufferMinutesSnapshot);
        }
        await using var verification = fixture.Open();
        Assert.Equal(60, (await verification.Appointments.SingleAsync(a => a.Id == originalId)).BookingBufferMinutesSnapshot);
        Assert.Equal(2, await verification.Appointments.CountAsync(a => a.NutritionistId == seed.NutritionistId));
    }

    [Fact]
    public async Task Calendar_buffer_snapshot_is_preserved_when_rescheduling_and_checked_against_other_bookings()
    {
        var seed = await SeedAsync(); long originalId; long neighborId;
        await using (var setup = fixture.Open())
        {
            var rule = await setup.AvailabilityRules.SingleAsync(r => r.NutritionistId == seed.NutritionistId);
            rule.BookingBufferMinutes = 60; await setup.SaveChangesAsync();
            originalId = (await Workflow(setup, seed).BookAsync(Request(seed, "calendar-buffer-original"), default)).Id;
            Assert.Equal(60, (await setup.Appointments.SingleAsync(a => a.Id == originalId)).BookingBufferMinutesSnapshot);
            rule.IsPublished = false; await setup.SaveChangesAsync();
            rule.BookingBufferMinutes = 15; rule.IsPublished = true; await setup.SaveChangesAsync();
            var neighbor = new Appointment { PatientId = seed.PatientId, NutritionistId = seed.NutritionistId, ServiceId = seed.ServiceId,
                AppointmentDateTime = seed.Start.AddMinutes(120), Duration = 30, Status = "Confirmed", BookingBufferMinutesSnapshot = 15 };
            setup.Appointments.Add(neighbor); await setup.SaveChangesAsync(); neighborId = neighbor.Id;
        }
        var move = new RescheduleAppointmentCommand { AppointmentId = originalId, PatientId = seed.PatientId, RequestedByUserId = seed.PatientUserId,
            AppointmentDateTime = seed.Start.AddMinutes(45), TimeZoneId = "Asia/Karachi" };
        await using (var blocked = fixture.Open())
            await Assert.ThrowsAsync<ConflictException>(() => Workflow(blocked, seed).RescheduleAsync(move, default));
        await using (var edit = fixture.Open())
        {
            var neighbor = await edit.Appointments.SingleAsync(a => a.Id == neighborId);
            neighbor.AppointmentDateTime = seed.Start.AddMinutes(135); await edit.SaveChangesAsync();
        }
        await using (var allowed = fixture.Open())
        {
            Assert.Equal(move.AppointmentDateTime, (await Workflow(allowed, seed).RescheduleAsync(move, default)).AppointmentDateTime);
            Assert.Equal(60, (await allowed.Appointments.SingleAsync(a => a.Id == originalId)).BookingBufferMinutesSnapshot);
        }
    }

    private static ClinicalSchedulingService Scheduling(NutriCare.Persistence.Contexts.NutriCareDbContext db)
        => new(new Repository<Service>(db), new Repository<AvailabilityRule>(db), new Repository<AvailabilityException>(db), new Repository<Appointment>(db));
}

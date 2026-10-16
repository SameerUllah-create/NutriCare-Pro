namespace NutriCare.Tests.Clinical;

using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Tests.Integrations;

public sealed class MixedServiceBufferTests
{
    [Theory]
    [InlineData(0, 60, 120)]
    [InlineData(210, 120, 60)]
    public async Task Legacy_longer_service_buffer_blocks_shorter_service_on_either_side(int existingMinutes, int blockedMinutes, int allowedMinutes)
    {
        var f = Fixture();
        f.Appointments.Items.Add(new Appointment { Id = 1, NutritionistId = 20, ServiceId = 2,
            AppointmentDateTime = f.Start.AddMinutes(existingMinutes), Duration = 45, Status = "Confirmed" });
        var slots = await f.Scheduling.GetAvailableSlotsAsync(20, 1, f.Start, f.Start.AddHours(7));
        Assert.DoesNotContain(f.Start.AddMinutes(blockedMinutes), slots);
        Assert.Contains(f.Start.AddMinutes(allowedMinutes), slots);
        await Assert.ThrowsAsync<ConflictException>(() => f.Scheduling.ValidateSlotAsync(20, 1, f.Start.AddMinutes(blockedMinutes)));
        Assert.Equal(1, (await f.Scheduling.ValidateSlotAsync(20, 1, f.Start.AddMinutes(allowedMinutes))).Id);
    }

    [Fact]
    public async Task Booked_buffer_survives_service_edits_and_ignored_reschedule_does_not_conflict_with_itself()
    {
        var f = Fixture();
        f.Services.Items.Single(s => s.Id == 2).BookingBufferMinutes = 15;
        f.Services.Items.Single(s => s.Id == 2).IsPublished = false;
        f.Appointments.Items.Add(new Appointment { Id = 1, NutritionistId = 20, ServiceId = 2,
            AppointmentDateTime = f.Start, Duration = 45, Status = "Confirmed", BookingBufferMinutesSnapshot = 60 });
        var blocked = f.Start.AddHours(1);
        Assert.DoesNotContain(blocked, await f.Scheduling.GetAvailableSlotsAsync(20, 1, f.Start, f.Start.AddHours(3)));
        await Assert.ThrowsAsync<ConflictException>(() => f.Scheduling.ValidateSlotAsync(20, 1, blocked));
        Assert.Equal(1, (await f.Scheduling.ValidateSlotAsync(20, 1, blocked, ignoredAppointmentId: 1)).Id);
        f.Appointments.Items[0].Status = "Cancelled";
        Assert.Contains(blocked, await f.Scheduling.GetAvailableSlotsAsync(20, 1, f.Start, f.Start.AddHours(3)));
        Assert.Equal(1, (await f.Scheduling.ValidateSlotAsync(20, 1, blocked)).Id);
    }

    [Fact]
    public async Task Exact_larger_buffer_gap_is_allowed_and_one_minute_less_is_rejected()
    {
        var f = Fixture();
        var visit = new Appointment { Id = 1, NutritionistId = 20, ServiceId = 2,
            AppointmentDateTime = f.Start.AddMinutes(15), Duration = 45, Status = "Confirmed", BookingBufferMinutesSnapshot = 60 };
        f.Appointments.Items.Add(visit);
        var candidate = f.Start.AddHours(2);
        Assert.Contains(candidate, await f.Scheduling.GetAvailableSlotsAsync(20, 1, f.Start, candidate));
        Assert.Equal(1, (await f.Scheduling.ValidateSlotAsync(20, 1, candidate)).Id);
        visit.AppointmentDateTime = visit.AppointmentDateTime.AddMinutes(1);
        Assert.DoesNotContain(candidate, await f.Scheduling.GetAvailableSlotsAsync(20, 1, f.Start, candidate));
        await Assert.ThrowsAsync<ConflictException>(() => f.Scheduling.ValidateSlotAsync(20, 1, candidate));
    }

    [Fact]
    public async Task Reschedule_preserves_its_own_larger_buffer_against_other_reservations()
    {
        var f = Fixture();
        f.Appointments.Items.AddRange([
            new Appointment { Id = 1, NutritionistId = 20, ServiceId = 1, AppointmentDateTime = f.Start, Duration = 45,
                Status = "Confirmed", BookingBufferMinutesSnapshot = 60 },
            new Appointment { Id = 2, NutritionistId = 20, ServiceId = 1, AppointmentDateTime = f.Start.AddHours(2).AddMinutes(30),
                Duration = 45, Status = "Confirmed", BookingBufferMinutesSnapshot = 15 }
        ]);
        var candidate = f.Start.AddHours(1);
        await Assert.ThrowsAsync<ConflictException>(() => f.Scheduling.ValidateSlotAsync(20, 1, candidate, ignoredAppointmentId: 1));
        f.Appointments.Items[1].AppointmentDateTime = f.Start.AddHours(2).AddMinutes(45);
        Assert.Equal(1, (await f.Scheduling.ValidateSlotAsync(20, 1, candidate, ignoredAppointmentId: 1)).Id);
    }

    private static (ClinicalSchedulingService Scheduling, MemoryRepository<Service> Services, MemoryRepository<Appointment> Appointments, DateTime Start) Fixture()
    {
        var start = DateTime.UtcNow.Date.AddDays(14).AddHours(5);
        var services = new MemoryRepository<Service>();
        services.Items.AddRange([
            new Service { Id = 1, NutritionistId = 20, IsActive = true, IsPublished = true, DurationMinutes = 45, BookingBufferMinutes = 15 },
            new Service { Id = 2, NutritionistId = 20, IsActive = true, IsPublished = true, DurationMinutes = 45, BookingBufferMinutes = 60 }
        ]);
        var rules = new MemoryRepository<AvailabilityRule>();
        rules.Items.Add(new AvailabilityRule { NutritionistId = 20, DayOfWeek = start.DayOfWeek, StartTime = new(10, 0), EndTime = new(18, 0),
            IsPublished = true, TimeZoneId = "Asia/Karachi", BookingBufferMinutes = 15 });
        var appointments = new MemoryRepository<Appointment>();
        return (new(services, rules, new MemoryRepository<AvailabilityException>(), appointments), services, appointments, start);
    }
}

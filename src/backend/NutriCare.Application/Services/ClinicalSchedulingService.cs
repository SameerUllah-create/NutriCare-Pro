namespace NutriCare.Application.Services;

using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public static class ClinicalTime
{
    public const string DefaultZone = "Asia/Karachi";

    public static TimeZoneInfo Zone(string? zoneId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(zoneId) ? DefaultZone : zoneId); }
        catch (TimeZoneNotFoundException) { throw new ArgumentException("The selected time zone is not supported"); }
        catch (InvalidTimeZoneException) { throw new ArgumentException("The selected time zone is invalid"); }
    }

    public static DateTime ToUtc(DateTime value, string? zoneId = null)
    {
        if (value.Kind == DateTimeKind.Utc) return value;
        if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
        var zone = Zone(zoneId);
        if (zone.IsInvalidTime(value) || zone.IsAmbiguousTime(value))
            throw new ArgumentException("Choose an unambiguous appointment time with an explicit UTC offset");
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), zone);
    }

    public static DateTime LocalToday(string? zoneId = null) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone(zoneId)).Date;

    public static bool Overlaps(DateTime start, DateTime end, DateTime existingStart, DateTime existingEnd, int bufferMinutes = 0)
        => start < existingEnd.AddMinutes(bufferMinutes) && end.AddMinutes(bufferMinutes) > existingStart;
}

public sealed class ClinicalSchedulingService(
    IRepository<Service> services,
    IRepository<AvailabilityRule> rules,
    IRepository<AvailabilityException> exceptions,
    IRepository<Appointment> appointments) : IClinicalSchedulingService
{
    public async Task<IReadOnlyCollection<DateTime>> GetAvailableSlotsAsync(long nutritionistId, long? serviceId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        ValidateRange(fromUtc, toUtc);
        var service = await GetServiceAsync(nutritionistId, serviceId);
        var calendar = (await rules.GetAsync(q => q.Where(r => r.NutritionistId == nutritionistId && r.DeletedAt == null && r.IsPublished))).ToArray();
        if (calendar.Length == 0) return [];
        var changes = (await exceptions.GetAsync(q => q.Where(e => e.NutritionistId == nutritionistId && e.DeletedAt == null && e.IsPublished &&
            e.StartAtUtc < toUtc.AddDays(1) && e.EndAtUtc > fromUtc.AddDays(-1)))).ToArray();
        var existing = (await appointments.GetAsync(q => q.Where(a => a.NutritionistId == nutritionistId && a.DeletedAt == null &&
            a.Status != "Cancelled" && a.Status != "Rejected" && a.AppointmentDateTime < toUtc.AddDays(1) &&
            a.AppointmentDateTime > fromUtc.AddDays(-2)))).ToArray();
        var reservationBuffers = await GetReservationBuffersAsync(existing, calendar);
        var slots = new SortedSet<DateTime>();
        var now = DateTime.UtcNow;
        foreach (var rule in calendar)
        {
            if (rule.EndTime <= rule.StartTime || rule.BookingBufferMinutes < 0) continue;
            var zone = ClinicalTime.Zone(rule.TimeZoneId);
            var localStart = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), zone).Date;
            var localEnd = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc), zone).Date;
            for (var day = localStart; day <= localEnd; day = day.AddDays(1))
            {
                if (day.DayOfWeek != rule.DayOfWeek) continue;
                for (var local = day.Add(rule.StartTime.ToTimeSpan()); local.AddMinutes(service.DurationMinutes) <= day.Add(rule.EndTime.ToTimeSpan());
                    local = local.AddMinutes(service.DurationMinutes + Math.Max(service.BookingBufferMinutes, rule.BookingBufferMinutes)))
                {
                    if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) continue;
                    var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
                    var end = start.AddMinutes(service.DurationMinutes);
                    if (start < fromUtc || start > toUtc || start <= now || !ServiceEffective(service, start, end) || !RuleEffective(rule, start, end)) continue;
                    if (changes.Any(e => !e.IsAvailable && ClosureOverlaps(e, start, end))) continue;
                    if (ConflictsWithReservation(existing, reservationBuffers, start, end, Math.Max(service.BookingBufferMinutes, rule.BookingBufferMinutes))) continue;
                    slots.Add(DateTime.SpecifyKind(start, DateTimeKind.Utc));
                }
            }
        }
        // Published exceptional openings use their own UTC grid; closures always win.
        foreach (var opening in changes.Where(e => e.IsAvailable))
        {
            var buffer = Math.Max(service.BookingBufferMinutes, calendar.Max(r => Math.Max(0, r.BookingBufferMinutes)));
            var step = service.DurationMinutes + buffer;
            var offset = Math.Max(0, Math.Ceiling((fromUtc - opening.StartAtUtc).TotalMinutes / step));
            for (var start = opening.StartAtUtc.AddMinutes(offset * step); start <= toUtc && start.AddMinutes(service.DurationMinutes) <= opening.EndAtUtc; start = start.AddMinutes(step))
            {
                var end = start.AddMinutes(service.DurationMinutes);
                if (start < fromUtc || start > toUtc || start <= now || !ServiceEffective(service, start, end) || !ExceptionEffective(opening, start, end) ||
                    changes.Any(e => !e.IsAvailable && ClosureOverlaps(e, start, end))) continue;
                if (!ConflictsWithReservation(existing, reservationBuffers, start, end, buffer))
                    slots.Add(DateTime.SpecifyKind(start, DateTimeKind.Utc));
            }
        }
        return slots.ToArray();
    }

    public async Task<Service> ValidateSlotAsync(long nutritionistId, long? serviceId, DateTime startUtc, long? ignoredAppointmentId = null, CancellationToken cancellationToken = default)
    {
        var service = await GetServiceAsync(nutritionistId, serviceId);
        if (startUtc <= DateTime.UtcNow) throw new ArgumentException("Appointment must be in the future");
        // Grid generation checks the full calendar. Exclude the moved reservation when
        // testing availability, using the same interval/buffer rule under the SQL lock.
        var bufferResult = await GetCalendarBufferAsync(nutritionistId, service, startUtc, cancellationToken);
        if (!bufferResult.HasValue) throw new ConflictException("The selected time is outside the published available calendar");
        var buffer = bufferResult.Value;
        var end = startUtc.AddMinutes(service.DurationMinutes);
        // Include neighboring reservations before resolving each reservation's
        // own buffer; filtering with only the requested service buffer misses
        // longer buffers belonging to a different service on either side.
        var reservations = (await appointments.GetAsync(q => q.Where(a => a.NutritionistId == nutritionistId && a.DeletedAt == null &&
            (!ignoredAppointmentId.HasValue || a.Id != ignoredAppointmentId.Value) && a.Status != "Cancelled" && a.Status != "Rejected" &&
            a.AppointmentDateTime < end.AddDays(1) && a.AppointmentDateTime > startUtc.AddDays(-2)))).ToArray();
        var calendar = (await rules.GetAsync(q => q.Where(r => r.NutritionistId == nutritionistId && r.DeletedAt == null && r.IsPublished))).ToArray();
        var reservationBuffers = await GetReservationBuffersAsync(reservations, calendar);
        if (ignoredAppointmentId.HasValue)
        {
            var moved = await appointments.GetByIdAsync(ignoredAppointmentId.Value);
            if (moved != null && moved.NutritionistId == nutritionistId)
                buffer = Math.Max(buffer, (await GetReservationBuffersAsync([moved], calendar))[moved.Id]);
        }
        if (ConflictsWithReservation(reservations, reservationBuffers, startUtc, end, buffer))
            throw new ConflictException("The selected appointment slot is no longer available");
        return service;
    }

    public async Task<int> GetBookingBufferAsync(long nutritionistId, long? serviceId, DateTime startUtc, CancellationToken cancellationToken = default)
    {
        var service = await GetServiceAsync(nutritionistId, serviceId);
        return await GetCalendarBufferAsync(nutritionistId, service, startUtc, cancellationToken)
            ?? throw new ConflictException("The selected time is outside the published available calendar");
    }

    private async Task<Dictionary<long, int>> GetReservationBuffersAsync(IReadOnlyCollection<Appointment> reservations, IReadOnlyCollection<AvailabilityRule> calendar)
    {
        var legacyServiceIds = reservations.Where(a => !a.BookingBufferMinutesSnapshot.HasValue && a.ServiceId.HasValue)
            .Select(a => a.ServiceId!.Value).Distinct().ToArray();
        // Historical configuration is not reconstructable for older rows. Use
        // current service data (including unpublished/deleted services) and the
        // largest published calendar buffer as a conservative legacy fallback.
        Service[] legacyServices = legacyServiceIds.Length == 0 ? [] :
            (await services.GetAsync(q => q.Where(s => legacyServiceIds.Contains(s.Id)))).ToArray();
        var serviceBuffers = legacyServices.ToDictionary(s => s.Id, s => Math.Max(0, s.BookingBufferMinutes));
        var calendarBuffer = calendar.Count == 0 ? 0 : calendar.Max(r => Math.Max(0, r.BookingBufferMinutes));
        return reservations.ToDictionary(a => a.Id, a => a.BookingBufferMinutesSnapshot.HasValue
            ? Math.Max(0, a.BookingBufferMinutesSnapshot.Value)
            : Math.Max(calendarBuffer, a.ServiceId.HasValue && serviceBuffers.TryGetValue(a.ServiceId.Value, out var value) ? value : 0));
    }

    private static bool ConflictsWithReservation(IEnumerable<Appointment> reservations, IReadOnlyDictionary<long, int> buffers, DateTime start, DateTime end, int requestedBuffer)
        => reservations.Any(a => ClinicalTime.Overlaps(start, end, a.AppointmentDateTime, a.AppointmentDateTime.AddMinutes(a.Duration),
            Math.Max(requestedBuffer, buffers[a.Id])));

    private async Task<int?> GetCalendarBufferAsync(long clinicianId, Service service, DateTime start, CancellationToken cancellationToken)
    {
        var end = start.AddMinutes(service.DurationMinutes);
        var calendar = (await rules.GetAsync(q => q.Where(r => r.NutritionistId == clinicianId && r.DeletedAt == null && r.IsPublished))).ToArray();
        if (calendar.Length == 0 || !ServiceEffective(service, start, end)) return null;
        var changes = (await exceptions.GetAsync(q => q.Where(e => e.NutritionistId == clinicianId && e.DeletedAt == null && e.IsPublished && e.StartAtUtc < end && e.EndAtUtc > start))).ToArray();
        if (changes.Any(e => !e.IsAvailable && ClosureOverlaps(e, start, end))) return null;
        var exceptionalBuffer = Math.Max(service.BookingBufferMinutes, calendar.Max(r => Math.Max(0, r.BookingBufferMinutes)));
        if (changes.Any(e => e.IsAvailable && ExceptionEffective(e, start, end) && start >= e.StartAtUtc && end <= e.EndAtUtc &&
            (start - e.StartAtUtc).TotalMinutes % (service.DurationMinutes + exceptionalBuffer) == 0)) return exceptionalBuffer;
        foreach (var rule in calendar)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(start, DateTimeKind.Utc), ClinicalTime.Zone(rule.TimeZoneId));
            var localEnd = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(end, DateTimeKind.Utc), ClinicalTime.Zone(rule.TimeZoneId));
            if (local.DayOfWeek != rule.DayOfWeek || local.Date != localEnd.Date || !RuleEffective(rule, start, end)) continue;
            var gridMinutes = (local - local.Date.Add(rule.StartTime.ToTimeSpan())).TotalMinutes;
            if (gridMinutes >= 0 && localEnd.TimeOfDay <= rule.EndTime.ToTimeSpan() &&
                gridMinutes % (service.DurationMinutes + Math.Max(service.BookingBufferMinutes, Math.Max(0, rule.BookingBufferMinutes))) == 0)
                return Math.Max(service.BookingBufferMinutes, Math.Max(0, rule.BookingBufferMinutes));
        }
        return null;
    }

    private async Task<Service> GetServiceAsync(long clinicianId, long? serviceId)
    {
        if (!serviceId.HasValue) throw new ArgumentException("Select a published consultation service");
        var service = await services.GetByIdAsync(serviceId.Value);
        if (service == null || service.DeletedAt != null || !service.IsActive || !service.IsPublished ||
            (service.NutritionistId.HasValue && service.NutritionistId != clinicianId) || service.DurationMinutes is < 5 or > 480)
            throw new ArgumentException("The selected service is not available");
        return service;
    }

    private static void ValidateRange(DateTime from, DateTime to)
    {
        if (from == default || to < from || (to - from).TotalDays > 31) throw new ArgumentException("Choose an availability range of at most 31 days");
    }

    private static bool ServiceEffective(Service service, DateTime start, DateTime end) =>
        (!service.EffectiveFrom.HasValue || start >= service.EffectiveFrom.Value) && (!service.EffectiveTo.HasValue || end <= service.EffectiveTo.Value);

    private static bool RuleEffective(AvailabilityRule rule, DateTime start, DateTime end) =>
        (!rule.EffectiveFrom.HasValue || start >= rule.EffectiveFrom.Value) && (!rule.EffectiveTo.HasValue || end <= rule.EffectiveTo.Value);

    private static bool ExceptionEffective(AvailabilityException exception, DateTime start, DateTime end) =>
        (!exception.EffectiveFrom.HasValue || start >= exception.EffectiveFrom.Value) && (!exception.EffectiveTo.HasValue || end <= exception.EffectiveTo.Value);

    private static bool ClosureOverlaps(AvailabilityException closure, DateTime start, DateTime end)
    {
        // A closure applies to the intersection of its UTC interval and effective
        // window, even when it becomes effective partway through an appointment.
        var closureStart = closure.EffectiveFrom.HasValue && closure.EffectiveFrom > closure.StartAtUtc ? closure.EffectiveFrom.Value : closure.StartAtUtc;
        var closureEnd = closure.EffectiveTo.HasValue && closure.EffectiveTo < closure.EndAtUtc ? closure.EffectiveTo.Value : closure.EndAtUtc;
        return closureStart < closureEnd && ClinicalTime.Overlaps(start, end, closureStart, closureEnd);
    }
}

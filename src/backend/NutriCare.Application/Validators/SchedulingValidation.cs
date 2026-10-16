namespace NutriCare.Application.Validators;

using NutriCare.Application.Services;

internal static class SchedulingValidation
{
    internal static bool IsFuture(DateTime value, string timeZoneId)
    {
        try { return ClinicalTime.ToUtc(value, timeZoneId) > DateTime.UtcNow; }
        catch (Exception ex) when (ex is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException) { return false; }
    }
}

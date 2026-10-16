namespace NutriCare.Application.Services;

using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;

public static class ConfigurationRules
{
    public static void Validate(BaseEntity entity)
    {
        var errors = new Dictionary<string, string[]>();
        void Require(bool condition, string field, string message) { if (!condition) errors[field] = [message]; }
        static bool CurrencyValid(string? code) => code == "PKR";
        switch (entity)
        {
            case Service s:
                Require(!string.IsNullOrWhiteSpace(s.Name) && s.Name.Length <= 150, "name", "A service name of at most 150 characters is required");
                Require(s.Price > 0 && s.Price <= 1_000_000, "price", "Price must be positive and at most 1,000,000");
                Require(CurrencyValid(s.Currency), "currency", "Services use PKR for this launch");
                Require(s.Price == decimal.Round(s.Price,2), "price", "Price may have at most two decimal places");
                Require(s.DurationMinutes is 0 or (>= 5 and <= 480), "durationMinutes", "Consultations must last 5–480 minutes");
                Require(s.DurationMinutes > 0 || s.DeliveryHours is > 0 and <= 720, "deliveryHours", "A non-appointment service requires a delivery window");
                Require(s.Mode is "Online" or "InPerson" or "Either" or "Async", "mode", "Choose a supported consultation mode");
                Require(s.Mode != "Async" || s.DurationMinutes == 0, "durationMinutes", "Asynchronous services do not reserve an appointment");
                Require(s.DurationMinutes != 0 || s.Mode == "Async", "mode", "A delivery-only service must use Async mode");
                Require(s.BookingBufferMinutes is >= 0 and <= 120, "bookingBufferMinutes", "Buffer must be 0–120 minutes");
                Require(string.IsNullOrWhiteSpace(s.PackageServiceCode) || PackageConfigurationRules.AllServiceCodes.Contains(s.PackageServiceCode), "packageServiceCode", "Choose a supported package service code");
                break;
            case ServicePackage p:
                Require(!string.IsNullOrWhiteSpace(p.Name) && p.Name.Length <= 150, "name", "Package name is required");
                Require(p.Price > 0 && p.Price <= 1_000_000, "price", "Invalid package price");
                Require(p.DurationDays is >= 0 and <= 730, "durationDays", "Duration must be 0–730 days; zero is an incomplete draft");
                Require(p.DurationWeeks is >= 0 and <= 105, "durationWeeks", "Legacy duration must be 0–105 weeks");
                Require(p.InitialConsultationCount is >= 0 and <= 104, "initialConsultationCount", "Initial consultation count must be 0–104");
                Require(p.InitialConsultationMinutes is 0 or (>= 5 and <= 480), "initialConsultationMinutes", "Initial consultation minutes must be zero for an incomplete draft or 5–480");
                Require(p.FollowUpConsultationCount is >= 0 and <= 500, "followUpConsultationCount", "Follow-up count must be 0–500");
                Require(p.FollowUpConsultationMinutes is 0 or (>= 5 and <= 480), "followUpConsultationMinutes", "Follow-up minutes must be zero for an incomplete draft or 5–480");
                Require(p.DietPlanCount is >= 0 and <= 104, "dietPlanCount", "Diet plan count must be 0–104");
                Require(p.DietPlanRevisionCount is >= 0 and <= 500, "dietPlanRevisionCount", "Diet plan revision count must be 0–500");
                Require(p.CreditExpiryDays is >= 0 and <= 730, "creditExpiryDays", "Credit expiry must be 0–730 days");
                Require(p.FirstConsultationWithinDays is >= 0 and <= 365, "firstConsultationWithinDays", "First consultation window must be 0–365 days");
                Require(p.ProgressTrackingFrequency is "" or "Basic" or "Weekly", "progressTrackingFrequency", "Choose Basic or Weekly tracking");
                Require(p.ActivationRule is "" or "FirstCompletedConsultation", "activationRule", "Only first completed consultation activation is supported");
                Require(p.NoShowCreditRule is "" or "CancellationPolicy", "noShowCreditRule", "No-show credit handling must use the approved cancellation policy");
                Require(p.EligibleServiceCodes != null && p.EligibleServiceCodes.All(PackageConfigurationRules.EligibleServiceCodes.Contains) &&
                    p.EligibleServiceCodes.Distinct(StringComparer.Ordinal).Count() == p.EligibleServiceCodes.Length, "eligibleServiceCodes", "Choose distinct supported care categories");
                Require(CurrencyValid(p.Currency), "currency", "Packages use PKR for this launch");
                Require(p.Price == decimal.Round(p.Price,2), "price", "Price may have at most two decimal places");
                break;
            case AvailabilityRule r:
                Require(Enum.IsDefined(r.DayOfWeek), "dayOfWeek", "Invalid weekday");
                Require(r.StartTime < r.EndTime, "endTime", "End time must be after start time");
                Require(r.BookingBufferMinutes is >= 0 and <= 120, "bookingBufferMinutes", "Buffer must be 0–120 minutes");
                try { TimeZoneInfo.FindSystemTimeZoneById(r.TimeZoneId); } catch { Require(false, "timeZoneId", "Unknown timezone"); }
                break;
            case AvailabilityException e:
                Require(e.StartAtUtc < e.EndAtUtc, "endAtUtc", "End must be after start");
                Require((e.EndAtUtc - e.StartAtUtc).TotalDays <= 366, "endAtUtc", "Exception range is too long");
                break;
            case CancellationPolicy p:
                Require(!string.IsNullOrWhiteSpace(p.Name), "name", "Policy name is required");
                Require(p.FullRefundHours >= p.FreeRescheduleHours && p.FullRefundHours <= 8760, "fullRefundHours", "Refund cutoff must be at or after the reschedule cutoff");
                Require(p.FreeRescheduleHours >= 0, "freeRescheduleHours", "Cutoff cannot be negative");
                Require(p.MaxFreeReschedules is >= 0 and <= 20, "maxFreeReschedules", "Reschedule limit must be 0–20");
                Require(PackageConfigurationRules.CreditActions.Contains(p.PackageNoShowCreditAction), "packageNoShowCreditAction", "Choose ReviewRequired, Release or Consume");
                Require(PackageConfigurationRules.CreditActions.Contains(p.PackagePatientCancellationCreditAction), "packagePatientCancellationCreditAction", "Choose ReviewRequired, Release or Consume");
                break;
            case PolicyDocument p:
                Require(!string.IsNullOrWhiteSpace(p.Purpose) && p.Purpose.Length <= 64, "purpose", "Policy purpose is required");
                Require(!string.IsNullOrWhiteSpace(p.Version) && p.Version.Length <= 40, "version", "Policy version is required");
                Require(!string.IsNullOrWhiteSpace(p.Title) && !string.IsNullOrWhiteSpace(p.Content), "content", "Policy title and content are required");
                break;
        }
        var type = entity.GetType();
        var from = type.GetProperty("EffectiveFrom")?.GetValue(entity) as DateTime?;
        var to = type.GetProperty("EffectiveTo")?.GetValue(entity) as DateTime?;
        Require(!from.HasValue || !to.HasValue || from < to, "effectiveTo", "Effective end must be after effective start");
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    public static bool IsEffective(DateTime? from, DateTime? to, DateTime now) => (!from.HasValue || from <= now) && (!to.HasValue || now < to);
}

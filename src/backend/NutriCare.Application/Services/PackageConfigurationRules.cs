namespace NutriCare.Application.Services;

using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;

public static class PackageConfigurationRules
{
    public static readonly string[] GeneralServiceCodes = ["GeneralNutrition", "WeightLoss", "WeightGain", "HealthyLifestyle"];
    public static readonly string[] RestrictedServiceCodes = ["Diabetes", "PCOS", "Pregnancy", "ChildNutrition", "SportsNutrition"];
    public static readonly string[] EligibleServiceCodes = [.. GeneralServiceCodes, .. RestrictedServiceCodes];
    public static readonly string[] AllServiceCodes = [.. EligibleServiceCodes, "FollowUp", "DietPlan"];
    public static readonly string[] CreditActions = ["ReviewRequired", "Release", "Consume"];

    public static void NormalizeDuration(ServicePackage package, bool daysProvided, bool weeksProvided)
    {
        if (package.DurationWeeks is < 0 or > 105)
            throw new ValidationException(new Dictionary<string, string[]> { ["durationWeeks"] = ["Legacy duration must be 0–105 weeks"] });
        if (!daysProvided && (weeksProvided || package.DurationDays == 0 && package.DurationWeeks > 0)) package.DurationDays = package.DurationWeeks * 7;
        var weeks = package.DurationDays == 0 ? 0 : (package.DurationDays + 6) / 7;
        if (daysProvided && weeksProvided && package.DurationWeeks != weeks)
            throw new ValidationException(new Dictionary<string, string[]> { ["durationWeeks"] = ["Legacy weeks must match the authoritative duration in days"] });
        package.DurationWeeks = weeks;
    }

    public static void ValidateForPublication(ServicePackage package)
    {
        ConfigurationRules.Validate(package);
        var errors = new Dictionary<string, string[]>();
        void Require(bool value, string name, string message) { if (!value) errors[name] = [message]; }
        Require(!string.IsNullOrWhiteSpace(package.NameUrdu), "nameUrdu", "An approved Urdu package name is required");
        Require(package.DurationDays > 0 && package.DurationWeeks == (package.DurationDays + 6) / 7, "durationDays", "A confirmed duration in days is required");
        Require(package.InitialConsultationCount > 0 && package.InitialConsultationMinutes >= 5, "initialConsultationCount", "At least one initial consultation with a valid duration is required");
        Require(package.FollowUpConsultationCount == 0 || package.FollowUpConsultationMinutes >= 5, "followUpConsultationMinutes", "Confirm follow-up duration when follow-ups are included");
        Require(package.DietPlanCount > 0, "dietPlanCount", "At least one diet plan is required");
        Require(package.CreditExpiryDays > 0 && package.CreditExpiryDays <= package.DurationDays, "creditExpiryDays", "Credit expiry must be positive and within the package duration");
        Require(package.FirstConsultationWithinDays > 0, "firstConsultationWithinDays", "Confirm the first consultation booking window");
        Require(package.ProgressTrackingFrequency is "Basic" or "Weekly", "progressTrackingFrequency", "Confirm Basic or Weekly progress tracking");
        Require(package.ActivationRule == "FirstCompletedConsultation", "activationRule", "Activation starts at the first completed consultation");
        Require(package.NoShowCreditRule == "CancellationPolicy", "noShowCreditRule", "Credit handling must use the approved cancellation policy");
        Require(package.EligibleServiceCodes is { Length: > 0 }, "eligibleServiceCodes", "Select at least one eligible care category for verified clinician review");
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    public static void InvalidateApproval(ServicePackage package)
    { package.NutritionistApprovedById = null; package.NutritionistApprovedAt = null; package.NutritionistApprovedVersion = null; }
}

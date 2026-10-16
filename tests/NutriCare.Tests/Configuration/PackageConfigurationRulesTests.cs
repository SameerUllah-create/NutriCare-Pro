namespace NutriCare.Tests.Configuration;

using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;

public sealed class PackageConfigurationRulesTests
{
    [Fact]
    public void Incomplete_counts_can_be_saved_as_a_draft_but_cannot_be_published()
    {
        var package = new ServicePackage { Name = "Unconfirmed package", Price = 4000 };
        ConfigurationRules.Validate(package);
        var error = Assert.Throws<ValidationException>(() => PackageConfigurationRules.ValidateForPublication(package));
        Assert.Contains("initialConsultationCount", error.Errors!.Keys);
        Assert.Contains("durationDays", error.Errors.Keys);
        Assert.Contains("eligibleServiceCodes", error.Errors.Keys);
    }

    [Fact]
    public void Credits_cannot_remain_spendable_beyond_the_package_duration()
    {
        var package = Complete(); package.CreditExpiryDays = 15;
        var error = Assert.Throws<ValidationException>(() => PackageConfigurationRules.ValidateForPublication(package));
        Assert.Contains("creditExpiryDays", error.Errors!.Keys);
        package.CreditExpiryDays = 14;
        PackageConfigurationRules.ValidateForPublication(package);
    }

    [Fact]
    public void Authoritative_days_keep_partial_week_durations_and_legacy_week_writes_are_converted()
    {
        var package = new ServicePackage { DurationDays = 15 };
        PackageConfigurationRules.NormalizeDuration(package, daysProvided: true, weeksProvided: false);
        Assert.Equal(15, package.DurationDays); Assert.Equal(3, package.DurationWeeks);
        package.DurationWeeks = 4;
        PackageConfigurationRules.NormalizeDuration(package, daysProvided: false, weeksProvided: true);
        Assert.Equal(28, package.DurationDays);
        package.DurationDays = 14; package.DurationWeeks = 3;
        Assert.Throws<ValidationException>(() => PackageConfigurationRules.NormalizeDuration(package, true, true));
        package.DurationWeeks = int.MaxValue;
        Assert.Throws<ValidationException>(() => PackageConfigurationRules.NormalizeDuration(package, false, true));
    }

    [Theory]
    [InlineData("duration")]
    [InlineData("initial")]
    [InlineData("followUp")]
    [InlineData("expiry")]
    [InlineData("category")]
    [InlineData("duplicateCategory")]
    [InlineData("activation")]
    public void Unsupported_or_incomplete_terms_cannot_pass_publication_validation(string kind)
    {
        var package = Complete();
        switch (kind)
        {
            case "duration": package.DurationDays = 0; break;
            case "initial": package.InitialConsultationMinutes = 0; break;
            case "followUp": package.FollowUpConsultationMinutes = 0; break;
            case "expiry": package.CreditExpiryDays = 0; break;
            case "category": package.EligibleServiceCodes = ["UnreviewedCategory"]; break;
            case "duplicateCategory": package.EligibleServiceCodes = ["WeightLoss", "WeightLoss"]; break;
            case "activation": package.ActivationRule = "Payment"; break;
        }
        Assert.Throws<ValidationException>(() => PackageConfigurationRules.ValidateForPublication(package));
    }

    [Theory]
    [InlineData("GeneralNutrition")]
    [InlineData("Diabetes")]
    [InlineData("FollowUp")]
    [InlineData("DietPlan")]
    public void Supported_optional_service_tags_are_valid(string code)
    {
        ConfigurationRules.Validate(new Service { Name = "Tagged consultation", NameUrdu = "مشاورت", Price = 2500, DurationMinutes = 45, Mode = "InPerson", PackageServiceCode = code });
    }

    [Fact]
    public void Unknown_service_tags_and_credit_actions_are_rejected_without_inventing_no_show_actions()
    {
        Assert.Throws<ValidationException>(() => ConfigurationRules.Validate(new Service { Name = "Service", Price = 2500, DurationMinutes = 45, PackageServiceCode = "AnyService" }));
        var policy = new CancellationPolicy();
        Assert.Equal("ReviewRequired", policy.PackageNoShowCreditAction);
        Assert.Equal("ReviewRequired", policy.PackagePatientCancellationCreditAction);
        ConfigurationRules.Validate(policy);
        policy.PackageNoShowCreditAction = "AutomaticRefund";
        Assert.Throws<ValidationException>(() => ConfigurationRules.Validate(policy));
    }

    internal static ServicePackage Complete() => new()
    {
        Name = "Reviewed package", NameUrdu = "پیکیج", Price = 4000, Currency = "PKR", DurationDays = 14, DurationWeeks = 2,
        InitialConsultationCount = 1, InitialConsultationMinutes = 45, FollowUpConsultationCount = 1, FollowUpConsultationMinutes = 20,
        DietPlanCount = 1, DietPlanRevisionCount = 0, ProgressTrackingFrequency = "Basic", CreditExpiryDays = 14, FirstConsultationWithinDays = 30,
        EligibleServiceCodes = [.. PackageConfigurationRules.GeneralServiceCodes], ActivationRule = "FirstCompletedConsultation", NoShowCreditRule = "CancellationPolicy",
        RestoreCreditsOnProviderCancellation = true, EffectiveFrom = DateTime.UtcNow.AddDays(1)
    };
}

namespace NutriCare.Tests.Configuration;

using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;

public class ConfigurationRulesTests
{
    [Theory]
    [InlineData(0,45,"Online",null)]
    [InlineData(2500,0,"Online",null)]
    [InlineData(2500,45,"Async",48)]
    [InlineData(2500,45,"Unsupported",null)]
    public void Invalid_service_cannot_be_saved(decimal price,int duration,string mode,int? delivery)
    {
        var service=new Service { Name="Consultation",Price=price,DurationMinutes=duration,Mode=mode,DeliveryHours=delivery };
        Assert.Throws<ValidationException>(()=>ConfigurationRules.Validate(service));
    }

    [Fact] public void Delivery_service_has_no_reservable_duration() => ConfigurationRules.Validate(new Service {Name="Diet plan",Price=2000,Mode="Async",DurationMinutes=0,DeliveryHours=48});
    [Fact] public void Refund_cutoff_cannot_be_shorter_than_free_reschedule_cutoff() => Assert.Throws<ValidationException>(()=>ConfigurationRules.Validate(new CancellationPolicy {FullRefundHours=6,FreeRescheduleHours=24}));
    [Fact] public void Inverted_hours_are_rejected() => Assert.Throws<ValidationException>(()=>ConfigurationRules.Validate(new AvailabilityRule {StartTime=new(18,0),EndTime=new(10,0)}));
    [Fact] public void Effective_end_is_exclusive()
    {
        var from=DateTime.UtcNow;var to=from.AddDays(1);
        Assert.True(ConfigurationRules.IsEffective(from,to,from));Assert.False(ConfigurationRules.IsEffective(from,to,to));
    }
    [Fact] public void Unknown_timezone_is_rejected() => Assert.Throws<ValidationException>(()=>ConfigurationRules.Validate(new AvailabilityRule {StartTime=new(10,0),EndTime=new(18,0),TimeZoneId="Unconfigured/Zone"}));
    [Fact] public void Unsupported_currency_is_rejected() => Assert.Throws<ValidationException>(()=>ConfigurationRules.Validate(new Service {Name="Consultation",Price=2500,DurationMinutes=45,Currency="USD"}));
    [Fact] public void Sub_minor_unit_prices_cannot_reach_checkout() => Assert.Throws<ValidationException>(()=>ConfigurationRules.Validate(new Service {Name="Consultation",Price=2500.001m,DurationMinutes=45}));
}

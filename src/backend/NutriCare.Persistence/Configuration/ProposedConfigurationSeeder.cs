namespace NutriCare.Persistence.Configuration;

using Microsoft.EntityFrameworkCore;
using NutriCare.Domain.Entities;
using NutriCare.Persistence.Contexts;
using System.Text.Json;
using NutriCare.Application.Services;

/// <summary>Seeds only proposed drafts. No seed or restart may approve or activate configuration.</summary>
public static class ProposedConfigurationSeeder
{
    public static async Task SeedAsync(NutriCareDbContext context, CancellationToken ct = default)
    {
        // Serialize seed attempts across app instances without ever modifying existing proposals.
        await using var tx = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (context.Database.IsSqlServer())
            await context.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='NutriCare:configuration-seed',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Configuration seeding is busy',1;", ct);
        if (await context.ConfigurationRevisions.AnyAsync(h => h.EntityType == "seed" && h.Action == "Proposed configuration v1", ct))
        { await SeedPackageVersion2Async(context, ct); await tx.CommitAsync(ct); return; }
        var proposals = new (string Name, string Urdu, int Minutes, decimal Price, int? Delivery)[]
        {
            ("Initial Nutrition Consultation", "ابتدائی غذائی مشاورت",45,2500,null),
            ("Weight Loss", "وزن کم کرنے کی مشاورت",45,2500,null),
            ("Weight Gain", "وزن بڑھانے کی مشاورت",45,2500,null),
            ("Diabetes Nutrition", "ذیابیطس کے لیے غذائی مشاورت",45,3000,null),
            ("PCOS Nutrition", "پی سی او ایس کے لیے غذائی مشاورت",45,3000,null),
            ("Pregnancy Nutrition", "حمل کے دوران غذائی مشاورت",45,3000,null),
            ("Child Nutrition", "بچوں کے لیے غذائی مشاورت",45,2500,null),
            ("Sports Nutrition", "کھیلوں کے لیے غذائی مشاورت",60,3500,null),
            ("Follow-up", "فالو اَپ مشاورت",20,1500,null),
            ("Personalized Diet Plan", "ذاتی غذائی منصوبہ",0,2000,48)
        };
        foreach (var p in proposals)
        {
            if (!await context.Services.AnyAsync(s => s.Name == p.Name, ct))
                context.Services.Add(new Service { Name = p.Name, NameUrdu = p.Urdu, DurationMinutes = p.Minutes,
                    Price = p.Price, DeliveryHours = p.Delivery, Mode = p.Delivery.HasValue ? "Async" : "Online",
                    Currency = "PKR", BookingBufferMinutes = 15, PackageServiceCode = PackageCode(p.Name), IsActive = false, IsPublished = false });
        }
        foreach (var p in new[] { ("Starter", "ابتدائی پیکیج", 2, 4000m), ("Monthly", "ماہانہ پیکیج", 4, 7500m), ("Transformation", "تبدیلی کا پیکیج", 12, 18000m) })
            if (!await context.ServicePackages.AnyAsync(s => s.Name == p.Item1, ct))
                context.ServicePackages.Add(ProposedPackage(p.Item1, p.Item2, p.Item3, p.Item4, 1));
        for (var day = 1; day <= 6; day++)
        {
            var weekday = (DayOfWeek)day;
            if (!await context.AvailabilityRules.AnyAsync(r => r.NutritionistId == 0 && r.DayOfWeek == weekday, ct))
                context.AvailabilityRules.Add(new AvailabilityRule { NutritionistId = 0, DayOfWeek = weekday,
                    StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(day == 6 ? 14 : 18, 0),
                    TimeZoneId = "Asia/Karachi", BookingBufferMinutes = 15, IsPublished = false });
        }
        // Sunday has no rule, so it remains closed. Clinician 0 is an unassigned draft, never publishable.
        if (!await context.CancellationPolicies.AnyAsync(p => p.Name == "Proposed cancellation policy", ct))
            context.CancellationPolicies.Add(new CancellationPolicy { Name = "Proposed cancellation policy", FullRefundHours = 24,
                FreeRescheduleHours = 6, MaxFreeReschedules = 1, ProviderCancellationRefund = true,
                NoShowAutomaticRefund = false, IsPublished = false, Status = "Draft" });
        var additions = context.ChangeTracker.Entries<BaseEntity>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToArray();
        await context.SaveChangesAsync(ct);
        foreach (var entity in additions)
            context.ConfigurationRevisions.Add(new ConfigurationRevision { EntityType = entity.GetType().Name, EntityId = entity.Id,
                Version = 1, Action = "Seeded proposed draft", ChangedByUserId = 0,
                SnapshotJson = JsonSerializer.Serialize(entity,entity.GetType()) });
        context.ConfigurationRevisions.Add(new ConfigurationRevision { EntityType = "seed", EntityId = 0, Version = 1,
            Action = "Proposed configuration v1", ChangedByUserId = 0, SnapshotJson = "{\"approved\":false}" });
        context.ConfigurationRevisions.Add(new ConfigurationRevision { EntityType = "seed", EntityId = 0, Version = 2,
            Action = "Proposed package configuration v2", ChangedByUserId = 0, SnapshotJson = "{\"approved\":false,\"checkoutEnabled\":false}" });
        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task SeedPackageVersion2Async(NutriCareDbContext context, CancellationToken ct)
    {
        if (await context.ConfigurationRevisions.AnyAsync(h => h.EntityType == "seed" && h.Action == "Proposed package configuration v2", ct)) return;
        // Preserve every existing v1 proposal, including owner edits. V2 terms
        // are separate drafts, never a reset of a price, approval or live row.
        foreach (var proposal in new[] { ("Starter", "ابتدائی پیکیج", 2, 4000m), ("Monthly", "ماہانہ پیکیج", 4, 7500m), ("Transformation", "تبدیلی کا پیکیج", 12, 18000m) })
        {
            var previous = await context.ServicePackages.Where(p => p.Name == proposal.Item1).Select(p => (int?)p.Version).MaxAsync(ct) ?? 1;
            var package = ProposedPackage(proposal.Item1, proposal.Item2, proposal.Item3, proposal.Item4, Math.Max(2, previous + 1));
            context.ServicePackages.Add(package); await context.SaveChangesAsync(ct);
            context.ConfigurationRevisions.Add(new ConfigurationRevision { EntityType = "packages", EntityId = package.Id, Version = package.Version,
                Action = "Seeded proposed package v2 draft", ChangedByUserId = 0, SnapshotJson = JsonSerializer.Serialize(package) });
        }
        context.ConfigurationRevisions.Add(new ConfigurationRevision { EntityType = "seed", EntityId = 0, Version = 2,
            Action = "Proposed package configuration v2", ChangedByUserId = 0, SnapshotJson = "{\"approved\":false,\"checkoutEnabled\":false}" });
        await context.SaveChangesAsync(ct);
    }

    private static ServicePackage ProposedPackage(string name, string urdu, int weeks, decimal price, int version) => new()
    {
        Name = name, NameUrdu = urdu, DurationWeeks = weeks, DurationDays = weeks * 7, Price = price, Currency = "PKR", Version = version,
        InitialConsultationCount = 1, InitialConsultationMinutes = 45,
        FollowUpConsultationCount = name == "Starter" ? 1 : name == "Monthly" ? 3 : 11, FollowUpConsultationMinutes = 20,
        DietPlanCount = name == "Transformation" ? 3 : 1, DietPlanRevisionCount = name == "Starter" ? 0 : name == "Monthly" ? 2 : 6,
        ProgressTrackingFrequency = name == "Starter" ? "Basic" : "Weekly", CreditExpiryDays = weeks * 7, FirstConsultationWithinDays = 30,
        EligibleServiceCodes = [.. PackageConfigurationRules.GeneralServiceCodes], ActivationRule = "FirstCompletedConsultation", NoShowCreditRule = "CancellationPolicy",
        RestoreCreditsOnProviderCancellation = true, IsActive = false, IsPublished = false
    };

    private static string? PackageCode(string name) => name switch
    {
        "Initial Nutrition Consultation" => "GeneralNutrition", "Weight Loss" => "WeightLoss", "Weight Gain" => "WeightGain",
        "Diabetes Nutrition" => "Diabetes", "PCOS Nutrition" => "PCOS", "Pregnancy Nutrition" => "Pregnancy", "Child Nutrition" => "ChildNutrition",
        "Sports Nutrition" => "SportsNutrition", "Follow-up" => "FollowUp", "Personalized Diet Plan" => "DietPlan", _ => null
    };
}

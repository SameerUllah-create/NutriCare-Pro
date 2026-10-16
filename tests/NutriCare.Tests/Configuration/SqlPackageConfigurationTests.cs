namespace NutriCare.Tests.Configuration;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Configuration;
using NutriCare.Persistence.Contexts;
using NutriCare.Persistence.Repositories;

[Trait("Category", "SqlIntegration")]
public sealed class SqlPackageConfigurationTests(IsolatedSqlDatabase fixture) : IClassFixture<IsolatedSqlDatabase>
{
    [Fact]
    public async Task Proposed_v2_terms_are_draft_only_and_upgrade_preserves_edited_v1_rows_without_restart_duplicates()
    {
        await using var db = fixture.Open(); await ProposedConfigurationSeeder.SeedAsync(db);
        string[] names = ["Starter", "Monthly", "Transformation"];
        var proposals = await db.ServicePackages.Where(p => names.Contains(p.Name)).ToListAsync();
        Assert.Equal(3, proposals.Count);
        AssertTerms(proposals.Single(p => p.Name == "Starter"), 14, 4000, 1, 1, 0, "Basic");
        AssertTerms(proposals.Single(p => p.Name == "Monthly"), 28, 7500, 3, 1, 2, "Weekly");
        AssertTerms(proposals.Single(p => p.Name == "Transformation"), 84, 18000, 11, 3, 6, "Weekly");
        var initialIds = proposals.Select(p => p.Id).ToArray(); var starter = proposals.Single(p => p.Name == "Starter");
        starter.Name = "Owner-edited starter"; starter.Price = 4321; starter.InitialConsultationCount = 2;
        var service = await db.Services.SingleAsync(s => s.Name == "Initial Nutrition Consultation");
        Assert.Equal("GeneralNutrition", service.PackageServiceCode); service.PackageServiceCode = null;
        db.ConfigurationRevisions.RemoveRange(await db.ConfigurationRevisions.Where(h => h.EntityType == "seed" && h.Action == "Proposed package configuration v2").ToListAsync());
        await db.SaveChangesAsync();
        await ProposedConfigurationSeeder.SeedAsync(db);
        Assert.Equal(4321, starter.Price); Assert.Equal(2, starter.InitialConsultationCount); Assert.Equal("Owner-edited starter", starter.Name);
        Assert.Null(service.PackageServiceCode); // Upgrading never tags an existing service implicitly.
        var upgraded = await db.ServicePackages.Where(p => !initialIds.Contains(p.Id) && names.Contains(p.Name)).ToListAsync();
        Assert.Equal(3, upgraded.Count); Assert.All(upgraded, p => { Assert.True(p.Version >= 2); AssertDraft(p); });
        var count = await db.ServicePackages.CountAsync(); await ProposedConfigurationSeeder.SeedAsync(db);
        Assert.Equal(count, await db.ServicePackages.CountAsync());
        Assert.Equal(1, await db.ConfigurationRevisions.CountAsync(h => h.EntityType == "seed" && h.Action == "Proposed package configuration v2"));
        var cancellation = await db.CancellationPolicies.SingleAsync(p => p.Name == "Proposed cancellation policy");
        Assert.Equal("ReviewRequired", cancellation.PackageNoShowCreditAction); Assert.Equal("ReviewRequired", cancellation.PackagePatientCancellationCreditAction);
    }

    [Fact]
    public async Task Only_verified_nutritionist_review_of_exact_content_allows_separate_publication_and_clone_clears_review()
    {
        await using var db = fixture.Open(); var actors = await Actors(db);
        var admin = Store(db, "Admin", actors.AdminId); var reviewer = Store(db, "Nutritionist", actors.NutritionistUserId);
        var package = (ServicePackage)await admin.SaveAsync("packages", null, Input(Complete()));
        await Assert.ThrowsAsync<ConflictException>(() => admin.PublishAsync("packages", package.Id, Approve(package), true));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => admin.ApprovePackageAsync(package.Id, Approve(package)));
        await Assert.ThrowsAsync<ValidationException>(() => reviewer.ApprovePackageAsync(package.Id, Input(new { expectedVersion = package.Version, approvalConfirmed = false })));
        await reviewer.ApprovePackageAsync(package.Id, Approve(package));
        Assert.False(package.IsPublished); Assert.False(package.IsActive); Assert.Equal(actors.NutritionistUserId, package.NutritionistApprovedById);
        var reviewed = package.Version;
        await admin.PublishAsync("packages", package.Id, Approve(package), true);
        Assert.True(package.IsPublished); Assert.True(package.IsActive); Assert.Equal(reviewed, package.Version); Assert.Equal(package.Version, package.NutritionistApprovedVersion);
        var clone = (ServicePackage)await admin.CreateRevisionAsync("packages", package.Id);
        AssertDraft(clone); Assert.True(package.IsPublished); Assert.True(clone.Version > package.Version);
        await admin.SaveAsync("packages", clone.Id, Input(new { expectedVersion = clone.Version, nutritionistApprovedById = actors.AdminId,
            nutritionistApprovedAt = DateTime.UtcNow, nutritionistApprovedVersion = clone.Version }));
        Assert.Null(clone.NutritionistApprovedById);
        Assert.True(await db.ConfigurationRevisions.AnyAsync(h => h.EntityType == "packages" && h.EntityId == package.Id && h.ChangedByUserId == actors.NutritionistUserId && h.Action.Contains("Nutritionist approved")));
    }

    [Fact]
    public async Task Editing_eligible_specialties_invalidates_review_and_requires_a_new_verified_review()
    {
        await using var db = fixture.Open(); var actors = await Actors(db);
        var admin = Store(db, "Admin", actors.AdminId); var reviewer = Store(db, "Nutritionist", actors.NutritionistUserId);
        var package = (ServicePackage)await admin.SaveAsync("packages", null, Input(Complete()));
        await reviewer.ApprovePackageAsync(package.Id, Approve(package)); var version = package.Version;
        await admin.SaveAsync("packages", package.Id, Input(new { expectedVersion = package.Version, eligibleServiceCodes = new[] { "Diabetes", "PCOS" } }));
        Assert.True(package.Version > version); Assert.Null(package.NutritionistApprovedVersion); Assert.Null(package.NutritionistApprovedById);
        await Assert.ThrowsAsync<ConflictException>(() => admin.PublishAsync("packages", package.Id, Approve(package), true));
        await reviewer.ApprovePackageAsync(package.Id, Approve(package));
        await admin.PublishAsync("packages", package.Id, Approve(package), true);
        Assert.Equal(new[] { "Diabetes", "PCOS" }, package.EligibleServiceCodes);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Unverified_inactive_or_unconfirmed_nutritionists_cannot_approve(bool verified, bool active, bool confirmed)
    {
        await using var db = fixture.Open(); var actors = await Actors(db);
        var profile = await db.Nutritionists.SingleAsync(n => n.UserId == actors.NutritionistUserId);
        profile.IsVerified = verified; var account = await db.Users.SingleAsync(u => u.Id == actors.NutritionistUserId);
        account.IsActive = active; account.EmailConfirmed = confirmed; await db.SaveChangesAsync();
        var package = (ServicePackage)await Store(db, "Admin", actors.AdminId).SaveAsync("packages", null, Input(Complete()));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Store(db, "Nutritionist", actors.NutritionistUserId).ApprovePackageAsync(package.Id, Approve(package)));
        AssertDraft(package);
    }

    [Fact]
    public async Task Approval_is_unusable_after_clinician_deactivation_and_stale_content_review_is_rejected()
    {
        var actorsId = (AdminId: 0L, NutritionistUserId: 0L); long id; byte[] stale;
        await using (var create = fixture.Open())
        {
            actorsId = await Actors(create);
            var package = (ServicePackage)await Store(create, "Admin", actorsId.AdminId).SaveAsync("packages", null, Input(Complete())); id = package.Id; stale = package.RowVersion;
        }
        await using (var edit = fixture.Open())
            await Store(edit, "Admin", actorsId.AdminId).SaveAsync("packages", id, Input(new { rowVersion = stale, price = 4500 }));
        await using (var staleReview = fixture.Open())
            await Assert.ThrowsAsync<ConflictException>(() => Store(staleReview, "Nutritionist", actorsId.NutritionistUserId).ApprovePackageAsync(id, Input(new { rowVersion = stale, approvalConfirmed = true })));
        await using var db = fixture.Open(); var row = await db.ServicePackages.SingleAsync(p => p.Id == id);
        await Store(db, "Nutritionist", actorsId.NutritionistUserId).ApprovePackageAsync(id, Approve(row));
        (await db.Users.SingleAsync(u => u.Id == actorsId.NutritionistUserId)).IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => Store(db, "Admin", actorsId.AdminId).PublishAsync("packages", row.Id, Approve(row), true));
        Assert.False(row.IsPublished);
    }

    private static ServicePackage Complete()
    { var row = PackageConfigurationRulesTests.Complete(); row.Name = "Package test " + Guid.NewGuid().ToString("N"); return row; }
    private static JsonElement Input(object value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static JsonElement Approve(ServicePackage package) => Input(new { expectedVersion = package.Version, approvalConfirmed = true });
    private static ConfigurationStore Store(NutriCareDbContext db, string role, long user) => new(db, new PackageActor(role, user), new AppointmentReservationLock(db));
    private static async Task<(long AdminId, long NutritionistUserId)> Actors(NutriCareDbContext db)
    {
        var admin = new User { Email = Guid.NewGuid() + "@example.test", FirstName = "Config", LastName = "Admin", IsActive = true, EmailConfirmed = true };
        var user = new User { Email = Guid.NewGuid() + "@example.test", FirstName = "Verified", LastName = "Nutritionist", IsActive = true, EmailConfirmed = true };
        db.Users.AddRange(admin, user); await db.SaveChangesAsync();
        db.Nutritionists.Add(new Nutritionist { UserId = user.Id, IsVerified = true, LicenseNumber = "PACKAGE-" + Guid.NewGuid().ToString("N") });
        await db.SaveChangesAsync(); return (admin.Id, user.Id);
    }
    private static void AssertDraft(ServicePackage p)
    { Assert.False(p.IsActive); Assert.False(p.IsPublished); Assert.Null(p.NutritionistApprovedById); Assert.Null(p.NutritionistApprovedAt); Assert.Null(p.NutritionistApprovedVersion); }
    private static void AssertTerms(ServicePackage p, int days, decimal price, int followUps, int plans, int revisions, string frequency)
    {
        Assert.Equal(days, p.DurationDays); Assert.Equal(days / 7, p.DurationWeeks); Assert.Equal(price, p.Price);
        Assert.Equal(1, p.InitialConsultationCount); Assert.Equal(45, p.InitialConsultationMinutes); Assert.Equal(followUps, p.FollowUpConsultationCount);
        Assert.Equal(20, p.FollowUpConsultationMinutes); Assert.Equal(plans, p.DietPlanCount); Assert.Equal(revisions, p.DietPlanRevisionCount);
        Assert.Equal(frequency, p.ProgressTrackingFrequency); Assert.Equal(days, p.CreditExpiryDays); Assert.Equal(30, p.FirstConsultationWithinDays);
        Assert.Equal(PackageConfigurationRules.GeneralServiceCodes, p.EligibleServiceCodes); Assert.Equal("FirstCompletedConsultation", p.ActivationRule);
        Assert.Equal("CancellationPolicy", p.NoShowCreditRule); Assert.True(p.RestoreCreditsOnProviderCancellation); AssertDraft(p);
    }
    private sealed class PackageActor(string role, long id) : ICurrentUserService
    { public long UserId => id; public string Role => role; public string Email => "test@example.test"; public bool IsAuthenticated => true; public bool CanAccessClinicalData => false; }
}

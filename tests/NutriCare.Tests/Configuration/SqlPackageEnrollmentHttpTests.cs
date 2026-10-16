namespace NutriCare.Tests.Configuration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;
using NutriCare.Tests.Integrations;

[Trait("Category", "SqlIntegration")]
public sealed class SqlPackageEnrollmentHttpTests(IsolatedSqlDatabase database) : IClassFixture<IsolatedSqlDatabase>
{
    [Fact]
    public async Task Patients_list_only_owned_enrollments_and_other_patient_details_are_not_found_while_clinicians_are_forbidden()
    {
        using var host = new SqlAuthHost(database.ConnectionString); var seed = await SeedAsync(host, true);
        using var patient = Client(host, seed.PatientToken); using var clinician = Client(host, seed.NutritionistToken);
        var listed = await patient.GetAsync("/api/package-enrollments"); Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var data = await Data(listed); var item = Assert.Single(data.GetProperty("items").EnumerateArray());
        Assert.Equal(seed.EnrollmentId, item.GetProperty("id").GetInt64()); Assert.False(data.GetProperty("checkoutEnabled").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await patient.GetAsync($"/api/package-enrollments/{seed.EnrollmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await patient.GetAsync($"/api/package-enrollments/{seed.ForeignEnrollmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await clinician.GetAsync("/api/package-enrollments")).StatusCode);
    }

    [Fact]
    public async Task Extension_rejects_patients_missing_reason_and_invalid_versions_without_history_or_validity_changes()
    {
        using var host = new SqlAuthHost(database.ConnectionString); var seed = await SeedAsync(host, true);
        using var patient = Client(host, seed.PatientToken); using var admin = Client(host, seed.AdminToken);
        var path = $"/api/package-enrollments/{seed.EnrollmentId}/extend";
        Assert.Equal(HttpStatusCode.Forbidden, (await patient.PostAsJsonAsync(path, new { days = 7, reason = "Unauthorized extension", rowVersion = seed.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path, new { days = 7, reason = "   ", rowVersion = seed.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(path, new { days = 7, reason = "Reviewed adjustment", rowVersion = "invalid-base64!" })).StatusCode);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>();
        var enrollment = await db.PackageEnrollments.SingleAsync(e => e.Id == seed.EnrollmentId);
        Assert.Equal(seed.PreviousValidityUtc, enrollment.ExpiresAtUtc);
        Assert.False(await db.PackageCreditHistory.AnyAsync(h => h.PackageEnrollmentId == seed.EnrollmentId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Administrator_extension_records_reason_before_after_and_rejects_a_stale_retry(bool active)
    {
        using var host = new SqlAuthHost(database.ConnectionString); var seed = await SeedAsync(host, active);
        using var admin = Client(host, seed.AdminToken); var path = $"/api/package-enrollments/{seed.EnrollmentId}/extend";
        var input = new { days = 7, reason = "  Approved service interruption adjustment  ", rowVersion = seed.RowVersion };
        var extended = await admin.PostAsJsonAsync(path, input); Assert.Equal(HttpStatusCode.OK, extended.StatusCode);
        var data = await Data(extended); Assert.NotEqual(seed.RowVersion, data.GetProperty("rowVersion").GetString());
        Assert.Equal(seed.PreviousValidityUtc.AddDays(7), data.GetProperty(active ? "expiresAtUtc" : "firstConsultationDueAtUtc").GetDateTime());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path, input)).StatusCode);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>();
        var history = await db.PackageCreditHistory.SingleAsync(h => h.PackageEnrollmentId == seed.EnrollmentId);
        Assert.Equal("ValidityExtended", history.Action); Assert.Equal(seed.AdminUserId, history.ActorUserId);
        Assert.Equal("Approved service interruption adjustment", history.Reason);
        Assert.Equal(seed.PreviousValidityUtc, history.PreviousExpiryUtc); Assert.Equal(seed.PreviousValidityUtc.AddDays(7), history.NewExpiryUtc);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.UserId == seed.AdminUserId && a.Description == "POST " + path && a.StatusCode == 200 && a.IsSuccess));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.UserId == seed.AdminUserId && a.Description == "POST " + path && a.StatusCode == 409 && !a.IsSuccess));
    }

    private static HttpClient Client(SqlAuthHost host, string token)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return client;
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");

    private static async Task<EnrollmentSeed> SeedAsync(SqlAuthHost host, bool active)
    {
        using var scope = host.Services.CreateScope(); var services = scope.ServiceProvider; var manager = services.GetRequiredService<UserManager<User>>();
        var owner = await Account("Patient"); var other = await Account("Patient"); var clinician = await Account("Nutritionist"); var admin = await Account("Admin");
        var db = services.GetRequiredService<NutriCareDbContext>();
        var patient = new Patient { UserId = owner.Id }; var foreign = new Patient { UserId = other.Id };
        db.Patients.AddRange(patient, foreign); db.Nutritionists.Add(new Nutritionist { UserId = clinician.Id, IsVerified = true, LicenseNumber = "ENROLLMENT-" + Guid.NewGuid().ToString("N") });
        await db.SaveChangesAsync();
        var package = PackageConfigurationRulesTests.Complete(); package.Name = "HTTP enrollment test " + Guid.NewGuid().ToString("N");
        package.IsPublished = true; package.IsActive = true; package.NutritionistApprovedById = clinician.Id; package.NutritionistApprovedAt = DateTime.UtcNow.AddDays(-2); package.NutritionistApprovedVersion = package.Version;
        db.ServicePackages.Add(package); await db.SaveChangesAsync();
        var payment = new Payment { PatientId = patient.Id, TransactionId = "enrollment-test-" + Guid.NewGuid().ToString("N"), Amount = package.Price, Currency = "PKR", Status = "Completed", RelatedToType = "Package", RelatedToId = package.Id, PaidAt = DateTime.UtcNow.AddDays(-1) };
        var foreignPayment = new Payment { PatientId = foreign.Id, TransactionId = "enrollment-test-" + Guid.NewGuid().ToString("N"), Amount = package.Price, Currency = "PKR", Status = "Completed", RelatedToType = "Package", RelatedToId = package.Id, PaidAt = DateTime.UtcNow.AddDays(-1) };
        db.Payments.AddRange(payment, foreignPayment); await db.SaveChangesAsync();
        var previous = DateTime.UtcNow.Date.AddDays(active ? 14 : 30);
        var enrollment = new PackageEnrollment { PatientId = patient.Id, ServicePackageId = package.Id, PaymentId = payment.Id, PackageVersion = package.Version,
            DefinitionSnapshotJson = JsonSerializer.Serialize(package), PurchasedAtUtc = DateTime.UtcNow.AddDays(-1), FirstConsultationDueAtUtc = active ? previous.AddDays(16) : previous,
            ActivatedAtUtc = active ? DateTime.UtcNow.AddDays(-1) : null, ExpiresAtUtc = active ? previous : null, Status = active ? "Active" : "AwaitingFirstConsultation" };
        var foreignEnrollment = new PackageEnrollment { PatientId = foreign.Id, ServicePackageId = package.Id, PaymentId = foreignPayment.Id, PackageVersion = package.Version,
            DefinitionSnapshotJson = JsonSerializer.Serialize(package), PurchasedAtUtc = DateTime.UtcNow.AddDays(-1), FirstConsultationDueAtUtc = previous, Status = "AwaitingFirstConsultation" };
        db.PackageEnrollments.AddRange(enrollment, foreignEnrollment); await db.SaveChangesAsync();
        var sessions = services.GetRequiredService<IAuthSessionService>();
        return new(enrollment.Id, foreignEnrollment.Id, admin.Id, previous, Convert.ToBase64String(enrollment.RowVersion),
            (await sessions.IssueAsync(owner, "Patient")).AccessToken, (await sessions.IssueAsync(clinician, "Nutritionist")).AccessToken,
            (await sessions.IssueAsync(admin, "Admin")).AccessToken);

        async Task<User> Account(string role)
        {
            var email = "enrollment-" + Guid.NewGuid().ToString("N") + "@example.test";
            var user = new User { UserName = email, Email = email, FirstName = "Enrollment", LastName = role, EmailConfirmed = true, IsActive = true };
            var result = await manager.CreateAsync(user, "EnrollmentHttpTest49Password"); Assert.True(result.Succeeded, string.Join(";", result.Errors.Select(e => e.Description)));
            Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded); return user;
        }
    }
    private sealed record EnrollmentSeed(long EnrollmentId, long ForeignEnrollmentId, long AdminUserId, DateTime PreviousValidityUtc, string RowVersion,
        string PatientToken, string NutritionistToken, string AdminToken);
}

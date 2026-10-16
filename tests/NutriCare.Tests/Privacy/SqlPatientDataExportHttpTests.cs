namespace NutriCare.Tests.Privacy;

using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;
using NutriCare.Tests.Configuration;
using NutriCare.Tests.Integrations;

[Trait("Category", "SqlIntegration")]
public sealed class SqlPatientDataExportHttpTests(IsolatedSqlDatabase database) : IClassFixture<IsolatedSqlDatabase>
{
    [Fact]
    public async Task Own_patient_bundle_contains_historic_files_after_revocation_and_preserves_json_contract_and_audit()
    {
        var storage = new ExportTestStorage(); using var baseHost = new SqlAuthHost(database.ConnectionString);
        using var host = Host(baseHost, storage); var seed = await SeedAsync(host, storage);
        using var client = Client(host, seed.PatientToken);
        var response = await client.GetAsync($"/api/privacy/export/bundle?patientId={seed.ForeignPatientId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains(".zip", response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? "");
        using (var bytes = new MemoryStream(await response.Content.ReadAsByteArrayAsync()))
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Read))
        {
            using var records = await JsonDocument.ParseAsync(zip.GetEntry("records.json")!.Open());
            var data = records.RootElement.GetProperty("data");
            Assert.Equal(seed.PatientUserId, data.GetProperty("user").GetProperty("id").GetInt64());
            Assert.DoesNotContain("FOREIGN_PRIVATE_DATA", data.GetRawText());
            Assert.DoesNotContain("PROVIDER_SECRET", data.GetRawText());
            Assert.Single(data.GetProperty("refunds").EnumerateArray());
            Assert.Single(data.GetProperty("intake").EnumerateArray());
            using var manifest = await JsonDocument.ParseAsync(zip.GetEntry("manifest.json")!.Open());
            Assert.Equal(2, manifest.RootElement.GetProperty("fileCount").GetInt32());
            foreach (var item in manifest.RootElement.GetProperty("files").EnumerateArray())
            {
                await using var stream = zip.GetEntry(item.GetProperty("archivePath").GetString()!)!.Open();
                Assert.Equal(item.GetProperty("sha256").GetString(), Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant());
            }
        }
        var jsonResponse = await client.GetAsync("/api/privacy/export");
        Assert.Equal(HttpStatusCode.OK, jsonResponse.StatusCode);
        var legacy = (await jsonResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        foreach (var key in new[] { "generatedAtUtc", "user", "patient", "appointments", "publishedPlans", "progress", "reports", "consents", "messages", "payments", "feedback" })
            Assert.True(legacy.TryGetProperty(key, out _), key);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.UserId == seed.PatientUserId && a.Description == "GET /api/privacy/export/bundle" && a.IsSuccess && a.StatusCode == 200));
        Assert.All(storage.Downloads, key => Assert.DoesNotContain($"patients/{seed.ForeignPatientId}/", key));
    }

    [Fact]
    public async Task Unauthenticated_and_administrator_bundle_requests_are_rejected_without_reading_private_storage()
    {
        var storage = new ExportTestStorage(); using var baseHost = new SqlAuthHost(database.ConnectionString);
        using var host = Host(baseHost, storage); var seed = await SeedAsync(host, storage);
        using var anonymous = Client(host, null); using var admin = Client(host, seed.AdminToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/privacy/export/bundle")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/privacy/export/bundle")).StatusCode);
        Assert.Empty(storage.Downloads);
    }

    [Fact]
    public async Task Missing_private_file_returns_audited_409_json_without_an_incomplete_archive_or_storage_identifiers()
    {
        var storage = new ExportTestStorage(); using var baseHost = new SqlAuthHost(database.ConnectionString);
        using var host = Host(baseHost, storage); var seed = await SeedAsync(host, storage);
        storage.Files.Remove(seed.OwnReportKey);
        using var client = Client(host, seed.PatientToken);
        var response = await client.GetAsync("/api/privacy/export/bundle");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("assisted export", body); Assert.DoesNotContain(seed.OwnReportKey, body); Assert.DoesNotContain("PROVIDER_SECRET", body);
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.UserId == seed.PatientUserId && a.Description == "GET /api/privacy/export/bundle" && !a.IsSuccess && a.StatusCode == 409));
    }

    private static WebApplicationFactory<Program> Host(SqlAuthHost source, ExportTestStorage storage) => source.WithWebHostBuilder(builder =>
        builder.ConfigureServices(services => { services.RemoveAll<IBlobStorageService>(); services.AddSingleton<IBlobStorageService>(storage); }));
    private static HttpClient Client(WebApplicationFactory<Program> host, string? token)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (token != null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    private static async Task<ExportSeed> SeedAsync(WebApplicationFactory<Program> host, ExportTestStorage storage)
    {
        using var scope = host.Services.CreateScope(); var services = scope.ServiceProvider;
        var manager = services.GetRequiredService<UserManager<User>>();
        var owner = await Account("Patient"); var other = await Account("Patient"); var clinician = await Account("Nutritionist"); var admin = await Account("Admin");
        var db = services.GetRequiredService<NutriCareDbContext>();
        var patient = new Patient { UserId = owner.Id }; var foreign = new Patient { UserId = other.Id };
        var nutritionist = new Nutritionist { UserId = clinician.Id, LicenseNumber = "EXPORT-" + Guid.NewGuid().ToString("N"), IsVerified = true };
        db.AddRange(patient, foreign, nutritionist); await db.SaveChangesAsync();
        db.ClinicalAssignments.Add(new ClinicalAssignment { PatientId = patient.Id, NutritionistId = nutritionist.Id, StartedAt = DateTime.UtcNow.AddDays(-5), EndedAt = DateTime.UtcNow.AddDays(-1) });
        var consent = new ConsentRecord { UserId = owner.Id, Purpose = "HealthData", PolicyVersion = "export-test", ConsentText = "Test historical consent", WithdrawnAt = DateTime.UtcNow.AddDays(-1) };
        db.Add(consent); await db.SaveChangesAsync();
        db.IntakeForms.Add(new IntakeForm { PatientId = patient.Id, ConsentRecordId = consent.Id, Goals = "Historic own intake" });
        var payment = new Payment { PatientId = patient.Id, TransactionId = "export-test-" + Guid.NewGuid().ToString("N"), Amount = 2500, Currency = "PKR", Status = "Completed", GatewayResponse = "PROVIDER_SECRET" };
        db.Add(payment); await db.SaveChangesAsync();
        db.PaymentRefunds.Add(new PaymentRefund { PaymentId = payment.Id, Amount = 500, Currency = "PKR", Status = "Completed", GatewayResponse = "PROVIDER_SECRET" });
        var ownKey = $"patients/{patient.Id}/medical-reports/own.pdf";
        var foreignKey = $"patients/{foreign.Id}/medical-reports/foreign.pdf";
        var reportBytes = Encoding.UTF8.GetBytes("%PDF-private-own-file"); var attachmentBytes = Encoding.UTF8.GetBytes("private-own-attachment");
        db.MedicalReports.AddRange(new MedicalReport { PatientId = patient.Id, UploadedByUserId = owner.Id, OriginalFileName = "own.pdf", ContentType = "application/pdf", BlobContainer = "private", BlobName = ownKey, FileSizeBytes = reportBytes.Length },
            new MedicalReport { PatientId = foreign.Id, UploadedByUserId = other.Id, OriginalFileName = "FOREIGN_PRIVATE_DATA.pdf", ContentType = "application/pdf", BlobContainer = "private", BlobName = foreignKey });
        var chatKey = $"chat/{Math.Min(owner.Id, clinician.Id)}-{Math.Max(owner.Id, clinician.Id)}/own.png";
        db.Messages.Add(new Message { SenderId = clinician.Id, ReceiverId = owner.Id, MessageBody = "Historic message after care ended", AttachmentFileName = "own.png",
            AttachmentContentType = "image/png", AttachmentFileSizeBytes = attachmentBytes.Length, AttachmentBlobContainer = "private", AttachmentBlobName = chatKey });
        db.Messages.Add(new Message { SenderId = clinician.Id, ReceiverId = other.Id, MessageBody = "FOREIGN_PRIVATE_DATA" });
        await db.SaveChangesAsync();
        storage.Files["private/" + ownKey] = reportBytes; storage.Files["private/" + chatKey] = attachmentBytes;
        storage.Files["private/" + foreignKey] = Encoding.UTF8.GetBytes("FOREIGN_PRIVATE_DATA");
        var sessions = services.GetRequiredService<IAuthSessionService>();
        return new(owner.Id, foreign.Id, (await sessions.IssueAsync(owner, "Patient")).AccessToken,
            (await sessions.IssueAsync(admin, "Admin")).AccessToken, "private/" + ownKey);

        async Task<User> Account(string role)
        {
            var email = "export-" + Guid.NewGuid().ToString("N") + "@example.test";
            var user = new User { UserName = email, Email = email, FirstName = "Export", LastName = role, EmailConfirmed = true, IsActive = true };
            var created = await manager.CreateAsync(user, "ExportTestPatient49Password"); Assert.True(created.Succeeded, string.Join(";", created.Errors.Select(e => e.Description)));
            Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded); return user;
        }
    }
    private sealed record ExportSeed(long PatientUserId, long ForeignPatientId, string PatientToken, string AdminToken, string OwnReportKey);
}

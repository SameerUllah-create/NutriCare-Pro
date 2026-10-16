namespace NutriCare.Tests.Privacy;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NutriCare.API.Privacy;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;
using NutriCare.Tests.Integrations;

public sealed class PatientDataExportTests
{
    [Fact]
    public async Task Bundle_contains_owned_retained_files_after_care_revocation_with_hashes_and_no_drafts_or_secrets()
    {
        await using var f = await ExportFixture.CreateAsync();
        var download = await f.Service.CreateBundleAsync(default);
        await using (download.Content)
        {
            Assert.Equal(download.Length, download.Content.Length);
            using var zip = new ZipArchive(download.Content, ZipArchiveMode.Read, leaveOpen: true);
            Assert.Equal(6, zip.Entries.Count);
            Assert.All(zip.Entries, e => { Assert.DoesNotContain("..", e.FullName); Assert.DoesNotContain("\\", e.FullName); Assert.False(e.FullName.StartsWith('/')); });
            using var records = await ReadJson(zip.GetEntry("records.json")!);
            var data = records.RootElement.GetProperty("data");
            Assert.Equal(1, data.GetProperty("user").GetProperty("id").GetInt64());
            Assert.Single(data.GetProperty("reports").EnumerateArray());
            Assert.Single(data.GetProperty("retainedReports").EnumerateArray());
            Assert.Single(data.GetProperty("messages").EnumerateArray());
            Assert.Single(data.GetProperty("retainedMessages").EnumerateArray());
            Assert.Single(data.GetProperty("refunds").EnumerateArray());
            Assert.Single(data.GetProperty("publishedPlans").EnumerateArray());
            Assert.Single(data.GetProperty("publishedPlanHistory").EnumerateArray());
            Assert.Equal("رضامندی", data.GetProperty("consents")[0].GetProperty("consentTextUrdu").GetString());
            var raw = data.GetRawText();
            foreach (var secret in new[] { "NEVER_EXPORT_DRAFT", "NEVER_EXPORT_FOREIGN", "NEVER_EXPORT_PASSWORD", "NEVER_EXPORT_STAMP", "NEVER_EXPORT_PROVIDER", "NEVER_EXPORT_HOST", "NEVER_EXPORT_JOIN", "blobContainer", "attachmentBlobName" })
                Assert.DoesNotContain(secret, raw);
            using var manifest = await ReadJson(zip.GetEntry("manifest.json")!);
            Assert.Equal(4, manifest.RootElement.GetProperty("fileCount").GetInt32());
            foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray())
            {
                var entry = zip.GetEntry(file.GetProperty("archivePath").GetString()!)!;
                await using var source = entry.Open(); using var buffer = new MemoryStream(); await source.CopyToAsync(buffer);
                Assert.Equal(file.GetProperty("sizeBytes").GetInt64(), buffer.Length);
                Assert.Equal(file.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant());
            }
            Assert.Equal(4, f.Storage.Downloads.Count);
            Assert.DoesNotContain(f.Storage.Downloads, x => x.Contains("patients/11/", StringComparison.Ordinal) || x.Contains("chat/2-3/", StringComparison.Ordinal));
        }
        Assert.Empty(f.TemporaryFiles());
    }

    [Theory]
    [InlineData("patients/11/medical-reports/foreign.pdf")]
    [InlineData("patients/10/medical-reports/../foreign.pdf")]
    [InlineData("https://storage.example.test/other.pdf")]
    public async Task Forged_or_remote_blob_references_are_denied_before_any_storage_access(string blob)
    {
        await using var f = await ExportFixture.CreateAsync();
        (await f.Db.MedicalReports.SingleAsync(r => r.Id == 100)).BlobName = blob; await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ConflictException>(() => f.Service.CreateBundleAsync(default));
        Assert.Contains("assisted export", error.Message);
        Assert.Empty(f.Storage.Downloads); Assert.Empty(f.TemporaryFiles());
        using var lease = await f.Gate.AcquireAsync(default);
    }

    [Fact]
    public async Task Missing_file_returns_explicit_failure_and_releases_private_temporary_file_and_capacity()
    {
        await using var f = await ExportFixture.CreateAsync();
        f.Storage.Files.Remove("private/patients/10/medical-reports/report.pdf");
        var error = await Assert.ThrowsAsync<ConflictException>(() => f.Service.CreateBundleAsync(default));
        Assert.Contains("no incomplete archive", error.Message);
        Assert.Empty(f.TemporaryFiles()); using var lease = await f.Gate.AcquireAsync(default);
    }

    [Theory]
    [InlineData("fileCount")]
    [InlineData("recordCount")]
    [InlineData("recordBytes")]
    [InlineData("fileBytes")]
    [InlineData("totalBytes")]
    [InlineData("archiveBytes")]
    public async Task Resource_limits_fail_without_silent_truncation_and_release_capacity(string kind)
    {
        await using var f = await ExportFixture.CreateAsync();
        switch (kind)
        {
            case "fileCount": f.Limits.MaxFiles = 1; break;
            case "recordCount": f.Limits.MaxRecords = 1; break;
            case "recordBytes": f.Limits.MaxRecordBytes = 8; break;
            case "archiveBytes": f.Limits.MaxArchiveBytes = 8; break;
            case "fileBytes": f.Limits.MaxFileBytes = 4; await HideMetadataSizes(f); break;
            case "totalBytes": f.Limits.MaxTotalFileBytes = 8; await HideMetadataSizes(f); break;
        }
        var error = await Assert.ThrowsAsync<ConflictException>(() => f.Service.CreateBundleAsync(default));
        Assert.Contains("limit", error.Message);
        Assert.Empty(f.TemporaryFiles()); using var lease = await f.Gate.AcquireAsync(default);
    }

    [Fact]
    public async Task Download_holds_capacity_until_stream_disposal_and_deletes_the_completed_archive()
    {
        await using var f = await ExportFixture.CreateAsync();
        var first = await f.Service.CreateBundleAsync(default);
        try
        {
            Assert.Single(f.TemporaryFiles());
            await Assert.ThrowsAsync<ConflictException>(() => f.Service.CreateBundleAsync(default));
        }
        finally { await first.Content.DisposeAsync(); }
        Assert.Empty(f.TemporaryFiles());
        var retry = await f.Service.CreateBundleAsync(default); await retry.Content.DisposeAsync();
        Assert.Empty(f.TemporaryFiles());
    }

    [Fact]
    public async Task Cancellation_during_private_file_copy_cleans_up_and_releases_capacity()
    {
        await using var f = await ExportFixture.CreateAsync();
        var blocked = new CancellationReadStream(); f.Storage.StreamOverride = _ => blocked;
        using var cancellation = new CancellationTokenSource();
        var pending = f.Service.CreateBundleAsync(cancellation.Token);
        await blocked.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(blocked.Disposed); Assert.Empty(f.TemporaryFiles()); using var lease = await f.Gate.AcquireAsync(default);
    }

    [Fact]
    public async Task Legacy_external_attachment_references_require_assisted_export_and_nonpatients_cannot_download_a_bundle()
    {
        await using var f = await ExportFixture.CreateAsync();
        (await f.Db.Messages.SingleAsync(m => m.Id == 200)).Attachments = "[\"https://untrusted.example.test/file.pdf\"]";
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.CreateBundleAsync(default)); Assert.Empty(f.Storage.Downloads);
        f.Actor.Role = "Nutritionist";
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => f.Service.CreateBundleAsync(default));
    }

    private static async Task HideMetadataSizes(ExportFixture f)
    {
        foreach (var row in await f.Db.MedicalReports.Where(r => r.PatientId == 10).ToListAsync()) row.FileSizeBytes = 0;
        foreach (var row in await f.Db.Messages.Where(m => m.SenderId == 1 || m.ReceiverId == 1).ToListAsync()) row.AttachmentFileSizeBytes = null;
        await f.Db.SaveChangesAsync();
    }
    private static async Task<JsonDocument> ReadJson(ZipArchiveEntry entry) { await using var input = entry.Open(); return await JsonDocument.ParseAsync(input); }

    private sealed class CancellationReadStream : MemoryStream
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { ReadStarted.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed class ExportFixture : IAsyncDisposable
    {
        public NutriCareDbContext Db { get; } = new(new DbContextOptionsBuilder<NutriCareDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        public ExportActor Actor { get; } = new(1);
        public ExportTestStorage Storage { get; } = new();
        public PatientDataExportOptions Limits { get; } = new() { MaxConcurrentExports = 1 };
        private readonly FakeEnvironment environment = new() { ContentRootPath = Path.Combine(Path.GetTempPath(), "NutriCareExportTest_" + Guid.NewGuid().ToString("N")) };
        public PatientExportResourceGate Gate { get; private set; } = null!;
        public PatientDataExportService Service { get; private set; } = null!;
        public string[] TemporaryFiles() => Directory.Exists(Path.Combine(environment.ContentRootPath, ".local", "private-exports"))
            ? Directory.GetFiles(Path.Combine(environment.ContentRootPath, ".local", "private-exports")) : [];
        public static async Task<ExportFixture> CreateAsync()
        {
            var f = new ExportFixture(); var db = f.Db;
            db.Users.AddRange(new User { Id = 1, Email = "owner@example.test", FirstName = "Owner", LastName = "Patient", PasswordHash = "NEVER_EXPORT_PASSWORD", SecurityStamp = "NEVER_EXPORT_STAMP" },
                new User { Id = 2, Email = "other@example.test", FirstName = "NEVER_EXPORT_FOREIGN" }, new User { Id = 3, Email = "clinician@example.test" });
            db.Patients.AddRange(new Patient { Id = 10, UserId = 1 }, new Patient { Id = 11, UserId = 2 });
            db.Nutritionists.Add(new Nutritionist { Id = 20, UserId = 3 });
            db.ClinicalAssignments.Add(new ClinicalAssignment { PatientId = 10, NutritionistId = 20, StartedAt = DateTime.UtcNow.AddDays(-2), EndedAt = DateTime.UtcNow.AddDays(-1) });
            AddReport(100, 10, null, "report.pdf"); AddReport(110, 10, DateTime.UtcNow.AddDays(-1), "retained.pdf"); AddReport(101, 11, null, "foreign.pdf");
            AddMessage(200, 3, 1, null); AddMessage(201, 1, 3, DateTime.UtcNow.AddDays(-1)); AddMessage(202, 2, 3, null);
            db.ConsentRecords.Add(new ConsentRecord { Id = 300, UserId = 1, Purpose = "HealthData", ConsentText = "Approved text", ConsentTextUrdu = "رضامندی", WithdrawnAt = DateTime.UtcNow.AddDays(-1) });
            db.DietPlans.AddRange(new DietPlan { Id = 400, PatientId = 10, NutritionistId = 20, PublishedVersionId = 401 }, new DietPlan { Id = 500, PatientId = 11, NutritionistId = 20, PublishedVersionId = 501 });
            db.DietPlanVersions.AddRange(new DietPlanVersion { Id = 401, DietPlanId = 400, VersionNumber = 1, SnapshotJson = "{\"title\":\"Published plan\"}", PublishedAt = DateTime.UtcNow.AddDays(-2) },
                new DietPlanVersion { Id = 402, DietPlanId = 400, VersionNumber = 2, SnapshotJson = "NEVER_EXPORT_DRAFT" },
                new DietPlanVersion { Id = 501, DietPlanId = 500, VersionNumber = 1, SnapshotJson = "NEVER_EXPORT_FOREIGN", PublishedAt = DateTime.UtcNow.AddDays(-2) });
            db.Payments.AddRange(new Payment { Id = 600, PatientId = 10, Amount = 2500, GatewayResponse = "NEVER_EXPORT_PROVIDER" }, new Payment { Id = 601, PatientId = 11, Amount = 5000 });
            db.PaymentRefunds.Add(new PaymentRefund { Id = 700, PaymentId = 600, Amount = 1000, GatewayResponse = "NEVER_EXPORT_PROVIDER" });
            db.Appointments.Add(new Appointment { Id = 800, PatientId = 10, NutritionistId = 20, Status = "Completed" });
            db.VideoConsultations.Add(new VideoConsultation { Id = 900, AppointmentId = 800, HostUrl = "NEVER_EXPORT_HOST", JoinToken = "NEVER_EXPORT_JOIN" });
            await db.SaveChangesAsync();
            var options = Options.Create(f.Limits); f.Gate = new(options);
            f.Service = new(db, f.Actor, new Lazy<IBlobStorageService>(() => f.Storage), options, f.Gate, f.environment); return f;

            void AddReport(long id, long patientId, DateTime? deleted, string name)
            {
                var key = $"patients/{patientId}/medical-reports/{name}"; var content = Encoding.UTF8.GetBytes("%PDF-own-report-" + id);
                db.MedicalReports.Add(new MedicalReport { Id = id, PatientId = patientId, OriginalFileName = "../../unsafe/" + name, ContentType = "application/pdf",
                    BlobContainer = "private", BlobName = key, FileSizeBytes = content.Length, DeletedAt = deleted }); f.Storage.Files["private/" + key] = content;
            }
            void AddMessage(long id, long sender, long receiver, DateTime? deleted)
            {
                var key = $"chat/{Math.Min(sender, receiver)}-{Math.Max(sender, receiver)}/{id}.png"; var content = Encoding.UTF8.GetBytes("own-attachment-" + id);
                db.Messages.Add(new Message { Id = id, SenderId = sender, ReceiverId = receiver, MessageBody = sender == 2 ? "NEVER_EXPORT_FOREIGN" : "Own care message",
                    AttachmentFileName = "../../../image.png", AttachmentBlobContainer = "private", AttachmentBlobName = key,
                    AttachmentContentType = "image/png", AttachmentFileSizeBytes = content.Length, DeletedAt = deleted }); f.Storage.Files["private/" + key] = content;
            }
        }
        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            foreach (var path in new[] { Path.Combine(environment.ContentRootPath, ".local", "private-exports"), Path.Combine(environment.ContentRootPath, ".local"), environment.ContentRootPath })
                if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
        }
    }
}

internal sealed class ExportActor(long id) : ICurrentUserService
{
    public long UserId => id;
    public string Email => "owner@example.test";
    public string Role { get; set; } = "Patient";
    public bool IsAuthenticated => true;
    public bool CanAccessClinicalData => false;
}
internal sealed class ExportTestStorage : IBlobStorageService
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public List<string> Downloads { get; } = [];
    public Func<string, Stream>? StreamOverride { get; set; }
    public Task<BlobDownloadResult> DownloadAsync(string containerName, string blobName, string contentType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); var key = containerName + "/" + blobName; Downloads.Add(key);
        if (!Files.TryGetValue(key, out var content)) throw new FileNotFoundException("Synthetic missing private file");
        return Task.FromResult(new BlobDownloadResult(StreamOverride?.Invoke(key) ?? new MemoryStream(content, writable: false), contentType));
    }
    public Task<BlobUploadResult> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

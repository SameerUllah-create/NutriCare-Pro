namespace NutriCare.API.Privacy;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

/// <summary>Data-rights self access. It never grants a clinician access to another person's records.</summary>
public sealed class PatientDataExportService(NutriCareDbContext db, ICurrentUserService current, Lazy<IBlobStorageService> storage,
    IOptions<PatientDataExportOptions> options, PatientExportResourceGate gate, IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly PatientDataExportOptions limits = options.Value;
    private const string Assisted = "A retained file could not be included safely. Request an assisted export; no incomplete archive was returned.";

    public async Task<IReadOnlyDictionary<string, object?>> ReadRecordsAsync(CancellationToken cancellationToken)
        => (await ReadSnapshotAsync(false, cancellationToken)).Records;

    public async Task<PatientExportDownload> CreateBundleAsync(CancellationToken cancellationToken)
    {
        if (!current.IsAuthenticated || current.Role != "Patient")
            throw new ForbiddenAccessException("The downloadable care bundle is available for your own patient account only");
        var lease = await gate.AcquireAsync(cancellationToken);
        FileStream? temporary = null;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(limits.TimeoutSeconds));
            var ct = deadline.Token;
            var snapshot = await ReadSnapshotAsync(true, ct);
            if (snapshot.UnresolvedLegacyAttachments) throw new ConflictException(Assisted);
            if (snapshot.Files.Count > limits.MaxFiles) throw Limit("file count");
            foreach (var file in snapshot.Files) ValidateReference(file);
            if (snapshot.Files.Sum(f => f.ExpectedBytes ?? 0) > limits.MaxTotalFileBytes) throw Limit("total file size");
            var records = JsonSerializer.SerializeToUtf8Bytes(ApiResponse<object>.SuccessResponse(snapshot.Records), Json);
            if (records.LongLength > limits.MaxRecordBytes) throw Limit("record size");

            var directory = Path.Combine(environment.ContentRootPath, ".local", "private-exports");
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
            else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None,
                BufferSize = 64 * 1024, Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose };
            if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            temporary = new FileStream(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".zip"), fileOptions);
            var manifestFiles = new List<object>();
            long totalBytes = 0;
            using (var archive = new ZipArchive(new ArchiveLimitStream(temporary, limits.MaxArchiveBytes), ZipArchiveMode.Create, leaveOpen: true))
            {
                var recordsEntry = archive.CreateEntry("records.json", CompressionLevel.Fastest);
                await using (var output = recordsEntry.Open()) await output.WriteAsync(records, ct);
                foreach (var file in snapshot.Files)
                {
                    ct.ThrowIfCancellationRequested();
                    var path = $"files/{file.Kind}/{file.RecordId}{SafeExtension(file.ContentType)}";
                    BlobDownloadResult downloaded;
                    try { downloaded = await storage.Value.DownloadAsync(file.Container, file.BlobName, file.ContentType, ct); }
                    catch (OperationCanceledException) { throw; }
                    catch { throw new ConflictException(Assisted); }
                    await using var input = downloaded.Content;
                    var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
                    await using var output = entry.Open();
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[64 * 1024];
                    long size = 0;
                    while (true)
                    {
                        int read;
                        try { read = await input.ReadAsync(buffer, ct); }
                        catch (OperationCanceledException) { throw; }
                        catch { throw new ConflictException(Assisted); }
                        if (read == 0) break;
                        size += read; totalBytes += read;
                        if (size > limits.MaxFileBytes || totalBytes > limits.MaxTotalFileBytes) throw Limit("file size");
                        hash.AppendData(buffer, 0, read);
                        await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    }
                    if (file.ExpectedBytes.HasValue && file.ExpectedBytes.Value != size) throw new ConflictException(Assisted);
                    manifestFiles.Add(new { kind = file.Kind, recordId = file.RecordId, originalFileName = DisplayName(file.OriginalName),
                        archivePath = path, contentType = file.ContentType, sizeBytes = size,
                        sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), file.DeletedAt });
                }
                var manifest = archive.CreateEntry("manifest.json", CompressionLevel.Fastest);
                await using var manifestOutput = manifest.Open();
                await JsonSerializer.SerializeAsync(manifestOutput, new { formatVersion = 1, snapshot.GeneratedAtUtc,
                    records = new { archivePath = "records.json", sizeBytes = records.LongLength,
                        sha256 = Convert.ToHexString(SHA256.HashData(records)).ToLowerInvariant() },
                    files = manifestFiles, fileCount = manifestFiles.Count, totalFileBytes = totalBytes,
                    scope = "Own retained patient records and report/message files, including historical files after care/contact revocation. Unpublished plans and provider/security secrets are excluded.",
                    consistency = "Records were read during export preparation; this is not a historical database backup." }, Json, ct);
            }
            await temporary.FlushAsync(ct);
            temporary.Position = 0;
            return new PatientExportDownload(new CompletedPatientExportStream(temporary, lease),
                $"nutricare-patient-data-{snapshot.GeneratedAtUtc:yyyyMMdd-HHmmss}.zip", temporary.Length);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { if (temporary != null) await temporary.DisposeAsync(); }
            finally { lease.Dispose(); }
            throw new ConflictException("Export preparation timed out. Retry or request an assisted export.");
        }
        catch
        {
            try { if (temporary != null) await temporary.DisposeAsync(); }
            finally { lease.Dispose(); }
            throw;
        }
    }

    private async Task<ExportSnapshot> ReadSnapshotAsync(bool requirePatient, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId <= 0) throw new UnauthorizedException("Sign in to export your own account");
        var budget = new RecordBudget(limits);
        var user = await db.Users.AsNoTracking().Where(u => u.Id == current.UserId && u.DeletedAt == null)
            .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName, u.PhoneNumber, u.CreatedAt }).SingleOrDefaultAsync(ct)
            ?? throw new UnauthorizedException("The account is not available");
        budget.Consume(user);
        var patient = await db.Patients.AsNoTracking().Where(p => p.UserId == current.UserId && p.DeletedAt == null)
            .Select(p => new { p.Id, p.DateOfBirth, p.Gender, p.Height, p.CurrentWeight, p.TargetWeight, p.MedicalHistory,
                p.Allergies, p.CurrentMedications, p.PreferredLanguage, p.BloodGroup, p.EmergencyContact,
                p.EmergencyContactPhone, p.Address, p.City, p.State, p.ZipCode, p.Country, p.SelectedMetricsJson }).SingleOrDefaultAsync(ct);
        if (patient != null) budget.Consume(patient);
        if (requirePatient && patient == null) throw new ForbiddenAccessException("A patient profile is required for your care bundle");
        var patientId = patient?.Id ?? 0;
        var ownedAppointments = db.Appointments.AsNoTracking().Where(a => a.PatientId == patientId);
        var ownedPlanIds = db.DietPlans.AsNoTracking().Where(p => p.PatientId == patientId).Select(p => p.Id);
        var visiblePlanIds = db.DietPlans.AsNoTracking().Where(p => p.PatientId == patientId && p.PublishedVersionId != null && p.DeletedAt == null).Select(p => p.PublishedVersionId);
        var reportRows = await ReadAsync(db.MedicalReports.AsNoTracking().Where(r => r.PatientId == patientId).OrderBy(r => r.Id), budget, ct);
        var messageRows = await ReadAsync(db.Messages.AsNoTracking().Where(m => m.SenderId == current.UserId || m.ReceiverId == current.UserId).OrderBy(m => m.Id), budget, ct);
        var paymentIds = db.Payments.AsNoTracking().Where(p => p.PatientId == patientId).Select(p => p.Id);
        var files = new List<PrivateExportFile>();
        foreach (var report in reportRows)
            files.Add(new("reports", report.Id, report.OriginalFileName, report.ContentType, report.FileSizeBytes > 0 ? report.FileSizeBytes : null,
                report.BlobContainer, report.BlobName, $"patients/{patientId}/medical-reports/", report.DeletedAt));
        var unresolved = false;
        foreach (var message in messageRows)
        {
            if (!string.IsNullOrWhiteSpace(message.Attachments) && message.Attachments.Trim() != "[]") unresolved = true;
            if (string.IsNullOrWhiteSpace(message.AttachmentBlobName) && string.IsNullOrWhiteSpace(message.AttachmentBlobContainer) &&
                string.IsNullOrWhiteSpace(message.AttachmentFileName) && !message.AttachmentFileSizeBytes.HasValue) continue;
            files.Add(new("messages", message.Id, message.AttachmentFileName ?? $"attachment-{message.Id}", message.AttachmentContentType ?? "application/octet-stream",
                message.AttachmentFileSizeBytes is > 0 ? message.AttachmentFileSizeBytes : null, message.AttachmentBlobContainer ?? "", message.AttachmentBlobName ?? "",
                $"chat/{Math.Min(message.SenderId, message.ReceiverId)}-{Math.Max(message.SenderId, message.ReceiverId)}/", message.DeletedAt));
        }
        var generatedAt = DateTime.UtcNow;
        var records = new Dictionary<string, object?>
        {
            ["generatedAtUtc"] = generatedAt, ["user"] = user, ["patient"] = patient,
            ["appointments"] = await ReadAsync(ownedAppointments.Where(a => a.DeletedAt == null).OrderBy(a => a.Id)
                .Select(a => new { a.Id, a.AppointmentDateTime, a.Duration, a.Status, a.AppointmentType, a.ConsultationNotes,
                    a.CancellationReason, a.PriceSnapshot, a.Currency, a.CreatedAt, a.UpdatedAt }), budget, ct),
            ["publishedPlans"] = await ReadAsync(db.DietPlanVersions.AsNoTracking().Where(v => visiblePlanIds.Contains(v.Id) && v.PublishedAt != null).OrderBy(v => v.Id)
                .Select(v => new { v.DietPlanId, v.VersionNumber, v.SnapshotJson, v.PublishedAt }), budget, ct),
            ["publishedPlanHistory"] = await ReadAsync(db.DietPlanVersions.AsNoTracking().Where(v => ownedPlanIds.Contains(v.DietPlanId) && v.PublishedAt != null).OrderBy(v => v.Id)
                .Select(v => new { v.DietPlanId, v.VersionNumber, v.SnapshotJson, v.PublishedAt }), budget, ct),
            ["progress"] = await ReadAsync(db.WeightTrackings.AsNoTracking().Where(w => w.PatientId == patientId && w.DeletedAt == null).OrderBy(w => w.Id)
                .Select(w => new { w.Id, w.PatientId, w.RecordDate, w.Weight, w.BMI, w.BMIIsCalculated, w.WaistCircumference,
                    w.ChestCircumference, w.HipCircumference, w.ArmCircumference, w.ThighCircumference, w.BodyFatPercentage,
                    w.MusclePercentage, w.WaterPercentage, w.WaterIntakeLiters, w.DailyCalories, w.ExerciseMinutes, w.ExerciseType,
                    w.BoneMassKg, w.MetabolicRate, w.Notes, w.CreatedAt, w.UpdatedAt, w.DeletedAt, w.RowVersion }), budget, ct),
            ["reports"] = reportRows.Where(r => r.DeletedAt == null).Select(ReportRecord).ToArray(),
            ["retainedReports"] = reportRows.Where(r => r.DeletedAt != null).Select(ReportRecord).ToArray(),
            ["messages"] = messageRows.Where(m => m.DeletedAt == null).Select(MessageRecord).ToArray(),
            ["retainedMessages"] = messageRows.Where(m => m.DeletedAt != null).Select(MessageRecord).ToArray(),
            ["consents"] = await ReadAsync(db.ConsentRecords.AsNoTracking().Where(c => c.UserId == current.UserId).OrderBy(c => c.Id)
                .Select(c => new { c.Id, c.UserId, c.Purpose, c.PolicyVersion, c.PolicyTitle, c.ConsentText, c.ConsentTextUrdu,
                    c.PresentedLanguage, c.GrantedAt, c.WithdrawnAt, c.IPAddress, c.CreatedAt, c.UpdatedAt, c.DeletedAt, c.RowVersion }), budget, ct),
            ["payments"] = await ReadAsync(db.Payments.AsNoTracking().Where(p => p.PatientId == patientId && p.DeletedAt == null).OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.Amount, p.Currency, p.Status, p.CreatedAt, p.PaidAt, p.RefundedAmount, p.RefundedAt }), budget, ct),
            ["refunds"] = await ReadAsync(db.PaymentRefunds.AsNoTracking().Where(r => paymentIds.Contains(r.PaymentId)).OrderBy(r => r.Id)
                .Select(r => new { r.Id, r.PaymentId, r.Amount, r.Currency, r.Status, r.Reason, r.RefundedAt, r.CreatedAt, r.UpdatedAt, r.DeletedAt }), budget, ct),
            ["invoices"] = await ReadAsync(db.Invoices.AsNoTracking().Where(i => i.PatientId == patientId).OrderBy(i => i.Id)
                .Select(i => new { i.Id, i.InvoiceNumber, i.Subtotal, i.TaxAmount, i.DiscountAmount, i.TotalAmount, i.Currency,
                    i.Status, i.IssuedAt, i.DueAt, i.PaidAt, i.Description, i.CreatedAt, i.UpdatedAt, i.DeletedAt }), budget, ct),
            ["feedback"] = await ReadAsync(db.PatientFeedbacks.AsNoTracking().Where(f => f.PatientId == patientId).OrderBy(f => f.Id)
                .Select(f => new { f.Id, f.PatientId, f.AppointmentId, f.Rating, f.Content, f.Status, f.PublicationConsent,
                    f.ConsentedAt, f.WithdrawnAt, f.CreatedAt, f.UpdatedAt, f.DeletedAt }), budget, ct),
            ["intake"] = await ReadAsync(db.IntakeForms.AsNoTracking().Where(i => i.PatientId == patientId).OrderBy(i => i.Id)
                .Select(i => new { i.Id, i.Goals, i.MedicalHistory, i.Allergies, i.CurrentMedications, i.Lifestyle,
                    i.ConsentRecordId, i.SubmittedAt, i.Version, i.CreatedAt, i.UpdatedAt, i.DeletedAt }), budget, ct),
            ["appointmentHistory"] = await ReadAsync(db.AppointmentHistories.AsNoTracking().Where(h => ownedAppointments.Select(a => a.Id).Contains(h.AppointmentId)).OrderBy(h => h.Id)
                .Select(h => new { h.Id, h.AppointmentId, h.FromStatus, h.ToStatus, h.PreviousStartAt, h.NewStartAt, h.ChangedByUserId, h.Reason, h.CreatedAt }), budget, ct),
            ["consultationNotes"] = await ReadAsync(db.ConsultationNotes.AsNoTracking().Where(n => ownedAppointments.Select(a => a.Id).Contains(n.AppointmentId)).OrderBy(n => n.Id)
                .Select(n => new { n.Id, n.AppointmentId, n.AuthorUserId, n.Content, n.SupersedesId, n.CreatedAt, n.UpdatedAt }), budget, ct),
            ["reportNotes"] = await ReadAsync(db.MedicalReportNotes.AsNoTracking().Where(n => db.MedicalReports.Where(r => r.PatientId == patientId).Select(r => r.Id).Contains(n.MedicalReportId)).OrderBy(n => n.Id)
                .Select(n => new { n.Id, n.MedicalReportId, n.AuthorUserId, n.Content, n.SupersedesId, n.CreatedAt, n.UpdatedAt }), budget, ct),
            ["progressHistory"] = await ReadAsync(db.ProgressRevisions.AsNoTracking().Where(r => db.WeightTrackings.Where(w => w.PatientId == patientId).Select(w => w.Id).Contains(r.WeightTrackingId)).OrderBy(r => r.Id)
                .Select(r => new { r.Id, r.WeightTrackingId, r.SnapshotJson, r.ChangedByUserId, r.Reason, r.CreatedAt }), budget, ct),
            ["adherence"] = await ReadAsync(db.DietPlanCompliances.AsNoTracking().Where(c => c.PatientId == patientId).OrderBy(c => c.Id)
                .Select(c => new { c.Id, c.DietPlanId, c.RecordDate, c.MealsCompleted, c.TotalMeals, c.WaterIntakeLiters,
                    c.TookSupplements, c.Notes, c.CreatedAt, c.UpdatedAt, c.DeletedAt }), budget, ct),
            ["dataRightsRequests"] = await ReadAsync(db.DataRightsRequests.AsNoTracking().Where(r => r.UserId == current.UserId).OrderBy(r => r.Id)
                .Select(r => new { r.Id, r.RequestType, r.Status, r.Reason, r.CompletedAt, r.CreatedAt, r.UpdatedAt }), budget, ct)
        };
        if (JsonSerializer.SerializeToUtf8Bytes(records, Json).LongLength > limits.MaxRecordBytes) throw Limit("record size");
        return new(generatedAt, records, files, unresolved);
    }

    private static object ReportRecord(MedicalReport r) => new { r.Id, r.OriginalFileName, r.ReportType, r.UploadedAt,
        downloadUrl = "/api/medicalreports/" + r.Id + "/download", r.ContentType, r.FileSizeBytes, r.PatientNotes,
        r.NutritionistNotes, r.ReviewedAt, r.Status, r.CreatedAt, r.UpdatedAt, r.DeletedAt };
    private static object MessageRecord(Message m) => new { m.Id, m.SenderId, m.ReceiverId, m.MessageBody, m.CreatedAt, m.IsRead,
        m.ReadAt, m.Subject, m.MessageType, m.AttachmentFileName, m.AttachmentContentType, m.AttachmentFileSizeBytes,
        attachmentDownloadUrl = string.IsNullOrWhiteSpace(m.AttachmentBlobName) ? null : "/api/chat/messages/" + m.Id + "/attachment", m.UpdatedAt, m.DeletedAt };

    private void ValidateReference(PrivateExportFile file)
    {
        if (string.IsNullOrWhiteSpace(file.Container) || file.Container.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) ||
            string.IsNullOrWhiteSpace(file.BlobName) || !file.BlobName.StartsWith(file.ExpectedPrefix, StringComparison.Ordinal) ||
            file.BlobName.Any(c => char.IsControl(c) || c is '\\' or ':' or '?' or '#' or '%') || file.BlobName.Split('/').Any(s => s is "." or ".."))
            throw new ConflictException(Assisted);
        if (file.ExpectedBytes > limits.MaxFileBytes) throw Limit("file size");
    }

    private static string SafeExtension(string contentType) => contentType.ToLowerInvariant() switch
    { "application/pdf" => ".pdf", "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", _ => ".bin" };
    private static string DisplayName(string name)
    {
        var result = Path.GetFileName(name.Replace('\\', '/'));
        return new string(result.Where(c => !char.IsControl(c)).Take(255).ToArray());
    }
    private static ConflictException Limit(string kind) => new($"The export exceeds the {kind} limit. Request an assisted export; no records or files were silently omitted.");
    private static async Task<List<T>> ReadAsync<T>(IQueryable<T> query, RecordBudget budget, CancellationToken ct)
    {
        var result = new List<T>();
        await foreach (var row in query.AsAsyncEnumerable().WithCancellation(ct)) { budget.Consume(row); result.Add(row); }
        return result;
    }
    private sealed class RecordBudget(PatientDataExportOptions limits)
    {
        private int count;
        private long bytes;
        public void Consume<T>(T value)
        {
            if (++count > limits.MaxRecords) throw Limit("record count");
            bytes += JsonSerializer.SerializeToUtf8Bytes(value, Json).LongLength;
            if (bytes > limits.MaxRecordBytes) throw Limit("record size");
        }
    }
    private sealed record PrivateExportFile(string Kind, long RecordId, string OriginalName, string ContentType, long? ExpectedBytes,
        string Container, string BlobName, string ExpectedPrefix, DateTime? DeletedAt);
    private sealed record ExportSnapshot(DateTime GeneratedAtUtc, IReadOnlyDictionary<string, object?> Records,
        IReadOnlyCollection<PrivateExportFile> Files, bool UnresolvedLegacyAttachments);
}

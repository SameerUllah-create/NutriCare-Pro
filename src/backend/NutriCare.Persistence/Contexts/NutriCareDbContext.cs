namespace NutriCare.Persistence.Contexts;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NutriCare.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

/// <summary>
/// NutriCare database context
/// </summary>
public class NutriCareDbContext : IdentityDbContext<User, Role, long>
{
    public NutriCareDbContext(DbContextOptions<NutriCareDbContext> options) : base(options) { }

    public DbSet<Patient> Patients { get; set; } = null!;
    public DbSet<Nutritionist> Nutritionists { get; set; } = null!;
    public DbSet<Appointment> Appointments { get; set; } = null!;
    public DbSet<VideoConsultation> VideoConsultations { get; set; } = null!;
    public DbSet<DietPlan> DietPlans { get; set; } = null!;
    public DbSet<DietPlanItem> DietPlanItems { get; set; } = null!;
    public DbSet<DietPlanCompliance> DietPlanCompliances { get; set; } = null!;
    public DbSet<WeightTracking> WeightTrackings { get; set; } = null!;
    public DbSet<MedicalReport> MedicalReports { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<Invoice> Invoices { get; set; } = null!;
    public DbSet<PaymentRefund> PaymentRefunds { get; set; } = null!;
    public DbSet<CancellationRefundInstruction> CancellationRefundInstructions => Set<CancellationRefundInstruction>();
    public DbSet<PackageEnrollment> PackageEnrollments => Set<PackageEnrollment>();
    public DbSet<PackageCreditUsage> PackageCreditUsages => Set<PackageCreditUsage>();
    public DbSet<PackageCreditHistory> PackageCreditHistory => Set<PackageCreditHistory>();
    public DbSet<Message> Messages { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<Blog> Blogs { get; set; } = null!;
    public DbSet<Review> Reviews { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
    public DbSet<AvailabilityRule> AvailabilityRules => Set<AvailabilityRule>();
    public DbSet<AvailabilityException> AvailabilityExceptions => Set<AvailabilityException>();
    public DbSet<CancellationPolicy> CancellationPolicies => Set<CancellationPolicy>();
    public DbSet<ClinicalAssignment> ClinicalAssignments => Set<ClinicalAssignment>();
    public DbSet<AppointmentHistory> AppointmentHistories => Set<AppointmentHistory>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<PolicyDocument> PolicyDocuments => Set<PolicyDocument>();
    public DbSet<IntakeForm> IntakeForms => Set<IntakeForm>();
    public DbSet<DietPlanVersion> DietPlanVersions => Set<DietPlanVersion>();
    public DbSet<ConsultationNote> ConsultationNotes => Set<ConsultationNote>();
    public DbSet<MedicalReportNote> MedicalReportNotes => Set<MedicalReportNote>();
    public DbSet<ProgressRevision> ProgressRevisions => Set<ProgressRevision>();
    public DbSet<ConfigurationRevision> ConfigurationRevisions => Set<ConfigurationRevision>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<ProviderWebhookEvent> ProviderWebhookEvents => Set<ProviderWebhookEvent>();
    public DbSet<DataRightsRequest> DataRightsRequests => Set<DataRightsRequest>();
    public DbSet<PublicContent> PublicContents => Set<PublicContent>();
    public DbSet<ContactInquiry> ContactInquiries => Set<ContactInquiry>();
    public DbSet<PatientFeedback> PatientFeedbacks => Set<PatientFeedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Explicit mappings keep getters out of SQL and make clinical updates concurrency-safe.
        foreach (var entity in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entity.ClrType))
            {
                modelBuilder.Entity(entity.ClrType).Ignore(nameof(BaseEntity.IsDeleted));
                modelBuilder.Entity(entity.ClrType).Property(nameof(BaseEntity.RowVersion)).IsRowVersion();
            }
        }
        modelBuilder.Entity<User>().Ignore(u => u.IsDeleted);
        modelBuilder.Entity<Role>().Ignore(r => r.IsDeleted);
        modelBuilder.Entity<AuthSession>().HasIndex(s => s.SessionId).IsUnique();
        modelBuilder.Entity<AuthSession>().HasIndex(s => s.RefreshTokenHash).IsUnique();
        modelBuilder.Entity<AuthSession>().HasIndex(s => new { s.UserId, s.RevokedAt, s.ExpiresAt });
        modelBuilder.Entity<ProviderWebhookEvent>().HasIndex(e => new { e.Provider, e.EventId }).IsUnique();
        modelBuilder.Entity<Message>().Property(m => m.ClientMessageId).HasMaxLength(128);
        modelBuilder.Entity<Message>().HasIndex(m => new { m.SenderId, m.ClientMessageId }).IsUnique().HasFilter("[ClientMessageId] IS NOT NULL");
        modelBuilder.Entity<Payment>().HasIndex(p => new { p.PatientId, p.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        modelBuilder.Entity<PaymentRefund>().HasIndex(p => new { p.PaymentId, p.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        modelBuilder.Entity<PaymentRefund>().Property(r => r.ProcessingToken).HasMaxLength(64);
        modelBuilder.Entity<PaymentRefund>().Property(r => r.LastErrorCode).HasMaxLength(128);
        var cancellationRefund = modelBuilder.Entity<CancellationRefundInstruction>();
        cancellationRefund.HasIndex(r => r.AppointmentId).IsUnique();
        cancellationRefund.HasIndex(r => new { r.Status, r.NextAttemptAt });
        cancellationRefund.Property(r => r.CancellationActor).HasMaxLength(32);
        cancellationRefund.Property(r => r.PolicySnapshotHash).HasMaxLength(64);
        cancellationRefund.Property(r => r.DecisionReason).HasMaxLength(128);
        cancellationRefund.Property(r => r.Currency).HasMaxLength(3);
        cancellationRefund.Property(r => r.Status).HasMaxLength(32);
        cancellationRefund.Property(r => r.LeaseToken).HasMaxLength(64);
        cancellationRefund.Property(r => r.LastErrorCode).HasMaxLength(128);
        cancellationRefund.HasOne<Appointment>().WithMany().HasForeignKey(r => r.AppointmentId);
        cancellationRefund.HasOne<User>().WithMany().HasForeignKey(r => r.CancelledByUserId);
        cancellationRefund.HasOne<CancellationPolicy>().WithMany().HasForeignKey(r => r.PolicyId);
        cancellationRefund.HasOne<Payment>().WithMany().HasForeignKey(r => r.PaymentId);
        cancellationRefund.HasOne<PaymentRefund>().WithMany().HasForeignKey(r => r.PaymentRefundId);
        modelBuilder.Entity<Notification>().HasIndex(n => new { n.UserId, n.Channel, n.EventKey }).IsUnique().HasFilter("[EventKey] IS NOT NULL");
        modelBuilder.Entity<Appointment>().HasIndex(a => new { a.PatientId, a.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        modelBuilder.Entity<ClinicalAssignment>().HasIndex(a => new { a.PatientId, a.NutritionistId, a.EndedAt });
        modelBuilder.Entity<DietPlanVersion>().HasIndex(v => new { v.DietPlanId, v.VersionNumber }).IsUnique();
        modelBuilder.Entity<PolicyDocument>().HasIndex(p => new { p.Purpose, p.Version }).IsUnique();
        modelBuilder.Entity<AvailabilityRule>().HasIndex(r => new { r.NutritionistId, r.DayOfWeek, r.IsPublished });
        modelBuilder.Entity<AvailabilityException>().HasIndex(r => new { r.NutritionistId, r.StartAtUtc, r.EndAtUtc });
        modelBuilder.Entity<ConfigurationRevision>().HasIndex(r => new { r.EntityType, r.EntityId, r.Version });
        modelBuilder.Entity<Service>().Property(s => s.PackageServiceCode).HasMaxLength(32);
        modelBuilder.Entity<ServicePackage>().Property(p => p.EligibleServiceCodes)
            .HasConversion(v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>())
            .HasDefaultValueSql("'[]'")
            .Metadata.SetValueComparer(new ValueComparer<string[]>((a, b) => a != null && b != null && a.SequenceEqual(b), v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)), v => v.ToArray()));
        modelBuilder.Entity<ServicePackage>().HasOne<User>().WithMany().HasForeignKey(p => p.NutritionistApprovedById);
        modelBuilder.Entity<CancellationPolicy>().Property(p => p.PackageNoShowCreditAction).HasMaxLength(32).HasDefaultValue("ReviewRequired");
        modelBuilder.Entity<CancellationPolicy>().Property(p => p.PackagePatientCancellationCreditAction).HasMaxLength(32).HasDefaultValue("ReviewRequired");
        var enrollment = modelBuilder.Entity<PackageEnrollment>();
        enrollment.HasIndex(e => e.PaymentId).IsUnique();
        enrollment.HasIndex(e => new { e.PatientId, e.Status });
        enrollment.Property(e => e.Status).HasMaxLength(32);
        enrollment.HasOne<ServicePackage>().WithMany().HasForeignKey(e => e.ServicePackageId);
        enrollment.HasOne<Patient>().WithMany().HasForeignKey(e => e.PatientId);
        enrollment.HasOne<Payment>().WithMany().HasForeignKey(e => e.PaymentId);
        modelBuilder.Entity<Appointment>().HasOne<PackageEnrollment>().WithMany().HasForeignKey(a => a.PackageEnrollmentId);
        modelBuilder.Entity<Appointment>().Property(a => a.PackageCreditKind).HasMaxLength(32);
        var credit = modelBuilder.Entity<PackageCreditUsage>();
        credit.HasIndex(u => new { u.ResourceType, u.ResourceId }).IsUnique();
        credit.HasIndex(u => new { u.PackageEnrollmentId, u.CreditKind, u.Status });
        credit.Property(u => u.ResourceType).HasMaxLength(32);
        credit.Property(u => u.CreditKind).HasMaxLength(32);
        credit.Property(u => u.Status).HasMaxLength(32);
        credit.Property(u => u.ServiceCode).HasMaxLength(32);
        credit.HasOne<PackageEnrollment>().WithMany().HasForeignKey(u => u.PackageEnrollmentId);
        var creditHistory = modelBuilder.Entity<PackageCreditHistory>();
        creditHistory.HasIndex(h => new { h.PackageEnrollmentId, h.CreatedAt });
        creditHistory.Property(h => h.Action).HasMaxLength(64);
        creditHistory.Property(h => h.Reason).HasMaxLength(1000);
        creditHistory.HasOne<PackageEnrollment>().WithMany().HasForeignKey(h => h.PackageEnrollmentId);
        creditHistory.HasOne<PackageCreditUsage>().WithMany().HasForeignKey(h => h.PackageCreditUsageId);
        creditHistory.HasOne<User>().WithMany().HasForeignKey(h => h.ActorUserId);
        modelBuilder.Entity<Service>().HasOne<Nutritionist>().WithMany().HasForeignKey(s=>s.NutritionistId);
        modelBuilder.Entity<Appointment>().HasOne<Service>().WithMany().HasForeignKey(a=>a.ServiceId);
        modelBuilder.Entity<Appointment>().HasOne<IntakeForm>().WithMany().HasForeignKey(a=>a.IntakeFormId);
        modelBuilder.Entity<Appointment>().HasOne<CancellationPolicy>().WithMany().HasForeignKey(a=>a.CancellationPolicyId);
        modelBuilder.Entity<ClinicalAssignment>().HasOne<Patient>().WithMany().HasForeignKey(a=>a.PatientId);
        modelBuilder.Entity<ClinicalAssignment>().HasOne<Nutritionist>().WithMany().HasForeignKey(a=>a.NutritionistId);
        modelBuilder.Entity<AppointmentHistory>().HasOne<Appointment>().WithMany().HasForeignKey(h=>h.AppointmentId);
        modelBuilder.Entity<ConsentRecord>().HasOne<User>().WithMany().HasForeignKey(c=>c.UserId);
        modelBuilder.Entity<IntakeForm>().HasOne<Patient>().WithMany().HasForeignKey(i=>i.PatientId);
        modelBuilder.Entity<IntakeForm>().HasOne<ConsentRecord>().WithMany().HasForeignKey(i=>i.ConsentRecordId);
        modelBuilder.Entity<DietPlanVersion>().HasOne<DietPlan>().WithMany().HasForeignKey(v=>v.DietPlanId);
        modelBuilder.Entity<ConsultationNote>().HasOne<Appointment>().WithMany().HasForeignKey(n=>n.AppointmentId);
        modelBuilder.Entity<MedicalReportNote>().HasOne<MedicalReport>().WithMany().HasForeignKey(n=>n.MedicalReportId);
        modelBuilder.Entity<ProgressRevision>().HasOne<WeightTracking>().WithMany().HasForeignKey(r=>r.WeightTrackingId);
        modelBuilder.Entity<AuthSession>().HasOne<User>().WithMany().HasForeignKey(s=>s.UserId);
        modelBuilder.Entity<ProviderWebhookEvent>().HasOne<Payment>().WithMany().HasForeignKey(e=>e.PaymentId);
        modelBuilder.Entity<DataRightsRequest>().HasOne<User>().WithMany().HasForeignKey(r=>r.UserId);
        modelBuilder.Entity<PatientFeedback>().HasOne<Patient>().WithMany().HasForeignKey(f=>f.PatientId);
        modelBuilder.Entity<PatientFeedback>().HasOne<Appointment>().WithMany().HasForeignKey(f=>f.AppointmentId);
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
        {
            // SQL datetime2 has no Kind; restore UTC on instants so JSON always carries an explicit offset.
            if ((property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?)) &&
                property.Name is not ("DateOfBirth" or "RecordDate" or "StartDate" or "EndDate" or "ReviewDate"))
                property.SetValueConverter(new ValueConverter<DateTime,DateTime>(v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v,DateTimeKind.Utc), v => DateTime.SpecifyKind(v,DateTimeKind.Utc)));
            if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
            {
                property.SetPrecision(18);
                property.SetScale(4);
            }
            if (property.Name is "IdempotencyKey" or "EventKey" or "EventId" or "SessionId" or "RefreshTokenHash") property.SetMaxLength(128);
            if (property.Name is "Provider" or "Channel" or "Purpose") property.SetMaxLength(64);
        }

        modelBuilder.Entity<User>().ToTable("Users");
        modelBuilder.Entity<Role>().ToTable("Roles");
        modelBuilder.Entity<IdentityUserRole<long>>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserClaim<long>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<long>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityRoleClaim<long>>().ToTable("RoleClaims");
        modelBuilder.Entity<IdentityUserToken<long>>().ToTable("UserTokens");

        // User configuration
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        modelBuilder.Entity<User>()
            .Property(u => u.IsActive)
            .HasDefaultValue(true);
        modelBuilder.Entity<User>()
            .Property(u => u.FirstName)
            .HasMaxLength(100)
            .IsRequired();
        modelBuilder.Entity<User>()
            .Property(u => u.LastName)
            .HasMaxLength(100)
            .IsRequired();

        modelBuilder.Entity<Role>().HasData(
            new Role("Admin")
            {
                Id = 1,
                NormalizedName = "ADMIN",
                ConcurrencyStamp = "role-admin-v1",
                CreatedAt = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                Description = "Full platform administration"
            },
            new Role("Nutritionist")
            {
                Id = 2,
                NormalizedName = "NUTRITIONIST",
                ConcurrencyStamp = "role-nutritionist-v1",
                CreatedAt = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                Description = "Nutrition consultation provider"
            },
            new Role("Patient")
            {
                Id = 3,
                NormalizedName = "PATIENT",
                ConcurrencyStamp = "role-patient-v1",
                CreatedAt = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                Description = "Patient portal user"
            });

        // Patient configuration
        modelBuilder.Entity<Patient>()
            .HasKey(p => p.Id);
        modelBuilder.Entity<Patient>()
            .HasOne(p => p.User)
            .WithOne(u => u.Patient)
            .HasForeignKey<Patient>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Patient>()
            .HasMany(p => p.Appointments)
            .WithOne(a => a.Patient)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Patient>()
            .HasMany(p => p.DietPlans)
            .WithOne(d => d.Patient)
            .HasForeignKey(d => d.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Patient>()
            .HasMany(p => p.WeightTrackings)
            .WithOne(w => w.Patient)
            .HasForeignKey(w => w.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Patient>()
            .HasMany(p => p.MedicalReports)
            .WithOne(r => r.Patient)
            .HasForeignKey(r => r.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Nutritionist configuration
        modelBuilder.Entity<Nutritionist>()
            .HasKey(n => n.Id);
        modelBuilder.Entity<Nutritionist>()
            .HasOne(n => n.User)
            .WithOne(u => u.Nutritionist)
            .HasForeignKey<Nutritionist>(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Nutritionist>()
            .HasIndex(n => n.LicenseNumber)
            .IsUnique();
        modelBuilder.Entity<Nutritionist>()
            .HasMany(n => n.Appointments)
            .WithOne(a => a.Nutritionist)
            .HasForeignKey(a => a.NutritionistId)
            .OnDelete(DeleteBehavior.Restrict);

        // Appointment configuration
        modelBuilder.Entity<Appointment>()
            .HasKey(a => a.Id);
        modelBuilder.Entity<Appointment>()
            .HasIndex(a => new { a.PatientId, a.AppointmentDateTime });
        modelBuilder.Entity<Appointment>()
            .HasIndex(a => new { a.NutritionistId, a.AppointmentDateTime });
        modelBuilder.Entity<Appointment>()
            .HasIndex(a => a.Status);
        modelBuilder.Entity<Appointment>()
            .Property(a => a.Status)
            .HasMaxLength(50)
            .IsRequired();
        modelBuilder.Entity<Appointment>()
            .Property(a => a.AppointmentType)
            .HasMaxLength(50)
            .IsRequired();
        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.VideoConsultation)
            .WithOne(v => v.Appointment)
            .HasForeignKey<VideoConsultation>(v => v.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VideoConsultation>()
            .HasKey(v => v.Id);
        modelBuilder.Entity<VideoConsultation>()
            .HasIndex(v => v.AppointmentId)
            .IsUnique();
        modelBuilder.Entity<VideoConsultation>()
            .HasIndex(v => new { v.Status, v.ScheduledStartTime });
        modelBuilder.Entity<VideoConsultation>()
            .Property(v => v.Provider)
            .HasMaxLength(50)
            .IsRequired();
        modelBuilder.Entity<VideoConsultation>()
            .Property(v => v.Status)
            .HasMaxLength(50)
            .IsRequired();

        // DietPlan configuration
        modelBuilder.Entity<DietPlan>()
            .HasKey(d => d.Id);
        modelBuilder.Entity<DietPlan>()
            .HasMany(d => d.Items)
            .WithOne(i => i.DietPlan)
            .HasForeignKey(i => i.DietPlanId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DietPlan>()
            .HasIndex(d => new { d.PatientId, d.Status });
        modelBuilder.Entity<DietPlan>()
            .HasIndex(d => d.NutritionistId);
        modelBuilder.Entity<DietPlan>()
            .HasMany(d => d.ComplianceRecords)
            .WithOne(c => c.DietPlan)
            .HasForeignKey(c => c.DietPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DietPlanItem>()
            .HasKey(i => i.Id);
        modelBuilder.Entity<DietPlanItem>()
            .HasIndex(i => new { i.DietPlanId, i.DayNumber, i.MealType });

        modelBuilder.Entity<DietPlanCompliance>()
            .HasKey(c => c.Id);
        modelBuilder.Entity<DietPlanCompliance>()
            .HasOne(c => c.Patient)
            .WithMany()
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DietPlanCompliance>()
            .HasIndex(c => new { c.DietPlanId, c.RecordDate })
            .IsUnique();

        // Weight tracking configuration
        modelBuilder.Entity<WeightTracking>()
            .HasKey(w => w.Id);
        modelBuilder.Entity<WeightTracking>()
            .HasIndex(w => new { w.PatientId, w.RecordDate })
            .IsUnique();
        modelBuilder.Entity<WeightTracking>()
            .HasIndex(w => w.RecordDate);

        // Medical report configuration
        modelBuilder.Entity<MedicalReport>()
            .HasKey(r => r.Id);
        modelBuilder.Entity<MedicalReport>()
            .HasIndex(r => new { r.PatientId, r.UploadedAt });
        modelBuilder.Entity<MedicalReport>()
            .HasIndex(r => r.ReportType);
        modelBuilder.Entity<MedicalReport>()
            .HasIndex(r => r.BlobName)
            .IsUnique();
        modelBuilder.Entity<MedicalReport>()
            .Property(r => r.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();
        modelBuilder.Entity<MedicalReport>()
            .Property(r => r.StoredFileName)
            .HasMaxLength(255)
            .IsRequired();
        modelBuilder.Entity<MedicalReport>()
            .Property(r => r.ContentType)
            .HasMaxLength(100)
            .IsRequired();
        modelBuilder.Entity<MedicalReport>()
            .Property(r => r.FileExtension)
            .HasMaxLength(20)
            .IsRequired();
        modelBuilder.Entity<MedicalReport>()
            .Property(r => r.ReportType)
            .HasMaxLength(50)
            .IsRequired();
        modelBuilder.Entity<MedicalReport>()
            .Property(r => r.Status)
            .HasMaxLength(50)
            .IsRequired();

        // Payment configuration
        modelBuilder.Entity<Payment>()
            .HasKey(p => p.Id);
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.TransactionId)
            .IsUnique();
        modelBuilder.Entity<Payment>()
            .HasIndex(p => new { p.PatientId, p.CreatedAt });
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.Status);
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Patient)
            .WithMany()
            .HasForeignKey(p => p.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Nutritionist)
            .WithMany()
            .HasForeignKey(p => p.NutritionistId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Invoice>()
            .HasKey(i => i.Id);
        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.InvoiceNumber)
            .IsUnique();
        modelBuilder.Entity<Invoice>()
            .HasIndex(i => new { i.PatientId, i.IssuedAt });
        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Patient)
            .WithMany()
            .HasForeignKey(i => i.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Nutritionist)
            .WithMany()
            .HasForeignKey(i => i.NutritionistId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PaymentRefund>()
            .HasKey(r => r.Id);
        modelBuilder.Entity<PaymentRefund>()
            .HasIndex(r => r.PaymentId);
        modelBuilder.Entity<PaymentRefund>()
            .HasOne(r => r.Payment)
            .WithMany(p => p.Refunds)
            .HasForeignKey(r => r.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Message configuration
        modelBuilder.Entity<Message>()
            .HasKey(m => m.Id);
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.ConversationId, m.CreatedAt });
        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.ReceiverId, m.IsRead });
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Message>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        // Review configuration
        modelBuilder.Entity<Review>()
            .HasKey(r => r.Id);
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Nutritionist)
            .WithMany()
            .HasForeignKey(r => r.NutritionistId)
            .OnDelete(DeleteBehavior.Restrict);

        // Notification configuration
        modelBuilder.Entity<Notification>()
            .HasKey(n => n.Id);
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Blog configuration
        modelBuilder.Entity<Blog>()
            .HasKey(b => b.Id);
        modelBuilder.Entity<Blog>()
            .HasIndex(b => b.Slug)
            .IsUnique();
        modelBuilder.Entity<Blog>()
            .HasIndex(b => new { b.Status, b.PublishedAt });
        modelBuilder.Entity<Blog>()
            .HasOne(b => b.Author)
            .WithMany()
            .HasForeignKey(b => b.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Blog>()
            .Property(b => b.Title)
            .HasMaxLength(200)
            .IsRequired();
        modelBuilder.Entity<Blog>()
            .Property(b => b.Slug)
            .HasMaxLength(220)
            .IsRequired();
        modelBuilder.Entity<Blog>()
            .Property(b => b.Status)
            .HasMaxLength(50)
            .IsRequired();

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>()
            .HasKey(a => a.Id);

        // Clinical/financial records are retained deliberately, never deleted through cascade paths.
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<User>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Role>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

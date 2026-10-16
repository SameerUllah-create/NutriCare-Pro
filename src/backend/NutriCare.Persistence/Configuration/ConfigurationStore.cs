namespace NutriCare.Persistence.Configuration;

using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

public sealed class ConfigurationStore(NutriCareDbContext db, ICurrentUserService current, IAppointmentReservationLock bookingLock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    { "Id","CreatedAt","UpdatedAt","DeletedAt","RowVersion","IsPublished","IsActive","ApprovedByUserId","ApprovedAt","Status","ExpectedVersion","NutritionistId","Version",
      "NutritionistApprovedById","NutritionistApprovedAt","NutritionistApprovedVersion" };
    public static Type EntityType(string kind) => kind switch
    {
        "services" => typeof(Service), "packages" => typeof(ServicePackage), "availability" => typeof(AvailabilityRule),
        "exceptions" => typeof(AvailabilityException), "cancellation-policies" => typeof(CancellationPolicy), "policies" => typeof(PolicyDocument),
        _ => throw new ArgumentException("Unknown configuration kind")
    };

    public async Task<BaseEntity> SaveAsync(string kind, long? id, JsonElement input, CancellationToken ct = default)
    {
        EnsureStaff();
        var entity = id.HasValue ? await FindAsync(kind, id.Value, ct) : (BaseEntity)Activator.CreateInstance(EntityType(kind))!;
        await AuthorizeAsync(entity, input, id == null, ct);
        if (id.HasValue)
        {
            CheckVersion(entity, input);
            if (IsPublished(entity)) throw new ConflictException("Published configuration cannot be edited. Create a draft revision or unpublish it first");
            if (entity is PolicyDocument && await db.ConfigurationRevisions.AnyAsync(h => h.EntityType == "policies" && h.EntityId == id && h.Action == "Approved and published",ct))
                throw new ConflictException("Previously published consent text is immutable. Create a new draft revision with a new policy version");
        }
        foreach (var field in input.EnumerateObject())
        {
            if (Protected.Contains(field.Name)) continue;
            var property = entity.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(field.Name, StringComparison.OrdinalIgnoreCase));
            if (property is not { CanWrite: true }) continue;
            try { property.SetValue(entity, field.Value.Deserialize(property.PropertyType, JsonOptions)); }
            catch (JsonException) { throw new ValidationException(new Dictionary<string,string[]> { [field.Name] = ["Invalid value"] }); }
        }
        if (entity is PolicyDocument policy && input.TryGetProperty("version", out var pv)) policy.Version = pv.GetString() ?? "";
        if (entity is Service service) service.PackageServiceCode = string.IsNullOrWhiteSpace(service.PackageServiceCode) ? null : service.PackageServiceCode.Trim();
        if (entity is ServicePackage draftPackage)
        {
            PackageConfigurationRules.NormalizeDuration(draftPackage,
                input.EnumerateObject().Any(p => p.Name.Equals("durationDays", StringComparison.OrdinalIgnoreCase)),
                input.EnumerateObject().Any(p => p.Name.Equals("durationWeeks", StringComparison.OrdinalIgnoreCase)));
            PackageConfigurationRules.InvalidateApproval(draftPackage);
        }
        Set(entity, "IsPublished", false); Set(entity, "IsActive", false); Set(entity, "Status", "Draft");
        NormalizeDates(entity);
        ConfigurationRules.Validate(entity);
        if (id.HasValue) IncrementVersion(entity);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!id.HasValue) db.Add(entity);
        await db.SaveChangesAsync(ct);
        AddHistory(kind, entity, id.HasValue ? "Updated draft" : "Created draft");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return entity;
    }

    public async Task<BaseEntity> PublishAsync(string kind, long id, JsonElement input, bool publish, CancellationToken ct = default)
    {
        EnsureStaff();
        var entity = await FindAsync(kind, id, ct);
        await AuthorizeAsync(entity, default, false, ct);
        CheckVersion(entity, input);
        if (publish && (!input.TryGetProperty("approvalConfirmed", out var approval) || approval.ValueKind != JsonValueKind.True))
            throw new ValidationException("Explicit approval confirmation is required before publication");
        ConfigurationRules.Validate(entity);
        if (publish && entity.GetType().GetProperty("EffectiveFrom")?.GetValue(entity) == null)
            throw new ValidationException(new Dictionary<string,string[]> { ["effectiveFrom"] = ["An effective start date is required before publication"] });
        if (publish && entity is Service or AvailabilityRule or AvailabilityException)
        {
            var clinicianId = ReadClinician(entity);
            if (clinicianId is not > 0 || !await db.Nutritionists.AnyAsync(n => n.Id == clinicianId && n.IsVerified && n.DeletedAt == null && n.User != null && n.User.IsActive && n.User.EmailConfirmed && n.User.DeletedAt == null, ct))
                throw new ValidationException("Select a professionally verified clinician with a confirmed active account before publication");
        }
        if (publish && entity is PolicyDocument p && (string.IsNullOrWhiteSpace(p.TitleUrdu) || string.IsNullOrWhiteSpace(p.ContentUrdu)))
            throw new ValidationException("Approved English and Urdu policy text is required for bilingual launch");
        if (publish && entity is Service s && string.IsNullOrWhiteSpace(s.NameUrdu) || publish && entity is ServicePackage package && string.IsNullOrWhiteSpace(package.NameUrdu))
            throw new ValidationException("Approved Urdu and English names are required for bilingual launch");
        if (publish && entity is ServicePackage reviewedPackage)
        {
            PackageConfigurationRules.ValidateForPublication(reviewedPackage);
            if (reviewedPackage.NutritionistApprovedVersion != reviewedPackage.Version || !reviewedPackage.NutritionistApprovedAt.HasValue ||
                !reviewedPackage.NutritionistApprovedById.HasValue || reviewedPackage.NutritionistApprovedAt > DateTime.UtcNow ||
                !await db.Nutritionists.AnyAsync(n => n.UserId == reviewedPackage.NutritionistApprovedById && n.IsVerified && n.DeletedAt == null &&
                    n.User != null && n.User.IsActive && n.User.EmailConfirmed && n.User.DeletedAt == null, ct))
                throw new ConflictException("A verified active nutritionist must approve this exact package content version before publication");
        }
        // Configuration changes and booking validation share this SQL lock.
        return await bookingLock.ExecuteAsync(ReadClinician(entity) ?? 0, async () =>
        {
            await ValidatePublicationConflictsAsync(entity, publish, ct);
            Set(entity, "IsPublished", publish); Set(entity, "IsActive", publish);
            Set(entity, "Status", publish ? "Published" : "Draft");
            Set(entity, "ApprovedByUserId", publish ? current.UserId : null);
            Set(entity, "ApprovedAt", publish ? DateTime.UtcNow : null);
            // Package Version identifies reviewed content. Publication state has
            // its own SQL row-version; it cannot advance a clinician's approval.
            if (entity is not ServicePackage) IncrementVersion(entity);
            var action = publish ? "Approved and published" : "Unpublished";
            if (!publish && input.TryGetProperty("reason",out var reason) && reason.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(reason.GetString()))
            {
                if (reason.GetString()!.Length > 1000) throw new ValidationException("Unpublish reason must be at most 1000 characters");
                action += ": " + reason.GetString()!.Trim();
            }
            AddHistory(kind, entity, action);
            await db.SaveChangesAsync(ct);
            return entity;
        }, ct);
    }

    public async Task<BaseEntity> CreateRevisionAsync(string kind, long id, CancellationToken ct = default)
    {
        EnsureStaff();
        var original = await FindAsync(kind, id, ct);
        await AuthorizeAsync(original, default, false, ct);
        var snapshot = JsonSerializer.SerializeToElement(original, original.GetType(), JsonOptions);
        var clone = (BaseEntity)snapshot.Deserialize(original.GetType(), JsonOptions)!;
        clone.Id = 0; clone.RowVersion = []; clone.CreatedAt = DateTime.UtcNow; clone.UpdatedAt = DateTime.UtcNow;
        Set(clone, "IsPublished", false); Set(clone, "IsActive", false); Set(clone, "ApprovedByUserId", null); Set(clone, "ApprovedAt", null); Set(clone, "Status", "Draft");
        if (clone is ServicePackage package) PackageConfigurationRules.InvalidateApproval(package);
        IncrementVersion(clone);
        if (clone is PolicyDocument p) p.Version += "-draft-" + DateTime.UtcNow.Ticks;
        db.Add(clone);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        AddHistory(kind, clone, $"Draft revision of {id}");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return clone;
    }

    public async Task<ServicePackage> ApprovePackageAsync(long id, JsonElement input, CancellationToken ct = default)
    {
        if (!current.IsAuthenticated || current.Role != "Nutritionist")
            throw new ForbiddenAccessException("Only a verified nutritionist may approve package content");
        var package = (ServicePackage)await FindAsync("packages", id, ct);
        CheckVersion(package, input);
        if (package.IsPublished) throw new ConflictException("Review a draft package revision before publication");
        if (!input.TryGetProperty("approvalConfirmed", out var confirmation) || confirmation.ValueKind != JsonValueKind.True)
            throw new ValidationException("Explicit nutritionist approval of the package content and eligible care categories is required");
        PackageConfigurationRules.ValidateForPublication(package);
        if (!package.EffectiveFrom.HasValue) throw new ValidationException("Set the proposed effective start date before nutritionist review");
        if (!await db.Nutritionists.AnyAsync(n => n.UserId == current.UserId && n.IsVerified && n.DeletedAt == null && n.User != null &&
            n.User.IsActive && n.User.EmailConfirmed && n.User.DeletedAt == null, ct))
            throw new ForbiddenAccessException("Professional verification and a confirmed active nutritionist account are required");
        return await bookingLock.ExecuteAsync(0, async () =>
        {
            if (package.NutritionistApprovedById == current.UserId && package.NutritionistApprovedVersion == package.Version && package.NutritionistApprovedAt.HasValue)
                return package;
            package.NutritionistApprovedById = current.UserId;
            package.NutritionistApprovedAt = DateTime.UtcNow;
            package.NutritionistApprovedVersion = package.Version;
            AddHistory("packages", package, "Nutritionist approved package content and eligible care categories");
            await db.SaveChangesAsync(ct);
            return package;
        }, ct);
    }

    public async Task<BaseEntity> FindAsync(string kind, long id, CancellationToken ct = default) =>
        await db.FindAsync(EntityType(kind), [id], ct) is BaseEntity { DeletedAt: null } entity ? entity : throw new EntityNotFoundException(kind, id);

    private async Task AuthorizeAsync(BaseEntity entity, JsonElement input, bool create, CancellationToken ct)
    {
        var property = entity.GetType().GetProperty("NutritionistId");
        if (property == null) return; // Practice-wide shared configuration; every change records the staff actor.
        if (current.Role == "Nutritionist")
        {
            var clinicianId = await db.Nutritionists.Where(n => n.UserId == current.UserId && n.DeletedAt == null).Select(n => (long?)n.Id).SingleOrDefaultAsync(ct)
                ?? throw new ForbiddenAccessException("A clinician profile is required");
            if (!create && ReadClinician(entity) is > 0 && ReadClinician(entity) != clinicianId) throw new ForbiddenAccessException("You may only configure your own calendar");
            if (input.ValueKind == JsonValueKind.Object && input.TryGetProperty("nutritionistId", out var requested) && requested.ValueKind == JsonValueKind.Number && requested.GetInt64() != clinicianId)
                throw new ForbiddenAccessException("You may not reassign another clinician's configuration");
            property.SetValue(entity, clinicianId);
        }
        else if (input.ValueKind == JsonValueKind.Object && input.TryGetProperty("nutritionistId", out var requested) && requested.ValueKind == JsonValueKind.Number)
            property.SetValue(entity, requested.GetInt64());
    }

    private async Task ValidatePublicationConflictsAsync(BaseEntity entity, bool publish, CancellationToken ct)
    {
        if (entity is AvailabilityRule r)
        {
            if (publish && await db.AvailabilityRules.AnyAsync(x => x.Id != r.Id && x.NutritionistId == r.NutritionistId && x.DayOfWeek == r.DayOfWeek && x.IsPublished && x.DeletedAt == null && x.StartTime < r.EndTime && x.EndTime > r.StartTime && (x.EffectiveTo == null || r.EffectiveFrom == null || x.EffectiveTo > r.EffectiveFrom) && (r.EffectiveTo == null || x.EffectiveFrom == null || r.EffectiveTo > x.EffectiveFrom), ct))
                throw new ConflictException("Published working-hour rules overlap");
            if (!publish)
            {
                var appointments = await db.Appointments.Where(a => a.NutritionistId == r.NutritionistId && a.DeletedAt == null && a.AppointmentDateTime > DateTime.UtcNow && a.Status != "Cancelled" && a.Status != "Rejected" && a.Status != "Completed" && a.Status != "No-show" && a.Status != "NoShow").ToListAsync(ct);
                var zone = TimeZoneInfo.FindSystemTimeZoneById(r.TimeZoneId);
                if (appointments.Any(a => { var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(a.AppointmentDateTime, DateTimeKind.Utc), zone); return local.DayOfWeek == r.DayOfWeek && TimeOnly.FromDateTime(local) >= r.StartTime && TimeOnly.FromDateTime(local) < r.EndTime && ConfigurationRules.IsEffective(r.EffectiveFrom,r.EffectiveTo,a.AppointmentDateTime); }))
                    throw new ConflictException("This calendar change affects an existing booking; review the affected bookings first");
            }
        }
        if (entity is AvailabilityException e && (publish && !e.IsAvailable || !publish && e.IsAvailable && e.IsPublished))
        {
            // The calendar uses the intersection of the exception interval and its
            // effective window. Apply exactly that interval when protecting bookings.
            var start = e.EffectiveFrom.HasValue && e.EffectiveFrom > e.StartAtUtc ? e.EffectiveFrom.Value : e.StartAtUtc;
            var end = e.EffectiveTo.HasValue && e.EffectiveTo < e.EndAtUtc ? e.EffectiveTo.Value : e.EndAtUtc;
            var now = DateTime.UtcNow;
            if (start < end && await db.Appointments.AnyAsync(a => a.NutritionistId == e.NutritionistId && a.DeletedAt == null &&
                a.Status != "Cancelled" && a.Status != "Rejected" && a.Status != "Completed" && a.Status != "No-show" && a.Status != "NoShow" &&
                a.AppointmentDateTime.AddMinutes(a.Duration) > now && a.AppointmentDateTime < end && a.AppointmentDateTime.AddMinutes(a.Duration) > start, ct))
                throw new ConflictException(publish
                    ? "This closure overlaps an existing booking; reschedule or cancel it first"
                    : "Removing this exceptional opening affects an existing booking; review the affected bookings first");
        }
        if (publish && entity is CancellationPolicy p && await db.CancellationPolicies.AnyAsync(x => x.Id != p.Id && x.IsPublished && x.DeletedAt == null && (x.EffectiveTo == null || p.EffectiveFrom == null || x.EffectiveTo > p.EffectiveFrom) && (p.EffectiveTo == null || x.EffectiveFrom == null || p.EffectiveTo > x.EffectiveFrom), ct))
            throw new ConflictException("A published cancellation policy already covers this effective range");
        if (publish && entity is Service service && await db.Services.AnyAsync(x=>x.Id!=service.Id && x.Name==service.Name && x.NutritionistId==service.NutritionistId && x.IsPublished && x.DeletedAt==null && (x.EffectiveTo==null || service.EffectiveFrom==null || x.EffectiveTo>service.EffectiveFrom) && (service.EffectiveTo==null || x.EffectiveFrom==null || service.EffectiveTo>x.EffectiveFrom),ct))
            throw new ConflictException("A published service with this name already covers the effective range");
        if (publish && entity is ServicePackage package && await db.ServicePackages.AnyAsync(x => x.Id != package.Id && x.Name == package.Name && x.IsPublished &&
            x.DeletedAt == null && (x.EffectiveTo == null || package.EffectiveFrom == null || x.EffectiveTo > package.EffectiveFrom) &&
            (package.EffectiveTo == null || x.EffectiveFrom == null || package.EffectiveTo > x.EffectiveFrom), ct))
            throw new ConflictException("A published package with this name already covers the effective range");
        if (publish && entity is PolicyDocument d && await db.PolicyDocuments.AnyAsync(x => x.Id != d.Id && x.Purpose == d.Purpose && x.IsPublished && x.DeletedAt == null && (x.EffectiveTo == null || d.EffectiveFrom == null || x.EffectiveTo > d.EffectiveFrom) && (d.EffectiveTo == null || x.EffectiveFrom == null || d.EffectiveTo > x.EffectiveFrom), ct))
            throw new ConflictException("A published policy already covers this purpose and range");
    }

    private void EnsureStaff() { if (!current.IsAuthenticated || current.Role is not ("Admin" or "Nutritionist")) throw new ForbiddenAccessException("Staff access is required"); }
    private static bool IsPublished(BaseEntity entity) => entity.GetType().GetProperty("IsPublished")?.GetValue(entity) is true;
    private static long? ReadClinician(BaseEntity entity) => entity.GetType().GetProperty("NutritionistId")?.GetValue(entity) as long?;
    private static void Set(BaseEntity entity, string name, object? value) => entity.GetType().GetProperty(name)?.SetValue(entity, value);
    private static void IncrementVersion(BaseEntity entity) { if (entity.GetType().GetProperty("Version") is { PropertyType: var type } p && type == typeof(int)) p.SetValue(entity, (int)p.GetValue(entity)! + 1); }
    private static void CheckVersion(BaseEntity entity, JsonElement input)
    {
        if (input.TryGetProperty("rowVersion", out var row) && row.ValueKind == JsonValueKind.String)
        {
            byte[] expected; try { expected = Convert.FromBase64String(row.GetString()!); } catch { throw new ValidationException("Invalid rowVersion"); }
            if (expected.Length == 0 || !expected.SequenceEqual(entity.RowVersion)) throw new ConflictException("Configuration changed; reload before saving");
        }
        else if (input.TryGetProperty("expectedVersion", out var version) && version.TryGetInt32(out var expected) && entity.GetType().GetProperty("Version")?.GetValue(entity) is int actual)
        { if (actual != expected) throw new ConflictException("Configuration changed; reload before saving"); }
        else throw new ValidationException("Provide rowVersion or expectedVersion to prevent lost updates");
    }
    private static void NormalizeDates(BaseEntity entity)
    {
        foreach (var name in new[] { "EffectiveFrom", "EffectiveTo", "StartAtUtc", "EndAtUtc" })
        {
            var p = entity.GetType().GetProperty(name);
            if (p?.GetValue(entity) is DateTime value)
                p.SetValue(entity, value.Kind == DateTimeKind.Unspecified ? TimeZoneInfo.ConvertTimeToUtc(value,TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi")) : value.ToUniversalTime());
        }
    }
    private void AddHistory(string kind, BaseEntity entity, string action) => db.ConfigurationRevisions.Add(new ConfigurationRevision
    { EntityType = kind, EntityId = entity.Id, Version = entity.GetType().GetProperty("Version")?.GetValue(entity) is int version ? version : 1,
      Action = action, SnapshotJson = JsonSerializer.Serialize(entity,entity.GetType(),JsonOptions), ChangedByUserId = current.UserId });
}

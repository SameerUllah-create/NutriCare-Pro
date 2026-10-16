# SQL Server Database - Entity Relationship Diagram & Implementation Guide

## Visual Entity Relationship Diagram (ERD)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          NutriCare Pro Database ERD                         │
└─────────────────────────────────────────────────────────────────────────────┘


                                    ┌──────────────┐
                                    │    Roles     │
                                    │──────────────│
                                    │ RoleId (PK)  │
                                    │ RoleName     │
                                    │ Description  │
                                    │ IsActive     │
                                    └──────┬───────┘
                                           │ 1
                                           │ FK
                                           │
                    ┌──────────────────────┼──────────────────────┐
                    │                      │                      │
                    │ Many                 │ Many                 │ Many
                    ▼                      ▼                      ▼
         ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
         │     Patients     │    │      Users       │    │  Nutritionists   │
         │──────────────────│    │──────────────────│    │──────────────────│
         │ PatientId (PK)   │    │ UserId (PK)      │    │ NutritionistId   │
         │ UserId (FK/UQ)   │───▶│ Email (UQ)       │◀───│ UserId (FK/UQ)   │
         │ Height           │    │ RoleId (FK)      │    │ LicenseNumber    │
         │ Weight           │    │ FirstName        │    │ Qualification    │
         │ TargetWeight     │    │ LastName         │    │ Specializations  │
         │ BloodGroup       │    │ PasswordHash     │    │ Experience       │
         │ Gender           │    │ IsActive         │    │ ConsultationFee  │
         │ MedicalHistory   │    │ CreatedAt        │    │ IsVerified       │
         │ Allergies        │    │ UpdatedAt        │    │ RatingAverage    │
         │ Medications      │    │ DeletedAt        │    │ CreatedAt        │
         │ Address          │    └──────┬───────────┘    └────────┬─────────┘
         │ City, State      │           │                         │
         │ CreatedAt        │           │ 1:Many                  │ 1:Many
         └────────┬────────┘           │                         │
                  │                    ▼                         ▼
                  │ 1:Many      ┌──────────────────┐    ┌──────────────────┐
                  └────────────▶│    Blogs         │    │   Appointments   │
                                │──────────────────│    │──────────────────│
            ┌──────────────┐    │ BlogId (PK)      │    │ AppointmentId    │
            │   Roles      │    │ AuthorId (FK)    │    │ PatientId (FK)   │
            │   Messages   │    │ Title            │    │ NutritionistId   │
            │   Comments   │    │ Slug (UQ)        │    │ DateTime         │
            │ Notifications│    │ Content          │    │ Duration         │
            │   Reviews    │    │ Status           │    │ Status           │
            └──────┬───────┘    │ Category         │    │ Type             │
                   │            │ Tags (JSON)      │    │ Location         │
                   ▼            │ ViewCount        │    │ Notes            │
            ┌──────────────────┐ │ LikeCount        │    │ CreatedAt        │
            │  Comments (Self) │ │ PublishedAt      │    └────────┬────────┘
            │  Nested Replies) │ │ CreatedAt        │             │
            └────────┬─────────┘ └────────┬─────────┘             │ 1:1
                     │                    │                        │
                     ▼                    ▼                        ▼
              ┌──────────────────┐ ┌──────────────────┐ ┌─────────────────┐
              │ Comments (Blog)  │ │     Reviews      │ │VideoConsultation│
              │──────────────────│ │──────────────────│ │──────────────────│
              │ CommentId (PK)   │ │ ReviewId (PK)    │ │ VCId (PK)        │
              │ BlogId (FK)      │ │ NutritionistId   │ │ AppointmentId    │
              │ UserId (FK)      │ │ PatientId (FK)   │ │ RoomName         │
              │ Text             │ │ Rating (1-5)     │ │ RecordingUrl     │
              │ Status (Pending) │ │ Title            │ │ Duration         │
              │ CreatedAt        │ │ Professionalism  │ │ CreatedAt        │
              └──────────────────┘ │ Knowledge        │ └──────────────────┘
                                   │ CommunicationSK  │
                                   │ CreatedAt        │
                                   └──────────────────┘


┌──────────────────────────────────────────────────────────────────────────────┐
│                         DIET PLAN HIERARCHY                                  │
└──────────────────────────────────────────────────────────────────────────────┘

        ┌──────────────────────────────┐
        │       DietPlans              │
        │──────────────────────────────│
        │ DietPlanId (PK)              │
        │ PatientId (FK)               │
        │ NutritionistId (FK)          │
        │ Title                        │
        │ Objective                    │
        │ StartDate, EndDate           │
        │ Status                       │
        │ DailyCalories                │
        │ ProteinGrams, CarbsGrams     │
        │ CreatedAt                    │
        └──────────────┬───────────────┘
                       │ 1:Many
                       │
                       ▼
        ┌──────────────────────────────┐
        │      DietPlanItems           │
        │──────────────────────────────│
        │ DietPlanItemId (PK)          │
        │ DietPlanId (FK)              │
        │ DayNumber                    │
        │ MealType (Breakfast, Lunch)  │
        │ ItemName                     │
        │ Quantity, Unit               │
        │ Calories                     │
        │ Protein, Carbs, Fats         │
        │ CreatedAt                    │
        └──────────────────────────────┘


┌──────────────────────────────────────────────────────────────────────────────┐
│                     PATIENT TRACKING HIERARCHY                               │
└──────────────────────────────────────────────────────────────────────────────┘

              ┌──────────────────┐
              │    Patients      │
              │──────────────────│
              │ PatientId (PK)   │
              │ UserId (FK)      │
              └────────┬─────────┘
                       │
         ┌─────────────┼─────────────┐
         │             │             │
         ▼ 1:Many      ▼ 1:Many      ▼ 1:Many
    ┌─────────────┐ ┌──────────────┐ ┌──────────────┐
    │   Progress  │ │     Weight   │ │   Medical    │
    │   Tracking  │ │   Tracking   │ │    Reports   │
    │─────────────│ │──────────────│ │──────────────│
    │ ProgressId  │ │ WeightTrkId  │ │ ReportId     │
    │ PatientId   │ │ PatientId    │ │ PatientId    │
    │ RecordDate  │ │ RecordDate   │ │ ReportType   │
    │ EnergyLevel │ │ Weight (UQ)  │ │ FileUrl      │
    │ SleepHours  │ │ BMI          │ │ Status       │
    │ Exercise    │ │ Measurements │ │ ExpiryDate   │
    │ CreatedAt   │ │ CreatedAt    │ │ CreatedAt    │
    └─────────────┘ └──────────────┘ └──────────────┘


┌──────────────────────────────────────────────────────────────────────────────┐
│                     FINANCIAL HIERARCHY                                      │
└──────────────────────────────────────────────────────────────────────────────┘

                   ┌──────────────────┐
                   │    Patients      │
                   │ NutritionistId   │
                   └────────┬─────────┘
                            │ 1:Many
                            │
                            ▼
                   ┌──────────────────────────────┐
                   │       Payments               │
                   │──────────────────────────────│
                   │ PaymentId (PK)               │
                   │ PatientId (FK)               │
                   │ NutritionistId (FK)          │
                   │ Amount                       │
                   │ TransactionId (UQ)           │
                   │ Status (Completed)           │
                   │ RelatedToType                │
                   │ CreatedAt                    │
                   └────────┬─────────────────────┘
                            │ 1:1
                            │
                            ▼
                   ┌──────────────────────────────┐
                   │        Invoices              │
                   │──────────────────────────────│
                   │ InvoiceId (PK)               │
                   │ PaymentId (FK)               │
                   │ InvoiceNumber (UQ)           │
                   │ Amount                       │
                   │ TaxAmount                    │
                   │ Status (Paid)                │
                   │ CreatedAt                    │
                   └──────────────────────────────┘


┌──────────────────────────────────────────────────────────────────────────────┐
│                  COMMUNICATION HIERARCHY                                     │
└──────────────────────────────────────────────────────────────────────────────┘

                        ┌──────────────┐
                        │    Users     │
                        └────────┬─────┘
                                 │
                ┌────────────────┼────────────────┐
                │                │                │
                ▼ 1:Many         ▼ 1:Many         ▼ 1:Many
        ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
        │    Messages      │ │  Notifications   │ │    AuditLogs     │
        │──────────────────│ │──────────────────│ │──────────────────│
        │ MessageId (PK)   │ │ NotificationId   │ │ AuditLogId (PK)  │
        │ SenderId (FK)    │ │ UserId (FK)      │ │ UserId (FK)      │
        │ ReceiverId (FK)  │ │ Type             │ │ Action           │
        │ Subject          │ │ Title            │ │ EntityType       │
        │ Body             │ │ Message          │ │ OldValues        │
        │ IsRead           │ │ Channel          │ │ NewValues        │
        │ Priority         │ │ Status           │ │ IpAddress        │
        │ CreatedAt        │ │ CreatedAt        │ │ CreatedAt        │
        └──────────────────┘ └──────────────────┘ └──────────────────┘
```

---

## Relationship Summary Table

| From Table | To Table | Type | Cardinality | Action |
|---|---|---|---|---|
| Roles | Users | FK | 1:Many | Restrict |
| Users | Patients | FK | 1:1 | Cascade |
| Users | Nutritionists | FK | 1:1 | Cascade |
| Patients | Appointments | FK | 1:Many | Cascade |
| Patients | DietPlans | FK | 1:Many | Cascade |
| Patients | PatientProgress | FK | 1:Many | Cascade |
| Patients | WeightTracking | FK | 1:Many | Cascade |
| Patients | MedicalReports | FK | 1:Many | Cascade |
| Patients | Payments | FK | 1:Many | Restrict |
| Patients | Invoices | FK | 1:Many | Restrict |
| Patients | Reviews | FK | 1:Many | Cascade |
| Nutritionists | Appointments | FK | 1:Many | Restrict |
| Nutritionists | DietPlans | FK | 1:Many | Restrict |
| Nutritionists | Payments | FK | 1:Many | Restrict |
| Nutritionists | Invoices | FK | 1:Many | Restrict |
| Nutritionists | Reviews | FK | 1:Many | Restrict |
| Users | Blogs | FK | 1:Many | Cascade |
| Users | Comments | FK | 1:Many | Cascade |
| Appointments | VideoConsultations | FK | 1:1 | Cascade |
| Appointments | Reviews | FK | 1:Many | Cascade |
| DietPlans | DietPlanItems | FK | 1:Many | Cascade |
| Blogs | Comments | FK | 1:Many | Cascade |
| Comments | Comments | FK | 1:Many | Cascade |
| Payments | Invoices | FK | 1:1 | Restrict |
| Users | Messages (Sender) | FK | 1:Many | Cascade |
| Users | Messages (Receiver) | FK | 1:Many | Cascade |
| Users | Notifications | FK | 1:Many | Cascade |
| Users | AuditLogs | FK | 1:Many | Restrict |

---

## Implementation Steps for Developers

### Phase 1: Database Creation

#### Step 1: Create Database
```sql
CREATE DATABASE NutriCarePro
COLLATE SQL_Latin1_General_CP1_CI_AS;

ALTER DATABASE NutriCarePro
SET RECOVERY FULL,
    AUTO_UPDATE_STATISTICS ON,
    AUTO_SHRINK OFF;
```

#### Step 2: Create Core Tables
Execute the migration script in order:
1. Roles (no dependencies)
2. Users (depends on Roles)
3. Patients (depends on Users)
4. Nutritionists (depends on Users)
5. Appointments (depends on Patients, Nutritionists)
6. Others...

#### Step 3: Create Indexes
All non-clustered indexes are included in the migration script.

#### Step 4: Create Views
Three summary views for reporting and dashboards.

#### Step 5: Create Stored Procedures
Four common operations as stored procedures.

---

### Phase 2: Entity Framework Core Integration

#### Step 1: Create DbContext
```csharp
public class NutriCareDbContext : DbContext
{
    public NutriCareDbContext(DbContextOptions<NutriCareDbContext> options)
        : base(options) { }

    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Patient> Patients { get; set; }
    public DbSet<Nutritionist> Nutritionists { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    // ... other DbSets
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure entities (keys, relationships, indexes)
        modelBuilder.Entity<User>()
            .HasKey(u => u.UserId);
            
        modelBuilder.Entity<User>()
            .HasMany(u => u.Patients)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // ... configure other relationships
    }
}
```

#### Step 2: Create Entity Models
```csharp
public class User
{
    public long UserId { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    // ... other properties
    
    // Navigation properties
    public Role Role { get; set; }
    public Patient Patient { get; set; }
    public Nutritionist Nutritionist { get; set; }
}

public class Appointment
{
    public long AppointmentId { get; set; }
    public long PatientId { get; set; }
    public long NutritionistId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    // ... other properties
    
    // Navigation properties
    public Patient Patient { get; set; }
    public Nutritionist Nutritionist { get; set; }
    public VideoConsultation VideoConsultation { get; set; }
}
```

#### Step 3: Configure Relationships
```csharp
// One-to-Many: Patient has many Appointments
modelBuilder.Entity<Appointment>()
    .HasOne(a => a.Patient)
    .WithMany(p => p.Appointments)
    .HasForeignKey(a => a.PatientId)
    .OnDelete(DeleteBehavior.Cascade);

// One-to-One: Appointment has VideoConsultation
modelBuilder.Entity<VideoConsultation>()
    .HasOne(v => v.Appointment)
    .WithOne(a => a.VideoConsultation)
    .HasForeignKey<VideoConsultation>(v => v.AppointmentId)
    .OnDelete(DeleteBehavior.Cascade);
```

#### Step 4: Configure Indexes
```csharp
modelBuilder.Entity<User>()
    .HasIndex(u => u.Email)
    .IsUnique();

modelBuilder.Entity<Appointment>()
    .HasIndex(a => new { a.PatientId, a.NutritionistId, a.AppointmentDateTime });
```

---

### Phase 3: Data Seeding

#### Step 1: Create Seeder
```csharp
public class DatabaseSeeder
{
    public static void Seed(NutriCareDbContext context)
    {
        if (!context.Roles.Any())
        {
            context.Roles.AddRange(
                new Role { RoleName = "Patient", Description = "Patient user" },
                new Role { RoleName = "Nutritionist", Description = "Nutritionist user" },
                new Role { RoleName = "Admin", Description = "Administrator" }
            );
            context.SaveChanges();
        }
        
        if (!context.Users.Any())
        {
            // Seed users
        }
    }
}
```

#### Step 2: Run During Startup
```csharp
public static void Main(string[] args)
{
    var host = CreateHostBuilder(args).Build();
    
    using (var scope = host.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<NutriCareDbContext>();
        context.Database.Migrate();
        DatabaseSeeder.Seed(context);
    }
    
    host.Run();
}
```

---

### Phase 4: Migrations Management

#### Create Migration
```bash
dotnet ef migrations add InitialCreate -p NutriCare.Persistence -s NutriCare.API
```

#### Apply Migration
```bash
dotnet ef database update -p NutriCare.Persistence -s NutriCare.API
```

#### Remove Last Migration
```bash
dotnet ef migrations remove -p NutriCare.Persistence -s NutriCare.API
```

---

### Phase 5: Performance Optimization

#### Create Indexes
All indexes are included in the schema script.

#### Enable Query Optimization
```csharp
// Use AsNoTracking for read-only queries
var appointments = context.Appointments
    .AsNoTracking()
    .Include(a => a.Patient)
    .Include(a => a.Nutritionist)
    .ToList();
```

#### Use Pagination
```csharp
var appointments = await context.Appointments
    .AsNoTracking()
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

---

### Phase 6: Backup & Security

#### Backup Database
```sql
BACKUP DATABASE NutriCarePro
TO DISK = 'C:\Backups\NutriCarePro.bak'
WITH INIT, COMPRESSION;
```

#### Enable TDE (Transparent Data Encryption)
```sql
CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'P@ssw0rd123!';

CREATE CERTIFICATE NutriCareEncryption
WITH SUBJECT = 'NutriCare Pro Database Encryption';

CREATE DATABASE ENCRYPTION KEY
WITH ALGORITHM = AES_256
ENCRYPTION BY SERVER CERTIFICATE NutriCareEncryption;

ALTER DATABASE NutriCarePro SET ENCRYPTION ON;
```

---

## Query Examples

### Get Patient Appointments
```sql
SELECT 
    a.AppointmentId,
    a.AppointmentDateTime,
    a.Status,
    n.NutritionistId,
    u.FirstName + ' ' + u.LastName AS NutritionistName,
    a.Notes
FROM Appointments a
INNER JOIN Nutritionists n ON a.NutritionistId = n.NutritionistId
INNER JOIN Users u ON n.UserId = u.UserId
WHERE a.PatientId = @PatientId
AND a.AppointmentDateTime > GETDATE()
ORDER BY a.AppointmentDateTime ASC;
```

### Get Nutritionist Revenue
```sql
SELECT 
    n.NutritionistId,
    u.FirstName + ' ' + u.LastName AS NutritionistName,
    COUNT(p.PaymentId) AS PaymentCount,
    SUM(p.Amount) AS TotalRevenue,
    AVG(p.Amount) AS AveragePayment
FROM Nutritionists n
INNER JOIN Users u ON n.UserId = u.UserId
LEFT JOIN Payments p ON n.NutritionistId = p.NutritionistId AND p.Status = 'Completed'
GROUP BY n.NutritionistId, u.FirstName, u.LastName;
```

### Get Patient Progress
```sql
SELECT TOP 30
    wt.WeightTrackingId,
    wt.RecordDate,
    wt.Weight,
    wt.BMI,
    wt.WaistCircumference,
    LAG(wt.Weight) OVER (ORDER BY wt.RecordDate) AS PreviousWeight,
    (wt.Weight - LAG(wt.Weight) OVER (ORDER BY wt.RecordDate)) AS WeightChange
FROM WeightTracking wt
WHERE wt.PatientId = @PatientId
ORDER BY wt.RecordDate DESC;
```

### Get Active Diet Plans
```sql
SELECT 
    dp.DietPlanId,
    dp.Title,
    dp.Objective,
    dp.DailyCalories,
    COUNT(dpi.DietPlanItemId) AS MealCount,
    dp.Status
FROM DietPlans dp
LEFT JOIN DietPlanItems dpi ON dp.DietPlanId = dpi.DietPlanId
WHERE dp.PatientId = @PatientId
AND dp.Status = 'Active'
GROUP BY dp.DietPlanId, dp.Title, dp.Objective, dp.DailyCalories, dp.Status;
```

---

## Troubleshooting Guide

### Foreign Key Error
**Problem:** "The INSERT, UPDATE, or DELETE statement conflicted with a FOREIGN KEY constraint"

**Solution:**
```sql
-- Check if referenced record exists
SELECT * FROM Patients WHERE PatientId = @PatientId;

-- If not, either insert patient first or check foreign key
EXEC sp_help 'Appointments';
```

### Duplicate Key Error
**Problem:** "Violation of UNIQUE KEY constraint"

**Solution:**
```sql
-- Check for duplicate emails
SELECT Email, COUNT(*) FROM Users GROUP BY Email HAVING COUNT(*) > 1;

-- Check for duplicate license numbers
SELECT LicenseNumber, COUNT(*) FROM Nutritionists 
GROUP BY LicenseNumber HAVING COUNT(*) > 1;
```

### Performance Issues
**Problem:** Slow queries

**Solution:**
```sql
-- Check missing indexes
SELECT * FROM sys.dm_db_missing_index_details;

-- Check index fragmentation
SELECT * FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED')
WHERE avg_fragmentation_in_percent > 10;

-- Rebuild indexes
DBCC SHOWCONTIG;
DBCC DBREINDEX;
```

---

**Document Version:** 1.0  
**Created:** June 2, 2026  
**Status:** ✅ Complete


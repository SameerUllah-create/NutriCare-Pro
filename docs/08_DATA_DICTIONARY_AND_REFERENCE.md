# SQL Server Database - Data Dictionary & Developer Reference

## Overview
Complete data dictionary for NutriCare Pro database with column specifications, data types, constraints, and usage guidelines.

---

## 🔤 SQL Server Data Types Reference

### Numeric Types
| Type | Range | Usage |
|------|-------|-------|
| **INT** | -2,147,483,648 to 2,147,483,647 | Whole numbers, IDs, counts |
| **BIGINT** | -9,223,372,036,854,775,808 to 9,223,372,036,854,775,807 | Large IDs, timestamps |
| **DECIMAL(p,s)** | Precision and Scale | Money, measurements (weight, height) |
| **BIT** | 0 or 1 | Boolean flags |
| **FLOAT** | Scientific notation | Percentages, ratings |
| **SMALLINT** | -32,768 to 32,767 | Age, durations |

### String Types
| Type | Max Length | Usage |
|------|-----------|-------|
| **NVARCHAR(n)** | Up to 4000 | Unicode strings (names, emails, descriptions) |
| **NVARCHAR(MAX)** | Up to 2GB | Large text (JSON, medical history) |
| **NCHAR(n)** | Fixed length | Codes, abbreviated values |

### Date/Time Types
| Type | Format | Usage |
|------|--------|-------|
| **DATETIME2** | YYYY-MM-DD HH:MM:SS.fffffff | Timestamps with high precision |
| **DATE** | YYYY-MM-DD | Date values without time |
| **TIME** | HH:MM:SS.fffffff | Time of day |
| **DATETIMEOFFSET** | With timezone | International timestamps |

---

## 📋 Complete Data Dictionary

### Table: Roles

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| RoleId | INT IDENTITY | PK | Unique role identifier | 1 |
| RoleName | NVARCHAR(50) | NOT NULL, UNIQUE | Role name | 'Patient' |
| Description | NVARCHAR(500) | | Role description | 'Patient user role' |
| IsActive | BIT | NOT NULL, DEFAULT 1 | Active status | 1 |
| CreatedAt | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | Creation timestamp | 2026-06-01 12:00:00 |
| UpdatedAt | DATETIME2 | NOT NULL, DEFAULT GETUTCDATE() | Last update timestamp | 2026-06-01 12:00:00 |

---

### Table: Users

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| UserId | BIGINT IDENTITY | PK | User ID | 1001 |
| Email | NVARCHAR(256) | NOT NULL, UNIQUE | Email address | user@example.com |
| FirstName | NVARCHAR(100) | NOT NULL | First name | John |
| LastName | NVARCHAR(100) | NOT NULL | Last name | Doe |
| PhoneNumber | NVARCHAR(20) | | Phone | +1-555-0123 |
| PasswordHash | NVARCHAR(MAX) | NOT NULL | Bcrypt hash | $2b$12$... |
| ProfileImageUrl | NVARCHAR(500) | | Azure blob URL | https://blob.../profile.jpg |
| RoleId | INT | FK, NOT NULL | Role | 1 |
| IsEmailVerified | BIT | NOT NULL, DEFAULT 0 | Email verified | 1 |
| IsPhoneVerified | BIT | NOT NULL, DEFAULT 0 | Phone verified | 0 |
| IsActive | BIT | NOT NULL, DEFAULT 1 | Active status | 1 |
| LastLoginAt | DATETIME2 | | Last login | 2026-06-01 10:00:00 |
| CreatedAt | DATETIME2 | NOT NULL | Creation time | 2026-06-01 08:00:00 |
| UpdatedAt | DATETIME2 | NOT NULL | Update time | 2026-06-01 08:00:00 |
| DeletedAt | DATETIME2 | | Soft delete | NULL |

---

### Table: Patients

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| PatientId | BIGINT IDENTITY | PK | Patient ID | 2001 |
| UserId | BIGINT | FK, UNIQUE | User reference | 1001 |
| DateOfBirth | DATE | NOT NULL, < GETDATE() | Birth date | 1990-05-15 |
| Gender | NVARCHAR(20) | | Gender | 'Male' |
| BloodGroup | NVARCHAR(10) | | Blood type | 'O+' |
| Height | DECIMAL(5,2) | CHECK > 0, < 300 | Height in cm | 175.50 |
| CurrentWeight | DECIMAL(5,2) | CHECK > 0, < 500 | Current weight kg | 85.00 |
| TargetWeight | DECIMAL(5,2) | CHECK > 0, < 500 | Target weight kg | 75.00 |
| MedicalHistory | NVARCHAR(MAX) | | Health history | JSON or text |
| Allergies | NVARCHAR(MAX) | | Allergies list | 'Peanuts, Shellfish' |
| CurrentMedications | NVARCHAR(MAX) | | Current meds | JSON array |
| EmergencyContact | NVARCHAR(100) | | Contact name | 'Jane Doe' |
| EmergencyContactPhone | NVARCHAR(20) | | Contact phone | '+1-555-9876' |
| Address | NVARCHAR(500) | | Street address | '123 Main St' |
| City | NVARCHAR(100) | | City | 'New York' |
| State | NVARCHAR(100) | | State/Province | 'NY' |
| ZipCode | NVARCHAR(20) | | Postal code | '10001' |
| Country | NVARCHAR(100) | | Country | 'USA' |
| PreferredLanguage | NVARCHAR(50) | DEFAULT 'English' | Language | 'English' |
| NotificationPreference | NVARCHAR(50) | | Notification method | 'Email' |
| CreatedAt | DATETIME2 | NOT NULL | Creation time | 2026-06-01 |
| UpdatedAt | DATETIME2 | NOT NULL | Update time | 2026-06-01 |

---

### Table: Nutritionists

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| NutritionistId | BIGINT IDENTITY | PK | Nutritionist ID | 3001 |
| UserId | BIGINT | FK, UNIQUE | User reference | 1002 |
| LicenseNumber | NVARCHAR(100) | UNIQUE | License number | 'LIC-2024-001' |
| Qualification | NVARCHAR(500) | | Qualification | 'M.S. Nutrition' |
| Specializations | NVARCHAR(MAX) | | JSON array | ["Weight Loss", "Diabetes"] |
| Experience | INT | CHECK >= 0 | Years | 10 |
| Bio | NVARCHAR(MAX) | | Biography | "Experienced nutritionist..." |
| ConsultationFee | DECIMAL(10,2) | CHECK > 0 | Fee per session | 50.00 |
| ConsultationDuration | INT | CHECK > 0, DEFAULT 30 | Minutes | 30 |
| IsVerified | BIT | NOT NULL, DEFAULT 0 | Verified status | 1 |
| VerificationDate | DATETIME2 | | Verification time | 2026-06-01 |
| VerificationDocumentUrl | NVARCHAR(500) | | Document URL | 'https://blob.../doc.pdf' |
| MaxPatientsPerDay | INT | CHECK > 0, DEFAULT 10 | Max capacity | 10 |
| AvailableHours | NVARCHAR(MAX) | | JSON schedule | {"Monday": "09:00-17:00"} |
| Timezone | NVARCHAR(100) | DEFAULT 'UTC' | Timezone | 'America/New_York' |
| RatingAverage | DECIMAL(3,2) | CHECK 0-5 | Average rating | 4.75 |
| TotalRatings | INT | | Rating count | 45 |
| TotalPatientsServed | INT | | Lifetime patients | 150 |
| BankAccountDetails | NVARCHAR(MAX) | | Encrypted JSON | Encrypted |
| TaxId | NVARCHAR(100) | | Tax ID | 'EIN-12-3456789' |
| CreatedAt | DATETIME2 | NOT NULL | Creation time | 2026-06-01 |
| UpdatedAt | DATETIME2 | NOT NULL | Update time | 2026-06-01 |

---

### Table: Appointments

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| AppointmentId | BIGINT IDENTITY | PK | Appointment ID | 4001 |
| PatientId | BIGINT | FK, NOT NULL | Patient | 2001 |
| NutritionistId | BIGINT | FK, NOT NULL | Nutritionist | 3001 |
| AppointmentDateTime | DATETIME2 | NOT NULL | Appointment time | 2026-06-15 14:00:00 |
| Duration | INT | CHECK > 0, DEFAULT 30 | Duration minutes | 30 |
| Status | NVARCHAR(50) | | Status | 'Scheduled' |
| AppointmentType | NVARCHAR(50) | | Type | 'Video' |
| Location | NVARCHAR(500) | | Video URL or address | 'https://zoom.../meeting' |
| Notes | NVARCHAR(MAX) | | Consultation notes | 'Discuss weight goals' |
| CancellationReason | NVARCHAR(500) | | Cancellation reason | NULL |
| CancelledBy | NVARCHAR(50) | | Who cancelled | NULL |
| CancelledAt | DATETIME2 | | Cancellation time | NULL |
| RescheduledFrom | BIGINT | FK | Previous appt | NULL |
| CreatedAt | DATETIME2 | NOT NULL | Creation time | 2026-06-01 |
| UpdatedAt | DATETIME2 | NOT NULL | Update time | 2026-06-01 |

**Status Values:** Scheduled, InProgress, Completed, Cancelled, NoShow, Rescheduled  
**Type Values:** Initial, Follow-up, Video, In-person

---

### Table: VideoConsultations

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| VideoConsultationId | BIGINT IDENTITY | PK | Video session ID | 5001 |
| AppointmentId | BIGINT | FK, UNIQUE | Appointment | 4001 |
| RoomName | NVARCHAR(255) | UNIQUE | Room identifier | 'apt-4001-2026-06-15' |
| JoinToken | NVARCHAR(MAX) | | JWT token | 'eyJhbGciOiJIUzI1NiI...' |
| CallStartTime | DATETIME2 | | Session start | 2026-06-15 14:00:00 |
| CallEndTime | DATETIME2 | | Session end | 2026-06-15 14:30:00 |
| Duration | INT | | Duration seconds | 1800 |
| RecordingUrl | NVARCHAR(500) | | Recording location | 'https://blob.../recording.mp4' |
| RecordingStatus | NVARCHAR(50) | | Recording status | 'Completed' |
| Notes | NVARCHAR(MAX) | | Session notes | NULL |
| CreatedAt | DATETIME2 | NOT NULL | Creation time | 2026-06-15 14:00:00 |
| UpdatedAt | DATETIME2 | NOT NULL | Update time | 2026-06-15 14:30:00 |

**RecordingStatus Values:** NotRecorded, Recording, Completed, Failed

---

### Table: DietPlans

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| DietPlanId | BIGINT IDENTITY | PK | Diet plan ID | 6001 |
| PatientId | BIGINT | FK, NOT NULL | Patient | 2001 |
| NutritionistId | BIGINT | FK, NOT NULL | Created by | 3001 |
| Title | NVARCHAR(255) | NOT NULL | Plan title | 'Weight Loss Plan' |
| Description | NVARCHAR(MAX) | | Description | 'Detailed plan...' |
| Objective | NVARCHAR(500) | | Objective | 'Weight Loss' |
| StartDate | DATE | NOT NULL | Start date | 2026-06-01 |
| EndDate | DATE | NOT NULL, >= StartDate | End date | 2026-08-31 |
| Duration | INT | | Duration days | 92 |
| Status | NVARCHAR(50) | | Status | 'Active' |
| DailyCalories | INT | CHECK > 0 | Daily calories | 2000 |
| ProteinGrams | DECIMAL(6,2) | | Protein g/day | 150.00 |
| CarbsGrams | DECIMAL(6,2) | | Carbs g/day | 250.00 |
| FatsGrams | DECIMAL(6,2) | | Fats g/day | 65.00 |
| FiberGrams | DECIMAL(6,2) | | Fiber g/day | 35.00 |
| SodiumMg | INT | | Sodium mg/day | 2300 |
| WaterIntakeLiters | DECIMAL(4,2) | | Water liters/day | 2.50 |
| MealFrequency | INT | CHECK > 0 | Meals per day | 3 |
| IsTemplate | BIT | DEFAULT 0 | Is template | 0 |
| Version | INT | DEFAULT 1 | Version number | 1 |
| Notes | NVARCHAR(MAX) | | Notes | NULL |
| CreatedAt | DATETIME2 | NOT NULL | Creation time | 2026-06-01 |
| UpdatedAt | DATETIME2 | NOT NULL | Update time | 2026-06-01 |

**Status Values:** Active, Completed, Archived, OnHold  
**Objective Values:** Weight Loss, Muscle Gain, Maintenance, Diabetes Management, etc.

---

### Table: DietPlanItems

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| DietPlanItemId | BIGINT IDENTITY | PK | Item ID | 7001 |
| DietPlanId | BIGINT | FK, NOT NULL CASCADE | Diet plan | 6001 |
| DayNumber | INT | CHECK 1-365, NOT NULL | Day number | 1 |
| MealType | NVARCHAR(50) | NOT NULL | Meal type | 'Breakfast' |
| MealTime | TIME | | Time | 08:00:00 |
| ItemName | NVARCHAR(255) | NOT NULL | Food name | 'Oatmeal with berries' |
| Quantity | DECIMAL(8,2) | CHECK > 0 | Quantity | 200.00 |
| Unit | NVARCHAR(20) | | Unit | 'grams' |
| Calories | INT | | Calories | 350 |
| Protein | DECIMAL(6,2) | | Protein g | 10.00 |
| Carbs | DECIMAL(6,2) | | Carbs g | 60.00 |
| Fats | DECIMAL(6,2) | | Fats g | 5.00 |
| Fiber | DECIMAL(6,2) | | Fiber g | 8.00 |
| Notes | NVARCHAR(500) | | Notes | 'Use low-fat milk' |
| CreatedAt | DATETIME2 | NOT NULL | Creation time | 2026-06-01 |
| UpdatedAt | DATETIME2 | NOT NULL | Update time | 2026-06-01 |

**MealType Values:** Breakfast, Lunch, Dinner, Snack1, Snack2  
**Unit Values:** grams, ml, cup, tbsp, pcs, etc.

---

### Table: WeightTracking

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| WeightTrackingId | BIGINT IDENTITY | PK | Tracking ID | 8001 |
| PatientId | BIGINT | FK, NOT NULL CASCADE | Patient | 2001 |
| RecordDate | DATE | NOT NULL | Date | 2026-06-01 |
| Weight | DECIMAL(5,2) | NOT NULL, CHECK 0-500 | Weight kg | 82.50 |
| BMI | DECIMAL(5,2) | CHECK 10-60 | Body Mass Index | 26.80 |
| WaistCircumference | DECIMAL(5,2) | | Waist cm | 85.00 |
| ChestCircumference | DECIMAL(5,2) | | Chest cm | 95.00 |
| HipCircumference | DECIMAL(5,2) | | Hip cm | 100.00 |
| ArmCircumference | DECIMAL(5,2) | | Arm cm | 30.00 |
| ThighCircumference | DECIMAL(5,2) | | Thigh cm | 55.00 |
| BodyFatPercentage | DECIMAL(5,2) | | Body fat % | 25.50 |
| MusclePercentage | DECIMAL(5,2) | | Muscle % | 40.00 |
| WaterPercentage | DECIMAL(5,2) | | Water % | 60.00 |
| BoneMassKg | DECIMAL(5,2) | | Bone mass kg | 3.50 |
| MetabolicRate | INT | | BMR kcal | 1800 |
| Notes | NVARCHAR(500) | | Notes | 'Feeling lighter' |
| CreatedAt | DATETIME2 | NOT NULL | Creation time | 2026-06-01 |

**Constraint:** UNIQUE (PatientId, RecordDate) - One record per patient per day

---

### Table: PatientProgress

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| ProgressId | BIGINT IDENTITY | PK | Progress ID | 9001 |
| PatientId | BIGINT | FK, NOT NULL CASCADE | Patient | 2001 |
| RecordDate | DATE | NOT NULL, <= GETDATE() | Date | 2026-06-01 |
| EnergyLevel | INT | CHECK 1-10 | Energy 1-10 | 7 |
| MoodScore | INT | CHECK 1-10 | Mood 1-10 | 8 |
| SleepHours | DECIMAL(4,2) | CHECK 0-24 | Sleep hours | 7.50 |
| SleepQuality | INT | CHECK 1-10 | Sleep quality | 7 |
| ExerciseMinutes | INT | | Exercise min | 45 |
| ExerciseType | NVARCHAR(100) | | Exercise type | 'Running' |
| WaterIntakeLiters | DECIMAL(4,2) | | Water liters | 2.50 |
| MealsAdhered | INT | | Meals followed | 3 |
| TotalMealsPlanned | INT | | Total planned | 3 |
| Notes | NVARCHAR(MAX) | | Notes | 'Felt great today' |
| Symptoms | NVARCHAR(MAX) | | Symptoms | NULL |
| CreatedAt | DATETIME2 | NOT NULL | Creation time | 2026-06-01 |
| UpdatedAt | DATETIME2 | NOT NULL | Update time | 2026-06-01 |

---

### Table: MedicalReports

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| MedicalReportId | BIGINT IDENTITY | PK | Report ID | 10001 |
| PatientId | BIGINT | FK, NOT NULL CASCADE | Patient | 2001 |
| ReportType | NVARCHAR(100) | NOT NULL | Report type | 'Blood Test' |
| ReportDate | DATE | NOT NULL, <= GETDATE() | Report date | 2026-05-15 |
| UploadDate | DATETIME2 | NOT NULL | Upload time | 2026-05-16 10:00:00 |
| FileName | NVARCHAR(255) | | File name | 'blood_test_2026-05.pdf' |
| FileUrl | NVARCHAR(500) | | Azure blob URL | 'https://blob.../file.pdf' |
| FileSize | BIGINT | | Bytes | 1048576 |
| MimeType | NVARCHAR(100) | | File type | 'application/pdf' |
| Description | NVARCHAR(MAX) | | Description | 'Routine checkup results' |
| IsSharedWithNutritionists | BIT | DEFAULT 0 | Shared | 1 |
| SharedNutritionists | NVARCHAR(MAX) | | JSON array | [3001, 3002] |
| ExpiryDate | DATE | | Expiry | 2027-05-15 |
| Status | NVARCHAR(50) | | Status | 'Active' |
| CreatedAt | DATETIME2 | NOT NULL | Creation time | 2026-05-16 |
| UpdatedAt | DATETIME2 | NOT NULL | Update time | 2026-05-16 |

---

### Table: Payments

| Column | Type | Constraints | Purpose | Example |
|--------|------|-----------|---------|---------|
| PaymentId | BIGINT IDENTITY | PK | Payment ID | 11001 |
| PatientId | BIGINT | FK, NOT NULL | Patient | 2001 |
| NutritionistId | BIGINT | FK | Nutritionist | 3001 |
| Amount | DECIMAL(10,2) | CHECK > 0 | Amount | 50.00 |
| Currency | NVARCHAR(10) | DEFAULT 'USD' | Currency | 'USD' |
| PaymentMethod | NVARCHAR(50) | | Method | 'Stripe' |
| TransactionId | NVARCHAR(255) | UNIQUE | External ID | 'ch_1234567890' |
| Status | NVARCHAR(50) | | Status | 'Completed' |
| Description | NVARCHAR(MAX) | | Description | 'Consultation on 2026-06-15' |
| RelatedToType | NVARCHAR(50) | | Related to | 'Appointment' |
| RelatedToId | BIGINT | | Related ID | 4001 |
| GatewayResponse | NVARCHAR(MAX) | | JSON response | {gateway response} |
| RefundedAmount | DECIMAL(10,2) | DEFAULT 0 | Refunded | 0.00 |
| RefundReason | NVARCHAR(500) | | Reason | NULL |
| RefundedAt | DATETIME2 | | Refund time | NULL |
| CreatedAt | DATETIME2 | NOT NULL | Creation time | 2026-06-15 |
| UpdatedAt | DATETIME2 | NOT NULL | Update time | 2026-06-15 |

**Status Values:** Pending, Completed, Failed, Refunded, PartialRefund  
**PaymentMethod Values:** Stripe, JazzCash, EasyPaisa, CreditCard

---

## 📊 Data Validation Rules

### Email Validation
```sql
CHECK (Email LIKE '%[a-zA-Z0-9]@[a-zA-Z0-9]%.%[a-zA-Z]%')
```

### Phone Number Validation
```sql
CHECK (PhoneNumber LIKE '+%[0-9]%' OR PhoneNumber LIKE '[0-9]%')
```

### Numeric Constraints
- **Age:** 18 ≤ Age ≤ 120
- **Weight:** 20 < Weight < 500 kg
- **Height:** 50 < Height < 300 cm
- **Rating:** 0 ≤ Rating ≤ 5
- **Energy Level:** 1 ≤ Level ≤ 10

### Date Constraints
- **DateOfBirth:** Must be past date
- **RecordDate:** Cannot be future date
- **EndDate:** Must be >= StartDate
- **DueDate:** Must be >= InvoiceDate

---

## 🔐 Security Guidelines

### Encryption Requirements
- **Passwords:** Use bcrypt with salt
- **Bank Details:** Use TDE + Column encryption
- **Medical Data:** Use TDE + Row-level security
- **Sensitive Fields:** Audit all access

### Access Control
```sql
-- Create roles for different access levels
CREATE ROLE db_patient;
CREATE ROLE db_nutritionist;
CREATE ROLE db_admin;

-- Grant permissions
GRANT SELECT ON dbo.Users TO db_patient;
GRANT SELECT, INSERT, UPDATE ON dbo.Appointments TO db_nutritionist;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL OBJECTS TO db_admin;
```

---

## 📈 Performance Tuning

### Query Optimization Techniques
1. **Use indexes** on frequently filtered columns
2. **Avoid N+1 queries** - use JOINs or INCLUDE clauses
3. **Use pagination** for large result sets
4. **Enable statistics** for query optimizer
5. **Use stored procedures** for complex operations

### Sample Optimized Query
```sql
SELECT 
    a.AppointmentId,
    a.AppointmentDateTime,
    u.FirstName + ' ' + u.LastName AS NutritionistName
FROM Appointments a (NOLOCK)
INNER JOIN Nutritionists n (NOLOCK) ON a.NutritionistId = n.NutritionistId
INNER JOIN Users u (NOLOCK) ON n.UserId = u.UserId
WHERE a.PatientId = @PatientId
    AND a.AppointmentDateTime >= GETDATE()
ORDER BY a.AppointmentDateTime ASC;
```

---

## 🔄 Common Operations

### Create User with Role
```sql
BEGIN TRANSACTION;

INSERT INTO dbo.Users (Email, FirstName, LastName, PasswordHash, RoleId)
VALUES ('user@example.com', 'John', 'Doe', '[bcrypt_hash]', 1);

DECLARE @UserId BIGINT = SCOPE_IDENTITY();

INSERT INTO dbo.Patients (UserId, Height, CurrentWeight)
VALUES (@UserId, 175.50, 85.00);

COMMIT TRANSACTION;
```

### Schedule Appointment
```sql
INSERT INTO dbo.Appointments 
    (PatientId, NutritionistId, AppointmentDateTime, Duration, Status, AppointmentType)
VALUES 
    (2001, 3001, '2026-06-15 14:00:00', 30, 'Scheduled', 'Video');
```

### Record Weight
```sql
INSERT INTO dbo.WeightTracking 
    (PatientId, RecordDate, Weight, BMI)
VALUES 
    (2001, CAST(GETDATE() AS DATE), 82.50, 26.80);
```

---

## 📚 Index Maintenance Scripts

### Check Index Fragmentation
```sql
SELECT 
    OBJECT_NAME(ips.object_id) AS TableName,
    i.name AS IndexName,
    ips.avg_fragmentation_in_percent AS Fragmentation,
    ips.page_count AS PageCount
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
INNER JOIN sys.indexes i ON ips.object_id = i.object_id 
    AND ips.index_id = i.index_id
WHERE ips.avg_fragmentation_in_percent > 10
    AND ips.page_count > 1000
ORDER BY ips.avg_fragmentation_in_percent DESC;
```

### Rebuild Fragmented Indexes
```sql
ALTER INDEX ALL ON dbo.Appointments REBUILD;
ALTER INDEX ALL ON dbo.WeightTracking REBUILD;
ALTER INDEX ALL ON dbo.Payments REBUILD;
```

### Update Statistics
```sql
EXEC sp_updatestats;
```

---

## 🚨 Error Handling

### Common SQL Errors

| Error | Cause | Solution |
|-------|-------|----------|
| 2627 | Duplicate key | Check UNIQUE constraints |
| 547 | FK violation | Verify referenced records exist |
| 515 | NULL not allowed | Provide required values |
| 8152 | String too long | Reduce string length |
| 6609 | Cannot access column | Check permissions |

---

## 📋 Data Standards

### Naming Conventions
- **Tables:** PascalCase, Plural (Users, Appointments)
- **Columns:** PascalCase, Descriptive (FirstName, AppointmentDateTime)
- **Indexes:** IX_TableName_Columns
- **Stored Procs:** sp_ActionDescription
- **Views:** vw_PurposeDescription

### Code Standards
- Always use parameterized queries (prevent SQL injection)
- Use transactions for multi-step operations
- Include meaningful comments
- Use NOLOCK for read-only queries
- Return meaningful error messages

---

## 📞 Support Resources

- **Documentation:** /docs/
- **Schema Script:** 001_CreateInitialSchema.sql
- **ERD Diagram:** 07_ERD_AND_IMPLEMENTATION_GUIDE.md
- **Architecture:** 01_ARCHITECTURE_OVERVIEW.md

---

**Document Version:** 1.0  
**Created:** June 2, 2026  
**Last Updated:** June 2, 2026  
**Status:** ✅ Complete


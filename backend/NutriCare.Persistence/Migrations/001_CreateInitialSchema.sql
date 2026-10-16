-- ============================================================================
-- NutriCare Pro - Initial Database Schema
-- Database: NutriCarePro
-- Created: June 2, 2026
-- Version: 1.0
-- ============================================================================

-- ============================================================================
-- PART 1: DROP EXISTING OBJECTS (Safe cleanup)
-- ============================================================================

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'AuditLogs')
    DROP TABLE [dbo].[AuditLogs];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Messages')
    DROP TABLE [dbo].[Messages];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'VideoConsultations')
    DROP TABLE [dbo].[VideoConsultations];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Reviews')
    DROP TABLE [dbo].[Reviews];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Notifications')
    DROP TABLE [dbo].[Notifications];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Comments')
    DROP TABLE [dbo].[Comments];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Blogs')
    DROP TABLE [dbo].[Blogs];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Invoices')
    DROP TABLE [dbo].[Invoices];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Payments')
    DROP TABLE [dbo].[Payments];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'MedicalReports')
    DROP TABLE [dbo].[MedicalReports];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'WeightTracking')
    DROP TABLE [dbo].[WeightTracking];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'PatientProgress')
    DROP TABLE [dbo].[PatientProgress];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'DietPlanItems')
    DROP TABLE [dbo].[DietPlanItems];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'DietPlans')
    DROP TABLE [dbo].[DietPlans];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Appointments')
    DROP TABLE [dbo].[Appointments];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Nutritionists')
    DROP TABLE [dbo].[Nutritionists];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Patients')
    DROP TABLE [dbo].[Patients];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Users')
    DROP TABLE [dbo].[Users];

IF EXISTS (SELECT * FROM sys.objects WHERE type = 'U' AND name = 'Roles')
    DROP TABLE [dbo].[Roles];

-- ============================================================================
-- PART 2: CREATE BASE TABLES (No dependencies)
-- ============================================================================

-- ============================================================================
-- Table: Roles
-- Description: Define system roles (Patient, Nutritionist, Admin)
-- ============================================================================
CREATE TABLE [dbo].[Roles] (
    [RoleId] INT PRIMARY KEY IDENTITY(1, 1),
    [RoleName] NVARCHAR(50) NOT NULL UNIQUE,
    [Description] NVARCHAR(500),
    [IsActive] BIT NOT NULL DEFAULT 1,
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Constraint: RoleName must be non-empty
    CONSTRAINT [CK_Roles_RoleName] CHECK (LEN(TRIM([RoleName])) > 0)
);

-- ============================================================================
-- Table: Users
-- Description: Base user entity for all users (Patients, Nutritionists, Admins)
-- ============================================================================
CREATE TABLE [dbo].[Users] (
    [UserId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [Email] NVARCHAR(256) NOT NULL UNIQUE,
    [FirstName] NVARCHAR(100) NOT NULL,
    [LastName] NVARCHAR(100) NOT NULL,
    [PhoneNumber] NVARCHAR(20),
    [PasswordHash] NVARCHAR(MAX) NOT NULL,
    [ProfileImageUrl] NVARCHAR(500),
    [RoleId] INT NOT NULL,
    [IsEmailVerified] BIT NOT NULL DEFAULT 0,
    [IsPhoneVerified] BIT NOT NULL DEFAULT 0,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [LastLoginAt] DATETIME2(7),
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    [DeletedAt] DATETIME2(7),
    
    -- Foreign Key
    CONSTRAINT [FK_Users_Roles] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Roles]([RoleId]),
    
    -- Constraints: Email and names validation
    CONSTRAINT [CK_Users_Email] CHECK (Email LIKE '%@%.%'),
    CONSTRAINT [CK_Users_FirstName] CHECK (LEN(TRIM([FirstName])) > 0),
    CONSTRAINT [CK_Users_LastName] CHECK (LEN(TRIM([LastName])) > 0)
);

-- Create indexes on Users table for frequently queried columns
CREATE NONCLUSTERED INDEX [IX_Users_Email] ON [dbo].[Users]([Email]);
CREATE NONCLUSTERED INDEX [IX_Users_RoleId] ON [dbo].[Users]([RoleId]);
CREATE NONCLUSTERED INDEX [IX_Users_IsActive] ON [dbo].[Users]([IsActive]);
CREATE NONCLUSTERED INDEX [IX_Users_CreatedAt] ON [dbo].[Users]([CreatedAt]);

-- ============================================================================
-- Table: Patients
-- Description: Patient-specific profile information
-- ============================================================================
CREATE TABLE [dbo].[Patients] (
    [PatientId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [UserId] BIGINT NOT NULL UNIQUE,
    [DateOfBirth] DATE,
    [Gender] NVARCHAR(20), -- Male, Female, Other
    [BloodGroup] NVARCHAR(10), -- A+, A-, B+, B-, O+, O-, AB+, AB-
    [Height] DECIMAL(5, 2), -- in cm
    [CurrentWeight] DECIMAL(5, 2), -- in kg
    [TargetWeight] DECIMAL(5, 2), -- in kg
    [MedicalHistory] NVARCHAR(MAX),
    [Allergies] NVARCHAR(MAX),
    [CurrentMedications] NVARCHAR(MAX),
    [EmergencyContact] NVARCHAR(100),
    [EmergencyContactPhone] NVARCHAR(20),
    [Address] NVARCHAR(500),
    [City] NVARCHAR(100),
    [State] NVARCHAR(100),
    [ZipCode] NVARCHAR(20),
    [Country] NVARCHAR(100),
    [PreferredLanguage] NVARCHAR(50) DEFAULT 'English',
    [NotificationPreference] NVARCHAR(50), -- Email, SMS, Both
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Key
    CONSTRAINT [FK_Patients_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE,
    
    -- Constraints: Age and weight validation
    CONSTRAINT [CK_Patients_DateOfBirth] CHECK ([DateOfBirth] < CAST(GETDATE() AS DATE)),
    CONSTRAINT [CK_Patients_Height] CHECK ([Height] > 0 AND [Height] < 300),
    CONSTRAINT [CK_Patients_CurrentWeight] CHECK ([CurrentWeight] > 0 AND [CurrentWeight] < 500),
    CONSTRAINT [CK_Patients_TargetWeight] CHECK ([TargetWeight] IS NULL OR ([TargetWeight] > 0 AND [TargetWeight] < 500)),
    CONSTRAINT [CK_Patients_Gender] CHECK ([Gender] IN ('Male', 'Female', 'Other', NULL))
);

CREATE NONCLUSTERED INDEX [IX_Patients_UserId] ON [dbo].[Patients]([UserId]);
CREATE NONCLUSTERED INDEX [IX_Patients_City] ON [dbo].[Patients]([City]);

-- ============================================================================
-- Table: Nutritionists
-- Description: Nutritionist-specific profile and credentials
-- ============================================================================
CREATE TABLE [dbo].[Nutritionists] (
    [NutritionistId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [UserId] BIGINT NOT NULL UNIQUE,
    [LicenseNumber] NVARCHAR(100) NOT NULL UNIQUE,
    [Qualification] NVARCHAR(500), -- M.S. Nutrition, Registered Dietitian, etc.
    [Specializations] NVARCHAR(MAX), -- JSON array: ["Weight Loss", "Diabetes", "Heart Disease"]
    [Experience] INT, -- Years of experience
    [Bio] NVARCHAR(MAX),
    [ConsultationFee] DECIMAL(10, 2), -- USD or local currency
    [ConsultationDuration] INT DEFAULT 30, -- minutes
    [IsVerified] BIT NOT NULL DEFAULT 0,
    [VerificationDate] DATETIME2(7),
    [VerificationDocumentUrl] NVARCHAR(500),
    [MaxPatientsPerDay] INT DEFAULT 10,
    [AvailableHours] NVARCHAR(MAX), -- JSON: {"Monday": "09:00-17:00", ...}
    [Timezone] NVARCHAR(100) DEFAULT 'UTC',
    [RatingAverage] DECIMAL(3, 2), -- 0-5 stars
    [TotalRatings] INT DEFAULT 0,
    [TotalPatientsServed] INT DEFAULT 0,
    [BankAccountDetails] NVARCHAR(MAX), -- Encrypted
    [TaxId] NVARCHAR(100),
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Key
    CONSTRAINT [FK_Nutritionists_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE,
    
    -- Constraints
    CONSTRAINT [CK_Nutritionists_Experience] CHECK ([Experience] >= 0),
    CONSTRAINT [CK_Nutritionists_ConsultationFee] CHECK ([ConsultationFee] > 0),
    CONSTRAINT [CK_Nutritionists_ConsultationDuration] CHECK ([ConsultationDuration] > 0),
    CONSTRAINT [CK_Nutritionists_MaxPatientsPerDay] CHECK ([MaxPatientsPerDay] > 0),
    CONSTRAINT [CK_Nutritionists_RatingAverage] CHECK ([RatingAverage] >= 0 AND [RatingAverage] <= 5)
);

CREATE NONCLUSTERED INDEX [IX_Nutritionists_UserId] ON [dbo].[Nutritionists]([UserId]);
CREATE NONCLUSTERED INDEX [IX_Nutritionists_IsVerified] ON [dbo].[Nutritionists]([IsVerified]);
CREATE NONCLUSTERED INDEX [IX_Nutritionists_RatingAverage] ON [dbo].[Nutritionists]([RatingAverage] DESC);

-- ============================================================================
-- PART 3: CREATE APPOINTMENT-RELATED TABLES
-- ============================================================================

-- ============================================================================
-- Table: Appointments
-- Description: Consultation appointments between patients and nutritionists
-- ============================================================================
CREATE TABLE [dbo].[Appointments] (
    [AppointmentId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [PatientId] BIGINT NOT NULL,
    [NutritionistId] BIGINT NOT NULL,
    [AppointmentDateTime] DATETIME2(7) NOT NULL,
    [Duration] INT NOT NULL DEFAULT 30, -- minutes
    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Scheduled', -- Scheduled, InProgress, Completed, Cancelled, NoShow, Rescheduled
    [AppointmentType] NVARCHAR(50), -- Initial, Follow-up, Video, In-person
    [Location] NVARCHAR(500), -- Physical location or video room URL
    [Notes] NVARCHAR(MAX), -- Patient notes for the appointment
    [CancellationReason] NVARCHAR(500),
    [CancelledBy] NVARCHAR(50), -- Patient, Nutritionist, Admin
    [CancelledAt] DATETIME2(7),
    [RescheduledFrom] BIGINT, -- Reference to original appointment if rescheduled
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Keys
    CONSTRAINT [FK_Appointments_Patients] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([PatientId]),
    CONSTRAINT [FK_Appointments_Nutritionists] FOREIGN KEY ([NutritionistId]) REFERENCES [dbo].[Nutritionists]([NutritionistId]),
    
    -- Constraints
    CONSTRAINT [CK_Appointments_Duration] CHECK ([Duration] > 0),
    CONSTRAINT [CK_Appointments_Status] CHECK ([Status] IN ('Scheduled', 'InProgress', 'Completed', 'Cancelled', 'NoShow', 'Rescheduled')),
    CONSTRAINT [CK_Appointments_AppointmentType] CHECK ([AppointmentType] IN ('Initial', 'Follow-up', 'Video', 'In-person', NULL))
);

CREATE NONCLUSTERED INDEX [IX_Appointments_PatientId] ON [dbo].[Appointments]([PatientId]);
CREATE NONCLUSTERED INDEX [IX_Appointments_NutritionistId] ON [dbo].[Appointments]([NutritionistId]);
CREATE NONCLUSTERED INDEX [IX_Appointments_AppointmentDateTime] ON [dbo].[Appointments]([AppointmentDateTime]);
CREATE NONCLUSTERED INDEX [IX_Appointments_Status] ON [dbo].[Appointments]([Status]);
CREATE NONCLUSTERED INDEX [IX_Appointments_PatientNutritionistDate] ON [dbo].[Appointments]([PatientId], [NutritionistId], [AppointmentDateTime]);

-- ============================================================================
-- Table: VideoConsultations
-- Description: Video consultation details linked to appointments
-- ============================================================================
CREATE TABLE [dbo].[VideoConsultations] (
    [VideoConsultationId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [AppointmentId] BIGINT NOT NULL UNIQUE,
    [RoomName] NVARCHAR(255) NOT NULL UNIQUE,
    [JoinToken] NVARCHAR(MAX) NOT NULL,
    [CallStartTime] DATETIME2(7),
    [CallEndTime] DATETIME2(7),
    [Duration] INT, -- seconds
    [RecordingUrl] NVARCHAR(500),
    [RecordingStatus] NVARCHAR(50), -- NotRecorded, Recording, Completed, Failed
    [Notes] NVARCHAR(MAX),
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Key
    CONSTRAINT [FK_VideoConsultations_Appointments] FOREIGN KEY ([AppointmentId]) REFERENCES [dbo].[Appointments]([AppointmentId]) ON DELETE CASCADE
);

CREATE NONCLUSTERED INDEX [IX_VideoConsultations_AppointmentId] ON [dbo].[VideoConsultations]([AppointmentId]);
CREATE NONCLUSTERED INDEX [IX_VideoConsultations_RecordingStatus] ON [dbo].[VideoConsultations]([RecordingStatus]);

-- ============================================================================
-- PART 4: CREATE DIET PLAN TABLES
-- ============================================================================

-- ============================================================================
-- Table: DietPlans
-- Description: Customized diet plans prescribed to patients
-- ============================================================================
CREATE TABLE [dbo].[DietPlans] (
    [DietPlanId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [PatientId] BIGINT NOT NULL,
    [NutritionistId] BIGINT NOT NULL,
    [Title] NVARCHAR(255) NOT NULL,
    [Description] NVARCHAR(MAX),
    [Objective] NVARCHAR(500), -- Weight Loss, Muscle Gain, Diabetes Management, etc.
    [StartDate] DATE NOT NULL,
    [EndDate] DATE NOT NULL,
    [Duration] INT, -- days
    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Active', -- Active, Completed, Archived, OnHold
    [DailyCalories] INT,
    [ProteinGrams] DECIMAL(6, 2),
    [CarbsGrams] DECIMAL(6, 2),
    [FatsGrams] DECIMAL(6, 2),
    [FiberGrams] DECIMAL(6, 2),
    [SodiumMg] INT,
    [WaterIntakeLiters] DECIMAL(4, 2),
    [MealFrequency] INT, -- Number of meals per day
    [IsTemplate] BIT DEFAULT 0, -- If true, can be used as template for other patients
    [Version] INT DEFAULT 1,
    [Notes] NVARCHAR(MAX),
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Keys
    CONSTRAINT [FK_DietPlans_Patients] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([PatientId]),
    CONSTRAINT [FK_DietPlans_Nutritionists] FOREIGN KEY ([NutritionistId]) REFERENCES [dbo].[Nutritionists]([NutritionistId]),
    
    -- Constraints
    CONSTRAINT [CK_DietPlans_DateRange] CHECK ([EndDate] >= [StartDate]),
    CONSTRAINT [CK_DietPlans_DailyCalories] CHECK ([DailyCalories] IS NULL OR [DailyCalories] > 0),
    CONSTRAINT [CK_DietPlans_Status] CHECK ([Status] IN ('Active', 'Completed', 'Archived', 'OnHold')),
    CONSTRAINT [CK_DietPlans_MealFrequency] CHECK ([MealFrequency] IS NULL OR [MealFrequency] > 0)
);

CREATE NONCLUSTERED INDEX [IX_DietPlans_PatientId] ON [dbo].[DietPlans]([PatientId]);
CREATE NONCLUSTERED INDEX [IX_DietPlans_NutritionistId] ON [dbo].[DietPlans]([NutritionistId]);
CREATE NONCLUSTERED INDEX [IX_DietPlans_Status] ON [dbo].[DietPlans]([Status]);
CREATE NONCLUSTERED INDEX [IX_DietPlans_StartDate] ON [dbo].[DietPlans]([StartDate]);

-- ============================================================================
-- Table: DietPlanItems
-- Description: Individual meals/items in a diet plan (normalized from single JSON)
-- ============================================================================
CREATE TABLE [dbo].[DietPlanItems] (
    [DietPlanItemId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [DietPlanId] BIGINT NOT NULL,
    [DayNumber] INT NOT NULL, -- 1-30 (day of the diet plan)
    [MealType] NVARCHAR(50) NOT NULL, -- Breakfast, Lunch, Dinner, Snack1, Snack2
    [MealTime] TIME(7), -- HH:mm:ss
    [ItemName] NVARCHAR(255) NOT NULL,
    [Quantity] DECIMAL(8, 2),
    [Unit] NVARCHAR(20), -- grams, ml, cup, tbsp, etc.
    [Calories] INT,
    [Protein] DECIMAL(6, 2),
    [Carbs] DECIMAL(6, 2),
    [Fats] DECIMAL(6, 2),
    [Fiber] DECIMAL(6, 2),
    [Notes] NVARCHAR(500),
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Key
    CONSTRAINT [FK_DietPlanItems_DietPlans] FOREIGN KEY ([DietPlanId]) REFERENCES [dbo].[DietPlans]([DietPlanId]) ON DELETE CASCADE,
    
    -- Constraints
    CONSTRAINT [CK_DietPlanItems_DayNumber] CHECK ([DayNumber] > 0 AND [DayNumber] <= 365),
    CONSTRAINT [CK_DietPlanItems_MealType] CHECK ([MealType] IN ('Breakfast', 'Lunch', 'Dinner', 'Snack1', 'Snack2', 'Other')),
    CONSTRAINT [CK_DietPlanItems_Quantity] CHECK ([Quantity] > 0)
);

CREATE NONCLUSTERED INDEX [IX_DietPlanItems_DietPlanId] ON [dbo].[DietPlanItems]([DietPlanId]);
CREATE NONCLUSTERED INDEX [IX_DietPlanItems_MealType] ON [dbo].[DietPlanItems]([MealType]);

-- ============================================================================
-- PART 5: CREATE PROGRESS TRACKING TABLES
-- ============================================================================

-- ============================================================================
-- Table: PatientProgress
-- Description: General progress tracking metrics (hydration, exercise, mood, etc.)
-- ============================================================================
CREATE TABLE [dbo].[PatientProgress] (
    [ProgressId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [PatientId] BIGINT NOT NULL,
    [RecordDate] DATE NOT NULL,
    [EnergyLevel] INT, -- 1-10 scale
    [MoodScore] INT, -- 1-10 scale
    [SleepHours] DECIMAL(4, 2),
    [SleepQuality] INT, -- 1-10 scale
    [ExerciseMinutes] INT,
    [ExerciseType] NVARCHAR(100), -- Walking, Running, Yoga, etc.
    [WaterIntakeLiters] DECIMAL(4, 2),
    [MealsAdhered] INT, -- Number of meals adhered to plan
    [TotalMealsPlanned] INT,
    [Notes] NVARCHAR(MAX),
    [Symptoms] NVARCHAR(MAX), -- Any health symptoms
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Key
    CONSTRAINT [FK_PatientProgress_Patients] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([PatientId]) ON DELETE CASCADE,
    
    -- Constraints
    CONSTRAINT [CK_PatientProgress_RecordDate] CHECK ([RecordDate] <= CAST(GETDATE() AS DATE)),
    CONSTRAINT [CK_PatientProgress_EnergyLevel] CHECK ([EnergyLevel] IS NULL OR ([EnergyLevel] >= 1 AND [EnergyLevel] <= 10)),
    CONSTRAINT [CK_PatientProgress_MoodScore] CHECK ([MoodScore] IS NULL OR ([MoodScore] >= 1 AND [MoodScore] <= 10)),
    CONSTRAINT [CK_PatientProgress_SleepHours] CHECK ([SleepHours] IS NULL OR ([SleepHours] > 0 AND [SleepHours] <= 24)),
    CONSTRAINT [CK_PatientProgress_ExerciseMinutes] CHECK ([ExerciseMinutes] IS NULL OR [ExerciseMinutes] >= 0)
);

CREATE NONCLUSTERED INDEX [IX_PatientProgress_PatientId] ON [dbo].[PatientProgress]([PatientId]);
CREATE NONCLUSTERED INDEX [IX_PatientProgress_RecordDate] ON [dbo].[PatientProgress]([RecordDate] DESC);
CREATE NONCLUSTERED INDEX [IX_PatientProgress_PatientRecordDate] ON [dbo].[PatientProgress]([PatientId], [RecordDate] DESC);

-- ============================================================================
-- Table: WeightTracking
-- Description: Dedicated weight and body measurement tracking
-- ============================================================================
CREATE TABLE [dbo].[WeightTracking] (
    [WeightTrackingId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [PatientId] BIGINT NOT NULL,
    [RecordDate] DATE NOT NULL,
    [Weight] DECIMAL(5, 2) NOT NULL, -- kg
    [BMI] DECIMAL(5, 2),
    [WaistCircumference] DECIMAL(5, 2), -- cm
    [ChestCircumference] DECIMAL(5, 2), -- cm
    [HipCircumference] DECIMAL(5, 2), -- cm
    [ArmCircumference] DECIMAL(5, 2), -- cm
    [ThighCircumference] DECIMAL(5, 2), -- cm
    [BodyFatPercentage] DECIMAL(5, 2),
    [MusclePercentage] DECIMAL(5, 2),
    [WaterPercentage] DECIMAL(5, 2),
    [BoneMassKg] DECIMAL(5, 2),
    [MetabolicRate] INT, -- Basal Metabolic Rate (BMR)
    [Notes] NVARCHAR(500),
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    
    -- Foreign Key
    CONSTRAINT [FK_WeightTracking_Patients] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([PatientId]) ON DELETE CASCADE,
    
    -- Constraints
    CONSTRAINT [CK_WeightTracking_RecordDate] CHECK ([RecordDate] <= CAST(GETDATE() AS DATE)),
    CONSTRAINT [CK_WeightTracking_Weight] CHECK ([Weight] > 0 AND [Weight] < 500),
    CONSTRAINT [CK_WeightTracking_BMI] CHECK ([BMI] IS NULL OR ([BMI] > 10 AND [BMI] < 60)),
    CONSTRAINT [CK_WeightTracking_Unique] UNIQUE ([PatientId], [RecordDate])
);

CREATE NONCLUSTERED INDEX [IX_WeightTracking_PatientId] ON [dbo].[WeightTracking]([PatientId]);
CREATE NONCLUSTERED INDEX [IX_WeightTracking_RecordDate] ON [dbo].[WeightTracking]([RecordDate] DESC);
CREATE NONCLUSTERED INDEX [IX_WeightTracking_PatientRecordDate] ON [dbo].[WeightTracking]([PatientId], [RecordDate] DESC);

-- ============================================================================
-- PART 6: CREATE MEDICAL & DOCUMENT TABLES
-- ============================================================================

-- ============================================================================
-- Table: MedicalReports
-- Description: Patient medical reports and test results
-- ============================================================================
CREATE TABLE [dbo].[MedicalReports] (
    [MedicalReportId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [PatientId] BIGINT NOT NULL,
    [ReportType] NVARCHAR(100) NOT NULL, -- Blood Test, X-ray, Scan, Allergy Test, etc.
    [ReportDate] DATE NOT NULL,
    [UploadDate] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [FileName] NVARCHAR(255) NOT NULL,
    [FileUrl] NVARCHAR(500) NOT NULL, -- Azure Blob Storage URL
    [FileSize] BIGINT, -- bytes
    [MimeType] NVARCHAR(100), -- application/pdf, image/jpeg, etc.
    [Description] NVARCHAR(MAX),
    [IsSharedWithNutritionists] BIT DEFAULT 0,
    [SharedNutritionists] NVARCHAR(MAX), -- JSON array of nutritionist IDs
    [ExpiryDate] DATE, -- When report validity expires
    [Status] NVARCHAR(50) DEFAULT 'Active', -- Active, Expired, Archived
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Key
    CONSTRAINT [FK_MedicalReports_Patients] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([PatientId]) ON DELETE CASCADE,
    
    -- Constraints
    CONSTRAINT [CK_MedicalReports_ReportDate] CHECK ([ReportDate] <= CAST(GETDATE() AS DATE)),
    CONSTRAINT [CK_MedicalReports_Status] CHECK ([Status] IN ('Active', 'Expired', 'Archived'))
);

CREATE NONCLUSTERED INDEX [IX_MedicalReports_PatientId] ON [dbo].[MedicalReports]([PatientId]);
CREATE NONCLUSTERED INDEX [IX_MedicalReports_ReportType] ON [dbo].[MedicalReports]([ReportType]);
CREATE NONCLUSTERED INDEX [IX_MedicalReports_UploadDate] ON [dbo].[MedicalReports]([UploadDate] DESC);

-- ============================================================================
-- PART 7: CREATE FINANCIAL TABLES
-- ============================================================================

-- ============================================================================
-- Table: Payments
-- Description: Payment records for consultations and services
-- ============================================================================
CREATE TABLE [dbo].[Payments] (
    [PaymentId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [PatientId] BIGINT NOT NULL,
    [NutritionistId] BIGINT,
    [Amount] DECIMAL(10, 2) NOT NULL,
    [Currency] NVARCHAR(10) DEFAULT 'USD',
    [PaymentMethod] NVARCHAR(50) NOT NULL, -- Stripe, JazzCash, EasyPaisa, CreditCard, etc.
    [TransactionId] NVARCHAR(255) UNIQUE, -- External gateway transaction ID
    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Completed, Failed, Refunded, PartialRefund
    [Description] NVARCHAR(MAX),
    [RelatedToType] NVARCHAR(50), -- Appointment, DietPlan, Consultation, Service
    [RelatedToId] BIGINT, -- ID of related appointment/diet plan
    [GatewayResponse] NVARCHAR(MAX), -- Full response from payment gateway (JSON)
    [RefundedAmount] DECIMAL(10, 2) DEFAULT 0,
    [RefundReason] NVARCHAR(500),
    [RefundedAt] DATETIME2(7),
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Keys
    CONSTRAINT [FK_Payments_Patients] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([PatientId]),
    CONSTRAINT [FK_Payments_Nutritionists] FOREIGN KEY ([NutritionistId]) REFERENCES [dbo].[Nutritionists]([NutritionistId]),
    
    -- Constraints
    CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [CK_Payments_Status] CHECK ([Status] IN ('Pending', 'Completed', 'Failed', 'Refunded', 'PartialRefund')),
    CONSTRAINT [CK_Payments_RefundedAmount] CHECK ([RefundedAmount] >= 0 AND [RefundedAmount] <= [Amount])
);

CREATE NONCLUSTERED INDEX [IX_Payments_PatientId] ON [dbo].[Payments]([PatientId]);
CREATE NONCLUSTERED INDEX [IX_Payments_NutritionistId] ON [dbo].[Payments]([NutritionistId]);
CREATE NONCLUSTERED INDEX [IX_Payments_Status] ON [dbo].[Payments]([Status]);
CREATE NONCLUSTERED INDEX [IX_Payments_CreatedAt] ON [dbo].[Payments]([CreatedAt] DESC);
CREATE NONCLUSTERED INDEX [IX_Payments_TransactionId] ON [dbo].[Payments]([TransactionId]);

-- ============================================================================
-- Table: Invoices
-- Description: Invoice records for payments
-- ============================================================================
CREATE TABLE [dbo].[Invoices] (
    [InvoiceId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [InvoiceNumber] NVARCHAR(50) NOT NULL UNIQUE, -- INV-2026-0001
    [PaymentId] BIGINT NOT NULL,
    [PatientId] BIGINT NOT NULL,
    [NutritionistId] BIGINT,
    [Amount] DECIMAL(10, 2) NOT NULL,
    [Currency] NVARCHAR(10) DEFAULT 'USD',
    [TaxAmount] DECIMAL(10, 2) DEFAULT 0,
    [TotalAmount] DECIMAL(10, 2) NOT NULL,
    [InvoiceDate] DATE NOT NULL,
    [DueDate] DATE,
    [PaidDate] DATE,
    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Unpaid', -- Unpaid, Paid, Overdue, Cancelled
    [Description] NVARCHAR(MAX),
    [Notes] NVARCHAR(MAX),
    [InvoicePdfUrl] NVARCHAR(500),
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Keys
    CONSTRAINT [FK_Invoices_Payments] FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[Payments]([PaymentId]),
    CONSTRAINT [FK_Invoices_Patients] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([PatientId]),
    CONSTRAINT [FK_Invoices_Nutritionists] FOREIGN KEY ([NutritionistId]) REFERENCES [dbo].[Nutritionists]([NutritionistId]),
    
    -- Constraints
    CONSTRAINT [CK_Invoices_Amount] CHECK ([Amount] > 0),
    CONSTRAINT [CK_Invoices_TaxAmount] CHECK ([TaxAmount] >= 0),
    CONSTRAINT [CK_Invoices_TotalAmount] CHECK ([TotalAmount] = [Amount] + [TaxAmount]),
    CONSTRAINT [CK_Invoices_Status] CHECK ([Status] IN ('Unpaid', 'Paid', 'Overdue', 'Cancelled')),
    CONSTRAINT [CK_Invoices_DateRange] CHECK ([DueDate] IS NULL OR [DueDate] >= [InvoiceDate])
);

CREATE NONCLUSTERED INDEX [IX_Invoices_InvoiceNumber] ON [dbo].[Invoices]([InvoiceNumber]);
CREATE NONCLUSTERED INDEX [IX_Invoices_PatientId] ON [dbo].[Invoices]([PatientId]);
CREATE NONCLUSTERED INDEX [IX_Invoices_Status] ON [dbo].[Invoices]([Status]);
CREATE NONCLUSTERED INDEX [IX_Invoices_InvoiceDate] ON [dbo].[Invoices]([InvoiceDate] DESC);

-- ============================================================================
-- PART 8: CREATE COMMUNICATION TABLES
-- ============================================================================

-- ============================================================================
-- Table: Messages
-- Description: Direct messaging between patients and nutritionists
-- ============================================================================
CREATE TABLE [dbo].[Messages] (
    [MessageId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [SenderId] BIGINT NOT NULL,
    [ReceiverId] BIGINT NOT NULL,
    [Subject] NVARCHAR(255),
    [MessageBody] NVARCHAR(MAX) NOT NULL,
    [Attachments] NVARCHAR(MAX), -- JSON array of file URLs
    [IsRead] BIT NOT NULL DEFAULT 0,
    [ReadAt] DATETIME2(7),
    [IsArchived] BIT DEFAULT 0,
    [ArchivedAt] DATETIME2(7),
    [MessageType] NVARCHAR(50), -- Text, Notification, Prescription, Report
    [Priority] NVARCHAR(50) DEFAULT 'Normal', -- Low, Normal, High, Urgent
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Keys
    CONSTRAINT [FK_Messages_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [dbo].[Users]([UserId]),
    CONSTRAINT [FK_Messages_ReceiverId] FOREIGN KEY ([ReceiverId]) REFERENCES [dbo].[Users]([UserId]),
    
    -- Constraints
    CONSTRAINT [CK_Messages_DifferentUsers] CHECK ([SenderId] <> [ReceiverId])
);

CREATE NONCLUSTERED INDEX [IX_Messages_ReceiverId] ON [dbo].[Messages]([ReceiverId]);
CREATE NONCLUSTERED INDEX [IX_Messages_IsRead] ON [dbo].[Messages]([IsRead]);
CREATE NONCLUSTERED INDEX [IX_Messages_CreatedAt] ON [dbo].[Messages]([CreatedAt] DESC);
CREATE NONCLUSTERED INDEX [IX_Messages_ReceiverRead] ON [dbo].[Messages]([ReceiverId], [IsRead]);

-- ============================================================================
-- Table: Notifications
-- Description: System notifications for users
-- ============================================================================
CREATE TABLE [dbo].[Notifications] (
    [NotificationId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [UserId] BIGINT NOT NULL,
    [Type] NVARCHAR(50) NOT NULL, -- Appointment, Message, Payment, DietPlan, Progress, System
    [Title] NVARCHAR(255) NOT NULL,
    [Message] NVARCHAR(MAX) NOT NULL,
    [Icon] NVARCHAR(100), -- Icon name or emoji
    [RelatedToType] NVARCHAR(50), -- Appointment, Payment, DietPlan, etc.
    [RelatedToId] BIGINT, -- ID of related object
    [ActionUrl] NVARCHAR(500), -- URL to navigate to
    [IsRead] BIT NOT NULL DEFAULT 0,
    [ReadAt] DATETIME2(7),
    [Channel] NVARCHAR(50), -- InApp, Email, SMS, Push
    [Status] NVARCHAR(50) DEFAULT 'Sent', -- Pending, Sent, Failed, Bounced
    [SentAt] DATETIME2(7),
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    
    -- Foreign Key
    CONSTRAINT [FK_Notifications_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE,
    
    -- Constraints
    CONSTRAINT [CK_Notifications_Type] CHECK ([Type] IN ('Appointment', 'Message', 'Payment', 'DietPlan', 'Progress', 'System', 'Review', 'Blog'))
);

CREATE NONCLUSTERED INDEX [IX_Notifications_UserId] ON [dbo].[Notifications]([UserId]);
CREATE NONCLUSTERED INDEX [IX_Notifications_IsRead] ON [dbo].[Notifications]([IsRead]);
CREATE NONCLUSTERED INDEX [IX_Notifications_CreatedAt] ON [dbo].[Notifications]([CreatedAt] DESC);
CREATE NONCLUSTERED INDEX [IX_Notifications_UserRead] ON [dbo].[Notifications]([UserId], [IsRead]);

-- ============================================================================
-- PART 9: CREATE CONTENT TABLES
-- ============================================================================

-- ============================================================================
-- Table: Blogs
-- Description: Blog posts for nutrition education
-- ============================================================================
CREATE TABLE [dbo].[Blogs] (
    [BlogId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [AuthorId] BIGINT NOT NULL,
    [Title] NVARCHAR(500) NOT NULL,
    [Slug] NVARCHAR(500) NOT NULL UNIQUE, -- SEO-friendly URL
    [Content] NVARCHAR(MAX) NOT NULL, -- Markdown content
    [Excerpt] NVARCHAR(1000), -- Summary of blog
    [FeaturedImageUrl] NVARCHAR(500),
    [Category] NVARCHAR(100), -- Weight Loss, Diabetes, Heart Disease, etc.
    [Tags] NVARCHAR(MAX), -- JSON array: ["health", "nutrition", "weight-loss"]
    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Draft', -- Draft, Published, Archived, Scheduled
    [ScheduledPublishDate] DATETIME2(7),
    [ViewCount] INT DEFAULT 0,
    [LikeCount] INT DEFAULT 0,
    [CommentCount] INT DEFAULT 0,
    [MetaDescription] NVARCHAR(500), -- For SEO
    [MetaKeywords] NVARCHAR(500), -- For SEO
    [IsCommentingEnabled] BIT DEFAULT 1,
    [PublishedAt] DATETIME2(7),
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Key
    CONSTRAINT [FK_Blogs_Users] FOREIGN KEY ([AuthorId]) REFERENCES [dbo].[Users]([UserId]),
    
    -- Constraints
    CONSTRAINT [CK_Blogs_Status] CHECK ([Status] IN ('Draft', 'Published', 'Archived', 'Scheduled'))
);

CREATE NONCLUSTERED INDEX [IX_Blogs_Slug] ON [dbo].[Blogs]([Slug]);
CREATE NONCLUSTERED INDEX [IX_Blogs_Status] ON [dbo].[Blogs]([Status]);
CREATE NONCLUSTERED INDEX [IX_Blogs_PublishedAt] ON [dbo].[Blogs]([PublishedAt] DESC);
CREATE NONCLUSTERED INDEX [IX_Blogs_Category] ON [dbo].[Blogs]([Category]);
CREATE NONCLUSTERED INDEX [IX_Blogs_AuthorId] ON [dbo].[Blogs]([AuthorId]);

-- ============================================================================
-- Table: Comments
-- Description: Comments on blog posts
-- ============================================================================
CREATE TABLE [dbo].[Comments] (
    [CommentId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [BlogId] BIGINT NOT NULL,
    [UserId] BIGINT NOT NULL,
    [ParentCommentId] BIGINT, -- For nested comments
    [CommentText] NVARCHAR(MAX) NOT NULL,
    [LikeCount] INT DEFAULT 0,
    [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Approved, Rejected, Spam
    [ApprovedAt] DATETIME2(7),
    [ApprovedBy] BIGINT, -- Moderator ID
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Keys
    CONSTRAINT [FK_Comments_Blogs] FOREIGN KEY ([BlogId]) REFERENCES [dbo].[Blogs]([BlogId]) ON DELETE CASCADE,
    CONSTRAINT [FK_Comments_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId]),
    CONSTRAINT [FK_Comments_ParentComment] FOREIGN KEY ([ParentCommentId]) REFERENCES [dbo].[Comments]([CommentId]) ON DELETE CASCADE,
    CONSTRAINT [FK_Comments_ApprovedBy] FOREIGN KEY ([ApprovedBy]) REFERENCES [dbo].[Users]([UserId]),
    
    -- Constraints
    CONSTRAINT [CK_Comments_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected', 'Spam'))
);

CREATE NONCLUSTERED INDEX [IX_Comments_BlogId] ON [dbo].[Comments]([BlogId]);
CREATE NONCLUSTERED INDEX [IX_Comments_Status] ON [dbo].[Comments]([Status]);
CREATE NONCLUSTERED INDEX [IX_Comments_CreatedAt] ON [dbo].[Comments]([CreatedAt] DESC);

-- ============================================================================
-- Table: Reviews
-- Description: Patient reviews for nutritionists
-- ============================================================================
CREATE TABLE [dbo].[Reviews] (
    [ReviewId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [NutritionistId] BIGINT NOT NULL,
    [PatientId] BIGINT NOT NULL,
    [AppointmentId] BIGINT, -- Optional: linked appointment
    [Rating] INT NOT NULL, -- 1-5 stars
    [Title] NVARCHAR(255),
    [ReviewText] NVARCHAR(MAX),
    [Professionalism] INT, -- 1-5
    [Knowledge] INT, -- 1-5
    [CommunicationSkills] INT, -- 1-5
    [TimelinessRating] INT, -- 1-5
    [ValueForMoney] INT, -- 1-5
    [IsAnonymous] BIT DEFAULT 0,
    [IsVerifiedPurchase] BIT DEFAULT 1,
    [HelpfulCount] INT DEFAULT 0,
    [Status] NVARCHAR(50) DEFAULT 'Approved', -- Pending, Approved, Rejected, Archived
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] DATETIME2(7),
    
    -- Foreign Keys
    CONSTRAINT [FK_Reviews_Nutritionists] FOREIGN KEY ([NutritionistId]) REFERENCES [dbo].[Nutritionists]([NutritionistId]),
    CONSTRAINT [FK_Reviews_Patients] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([PatientId]),
    CONSTRAINT [FK_Reviews_Appointments] FOREIGN KEY ([AppointmentId]) REFERENCES [dbo].[Appointments]([AppointmentId]),
    
    -- Constraints
    CONSTRAINT [CK_Reviews_Rating] CHECK ([Rating] >= 1 AND [Rating] <= 5),
    CONSTRAINT [CK_Reviews_DetailedRating] CHECK (
        ([Professionalism] IS NULL OR ([Professionalism] >= 1 AND [Professionalism] <= 5)) AND
        ([Knowledge] IS NULL OR ([Knowledge] >= 1 AND [Knowledge] <= 5)) AND
        ([CommunicationSkills] IS NULL OR ([CommunicationSkills] >= 1 AND [CommunicationSkills] <= 5))
    ),
    CONSTRAINT [CK_Reviews_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected', 'Archived'))
);

CREATE NONCLUSTERED INDEX [IX_Reviews_NutritionistId] ON [dbo].[Reviews]([NutritionistId]);
CREATE NONCLUSTERED INDEX [IX_Reviews_PatientId] ON [dbo].[Reviews]([PatientId]);
CREATE NONCLUSTERED INDEX [IX_Reviews_Status] ON [dbo].[Reviews]([Status]);
CREATE NONCLUSTERED INDEX [IX_Reviews_CreatedAt] ON [dbo].[Reviews]([CreatedAt] DESC);

-- ============================================================================
-- PART 10: CREATE AUDIT TABLE
-- ============================================================================

-- ============================================================================
-- Table: AuditLogs
-- Description: Audit trail for compliance and security
-- ============================================================================
CREATE TABLE [dbo].[AuditLogs] (
    [AuditLogId] BIGINT PRIMARY KEY IDENTITY(1, 1),
    [UserId] BIGINT,
    [Action] NVARCHAR(100) NOT NULL, -- Create, Read, Update, Delete, Login, Logout
    [EntityType] NVARCHAR(100) NOT NULL, -- Users, Appointments, Payments, etc.
    [EntityId] BIGINT,
    [OldValues] NVARCHAR(MAX), -- JSON of previous values
    [NewValues] NVARCHAR(MAX), -- JSON of new values
    [Description] NVARCHAR(MAX),
    [IpAddress] NVARCHAR(50),
    [UserAgent] NVARCHAR(500),
    [StatusCode] INT,
    [IsSuccess] BIT DEFAULT 1,
    [ErrorMessage] NVARCHAR(MAX),
    [CreatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    
    -- Foreign Key
    CONSTRAINT [FK_AuditLogs_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([UserId])
);

CREATE NONCLUSTERED INDEX [IX_AuditLogs_UserId] ON [dbo].[AuditLogs]([UserId]);
CREATE NONCLUSTERED INDEX [IX_AuditLogs_EntityType] ON [dbo].[AuditLogs]([EntityType]);
CREATE NONCLUSTERED INDEX [IX_AuditLogs_CreatedAt] ON [dbo].[AuditLogs]([CreatedAt] DESC);
CREATE NONCLUSTERED INDEX [IX_AuditLogs_Action] ON [dbo].[AuditLogs]([Action]);

-- ============================================================================
-- PART 11: CREATE SUMMARY VIEWS
-- ============================================================================

-- View: Patient Dashboard Summary
CREATE OR ALTER VIEW [dbo].[vw_PatientDashboardSummary] AS
SELECT 
    p.[PatientId],
    u.[UserId],
    u.[FirstName] + ' ' + u.[LastName] AS PatientName,
    u.[Email],
    p.[CurrentWeight],
    p.[TargetWeight],
    (p.[TargetWeight] - p.[CurrentWeight]) AS WeightToLose,
    (SELECT COUNT(*) FROM [dbo].[Appointments] WHERE [PatientId] = p.[PatientId] AND [Status] = 'Scheduled') AS UpcomingAppointments,
    (SELECT COUNT(*) FROM [dbo].[DietPlans] WHERE [PatientId] = p.[PatientId] AND [Status] = 'Active') AS ActiveDietPlans,
    (SELECT COUNT(*) FROM [dbo].[MedicalReports] WHERE [PatientId] = p.[PatientId]) AS MedicalReportsCount,
    (SELECT TOP 1 [Weight] FROM [dbo].[WeightTracking] WHERE [PatientId] = p.[PatientId] ORDER BY [RecordDate] DESC) AS LatestWeight,
    (SELECT TOP 1 [RecordDate] FROM [dbo].[WeightTracking] WHERE [PatientId] = p.[PatientId] ORDER BY [RecordDate] DESC) AS LatestWeightDate
FROM [dbo].[Patients] p
INNER JOIN [dbo].[Users] u ON p.[UserId] = u.[UserId];

-- View: Nutritionist Patient Summary
CREATE OR ALTER VIEW [dbo].[vw_NutritionistPatientSummary] AS
SELECT 
    n.[NutritionistId],
    u.[FirstName] + ' ' + u.[LastName] AS NutritionistName,
    COUNT(DISTINCT a.[PatientId]) AS TotalPatients,
    COUNT(DISTINCT CASE WHEN a.[Status] = 'Scheduled' THEN a.[AppointmentId] END) AS UpcomingAppointments,
    COUNT(DISTINCT d.[DietPlanId]) AS TotalDietPlans,
    AVG(CAST(n.[RatingAverage] AS FLOAT)) AS AverageRating,
    SUM(CASE WHEN p.[Status] = 'Completed' THEN p.[Amount] ELSE 0 END) AS TotalEarnings
FROM [dbo].[Nutritionists] n
INNER JOIN [dbo].[Users] u ON n.[UserId] = u.[UserId]
LEFT JOIN [dbo].[Appointments] a ON n.[NutritionistId] = a.[NutritionistId]
LEFT JOIN [dbo].[DietPlans] d ON n.[NutritionistId] = d.[NutritionistId]
LEFT JOIN [dbo].[Payments] p ON n.[NutritionistId] = p.[NutritionistId]
GROUP BY n.[NutritionistId], u.[FirstName], u.[LastName];

-- View: Payment Summary by Status
CREATE OR ALTER VIEW [dbo].[vw_PaymentSummary] AS
SELECT 
    [Status],
    COUNT(*) AS PaymentCount,
    SUM([Amount]) AS TotalAmount,
    AVG([Amount]) AS AverageAmount,
    MIN([Amount]) AS MinAmount,
    MAX([Amount]) AS MaxAmount
FROM [dbo].[Payments]
GROUP BY [Status];

-- ============================================================================
-- PART 12: CREATE STORED PROCEDURES
-- ============================================================================

-- Stored Procedure: Get Patient Appointment History
CREATE OR ALTER PROCEDURE [dbo].[sp_GetPatientAppointmentHistory]
    @PatientId BIGINT,
    @PageNumber INT = 1,
    @PageSize INT = 10
AS
BEGIN
    SELECT 
        a.[AppointmentId],
        a.[AppointmentDateTime],
        a.[Status],
        a.[AppointmentType],
        n.[NutritionistId],
        u.[FirstName] + ' ' + u.[LastName] AS NutritionistName,
        a.[Notes],
        ROW_NUMBER() OVER (ORDER BY a.[AppointmentDateTime] DESC) AS RowNum
    FROM [dbo].[Appointments] a
    INNER JOIN [dbo].[Nutritionists] n ON a.[NutritionistId] = n.[NutritionistId]
    INNER JOIN [dbo].[Users] u ON n.[UserId] = u.[UserId]
    WHERE a.[PatientId] = @PatientId
    ORDER BY a.[AppointmentDateTime] DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;

-- Stored Procedure: Calculate Patient Weight Progress
CREATE OR ALTER PROCEDURE [dbo].[sp_CalculatePatientWeightProgress]
    @PatientId BIGINT,
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SELECT 
        p.[PatientId],
        COUNT(*) AS RecordsCount,
        MAX(w.[Weight]) AS MaxWeight,
        MIN(w.[Weight]) AS MinWeight,
        (MAX(w.[Weight]) - MIN(w.[Weight])) AS WeightChange,
        AVG(w.[Weight]) AS AverageWeight,
        (SELECT [Weight] FROM [dbo].[WeightTracking] WHERE [PatientId] = p.[PatientId] AND [RecordDate] = @StartDate) AS StartWeight,
        (SELECT [Weight] FROM [dbo].[WeightTracking] WHERE [PatientId] = p.[PatientId] AND [RecordDate] = @EndDate) AS EndWeight
    FROM [dbo].[Patients] p
    INNER JOIN [dbo].[WeightTracking] w ON p.[PatientId] = w.[PatientId]
    WHERE p.[PatientId] = @PatientId
    AND w.[RecordDate] BETWEEN @StartDate AND @EndDate
    GROUP BY p.[PatientId];
END;

-- Stored Procedure: Get Nutritionist Available Slots
CREATE OR ALTER PROCEDURE [dbo].[sp_GetAvailableSlots]
    @NutritionistId BIGINT,
    @Date DATE,
    @SlotDurationMinutes INT = 30
AS
BEGIN
    DECLARE @WorkStartTime TIME = '09:00:00';
    DECLARE @WorkEndTime TIME = '17:00:00';
    
    -- Generate time slots
    WITH TimeSlots AS (
        SELECT DATEADD(MINUTE, number * @SlotDurationMinutes, CAST(@Date AS DATETIME2)) AS SlotDateTime
        FROM master.dbo.spt_values
        WHERE type = 'P'
        AND DATEADD(MINUTE, number * @SlotDurationMinutes, CAST(@Date AS DATETIME2)) < DATEADD(DAY, 1, CAST(@Date AS DATETIME2))
        AND CAST(DATEADD(MINUTE, number * @SlotDurationMinutes, CAST(@Date AS DATETIME2)) AS TIME) >= @WorkStartTime
        AND CAST(DATEADD(MINUTE, number * @SlotDurationMinutes, CAST(@Date AS DATETIME2)) AS TIME) < @WorkEndTime
    )
    SELECT 
        ts.SlotDateTime,
        CASE 
            WHEN EXISTS(SELECT 1 FROM [dbo].[Appointments] a 
                       WHERE a.[NutritionistId] = @NutritionistId 
                       AND a.[AppointmentDateTime] = ts.SlotDateTime 
                       AND a.[Status] IN ('Scheduled', 'InProgress'))
            THEN 0
            ELSE 1
        END AS IsAvailable
    FROM TimeSlots ts
    ORDER BY ts.SlotDateTime;
END;

-- Stored Procedure: Record Audit Log
CREATE OR ALTER PROCEDURE [dbo].[sp_InsertAuditLog]
    @UserId BIGINT,
    @Action NVARCHAR(100),
    @EntityType NVARCHAR(100),
    @EntityId BIGINT,
    @OldValues NVARCHAR(MAX),
    @NewValues NVARCHAR(MAX),
    @Description NVARCHAR(MAX),
    @IpAddress NVARCHAR(50),
    @UserAgent NVARCHAR(500)
AS
BEGIN
    INSERT INTO [dbo].[AuditLogs] 
    ([UserId], [Action], [EntityType], [EntityId], [OldValues], [NewValues], [Description], [IpAddress], [UserAgent])
    VALUES 
    (@UserId, @Action, @EntityType, @EntityId, @OldValues, @NewValues, @Description, @IpAddress, @UserAgent);
END;

-- ============================================================================
-- PRINT COMPLETION MESSAGE
-- ============================================================================
PRINT '========================================';
PRINT 'NutriCare Pro Database Schema Created Successfully!';
PRINT '========================================';
PRINT 'Total Tables: 19';
PRINT 'Total Indexes: 45+';
PRINT 'Total Views: 3';
PRINT 'Total Stored Procedures: 4';
PRINT '========================================';


-- ============================================================================
-- NutriCare Pro - Diet Plan Management Module Schema
-- Module: Diet Plans
-- ============================================================================

IF OBJECT_ID(N'[dbo].[DietPlans]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DietPlans] (
        [Id] BIGINT IDENTITY(1, 1) NOT NULL,
        [PatientId] BIGINT NOT NULL,
        [NutritionistId] BIGINT NOT NULL,
        [Title] NVARCHAR(255) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [Objective] NVARCHAR(500) NULL,
        [StartDate] DATETIME2 NOT NULL,
        [EndDate] DATETIME2 NOT NULL,
        [Duration] INT NOT NULL,
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_DietPlans_Status] DEFAULT N'Active',
        [DailyCalories] INT NOT NULL,
        [ProteinGrams] DECIMAL(8, 2) NOT NULL,
        [CarbsGrams] DECIMAL(8, 2) NOT NULL,
        [FatsGrams] DECIMAL(8, 2) NOT NULL,
        [FiberGrams] DECIMAL(8, 2) NULL,
        [SodiumMg] INT NULL,
        [WaterIntakeLiters] DECIMAL(4, 2) NULL,
        [Supplements] NVARCHAR(MAX) NULL,
        [Instructions] NVARCHAR(MAX) NULL,
        [MealFrequency] INT NULL,
        [IsTemplate] BIT NOT NULL CONSTRAINT [DF_DietPlans_IsTemplate] DEFAULT 0,
        [Version] INT NOT NULL CONSTRAINT [DF_DietPlans_Version] DEFAULT 1,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_DietPlans_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_DietPlans_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        [DeletedAt] DATETIME2 NULL,

        CONSTRAINT [PK_DietPlans] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_DietPlans_Patients] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DietPlans_Nutritionists] FOREIGN KEY ([NutritionistId]) REFERENCES [dbo].[Nutritionists]([Id]),
        CONSTRAINT [CK_DietPlans_DateRange] CHECK ([EndDate] >= [StartDate]),
        CONSTRAINT [CK_DietPlans_DailyCalories] CHECK ([DailyCalories] > 0),
        CONSTRAINT [CK_DietPlans_Macros] CHECK ([ProteinGrams] >= 0 AND [CarbsGrams] >= 0 AND [FatsGrams] >= 0),
        CONSTRAINT [CK_DietPlans_Status] CHECK ([Status] IN (N'Active', N'Completed', N'Archived', N'OnHold'))
    );
END;
GO

IF COL_LENGTH(N'[dbo].[DietPlans]', N'Supplements') IS NULL
BEGIN
    ALTER TABLE [dbo].[DietPlans] ADD [Supplements] NVARCHAR(MAX) NULL;
END;
GO

IF COL_LENGTH(N'[dbo].[DietPlans]', N'Instructions') IS NULL
BEGIN
    ALTER TABLE [dbo].[DietPlans] ADD [Instructions] NVARCHAR(MAX) NULL;
END;
GO

IF OBJECT_ID(N'[dbo].[DietPlanItems]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DietPlanItems] (
        [Id] BIGINT IDENTITY(1, 1) NOT NULL,
        [DietPlanId] BIGINT NOT NULL,
        [DayNumber] INT NOT NULL,
        [MealType] NVARCHAR(50) NOT NULL,
        [MealTime] TIME NULL,
        [ItemName] NVARCHAR(255) NOT NULL,
        [Quantity] DECIMAL(8, 2) NOT NULL,
        [Unit] NVARCHAR(20) NOT NULL,
        [Calories] INT NOT NULL,
        [Protein] DECIMAL(8, 2) NULL,
        [Carbs] DECIMAL(8, 2) NULL,
        [Fats] DECIMAL(8, 2) NULL,
        [Fiber] DECIMAL(8, 2) NULL,
        [Notes] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_DietPlanItems_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_DietPlanItems_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        [DeletedAt] DATETIME2 NULL,

        CONSTRAINT [PK_DietPlanItems] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_DietPlanItems_DietPlans] FOREIGN KEY ([DietPlanId]) REFERENCES [dbo].[DietPlans]([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_DietPlanItems_DayNumber] CHECK ([DayNumber] BETWEEN 1 AND 365),
        CONSTRAINT [CK_DietPlanItems_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [CK_DietPlanItems_Calories] CHECK ([Calories] >= 0)
    );
END;
GO

IF OBJECT_ID(N'[dbo].[DietPlanCompliances]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DietPlanCompliances] (
        [Id] BIGINT IDENTITY(1, 1) NOT NULL,
        [DietPlanId] BIGINT NOT NULL,
        [PatientId] BIGINT NOT NULL,
        [RecordDate] DATETIME2 NOT NULL,
        [MealsCompleted] INT NOT NULL,
        [TotalMeals] INT NOT NULL,
        [WaterIntakeLiters] DECIMAL(4, 2) NOT NULL,
        [TookSupplements] BIT NOT NULL,
        [Notes] NVARCHAR(2000) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_DietPlanCompliances_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_DietPlanCompliances_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        [DeletedAt] DATETIME2 NULL,

        CONSTRAINT [PK_DietPlanCompliances] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_DietPlanCompliances_DietPlans] FOREIGN KEY ([DietPlanId]) REFERENCES [dbo].[DietPlans]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DietPlanCompliances_Patients] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([Id]),
        CONSTRAINT [CK_DietPlanCompliances_Meals] CHECK ([TotalMeals] > 0 AND [MealsCompleted] >= 0 AND [MealsCompleted] <= [TotalMeals]),
        CONSTRAINT [CK_DietPlanCompliances_Water] CHECK ([WaterIntakeLiters] >= 0 AND [WaterIntakeLiters] <= 15)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DietPlans_PatientId_Status' AND [object_id] = OBJECT_ID(N'[dbo].[DietPlans]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DietPlans_PatientId_Status] ON [dbo].[DietPlans]([PatientId], [Status]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DietPlans_NutritionistId' AND [object_id] = OBJECT_ID(N'[dbo].[DietPlans]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DietPlans_NutritionistId] ON [dbo].[DietPlans]([NutritionistId]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DietPlanItems_DietPlanId_DayNumber_MealType' AND [object_id] = OBJECT_ID(N'[dbo].[DietPlanItems]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_DietPlanItems_DietPlanId_DayNumber_MealType] ON [dbo].[DietPlanItems]([DietPlanId], [DayNumber], [MealType]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_DietPlanCompliances_DietPlanId_RecordDate' AND [object_id] = OBJECT_ID(N'[dbo].[DietPlanCompliances]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_DietPlanCompliances_DietPlanId_RecordDate] ON [dbo].[DietPlanCompliances]([DietPlanId], [RecordDate]);
END;
GO

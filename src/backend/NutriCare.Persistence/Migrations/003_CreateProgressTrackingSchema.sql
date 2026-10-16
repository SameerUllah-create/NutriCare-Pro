IF OBJECT_ID(N'[dbo].[WeightTrackings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WeightTrackings]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WeightTrackings] PRIMARY KEY,
        [PatientId] BIGINT NOT NULL,
        [RecordDate] DATETIME2 NOT NULL,
        [Weight] DECIMAL(18,2) NOT NULL,
        [BMI] DECIMAL(18,2) NULL,
        [WaistCircumference] DECIMAL(18,2) NULL,
        [ChestCircumference] DECIMAL(18,2) NULL,
        [HipCircumference] DECIMAL(18,2) NULL,
        [ArmCircumference] DECIMAL(18,2) NULL,
        [ThighCircumference] DECIMAL(18,2) NULL,
        [BodyFatPercentage] DECIMAL(18,2) NULL,
        [MusclePercentage] DECIMAL(18,2) NULL,
        [WaterPercentage] DECIMAL(18,2) NULL,
        [WaterIntakeLiters] DECIMAL(18,2) NULL,
        [DailyCalories] INT NULL,
        [ExerciseMinutes] INT NULL,
        [ExerciseType] NVARCHAR(100) NULL,
        [BoneMassKg] DECIMAL(18,2) NULL,
        [MetabolicRate] INT NULL,
        [Notes] NVARCHAR(2000) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_WeightTrackings_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_WeightTrackings_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_WeightTrackings_Patients_PatientId]
            FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([Id]) ON DELETE CASCADE
    );
END
GO

IF COL_LENGTH(N'[dbo].[WeightTrackings]', N'WaterIntakeLiters') IS NULL
BEGIN
    ALTER TABLE [dbo].[WeightTrackings] ADD [WaterIntakeLiters] DECIMAL(18,2) NULL;
END
GO

IF COL_LENGTH(N'[dbo].[WeightTrackings]', N'DailyCalories') IS NULL
BEGIN
    ALTER TABLE [dbo].[WeightTrackings] ADD [DailyCalories] INT NULL;
END
GO

IF COL_LENGTH(N'[dbo].[WeightTrackings]', N'ExerciseMinutes') IS NULL
BEGIN
    ALTER TABLE [dbo].[WeightTrackings] ADD [ExerciseMinutes] INT NULL;
END
GO

IF COL_LENGTH(N'[dbo].[WeightTrackings]', N'ExerciseType') IS NULL
BEGIN
    ALTER TABLE [dbo].[WeightTrackings] ADD [ExerciseType] NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_WeightTrackings_PatientId_RecordDate'
      AND [object_id] = OBJECT_ID(N'[dbo].[WeightTrackings]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_WeightTrackings_PatientId_RecordDate]
        ON [dbo].[WeightTrackings]([PatientId], [RecordDate])
        WHERE [DeletedAt] IS NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_WeightTrackings_RecordDate'
      AND [object_id] = OBJECT_ID(N'[dbo].[WeightTrackings]')
)
BEGIN
    CREATE INDEX [IX_WeightTrackings_RecordDate]
        ON [dbo].[WeightTrackings]([RecordDate]);
END
GO

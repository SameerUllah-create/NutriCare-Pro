-- ============================================================================
-- NutriCare Pro - Appointment Management Module Schema
-- Module: Appointments
-- ============================================================================

IF OBJECT_ID(N'[dbo].[Appointments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Appointments] (
        [Id] BIGINT IDENTITY(1, 1) NOT NULL,
        [PatientId] BIGINT NOT NULL,
        [NutritionistId] BIGINT NOT NULL,
        [AppointmentDateTime] DATETIME2 NOT NULL,
        [Duration] INT NOT NULL CONSTRAINT [DF_Appointments_Duration] DEFAULT 30,
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Appointments_Status] DEFAULT N'Pending',
        [AppointmentType] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Appointments_AppointmentType] DEFAULT N'Video',
        [Location] NVARCHAR(500) NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [ConsultationNotes] NVARCHAR(MAX) NULL,
        [CancellationReason] NVARCHAR(1000) NULL,
        [CancelledBy] NVARCHAR(50) NULL,
        [CancelledAt] DATETIME2 NULL,
        [ApprovedAt] DATETIME2 NULL,
        [ApprovedByUserId] BIGINT NULL,
        [RejectedAt] DATETIME2 NULL,
        [RejectedByUserId] BIGINT NULL,
        [RejectionReason] NVARCHAR(1000) NULL,
        [CompletedAt] DATETIME2 NULL,
        [CompletedByUserId] BIGINT NULL,
        [RescheduledFromId] BIGINT NULL,
        [RescheduledAt] DATETIME2 NULL,
        [RescheduledByUserId] BIGINT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Appointments_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Appointments_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        [DeletedAt] DATETIME2 NULL,

        CONSTRAINT [PK_Appointments] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [FK_Appointments_Patients] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Appointments_Nutritionists] FOREIGN KEY ([NutritionistId]) REFERENCES [dbo].[Nutritionists]([Id]),
        CONSTRAINT [FK_Appointments_RescheduledFrom] FOREIGN KEY ([RescheduledFromId]) REFERENCES [dbo].[Appointments]([Id]),
        CONSTRAINT [CK_Appointments_Duration] CHECK ([Duration] > 0),
        CONSTRAINT [CK_Appointments_Status] CHECK ([Status] IN (
            N'Pending', N'Approved', N'InProgress', N'Completed', N'Cancelled', N'Rejected', N'NoShow'
        )),
        CONSTRAINT [CK_Appointments_AppointmentType] CHECK ([AppointmentType] IN (
            N'Initial', N'FollowUp', N'Video', N'InPerson'
        ))
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Appointments_PatientId_AppointmentDateTime' AND [object_id] = OBJECT_ID(N'[dbo].[Appointments]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Appointments_PatientId_AppointmentDateTime]
    ON [dbo].[Appointments]([PatientId], [AppointmentDateTime]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Appointments_NutritionistId_AppointmentDateTime' AND [object_id] = OBJECT_ID(N'[dbo].[Appointments]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Appointments_NutritionistId_AppointmentDateTime]
    ON [dbo].[Appointments]([NutritionistId], [AppointmentDateTime]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Appointments_Status' AND [object_id] = OBJECT_ID(N'[dbo].[Appointments]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Appointments_Status]
    ON [dbo].[Appointments]([Status]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Appointments_AdminSearch' AND [object_id] = OBJECT_ID(N'[dbo].[Appointments]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Appointments_AdminSearch]
    ON [dbo].[Appointments]([Status], [AppointmentDateTime])
    INCLUDE ([PatientId], [NutritionistId], [AppointmentType]);
END;
GO

IF OBJECT_ID(N'[dbo].[MedicalReports]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MedicalReports]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_MedicalReports] PRIMARY KEY,
        [PatientId] BIGINT NOT NULL,
        [UploadedByUserId] BIGINT NOT NULL,
        [ReportType] NVARCHAR(50) NOT NULL,
        [OriginalFileName] NVARCHAR(255) NOT NULL,
        [StoredFileName] NVARCHAR(255) NOT NULL,
        [ContentType] NVARCHAR(100) NOT NULL,
        [FileExtension] NVARCHAR(20) NOT NULL,
        [FileSizeBytes] BIGINT NOT NULL,
        [BlobContainer] NVARCHAR(255) NOT NULL,
        [BlobName] NVARCHAR(1000) NOT NULL,
        [PatientNotes] NVARCHAR(2000) NULL,
        [NutritionistNotes] NVARCHAR(5000) NULL,
        [ReviewedByUserId] BIGINT NULL,
        [ReviewedAt] DATETIME2 NULL,
        [UploadedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MedicalReports_UploadedAt] DEFAULT SYSUTCDATETIME(),
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_MedicalReports_Status] DEFAULT N'Uploaded',
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MedicalReports_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MedicalReports_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_MedicalReports_Patients_PatientId]
            FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_MedicalReports_PatientId_UploadedAt'
      AND [object_id] = OBJECT_ID(N'[dbo].[MedicalReports]')
)
BEGIN
    CREATE INDEX [IX_MedicalReports_PatientId_UploadedAt]
        ON [dbo].[MedicalReports]([PatientId], [UploadedAt]);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_MedicalReports_ReportType'
      AND [object_id] = OBJECT_ID(N'[dbo].[MedicalReports]')
)
BEGIN
    CREATE INDEX [IX_MedicalReports_ReportType]
        ON [dbo].[MedicalReports]([ReportType]);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_MedicalReports_BlobName'
      AND [object_id] = OBJECT_ID(N'[dbo].[MedicalReports]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_MedicalReports_BlobName]
        ON [dbo].[MedicalReports]([BlobName])
        WHERE [DeletedAt] IS NULL;
END
GO

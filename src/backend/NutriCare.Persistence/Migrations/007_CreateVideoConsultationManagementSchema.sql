IF COL_LENGTH(N'[dbo].[VideoConsultations]', N'Provider') IS NULL
BEGIN
    ALTER TABLE [dbo].[VideoConsultations] ADD [Provider] NVARCHAR(50) NOT NULL CONSTRAINT [DF_VideoConsultations_Provider] DEFAULT N'GoogleMeet';
END
GO

IF COL_LENGTH(N'[dbo].[VideoConsultations]', N'ProviderMeetingId') IS NULL
BEGIN
    ALTER TABLE [dbo].[VideoConsultations] ADD [ProviderMeetingId] NVARCHAR(255) NULL;
END
GO

IF COL_LENGTH(N'[dbo].[VideoConsultations]', N'MeetingUrl') IS NULL
BEGIN
    ALTER TABLE [dbo].[VideoConsultations] ADD [MeetingUrl] NVARCHAR(2000) NULL;
END
GO

IF COL_LENGTH(N'[dbo].[VideoConsultations]', N'HostUrl') IS NULL
BEGIN
    ALTER TABLE [dbo].[VideoConsultations] ADD [HostUrl] NVARCHAR(2000) NULL;
END
GO

IF COL_LENGTH(N'[dbo].[VideoConsultations]', N'ScheduledStartTime') IS NULL
BEGIN
    ALTER TABLE [dbo].[VideoConsultations] ADD [ScheduledStartTime] DATETIME2 NOT NULL CONSTRAINT [DF_VideoConsultations_ScheduledStartTime] DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH(N'[dbo].[VideoConsultations]', N'ScheduledEndTime') IS NULL
BEGIN
    ALTER TABLE [dbo].[VideoConsultations] ADD [ScheduledEndTime] DATETIME2 NOT NULL CONSTRAINT [DF_VideoConsultations_ScheduledEndTime] DEFAULT SYSUTCDATETIME();
END
GO

IF COL_LENGTH(N'[dbo].[VideoConsultations]', N'Status') IS NULL
BEGIN
    ALTER TABLE [dbo].[VideoConsultations] ADD [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_VideoConsultations_Status] DEFAULT N'Scheduled';
END
GO

IF COL_LENGTH(N'[dbo].[VideoConsultations]', N'CreatedByUserId') IS NULL
BEGIN
    ALTER TABLE [dbo].[VideoConsultations] ADD [CreatedByUserId] BIGINT NOT NULL CONSTRAINT [DF_VideoConsultations_CreatedByUserId] DEFAULT 0;
END
GO

IF COL_LENGTH(N'[dbo].[VideoConsultations]', N'ProviderResponse') IS NULL
BEGIN
    ALTER TABLE [dbo].[VideoConsultations] ADD [ProviderResponse] NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_VideoConsultations_AppointmentId'
      AND [object_id] = OBJECT_ID(N'[dbo].[VideoConsultations]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_VideoConsultations_AppointmentId]
        ON [dbo].[VideoConsultations]([AppointmentId]);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_VideoConsultations_Status_ScheduledStartTime'
      AND [object_id] = OBJECT_ID(N'[dbo].[VideoConsultations]')
)
BEGIN
    CREATE INDEX [IX_VideoConsultations_Status_ScheduledStartTime]
        ON [dbo].[VideoConsultations]([Status], [ScheduledStartTime]);
END
GO

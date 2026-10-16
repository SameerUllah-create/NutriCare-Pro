IF COL_LENGTH(N'[dbo].[Messages]', N'ConversationId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Messages] ADD [ConversationId] NVARCHAR(100) NOT NULL CONSTRAINT [DF_Messages_ConversationId] DEFAULT N'';
END
GO

IF COL_LENGTH(N'[dbo].[Messages]', N'AttachmentFileName') IS NULL
BEGIN
    ALTER TABLE [dbo].[Messages] ADD [AttachmentFileName] NVARCHAR(255) NULL;
END
GO

IF COL_LENGTH(N'[dbo].[Messages]', N'AttachmentContentType') IS NULL
BEGIN
    ALTER TABLE [dbo].[Messages] ADD [AttachmentContentType] NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH(N'[dbo].[Messages]', N'AttachmentFileSizeBytes') IS NULL
BEGIN
    ALTER TABLE [dbo].[Messages] ADD [AttachmentFileSizeBytes] BIGINT NULL;
END
GO

IF COL_LENGTH(N'[dbo].[Messages]', N'AttachmentBlobContainer') IS NULL
BEGIN
    ALTER TABLE [dbo].[Messages] ADD [AttachmentBlobContainer] NVARCHAR(255) NULL;
END
GO

IF COL_LENGTH(N'[dbo].[Messages]', N'AttachmentBlobName') IS NULL
BEGIN
    ALTER TABLE [dbo].[Messages] ADD [AttachmentBlobName] NVARCHAR(1000) NULL;
END
GO

IF COL_LENGTH(N'[dbo].[Messages]', N'DeliveredAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Messages] ADD [DeliveredAt] DATETIME2 NULL;
END
GO

UPDATE [dbo].[Messages]
SET [ConversationId] =
    CONCAT(
        CASE WHEN [SenderId] < [ReceiverId] THEN [SenderId] ELSE [ReceiverId] END,
        N':',
        CASE WHEN [SenderId] < [ReceiverId] THEN [ReceiverId] ELSE [SenderId] END
    )
WHERE [ConversationId] = N'';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Messages_ConversationId_CreatedAt' AND [object_id] = OBJECT_ID(N'[dbo].[Messages]'))
BEGIN
    CREATE INDEX [IX_Messages_ConversationId_CreatedAt] ON [dbo].[Messages]([ConversationId], [CreatedAt]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Messages_ReceiverId_IsRead' AND [object_id] = OBJECT_ID(N'[dbo].[Messages]'))
BEGIN
    CREATE INDEX [IX_Messages_ReceiverId_IsRead] ON [dbo].[Messages]([ReceiverId], [IsRead]);
END
GO

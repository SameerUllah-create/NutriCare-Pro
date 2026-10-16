IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Invoices]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Invoices] PRIMARY KEY,
        [InvoiceNumber] NVARCHAR(50) NOT NULL,
        [PatientId] BIGINT NOT NULL,
        [NutritionistId] BIGINT NULL,
        [Subtotal] DECIMAL(18,2) NOT NULL,
        [TaxAmount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Invoices_TaxAmount] DEFAULT 0,
        [DiscountAmount] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_Invoices_DiscountAmount] DEFAULT 0,
        [TotalAmount] DECIMAL(18,2) NOT NULL,
        [Currency] NVARCHAR(3) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Invoices_Status] DEFAULT N'Issued',
        [IssuedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Invoices_IssuedAt] DEFAULT SYSUTCDATETIME(),
        [DueAt] DATETIME2 NULL,
        [PaidAt] DATETIME2 NULL,
        [Description] NVARCHAR(1000) NULL,
        [RelatedToType] NVARCHAR(50) NULL,
        [RelatedToId] BIGINT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Invoices_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Invoices_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_Invoices_Patients_PatientId]
            FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients]([Id]),
        CONSTRAINT [FK_Invoices_Nutritionists_NutritionistId]
            FOREIGN KEY ([NutritionistId]) REFERENCES [dbo].[Nutritionists]([Id])
    );
END
GO

IF COL_LENGTH(N'[dbo].[Payments]', N'InvoiceId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Payments] ADD [InvoiceId] BIGINT NULL;
END
GO

IF COL_LENGTH(N'[dbo].[Payments]', N'ProviderPaymentId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Payments] ADD [ProviderPaymentId] NVARCHAR(255) NULL;
END
GO

IF COL_LENGTH(N'[dbo].[Payments]', N'CheckoutUrl') IS NULL
BEGIN
    ALTER TABLE [dbo].[Payments] ADD [CheckoutUrl] NVARCHAR(2000) NULL;
END
GO

IF COL_LENGTH(N'[dbo].[Payments]', N'FailureReason') IS NULL
BEGIN
    ALTER TABLE [dbo].[Payments] ADD [FailureReason] NVARCHAR(1000) NULL;
END
GO

IF COL_LENGTH(N'[dbo].[Payments]', N'PaidAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Payments] ADD [PaidAt] DATETIME2 NULL;
END
GO

IF OBJECT_ID(N'[dbo].[PaymentRefunds]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PaymentRefunds]
    (
        [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_PaymentRefunds] PRIMARY KEY,
        [PaymentId] BIGINT NOT NULL,
        [Amount] DECIMAL(18,2) NOT NULL,
        [Currency] NVARCHAR(3) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_PaymentRefunds_Status] DEFAULT N'Pending',
        [Reason] NVARCHAR(1000) NULL,
        [ProviderRefundId] NVARCHAR(255) NULL,
        [GatewayResponse] NVARCHAR(MAX) NULL,
        [RefundedAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_PaymentRefunds_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_PaymentRefunds_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_PaymentRefunds_Payments_PaymentId]
            FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[Payments]([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Invoices_InvoiceNumber' AND [object_id] = OBJECT_ID(N'[dbo].[Invoices]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_InvoiceNumber] ON [dbo].[Invoices]([InvoiceNumber]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Invoices_PatientId_IssuedAt' AND [object_id] = OBJECT_ID(N'[dbo].[Invoices]'))
BEGIN
    CREATE INDEX [IX_Invoices_PatientId_IssuedAt] ON [dbo].[Invoices]([PatientId], [IssuedAt]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Payments_TransactionId' AND [object_id] = OBJECT_ID(N'[dbo].[Payments]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_TransactionId] ON [dbo].[Payments]([TransactionId]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Payments_PatientId_CreatedAt' AND [object_id] = OBJECT_ID(N'[dbo].[Payments]'))
BEGIN
    CREATE INDEX [IX_Payments_PatientId_CreatedAt] ON [dbo].[Payments]([PatientId], [CreatedAt]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Payments_Status' AND [object_id] = OBJECT_ID(N'[dbo].[Payments]'))
BEGIN
    CREATE INDEX [IX_Payments_Status] ON [dbo].[Payments]([Status]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_PaymentRefunds_PaymentId' AND [object_id] = OBJECT_ID(N'[dbo].[PaymentRefunds]'))
BEGIN
    CREATE INDEX [IX_PaymentRefunds_PaymentId] ON [dbo].[PaymentRefunds]([PaymentId]);
END
GO

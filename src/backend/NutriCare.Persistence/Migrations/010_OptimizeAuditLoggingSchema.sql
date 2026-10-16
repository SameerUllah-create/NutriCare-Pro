IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_CreatedAt')
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAt]
    ON [AuditLogs]([CreatedAt] DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_UserId_CreatedAt')
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId_CreatedAt]
    ON [AuditLogs]([UserId], [CreatedAt] DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_Entity_Action')
BEGIN
    CREATE INDEX [IX_AuditLogs_Entity_Action]
    ON [AuditLogs]([EntityType], [Action]);
END;

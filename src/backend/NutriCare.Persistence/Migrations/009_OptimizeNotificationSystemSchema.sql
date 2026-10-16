IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_User_Channel_Read_CreatedAt')
BEGIN
    CREATE INDEX [IX_Notifications_User_Channel_Read_CreatedAt]
    ON [Notifications]([UserId], [Channel], [IsRead], [CreatedAt] DESC);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_RelatedTo')
BEGIN
    CREATE INDEX [IX_Notifications_RelatedTo]
    ON [Notifications]([RelatedToType], [RelatedToId]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Notifications_Status_Channel')
BEGIN
    CREATE INDEX [IX_Notifications_Status_Channel]
    ON [Notifications]([Status], [Channel]);
END;

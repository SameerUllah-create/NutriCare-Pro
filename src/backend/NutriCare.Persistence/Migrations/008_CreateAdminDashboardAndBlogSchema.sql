IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Blogs')
BEGIN
    CREATE TABLE [Blogs] (
        [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Blogs] PRIMARY KEY,
        [AuthorUserId] BIGINT NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Slug] NVARCHAR(220) NOT NULL,
        [Summary] NVARCHAR(MAX) NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [CoverImageUrl] NVARCHAR(MAX) NULL,
        [Category] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_Blogs_Category] DEFAULT 'General',
        [Tags] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_Blogs_Tags] DEFAULT '',
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Blogs_Status] DEFAULT 'Draft',
        [PublishedAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Blogs_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Blogs_UpdatedAt] DEFAULT SYSUTCDATETIME(),
        [DeletedAt] DATETIME2 NULL,
        CONSTRAINT [FK_Blogs_Users_AuthorUserId] FOREIGN KEY ([AuthorUserId]) REFERENCES [Users]([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Blogs_Slug')
BEGIN
    CREATE UNIQUE INDEX [IX_Blogs_Slug] ON [Blogs]([Slug]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Blogs_Status_PublishedAt')
BEGIN
    CREATE INDEX [IX_Blogs_Status_PublishedAt] ON [Blogs]([Status], [PublishedAt]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Blogs_AuthorUserId')
BEGIN
    CREATE INDEX [IX_Blogs_AuthorUserId] ON [Blogs]([AuthorUserId]);
END;

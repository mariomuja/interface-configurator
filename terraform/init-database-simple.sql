-- Create TransportData table
-- Primary Key is GUID with DEFAULT NEWID() to auto-generate GUIDs
-- CsvDataJson stores ALL CSV columns as JSON to mirror exact CSV structure
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TransportData] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [CsvDataJson] NVARCHAR(MAX) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX [IX_TransportData_CreatedAt] ON [dbo].[TransportData]([CreatedAt]);
END

-- Create ProcessLogs table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ProcessLogs] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Level] NVARCHAR(50) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [Details] NVARCHAR(MAX) NULL
    );
    CREATE INDEX [IX_ProcessLogs_Timestamp] ON [dbo].[ProcessLogs]([Timestamp] DESC);
    CREATE INDEX [IX_ProcessLogs_Level] ON [dbo].[ProcessLogs]([Level]);
END

SELECT 'Database initialization completed' AS Status;

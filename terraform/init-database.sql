-- Database Initialization Script for Infrastructure as Code App
-- This script creates the required tables: TransportData and ProcessLogs

-- Create TransportData table
-- Primary Key is GUID with DEFAULT NEWID() to auto-generate GUIDs
-- CsvDataJson stores ALL CSV columns as JSON to mirror exact CSV structure
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TransportData] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [CsvDataJson] NVARCHAR(MAX) NOT NULL,
        [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    
    CREATE INDEX [IX_TransportData_datetime_created] ON [dbo].[TransportData]([datetime_created]);
    
    PRINT 'TransportData table created successfully';
END
ELSE
BEGIN
    PRINT 'TransportData table already exists';
END
GO

-- Create ProcessLogs table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ProcessLogs] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Level] NVARCHAR(50) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [Details] NVARCHAR(MAX) NULL,
        [Component] NVARCHAR(200) NULL
    );
    
    CREATE INDEX [IX_ProcessLogs_datetime_created] ON [dbo].[ProcessLogs]([datetime_created] DESC);
    CREATE INDEX [IX_ProcessLogs_Level] ON [dbo].[ProcessLogs]([Level]);
    
    PRINT 'ProcessLogs table created successfully';
END
ELSE
BEGIN
    PRINT 'ProcessLogs table already exists';
END
GO

PRINT 'Database initialization completed successfully';
GO




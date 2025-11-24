-- InterfaceConfigDb Database Initialization Script (formerly MessageBox)
-- This script creates the InterfaceConfigDb database schema
-- The database stores interface configurations, adapter instances, and process logs
-- Note: Messaging is now handled via Azure Service Bus, not this database

USE [InterfaceConfigDb]
GO

-- Messages and MessageSubscriptions tables removed
-- These tables are no longer used - messaging is handled via Azure Service Bus
GO

-- Create ProcessLogs table (moved from main database to MessageBox)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ProcessLogs] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Level] NVARCHAR(50) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [Details] NVARCHAR(MAX) NULL,
        [Component] NVARCHAR(200) NULL,
        [InterfaceName] NVARCHAR(200) NULL -- Link to interface if applicable
        -- MessageId column removed - Messages table no longer exists
    );
    
    CREATE INDEX [IX_ProcessLogs_datetime_created] ON [dbo].[ProcessLogs]([datetime_created] DESC);
    CREATE INDEX [IX_ProcessLogs_Level] ON [dbo].[ProcessLogs]([Level]);
    CREATE INDEX [IX_ProcessLogs_InterfaceName] ON [dbo].[ProcessLogs]([InterfaceName]);
    -- IX_ProcessLogs_MessageId index removed - MessageId column no longer exists
    
    PRINT 'ProcessLogs table created successfully';
END
ELSE
BEGIN
    PRINT 'ProcessLogs table already exists';
END
GO

-- Create AdapterInstances table (tracks adapter instance metadata)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdapterInstances]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AdapterInstances] (
        [AdapterInstanceGuid] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [InterfaceName] NVARCHAR(200) NOT NULL,
        [InstanceName] NVARCHAR(200) NOT NULL,
        [AdapterName] NVARCHAR(100) NOT NULL,
        [AdapterType] NVARCHAR(50) NOT NULL,
        [IsEnabled] BIT NOT NULL DEFAULT 1,
        [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [datetime_updated] DATETIME2 NULL
    );
    
    CREATE INDEX [IX_AdapterInstances_InterfaceName] ON [dbo].[AdapterInstances]([InterfaceName]);
    CREATE INDEX [IX_AdapterInstances_AdapterName] ON [dbo].[AdapterInstances]([AdapterName]);
    CREATE INDEX [IX_AdapterInstances_AdapterType] ON [dbo].[AdapterInstances]([AdapterType]);
    
    PRINT 'AdapterInstances table created successfully';
END
ELSE
BEGIN
    PRINT 'AdapterInstances table already exists';
END
GO

PRINT 'InterfaceConfigDb database initialization completed successfully';
GO



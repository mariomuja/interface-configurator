-- MessageBox Database Initialization Script
-- This script creates the MessageBox database schema similar to Microsoft BizTalk Server message box
-- The MessageBox serves as a staging area for all data flowing through adapters

USE [MessageBox]
GO

-- Create Messages table (similar to BizTalk Server message box)
-- Stores all messages/data flowing through the system
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Messages] (
        [MessageId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [InterfaceName] NVARCHAR(200) NOT NULL, -- e.g., "FromCsvToSqlServerExample"
        [AdapterName] NVARCHAR(100) NOT NULL, -- e.g., "CSV", "SqlServer", "JSON", "SAP"
        [AdapterType] NVARCHAR(50) NOT NULL, -- "Source" or "Destination"
        [AdapterInstanceGuid] UNIQUEIDENTIFIER NOT NULL, -- GUID identifying the adapter instance that created this message
        [MessageData] NVARCHAR(MAX) NOT NULL, -- JSON format: {"headers": [...], "record": {...}} - single record per message (debatching)
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- "Pending", "InProgress", "Processed", "Error", "DeadLetter"
        [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [datetime_processed] DATETIME2 NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [ProcessingDetails] NVARCHAR(MAX) NULL,
        [MessageHash] NVARCHAR(64) NULL, -- SHA256 hash for idempotency checking
        [RetryCount] INT NOT NULL DEFAULT 0, -- Number of retry attempts
        [MaxRetries] INT NOT NULL DEFAULT 3, -- Maximum number of retries
        [InProgressUntil] DATETIME2 NULL, -- Lock expiration time for in-progress messages
        [LastRetryTime] DATETIME2 NULL, -- Last time a retry was attempted
        [DeadLetter] BIT NOT NULL DEFAULT 0 -- Whether message is in dead letter queue
    );
    
    CREATE INDEX [IX_Messages_InterfaceName] ON [dbo].[Messages]([InterfaceName]);
    CREATE INDEX [IX_Messages_AdapterName] ON [dbo].[Messages]([AdapterName]);
    CREATE INDEX [IX_Messages_AdapterType] ON [dbo].[Messages]([AdapterType]);
    CREATE INDEX [IX_Messages_Status] ON [dbo].[Messages]([Status]);
    CREATE INDEX [IX_Messages_datetime_created] ON [dbo].[Messages]([datetime_created] DESC);
    CREATE INDEX [IX_Messages_Status_InterfaceName] ON [dbo].[Messages]([Status], [InterfaceName]);
    CREATE INDEX [IX_Messages_AdapterInstanceGuid] ON [dbo].[Messages]([AdapterInstanceGuid]);
    CREATE INDEX [IX_Messages_MessageHash] ON [dbo].[Messages]([MessageHash]);
    
    PRINT 'Messages table created successfully';
END
ELSE
BEGIN
    PRINT 'Messages table already exists';
END
GO

-- Create MessageSubscriptions table (tracks which adapters have processed which messages)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MessageSubscriptions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MessageSubscriptions] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [MessageId] UNIQUEIDENTIFIER NOT NULL,
        [InterfaceName] NVARCHAR(200) NOT NULL,
        [SubscriberAdapterName] NVARCHAR(100) NOT NULL, -- e.g., "SqlServer", "CSV"
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- "Pending", "Processed", "Error"
        [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [datetime_processed] DATETIME2 NULL,
        [ProcessingDetails] NVARCHAR(MAX) NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL
    );
    
    CREATE INDEX [IX_MessageSubscriptions_MessageId] ON [dbo].[MessageSubscriptions]([MessageId]);
    CREATE INDEX [IX_MessageSubscriptions_MessageId_Subscriber] ON [dbo].[MessageSubscriptions]([MessageId], [SubscriberAdapterName]);
    CREATE INDEX [IX_MessageSubscriptions_Status] ON [dbo].[MessageSubscriptions]([Status]);
    
    PRINT 'MessageSubscriptions table created successfully';
END
ELSE
BEGIN
    PRINT 'MessageSubscriptions table already exists';
END
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
        [InterfaceName] NVARCHAR(200) NULL, -- Link to interface if applicable
        [MessageId] UNIQUEIDENTIFIER NULL -- Link to message if applicable
    );
    
    CREATE INDEX [IX_ProcessLogs_datetime_created] ON [dbo].[ProcessLogs]([datetime_created] DESC);
    CREATE INDEX [IX_ProcessLogs_Level] ON [dbo].[ProcessLogs]([Level]);
    CREATE INDEX [IX_ProcessLogs_InterfaceName] ON [dbo].[ProcessLogs]([InterfaceName]);
    CREATE INDEX [IX_ProcessLogs_MessageId] ON [dbo].[ProcessLogs]([MessageId]);
    
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

PRINT 'MessageBox database initialization completed successfully';
GO



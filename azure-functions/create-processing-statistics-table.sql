-- Create ProcessingStatistics table in InterfaceConfigDb database
-- Run this script if the table doesn't exist after deployment
-- Updated to include additional statistics columns

USE [InterfaceConfigDb]; -- Replace with your actual database name
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessingStatistics')
BEGIN
    CREATE TABLE [dbo].[ProcessingStatistics] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [InterfaceName] NVARCHAR(200) NOT NULL,
        [RowsProcessed] INT NOT NULL,
        [RowsSucceeded] INT NOT NULL,
        [RowsFailed] INT NOT NULL,
        [ProcessingDurationMs] BIGINT NOT NULL,
        [ProcessingStartTime] DATETIME2 NOT NULL,
        [ProcessingEndTime] DATETIME2 NOT NULL,
        [SourceFile] NVARCHAR(500) NULL,
        [AdapterType] NVARCHAR(50) NULL,
        [AdapterName] NVARCHAR(100) NULL,
        [AdapterInstanceGuid] UNIQUEIDENTIFIER NULL,
        [SourceName] NVARCHAR(500) NULL,
        [DestinationName] NVARCHAR(500) NULL,
        [BatchSize] INT NULL,
        [UseTransaction] BIT NULL,
        [RowsPerSecond] FLOAT NULL
    );
    
    -- Create indexes for better query performance
    CREATE INDEX [IX_ProcessingStatistics_InterfaceName] 
        ON [dbo].[ProcessingStatistics] ([InterfaceName]);
    
    CREATE INDEX [IX_ProcessingStatistics_ProcessingEndTime] 
        ON [dbo].[ProcessingStatistics] ([ProcessingEndTime]);
    
    CREATE INDEX [IX_ProcessingStatistics_InterfaceName_ProcessingEndTime] 
        ON [dbo].[ProcessingStatistics] ([InterfaceName], [ProcessingEndTime]);
    
    CREATE INDEX [IX_ProcessingStatistics_AdapterType] 
        ON [dbo].[ProcessingStatistics] ([AdapterType]);
    
    CREATE INDEX [IX_ProcessingStatistics_AdapterInstanceGuid] 
        ON [dbo].[ProcessingStatistics] ([AdapterInstanceGuid]);
    
    CREATE INDEX [IX_ProcessingStatistics_InterfaceName_AdapterType] 
        ON [dbo].[ProcessingStatistics] ([InterfaceName], [AdapterType]);
    
    PRINT 'ProcessingStatistics table created successfully';
END
ELSE
BEGIN
    -- Add new columns if they don't exist (for existing tables)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProcessingStatistics') AND name = 'AdapterType')
    BEGIN
        ALTER TABLE [dbo].[ProcessingStatistics] ADD [AdapterType] NVARCHAR(50) NULL;
        PRINT 'Added AdapterType column';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProcessingStatistics') AND name = 'AdapterName')
    BEGIN
        ALTER TABLE [dbo].[ProcessingStatistics] ADD [AdapterName] NVARCHAR(100) NULL;
        PRINT 'Added AdapterName column';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProcessingStatistics') AND name = 'AdapterInstanceGuid')
    BEGIN
        ALTER TABLE [dbo].[ProcessingStatistics] ADD [AdapterInstanceGuid] UNIQUEIDENTIFIER NULL;
        PRINT 'Added AdapterInstanceGuid column';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProcessingStatistics') AND name = 'SourceName')
    BEGIN
        ALTER TABLE [dbo].[ProcessingStatistics] ADD [SourceName] NVARCHAR(500) NULL;
        PRINT 'Added SourceName column';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProcessingStatistics') AND name = 'DestinationName')
    BEGIN
        ALTER TABLE [dbo].[ProcessingStatistics] ADD [DestinationName] NVARCHAR(500) NULL;
        PRINT 'Added DestinationName column';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProcessingStatistics') AND name = 'BatchSize')
    BEGIN
        ALTER TABLE [dbo].[ProcessingStatistics] ADD [BatchSize] INT NULL;
        PRINT 'Added BatchSize column';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProcessingStatistics') AND name = 'UseTransaction')
    BEGIN
        ALTER TABLE [dbo].[ProcessingStatistics] ADD [UseTransaction] BIT NULL;
        PRINT 'Added UseTransaction column';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProcessingStatistics') AND name = 'RowsPerSecond')
    BEGIN
        ALTER TABLE [dbo].[ProcessingStatistics] ADD [RowsPerSecond] FLOAT NULL;
        PRINT 'Added RowsPerSecond column';
    END
    
    -- Create indexes for new columns if they don't exist
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProcessingStatistics_AdapterType' AND object_id = OBJECT_ID('dbo.ProcessingStatistics'))
    BEGIN
        CREATE INDEX [IX_ProcessingStatistics_AdapterType] 
            ON [dbo].[ProcessingStatistics] ([AdapterType]);
        PRINT 'Created index IX_ProcessingStatistics_AdapterType';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProcessingStatistics_AdapterInstanceGuid' AND object_id = OBJECT_ID('dbo.ProcessingStatistics'))
    BEGIN
        CREATE INDEX [IX_ProcessingStatistics_AdapterInstanceGuid] 
            ON [dbo].[ProcessingStatistics] ([AdapterInstanceGuid]);
        PRINT 'Created index IX_ProcessingStatistics_AdapterInstanceGuid';
    END
    
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProcessingStatistics_InterfaceName_AdapterType' AND object_id = OBJECT_ID('dbo.ProcessingStatistics'))
    BEGIN
        CREATE INDEX [IX_ProcessingStatistics_InterfaceName_AdapterType] 
            ON [dbo].[ProcessingStatistics] ([InterfaceName], [AdapterType]);
        PRINT 'Created index IX_ProcessingStatistics_InterfaceName_AdapterType';
    END
    
    PRINT 'ProcessingStatistics table updated successfully';
END
GO

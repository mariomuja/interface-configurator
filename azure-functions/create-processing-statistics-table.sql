-- Create ProcessingStatistics table in MessageBox database
-- Run this script if the table doesn't exist after deployment

USE [MessageBox]; -- Replace with your actual MessageBox database name
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
        [SourceFile] NVARCHAR(500) NULL
    );
    
    -- Create indexes for better query performance
    CREATE INDEX [IX_ProcessingStatistics_InterfaceName] 
        ON [dbo].[ProcessingStatistics] ([InterfaceName]);
    
    CREATE INDEX [IX_ProcessingStatistics_ProcessingEndTime] 
        ON [dbo].[ProcessingStatistics] ([ProcessingEndTime]);
    
    CREATE INDEX [IX_ProcessingStatistics_InterfaceName_ProcessingEndTime] 
        ON [dbo].[ProcessingStatistics] ([InterfaceName], [ProcessingEndTime]);
    
    PRINT 'ProcessingStatistics table created successfully';
END
ELSE
BEGIN
    PRINT 'ProcessingStatistics table already exists';
END
GO


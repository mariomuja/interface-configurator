-- Remove MessageId column from ProcessLogs table
-- This is needed because the Messages table has been removed

USE [InterfaceConfigDb]
GO

-- Drop MessageId index if it exists
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProcessLogs_MessageId' AND object_id = OBJECT_ID(N'[dbo].[ProcessLogs]'))
BEGIN
    DROP INDEX [IX_ProcessLogs_MessageId] ON [dbo].[ProcessLogs];
    PRINT 'Dropped IX_ProcessLogs_MessageId index';
END
ELSE
BEGIN
    PRINT 'IX_ProcessLogs_MessageId index does not exist';
END
GO

-- Drop MessageId column if it exists
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProcessLogs]') AND name = 'MessageId')
BEGIN
    ALTER TABLE [dbo].[ProcessLogs]
    DROP COLUMN [MessageId];
    PRINT 'Removed MessageId column from ProcessLogs table';
END
ELSE
BEGIN
    PRINT 'MessageId column does not exist in ProcessLogs table';
END
GO

PRINT 'ProcessLogs table update completed successfully';
GO


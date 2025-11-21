-- MessageBox Database Update Script
-- This script adds missing columns to existing Messages table
-- Run this if the Messages table already exists but is missing required columns

USE [MessageBox]
GO

-- Add AdapterInstanceGuid column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND name = 'AdapterInstanceGuid')
BEGIN
    ALTER TABLE [dbo].[Messages]
    ADD [AdapterInstanceGuid] UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    PRINT 'Added AdapterInstanceGuid column to Messages table';
END
ELSE
BEGIN
    PRINT 'AdapterInstanceGuid column already exists';
END
GO

-- Add MessageHash column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND name = 'MessageHash')
BEGIN
    ALTER TABLE [dbo].[Messages]
    ADD [MessageHash] NVARCHAR(64) NULL;
    PRINT 'Added MessageHash column to Messages table';
END
ELSE
BEGIN
    PRINT 'MessageHash column already exists';
END
GO

-- Add RetryCount column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND name = 'RetryCount')
BEGIN
    ALTER TABLE [dbo].[Messages]
    ADD [RetryCount] INT NOT NULL DEFAULT 0;
    PRINT 'Added RetryCount column to Messages table';
END
ELSE
BEGIN
    PRINT 'RetryCount column already exists';
END
GO

-- Add MaxRetries column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND name = 'MaxRetries')
BEGIN
    ALTER TABLE [dbo].[Messages]
    ADD [MaxRetries] INT NOT NULL DEFAULT 3;
    PRINT 'Added MaxRetries column to Messages table';
END
ELSE
BEGIN
    PRINT 'MaxRetries column already exists';
END
GO

-- Add InProgressUntil column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND name = 'InProgressUntil')
BEGIN
    ALTER TABLE [dbo].[Messages]
    ADD [InProgressUntil] DATETIME2 NULL;
    PRINT 'Added InProgressUntil column to Messages table';
END
ELSE
BEGIN
    PRINT 'InProgressUntil column already exists';
END
GO

-- Add LastRetryTime column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND name = 'LastRetryTime')
BEGIN
    ALTER TABLE [dbo].[Messages]
    ADD [LastRetryTime] DATETIME2 NULL;
    PRINT 'Added LastRetryTime column to Messages table';
END
ELSE
BEGIN
    PRINT 'LastRetryTime column already exists';
END
GO

-- Add DeadLetter column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND name = 'DeadLetter')
BEGIN
    ALTER TABLE [dbo].[Messages]
    ADD [DeadLetter] BIT NOT NULL DEFAULT 0;
    PRINT 'Added DeadLetter column to Messages table';
END
ELSE
BEGIN
    PRINT 'DeadLetter column already exists';
END
GO

-- Create indexes if they don't exist
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Messages_AdapterInstanceGuid' AND object_id = OBJECT_ID(N'[dbo].[Messages]'))
BEGIN
    CREATE INDEX [IX_Messages_AdapterInstanceGuid] ON [dbo].[Messages]([AdapterInstanceGuid]);
    PRINT 'Created index IX_Messages_AdapterInstanceGuid';
END
ELSE
BEGIN
    PRINT 'Index IX_Messages_AdapterInstanceGuid already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Messages_MessageHash' AND object_id = OBJECT_ID(N'[dbo].[Messages]'))
BEGIN
    CREATE INDEX [IX_Messages_MessageHash] ON [dbo].[Messages]([MessageHash]);
    PRINT 'Created index IX_Messages_MessageHash';
END
ELSE
BEGIN
    PRINT 'Index IX_Messages_MessageHash already exists';
END
GO

PRINT 'MessageBox database update completed successfully';
GO


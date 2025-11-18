-- Migration Script: Add datetime_created column to all tables
-- Every SQL table MUST have a datetime_created column of type DateTime with DEFAULT GETUTCDATE()

-- Migrate TransportData table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND type in (N'U'))
BEGIN
    -- Check if datetime_created column exists
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND name = 'datetime_created')
    BEGIN
        PRINT 'Adding datetime_created column to TransportData table...';
        
        -- Add datetime_created column with default
        ALTER TABLE [dbo].[TransportData]
        ADD [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE();
        
        -- Copy data from CreatedAt if it exists
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND name = 'CreatedAt')
        BEGIN
            UPDATE [dbo].[TransportData]
            SET [datetime_created] = [CreatedAt];
            
            -- Drop old CreatedAt column
            ALTER TABLE [dbo].[TransportData]
            DROP COLUMN [CreatedAt];
        END
        
        -- Drop old index if exists
        IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TransportData_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[TransportData]'))
        BEGIN
            DROP INDEX [IX_TransportData_CreatedAt] ON [dbo].[TransportData];
        END
        
        -- Create new index on datetime_created
        CREATE INDEX [IX_TransportData_datetime_created] ON [dbo].[TransportData]([datetime_created]);
        
        PRINT 'TransportData table migrated successfully';
    END
    ELSE
    BEGIN
        PRINT 'TransportData table already has datetime_created column';
    END
END
GO

-- Migrate ProcessLogs table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessLogs]') AND type in (N'U'))
BEGIN
    -- Check if datetime_created column exists
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProcessLogs]') AND name = 'datetime_created')
    BEGIN
        PRINT 'Adding datetime_created column to ProcessLogs table...';
        
        -- Add datetime_created column with default
        ALTER TABLE [dbo].[ProcessLogs]
        ADD [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE();
        
        -- Copy data from Timestamp if it exists
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProcessLogs]') AND name = 'Timestamp')
        BEGIN
            UPDATE [dbo].[ProcessLogs]
            SET [datetime_created] = [Timestamp];
            
            -- Drop old Timestamp column
            ALTER TABLE [dbo].[ProcessLogs]
            DROP COLUMN [Timestamp];
        END
        
        -- Drop old index if exists
        IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProcessLogs_Timestamp' AND object_id = OBJECT_ID(N'[dbo].[ProcessLogs]'))
        BEGIN
            DROP INDEX [IX_ProcessLogs_Timestamp] ON [dbo].[ProcessLogs];
        END
        
        -- Create new index on datetime_created
        CREATE INDEX [IX_ProcessLogs_datetime_created] ON [dbo].[ProcessLogs]([datetime_created] DESC);
        
        PRINT 'ProcessLogs table migrated successfully';
    END
    ELSE
    BEGIN
        PRINT 'ProcessLogs table already has datetime_created column';
    END
END
GO

PRINT 'Migration to datetime_created completed successfully';
GO







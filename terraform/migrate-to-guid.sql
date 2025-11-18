-- Migration Script: Convert TransportData table to use GUID primary key
-- This script updates the existing table structure to match the new requirements:
-- 1. Primary key is GUID with DEFAULT NEWID()
-- 2. All CSV columns are stored in CsvDataJson column

-- Check if table exists and has old structure
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND type in (N'U'))
BEGIN
    -- Check if table needs migration (has old INT IDENTITY column)
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND name = 'Id' AND system_type_id = 56) -- INT type
    BEGIN
        PRINT 'Migrating TransportData table from INT IDENTITY to GUID...';
        
        -- Drop existing table (data will be lost - this is expected for migration)
        DROP TABLE [dbo].[TransportData];
        
        PRINT 'Old TransportData table dropped';
    END
END

-- Create TransportData table with GUID primary key
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
    
    PRINT 'TransportData table created successfully with GUID primary key';
END
ELSE
BEGIN
    PRINT 'TransportData table already exists with correct structure';
END
GO

PRINT 'Migration completed successfully';
GO







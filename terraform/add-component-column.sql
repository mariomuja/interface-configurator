-- Add Component column to ProcessLogs table if it doesn't exist
-- This column stores Azure component information (ResourceGroup/FunctionApp/FunctionName)

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessLogs]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProcessLogs]') AND name = 'Component')
    BEGIN
        PRINT 'Adding Component column to ProcessLogs table...';
        ALTER TABLE [dbo].[ProcessLogs]
        ADD [Component] NVARCHAR(200) NULL;
        PRINT 'Component column added successfully';
    END
    ELSE
    BEGIN
        PRINT 'Component column already exists in ProcessLogs table';
    END
END
ELSE
BEGIN
    PRINT 'ProcessLogs table does not exist. Component column will be created when the table is created.';
END
GO


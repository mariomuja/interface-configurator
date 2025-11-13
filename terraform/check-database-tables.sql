-- Check if database tables exist
-- Run this script to verify if TransportData and ProcessLogs tables are already created

-- Check TransportData table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND type in (N'U'))
BEGIN
    SELECT 'TransportData table EXISTS' AS Status;
    SELECT COUNT(*) AS RowCount FROM TransportData;
END
ELSE
BEGIN
    SELECT 'TransportData table DOES NOT EXIST' AS Status;
END
GO

-- Check ProcessLogs table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessLogs]') AND type in (N'U'))
BEGIN
    SELECT 'ProcessLogs table EXISTS' AS Status;
    SELECT COUNT(*) AS RowCount FROM ProcessLogs;
END
ELSE
BEGIN
    SELECT 'ProcessLogs table DOES NOT EXIST' AS Status;
END
GO




-- Check MessageBox Database Data
-- This script checks if data exists in the MessageBox tables

USE [MessageBox]
GO

-- Check Messages table structure
PRINT '=== Messages Table Structure ===';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Messages'
ORDER BY ORDINAL_POSITION;
GO

-- Check if Messages table has data
PRINT '';
PRINT '=== Messages Table Row Count ===';
SELECT COUNT(*) AS TotalMessages FROM [dbo].[Messages];
GO

-- Check messages by interface
PRINT '';
PRINT '=== Messages by Interface ===';
SELECT 
    InterfaceName,
    COUNT(*) AS MessageCount,
    MIN(datetime_created) AS FirstMessage,
    MAX(datetime_created) AS LastMessage
FROM [dbo].[Messages]
GROUP BY InterfaceName
ORDER BY MessageCount DESC;
GO

-- Check messages by status
PRINT '';
PRINT '=== Messages by Status ===';
SELECT 
    Status,
    COUNT(*) AS MessageCount
FROM [dbo].[Messages]
GROUP BY Status
ORDER BY MessageCount DESC;
GO

-- Check messages by adapter
PRINT '';
PRINT '=== Messages by Adapter ===';
SELECT 
    AdapterName,
    AdapterType,
    COUNT(*) AS MessageCount
FROM [dbo].[Messages]
GROUP BY AdapterName, AdapterType
ORDER BY MessageCount DESC;
GO

-- Show recent messages (last 10)
PRINT '';
PRINT '=== Recent Messages (Last 10) ===';
SELECT TOP 10
    MessageId,
    InterfaceName,
    AdapterName,
    AdapterType,
    Status,
    datetime_created,
    LEN(MessageData) AS MessageDataLength
FROM [dbo].[Messages]
ORDER BY datetime_created DESC;
GO

-- Check MessageSubscriptions table
PRINT '';
PRINT '=== MessageSubscriptions Table Row Count ===';
SELECT COUNT(*) AS TotalSubscriptions FROM [dbo].[MessageSubscriptions];
GO

-- Check AdapterInstances table
PRINT '';
PRINT '=== AdapterInstances Table Row Count ===';
SELECT COUNT(*) AS TotalAdapterInstances FROM [dbo].[AdapterInstances];
GO

-- Show adapter instances
PRINT '';
PRINT '=== Adapter Instances ===';
SELECT 
    AdapterInstanceGuid,
    InterfaceName,
    InstanceName,
    AdapterName,
    AdapterType,
    IsEnabled
FROM [dbo].[AdapterInstances]
ORDER BY InterfaceName, AdapterType;
GO

-- Check if AdapterInstanceGuid column exists
PRINT '';
PRINT '=== Check for AdapterInstanceGuid Column ===';
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND name = 'AdapterInstanceGuid')
BEGIN
    PRINT 'AdapterInstanceGuid column EXISTS';
    
    -- Check if there are messages with empty GUIDs
    SELECT COUNT(*) AS MessagesWithEmptyGuid
    FROM [dbo].[Messages]
    WHERE AdapterInstanceGuid = '00000000-0000-0000-0000-000000000000';
END
ELSE
BEGIN
    PRINT 'AdapterInstanceGuid column DOES NOT EXIST - This is the problem!';
    PRINT 'The Messages table is missing required columns.';
    PRINT 'Run update-messagebox-database.sql to add missing columns.';
END
GO


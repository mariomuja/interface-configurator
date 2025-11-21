-- Check MessageBox Database Tables
-- Run this script to verify if data is being written to MessageBox

USE [MessageBox]
GO

-- Check Messages table
SELECT 
    COUNT(*) AS TotalMessages,
    COUNT(DISTINCT InterfaceName) AS UniqueInterfaces,
    COUNT(DISTINCT AdapterName) AS UniqueAdapters,
    MIN(datetime_created) AS OldestMessage,
    MAX(datetime_created) AS NewestMessage
FROM [dbo].[Messages]
GO

-- Check messages by interface
SELECT 
    InterfaceName,
    AdapterName,
    AdapterType,
    Status,
    COUNT(*) AS MessageCount,
    MIN(datetime_created) AS FirstMessage,
    MAX(datetime_created) AS LastMessage
FROM [dbo].[Messages]
GROUP BY InterfaceName, AdapterName, AdapterType, Status
ORDER BY MAX(datetime_created) DESC
GO

-- Check recent messages (last 24 hours)
SELECT TOP 20
    MessageId,
    InterfaceName,
    AdapterName,
    AdapterType,
    Status,
    datetime_created,
    LEFT(MessageData, 100) AS MessageDataPreview
FROM [dbo].[Messages]
WHERE datetime_created > DATEADD(hour, -24, GETUTCDATE())
ORDER BY datetime_created DESC
GO

-- Check if AdapterInstanceGuid column exists
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Messages'
ORDER BY ORDINAL_POSITION
GO

-- Check MessageSubscriptions table
SELECT 
    COUNT(*) AS TotalSubscriptions,
    COUNT(DISTINCT MessageId) AS UniqueMessages,
    COUNT(DISTINCT SubscriberAdapterName) AS UniqueSubscribers
FROM [dbo].[MessageSubscriptions]
GO

-- Check AdapterInstances table
SELECT 
    COUNT(*) AS TotalInstances,
    COUNT(DISTINCT InterfaceName) AS UniqueInterfaces
FROM [dbo].[AdapterInstances]
GO

-- List all adapter instances
SELECT 
    AdapterInstanceGuid,
    InterfaceName,
    InstanceName,
    AdapterName,
    AdapterType,
    IsEnabled
FROM [dbo].[AdapterInstances]
ORDER BY InterfaceName, AdapterType
GO


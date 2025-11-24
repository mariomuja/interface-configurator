-- Drop Messages and Subscription Tables
-- This script removes the Messages, MessageSubscriptions, AdapterSubscriptions, and MessageProcessing tables
-- These tables are no longer used - messaging is handled via Azure Service Bus

USE [InterfaceConfigDb]
GO

-- Drop MessageProcessing table if it exists
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MessageProcessing]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[MessageProcessing];
    PRINT 'Dropped MessageProcessing table';
END
ELSE
BEGIN
    PRINT 'MessageProcessing table does not exist';
END
GO

-- Drop AdapterSubscriptions table if it exists
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdapterSubscriptions]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[AdapterSubscriptions];
    PRINT 'Dropped AdapterSubscriptions table';
END
ELSE
BEGIN
    PRINT 'AdapterSubscriptions table does not exist';
END
GO

-- Drop MessageSubscriptions table if it exists
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MessageSubscriptions]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[MessageSubscriptions];
    PRINT 'Dropped MessageSubscriptions table';
END
ELSE
BEGIN
    PRINT 'MessageSubscriptions table does not exist';
END
GO

-- Drop Messages table if it exists
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[Messages];
    PRINT 'Dropped Messages table';
END
ELSE
BEGIN
    PRINT 'Messages table does not exist';
END
GO

PRINT 'Message and subscription tables removal completed successfully';
GO


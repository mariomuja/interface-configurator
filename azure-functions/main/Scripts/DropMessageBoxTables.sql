-- Drop MessageBox tables that are no longer needed after migration to Service Bus
-- These tables were used for message staging but are now replaced by Azure Service Bus

USE InterfaceConfigDb;
GO

-- Drop MessageProcessing table (tracks message processing status)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MessageProcessing]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[MessageProcessing];
    PRINT 'MessageProcessing table dropped successfully.';
END
ELSE
BEGIN
    PRINT 'MessageProcessing table does not exist.';
END
GO

-- Drop MessageSubscriptions table (tracks which adapters subscribed to messages)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MessageSubscriptions]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[MessageSubscriptions];
    PRINT 'MessageSubscriptions table dropped successfully.';
END
ELSE
BEGIN
    PRINT 'MessageSubscriptions table does not exist.';
END
GO

-- Drop AdapterSubscriptions table (BizTalk-style subscription filters)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdapterSubscriptions]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[AdapterSubscriptions];
    PRINT 'AdapterSubscriptions table dropped successfully.';
END
ELSE
BEGIN
    PRINT 'AdapterSubscriptions table does not exist.';
END
GO

-- Drop Messages table (main message staging table)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Messages]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[Messages];
    PRINT 'Messages table dropped successfully.';
END
ELSE
BEGIN
    PRINT 'Messages table does not exist.';
END
GO

PRINT 'MessageBox table cleanup completed. All messaging is now handled via Azure Service Bus.';
GO


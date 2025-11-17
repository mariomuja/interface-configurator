-- Create AdapterSettings table for persistent adapter configuration
-- This allows configuring adapters (CSV, JSON, SAP, SQL Server, etc.) independently
-- Similar to Logic Apps connectors - enables swapping source and destination adapters

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdapterSettings]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AdapterSettings] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [AdapterName] NVARCHAR(100) NOT NULL,
        [AdapterType] NVARCHAR(50) NOT NULL,
        [SettingKey] NVARCHAR(200) NOT NULL,
        [SettingValue] NVARCHAR(1000) NULL,
        [Description] NVARCHAR(500) NULL,
        [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [datetime_updated] DATETIME2 NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        CONSTRAINT [UQ_AdapterSettings] UNIQUE ([AdapterName], [AdapterType], [SettingKey], [IsActive])
    );
    
    CREATE INDEX [IX_AdapterSettings_Adapter] ON [dbo].[AdapterSettings]([AdapterName], [AdapterType], [IsActive]);
    
    -- Insert default CSV separator configuration
    INSERT INTO [dbo].[AdapterSettings] ([AdapterName], [AdapterType], [SettingKey], [SettingValue], [Description], [IsActive])
    VALUES ('CSV', 'Source', 'FieldSeparator', '║', 'CSV field separator: Box Drawing Double Vertical Line (U+2551)', 1);
    
    PRINT 'AdapterSettings table created successfully with default CSV separator';
END
ELSE
BEGIN
    -- Ensure default CSV separator exists
    IF NOT EXISTS (SELECT * FROM [dbo].[AdapterSettings] 
                   WHERE [AdapterName] = 'CSV' 
                   AND [AdapterType] = 'Source' 
                   AND [SettingKey] = 'FieldSeparator' 
                   AND [IsActive] = 1)
    BEGIN
        INSERT INTO [dbo].[AdapterSettings] ([AdapterName], [AdapterType], [SettingKey], [SettingValue], [Description], [IsActive])
        VALUES ('CSV', 'Source', 'FieldSeparator', '║', 'CSV field separator: Box Drawing Double Vertical Line (U+2551)', 1);
        PRINT 'Default CSV separator added to AdapterSettings';
    END
    ELSE
    BEGIN
        PRINT 'AdapterSettings table already exists';
    END
END
GO


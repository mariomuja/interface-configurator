-- Drop TransportData table to remove old structure
-- This will allow the Function App to recreate it with the correct structure (PrimaryKey instead of Id)

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND type in (N'U'))
BEGIN
    DROP TABLE [dbo].[TransportData];
    PRINT 'TransportData table dropped successfully';
END
ELSE
BEGIN
    PRINT 'TransportData table does not exist';
END
GO







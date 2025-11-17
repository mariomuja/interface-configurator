# Database Initialization Script
# This script creates the required tables: TransportData and ProcessLogs

param(
    [Parameter(Mandatory=$true)]
    [string]$SqlPassword
)

# Get SQL connection details from Azure
Write-Host "`n=== Hole SQL-Verbindungsdaten ===" -ForegroundColor Cyan
$sqlServer = az sql server list --resource-group rg-infrastructure-as-code --query "[0].fullyQualifiedDomainName" -o tsv
$sqlDatabase = az sql db list --resource-group rg-infrastructure-as-code --server (az sql server list --resource-group rg-infrastructure-as-code --query "[0].name" -o tsv) --query "[0].name" -o tsv
$sqlAdmin = az sql server list --resource-group rg-infrastructure-as-code --query "[0].administratorLogin" -o tsv

Write-Host "SQL Server: $sqlServer" -ForegroundColor White
Write-Host "SQL Database: $sqlDatabase" -ForegroundColor White
Write-Host "SQL Admin: $sqlAdmin" -ForegroundColor White

# SQL Script to create tables
$sqlScript = @"
-- Create TransportData table
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
    PRINT 'TransportData table created successfully';
END
ELSE
BEGIN
    PRINT 'TransportData table already exists';
END

-- Create ProcessLogs table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[ProcessLogs] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Level] NVARCHAR(50) NOT NULL,
        [Message] NVARCHAR(MAX) NOT NULL,
        [Details] NVARCHAR(MAX) NULL
    );
    CREATE INDEX [IX_ProcessLogs_Timestamp] ON [dbo].[ProcessLogs]([Timestamp] DESC);
    CREATE INDEX [IX_ProcessLogs_Level] ON [dbo].[ProcessLogs]([Level]);
    PRINT 'ProcessLogs table created successfully';
END
ELSE
BEGIN
    PRINT 'ProcessLogs table already exists';
END

SELECT 'Database initialization completed' AS Status;
"@

# Save script to temporary file
$scriptFile = "terraform/init-database-temp.sql"
$sqlScript | Out-File -FilePath $scriptFile -Encoding UTF8 -Force

Write-Host "`n=== Führe SQL-Script aus ===" -ForegroundColor Cyan
$result = sqlcmd -S $sqlServer -d $sqlDatabase -U $sqlAdmin -P $SqlPassword -i $scriptFile -W 2>&1

Write-Host $result

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ Datenbank-Initialisierung erfolgreich!" -ForegroundColor Green
    
    # Verify tables were created
    Write-Host "`n=== Prüfe erstellte Tabellen ===" -ForegroundColor Cyan
    $verifyQuery = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'dbo' ORDER BY TABLE_NAME"
    $verifyScript = "terraform/verify-tables-temp.sql"
    $verifyQuery | Out-File -FilePath $verifyScript -Encoding UTF8 -Force
    $tables = sqlcmd -S $sqlServer -d $sqlDatabase -U $sqlAdmin -P $SqlPassword -i $verifyScript -W -h -1 2>&1
    Write-Host "Gefundene Tabellen:" -ForegroundColor White
    Write-Host $tables
    
    # Cleanup
    Remove-Item $scriptFile -ErrorAction SilentlyContinue
    Remove-Item $verifyScript -ErrorAction SilentlyContinue
} else {
    Write-Host "`n⚠️  Fehler bei der Initialisierung" -ForegroundColor Red
    Remove-Item $scriptFile -ErrorAction SilentlyContinue
    exit 1
}


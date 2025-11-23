# Initialize Databases and Restore Demo Interface Configuration
# This script:
# 1. Initializes the app-database (TransportData table)
# 2. Initializes the MessageBox database (Messages, MessageSubscriptions, AdapterInstances tables)
# 3. Restores the demo interface configuration to Blob Storage

param(
    [string]$ResourceGroupName = "rg-interface-configurator",
    [string]$SqlServerName = "sql-main-database",
    [string]$SqlAdminLogin = "sqladmin",
    [string]$SqlAdminPassword = "InfrastructureAsCode2024!Secure",
    [string]$StorageAccountName = "stappgeneral",
    [string]$FunctionAppName = "func-integration-main"
)

Write-Host "`n=== Initializing Databases and Restoring Demo Interface ===" -ForegroundColor Cyan

# Get SQL Server FQDN
Write-Host "`n[1/4] Getting SQL Server information..." -ForegroundColor Yellow
$sqlServer = az sql server show --resource-group $ResourceGroupName --name $SqlServerName --query "fullyQualifiedDomainName" -o tsv 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error getting SQL Server: $sqlServer" -ForegroundColor Red
    exit 1
}
Write-Host "✅ SQL Server: $sqlServer" -ForegroundColor Green

# Get storage account connection string
Write-Host "`n[2/4] Getting storage account connection string..." -ForegroundColor Yellow
$storageConnectionString = az storage account show-connection-string --resource-group $ResourceGroupName --name $StorageAccountName --query "connectionString" -o tsv 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error getting storage connection string: $storageConnectionString" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Storage account connection string retrieved" -ForegroundColor Green

# Initialize app-database
Write-Host "`n[3/4] Initializing app-database..." -ForegroundColor Yellow
$initAppDbScript = @"
USE [app-database]
GO

-- Create TransportData table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TransportData] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [CsvDataJson] NVARCHAR(MAX) NOT NULL,
        [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    
    CREATE INDEX [IX_TransportData_datetime_created] ON [dbo].[TransportData]([datetime_created]);
    
    PRINT 'TransportData table created successfully';
END
ELSE
BEGIN
    PRINT 'TransportData table already exists';
END
GO
"@

$initAppDbScript | Out-File -FilePath "$env:TEMP\init-app-database.sql" -Encoding UTF8
Write-Host "Running initialization script on app-database..." -ForegroundColor Gray

$sqlcmdResult = sqlcmd -S $sqlServer -d "app-database" -U $SqlAdminLogin -P $SqlAdminPassword -i "$env:TEMP\init-app-database.sql" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ app-database initialized successfully" -ForegroundColor Green
} else {
    Write-Host "⚠️  Warning: $sqlcmdResult" -ForegroundColor Yellow
}

# Initialize MessageBox database
Write-Host "`n[4/4] Initializing MessageBox database..." -ForegroundColor Yellow
$initMessageBoxScript = Get-Content -Path "terraform\init-messagebox-database.sql" -Raw
$initMessageBoxScript | Out-File -FilePath "$env:TEMP\init-messagebox-database.sql" -Encoding UTF8
Write-Host "Running initialization script on MessageBox database..." -ForegroundColor Gray

$sqlcmdResult = sqlcmd -S $sqlServer -d "MessageBox" -U $SqlAdminLogin -P $SqlAdminPassword -i "$env:TEMP\init-messagebox-database.sql" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ MessageBox database initialized successfully" -ForegroundColor Green
} else {
    Write-Host "⚠️  Warning: $sqlcmdResult" -ForegroundColor Yellow
}

# Create demo interface configuration
Write-Host "`n[5/5] Creating demo interface configuration..." -ForegroundColor Yellow

$demoConfig = @{
    InterfaceName = "FromCsvToSqlServerExample"
    Description = "Demo interface: CSV Source to SQL Server Destination"
    CreatedAt = (Get-Date).ToUniversalTime().ToString("o")
    UpdatedAt = (Get-Date).ToUniversalTime().ToString("o")
    Sources = @{
        "CSV Source" = @{
            InstanceName = "CSV Source"
            AdapterName = "CSV"
            IsEnabled = $true
            AdapterInstanceGuid = [guid]::NewGuid().ToString()
            Configuration = '{"source":"csv-files/csv-incoming","enabled":true}'
            SourceReceiveFolder = "csv-incoming"
            SourceFileMask = "*.txt"
            SourceBatchSize = 100
            SourceFieldSeparator = "║"
            CsvPollingInterval = 10
            CsvAdapterType = "FILE"
            CreatedAt = (Get-Date).ToUniversalTime().ToString("o")
            UpdatedAt = (Get-Date).ToUniversalTime().ToString("o")
        }
    }
    Destinations = @{
        "SQL Destination" = @{
            InstanceName = "SQL Destination"
            AdapterName = "SqlServer"
            IsEnabled = $true
            AdapterInstanceGuid = [guid]::NewGuid().ToString()
            Configuration = (@{
                destination = "TransportData"
                tableName = "TransportData"
                sqlServerName = $sqlServer
                sqlDatabaseName = "app-database"
                sqlUserName = $SqlAdminLogin
                sqlPassword = $SqlAdminPassword
                sqlIntegratedSecurity = $false
            } | ConvertTo-Json -Compress)
            SqlServerName = $sqlServer
            SqlDatabaseName = "app-database"
            SqlUserName = $SqlAdminLogin
            SqlPassword = $SqlAdminPassword
            SqlIntegratedSecurity = $false
            SqlTableName = "TransportData"
            SqlUseTransaction = $false
            SqlBatchSize = 1000
            SqlCommandTimeout = 30
            SqlFailOnBadStatement = $false
            CreatedAt = (Get-Date).ToUniversalTime().ToString("o")
            UpdatedAt = (Get-Date).ToUniversalTime().ToString("o")
        }
    }
}

# Read existing configurations or create new list
$configsJson = @"
[
  $($demoConfig | ConvertTo-Json -Depth 10)
]
"@

# Upload to Blob Storage
Write-Host "Uploading demo interface configuration to Blob Storage..." -ForegroundColor Gray
$configsJson | Out-File -FilePath "$env:TEMP\interface-configurations.json" -Encoding UTF8

az storage blob upload `
    --account-name $StorageAccountName `
    --container-name "function-config" `
    --name "interface-configurations.json" `
    --file "$env:TEMP\interface-configurations.json" `
    --connection-string $storageConnectionString `
    --overwrite 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Demo interface configuration uploaded successfully" -ForegroundColor Green
    Write-Host "   Interface Name: FromCsvToSqlServerExample" -ForegroundColor Cyan
    Write-Host "   Source: CSV (csv-incoming folder)" -ForegroundColor Cyan
    Write-Host "   Destination: SQL Server (TransportData table)" -ForegroundColor Cyan
} else {
    Write-Host "❌ Error uploading configuration" -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Initialization Complete! ===" -ForegroundColor Green
Write-Host "✅ app-database initialized" -ForegroundColor Green
Write-Host "✅ MessageBox database initialized" -ForegroundColor Green
Write-Host "✅ Demo interface configuration restored" -ForegroundColor Green


# Diagnose MessageBox Database and Data Flow
# This script checks if data is being written to MessageBox

Write-Host "=== MessageBox Database Diagnosis ===" -ForegroundColor Cyan
Write-Host ""

# Check if we can connect to the database
$sqlServer = az sql server list --query "[0].fullyQualifiedDomainName" -o tsv
$sqlDatabase = "MessageBox"
$sqlUser = az sql server list --query "[0].administratorLogin" -o tsv

Write-Host "SQL Server: $sqlServer" -ForegroundColor Yellow
Write-Host "Database: $sqlDatabase" -ForegroundColor Yellow
Write-Host "User: $sqlUser" -ForegroundColor Yellow
Write-Host ""

# Check table structure
Write-Host "Checking Messages table structure..." -ForegroundColor Cyan
$checkColumns = @"
USE [MessageBox]
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Messages'
ORDER BY ORDINAL_POSITION
"@

Write-Host "Table Columns:" -ForegroundColor Green
az sql db query --server $sqlServer --database $sqlDatabase --query "$checkColumns" -o table

Write-Host ""
Write-Host "Checking if AdapterInstanceGuid column exists..." -ForegroundColor Cyan
$checkGuidColumn = @"
USE [MessageBox]
SELECT COUNT(*) AS ColumnExists
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Messages' AND COLUMN_NAME = 'AdapterInstanceGuid'
"@

$guidExists = az sql db query --server $sqlServer --database $sqlDatabase --query "$checkGuidColumn" -o tsv
if ($guidExists -eq "0") {
    Write-Host "ERROR: AdapterInstanceGuid column is MISSING!" -ForegroundColor Red
    Write-Host "This will prevent data from being written to MessageBox." -ForegroundColor Red
    Write-Host "Run terraform/update-messagebox-database.sql to fix this." -ForegroundColor Yellow
} else {
    Write-Host "OK: AdapterInstanceGuid column exists" -ForegroundColor Green
}

Write-Host ""
Write-Host "Checking message count..." -ForegroundColor Cyan
$checkCount = @"
USE [MessageBox]
SELECT COUNT(*) AS TotalMessages FROM [dbo].[Messages]
"@

$messageCount = az sql db query --server $sqlServer --database $sqlDatabase --query "$checkCount" -o tsv
Write-Host "Total Messages in database: $messageCount" -ForegroundColor $(if ($messageCount -gt 0) { "Green" } else { "Yellow" })

Write-Host ""
Write-Host "Checking recent messages (last 24 hours)..." -ForegroundColor Cyan
$checkRecent = @"
USE [MessageBox]
SELECT TOP 10
    MessageId,
    InterfaceName,
    AdapterName,
    AdapterType,
    Status,
    datetime_created
FROM [dbo].[Messages]
WHERE datetime_created > DATEADD(hour, -24, GETUTCDATE())
ORDER BY datetime_created DESC
"@

az sql db query --server $sqlServer --database $sqlDatabase --query "$checkRecent" -o table

Write-Host ""
Write-Host "Checking adapter instances..." -ForegroundColor Cyan
$checkInstances = @"
USE [MessageBox]
SELECT 
    AdapterInstanceGuid,
    InterfaceName,
    InstanceName,
    AdapterName,
    AdapterType,
    IsEnabled
FROM [dbo].[AdapterInstances]
ORDER BY InterfaceName, AdapterType
"@

az sql db query --server $sqlServer --database $sqlDatabase --query "$checkInstances" -o table

Write-Host ""
Write-Host "=== Diagnosis Complete ===" -ForegroundColor Cyan


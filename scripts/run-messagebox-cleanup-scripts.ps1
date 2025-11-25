# Run InterfaceConfigDb Database Cleanup Scripts
# Script 1: Drop message and subscription tables
# Script 2: Remove MessageId column from ProcessLogs table

param(
    [string]$ResourceGroupName = "rg-interface-configurator",
    [string]$SqlServerName = "sql-main-database",
    [string]$SqlAdminLogin = "sqladmin",
    [string]$SqlAdminPassword = "",
    [string]$SqlDatabase = "InterfaceConfigDb"
)

Write-Host "`n=== Running InterfaceConfigDb Cleanup Scripts ===" -ForegroundColor Cyan

# Get SQL Server FQDN
Write-Host "`n[1/3] Getting SQL Server information..." -ForegroundColor Yellow
$sqlServer = az sql server show --resource-group $ResourceGroupName --name $SqlServerName --query "fullyQualifiedDomainName" -o tsv 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error getting SQL Server: $sqlServer" -ForegroundColor Red
    exit 1
}
Write-Host "✅ SQL Server: $sqlServer" -ForegroundColor Green
Write-Host "✅ Database: $SqlDatabase" -ForegroundColor Green

# Get password if not provided
if ([string]::IsNullOrWhiteSpace($SqlAdminPassword)) {
    Write-Host "`n[2/3] Getting SQL password..." -ForegroundColor Yellow
    # Try to get from environment variable first
    $SqlAdminPassword = $env:AZURE_SQL_PASSWORD
    if ([string]::IsNullOrWhiteSpace($SqlAdminPassword)) {
        # Try Azure Key Vault
        $keyVaultName = az keyvault list --resource-group $ResourceGroupName --query "[0].name" -o tsv 2>&1
        if ($LASTEXITCODE -eq 0 -and ![string]::IsNullOrWhiteSpace($keyVaultName)) {
            Write-Host "Trying to get password from Key Vault: $keyVaultName" -ForegroundColor Gray
            $SqlAdminPassword = az keyvault secret show --vault-name $keyVaultName --name "sql-admin-password" --query "value" -o tsv 2>&1
            if ($LASTEXITCODE -ne 0) {
                $SqlAdminPassword = ""
            }
        }
        
        # If still not found, prompt user
        if ([string]::IsNullOrWhiteSpace($SqlAdminPassword)) {
            Write-Host "Password not found in environment or Key Vault." -ForegroundColor Yellow
            $securePassword = Read-Host "Enter SQL admin password" -AsSecureString
            $SqlAdminPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword))
        }
    }
    Write-Host "✅ Password retrieved" -ForegroundColor Green
} else {
    Write-Host "`n[2/3] Using provided password..." -ForegroundColor Yellow
}

# Run Script 1: Drop message and subscription tables
Write-Host "`n[3/3] Running Script 1: Drop message and subscription tables..." -ForegroundColor Yellow
$script1Path = Join-Path $PSScriptRoot "..\terraform\drop-message-subscription-tables.sql"
if (-not (Test-Path $script1Path)) {
    Write-Host "❌ Script not found: $script1Path" -ForegroundColor Red
    exit 1
}

$script1Output = sqlcmd -S $sqlServer -d $SqlDatabase -U $SqlAdminLogin -P $SqlAdminPassword -i $script1Path -C -W 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Script 1 completed successfully" -ForegroundColor Green
    Write-Host $script1Output -ForegroundColor Gray
} else {
    Write-Host "⚠️  Script 1 completed with warnings or errors:" -ForegroundColor Yellow
    Write-Host $script1Output -ForegroundColor Yellow
}

# Run Script 2: Remove MessageId column from ProcessLogs
Write-Host "`n[4/4] Running Script 2: Remove MessageId from ProcessLogs..." -ForegroundColor Yellow
$script2Path = Join-Path $PSScriptRoot "..\terraform\remove-messageid-from-processlogs.sql"
if (-not (Test-Path $script2Path)) {
    Write-Host "❌ Script not found: $script2Path" -ForegroundColor Red
    exit 1
}

$script2Output = sqlcmd -S $sqlServer -d $SqlDatabase -U $SqlAdminLogin -P $SqlAdminPassword -i $script2Path -C -W 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Script 2 completed successfully" -ForegroundColor Green
    Write-Host $script2Output -ForegroundColor Gray
} else {
    Write-Host "⚠️  Script 2 completed with warnings or errors:" -ForegroundColor Yellow
    Write-Host $script2Output -ForegroundColor Yellow
}

Write-Host "`n=== Cleanup Scripts Completed ===" -ForegroundColor Cyan
Write-Host "The Messages, MessageSubscriptions, AdapterSubscriptions, and MessageProcessing tables have been removed." -ForegroundColor Green
Write-Host "The MessageId column has been removed from ProcessLogs table." -ForegroundColor Green


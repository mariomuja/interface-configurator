# Script to automatically update local.settings.json with Azure credentials
# Usage: .\update-local-settings.ps1

Write-Host "Updating local.settings.json with Azure credentials..." -ForegroundColor Cyan
Write-Host ""

# Check if Azure CLI is available
$azCmd = Get-Command az -ErrorAction SilentlyContinue
if (-not $azCmd) {
    Write-Host "❌ Azure CLI not found. Please install it:" -ForegroundColor Red
    Write-Host "   https://aka.ms/installazurecliwindows" -ForegroundColor Yellow
    exit 1
}

# Check if logged in
$azAccount = az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Not logged in to Azure. Please run: az login" -ForegroundColor Red
    exit 1
}

$resourceGroup = "rg-interface-configuration"

Write-Host "1. Getting Azure SQL Server details..." -ForegroundColor Yellow
try {
    $sqlServer = az sql server list --resource-group $resourceGroup --query "[0].fullyQualifiedDomainName" -o tsv
    $sqlServerName = az sql server list --resource-group $resourceGroup --query "[0].name" -o tsv
    $sqlAdmin = az sql server list --resource-group $resourceGroup --query "[0].administratorLogin" -o tsv
    
    if ([string]::IsNullOrWhiteSpace($sqlServer)) {
        Write-Host "   ⚠️  No SQL Server found in resource group $resourceGroup" -ForegroundColor Yellow
        Write-Host "   Please enter SQL Server details manually:" -ForegroundColor Yellow
        $sqlServer = Read-Host "   SQL Server FQDN (e.g., server.database.windows.net)"
        $sqlAdmin = Read-Host "   SQL Admin Username"
    } else {
        Write-Host "   ✓ SQL Server: $sqlServer" -ForegroundColor Green
        Write-Host "   ✓ SQL Admin: $sqlAdmin" -ForegroundColor Green
    }
} catch {
    Write-Host "   ⚠️  Error getting SQL Server: $_" -ForegroundColor Yellow
    $sqlServer = Read-Host "   Please enter SQL Server FQDN manually"
    $sqlAdmin = Read-Host "   Please enter SQL Admin Username manually"
}

Write-Host ""
Write-Host "2. Getting SQL Database name..." -ForegroundColor Yellow
try {
    $sqlDatabase = az sql db list --resource-group $resourceGroup --server $sqlServerName --query "[0].name" -o tsv
    if ([string]::IsNullOrWhiteSpace($sqlDatabase)) {
        $sqlDatabase = "AppDatabase"
        Write-Host "   ⚠️  No database found, using default: $sqlDatabase" -ForegroundColor Yellow
    } else {
        Write-Host "   ✓ Database: $sqlDatabase" -ForegroundColor Green
    }
} catch {
    Write-Host "   ⚠️  Error getting database, using default: AppDatabase" -ForegroundColor Yellow
    $sqlDatabase = "AppDatabase"
}

Write-Host ""
Write-Host "3. Getting SQL Password..." -ForegroundColor Yellow
$sqlPassword = Read-Host "   Enter SQL Server Password" -AsSecureString
$sqlPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sqlPassword))

Write-Host ""
Write-Host "4. Getting Storage Account connection string..." -ForegroundColor Yellow
try {
    # Try to get the main storage account
    $storageAccountName = az storage account list --resource-group $resourceGroup --query "[0].name" -o tsv
    if ([string]::IsNullOrWhiteSpace($storageAccountName)) {
        Write-Host "   ⚠️  No storage account found" -ForegroundColor Yellow
        $storageAccountName = Read-Host "   Please enter Storage Account Name manually"
    } else {
        Write-Host "   ✓ Storage Account: $storageAccountName" -ForegroundColor Green
    }
    
    $storageConn = az storage account show-connection-string --name $storageAccountName --resource-group $resourceGroup --query "connectionString" -o tsv
    if ([string]::IsNullOrWhiteSpace($storageConn)) {
        Write-Host "   ⚠️  Could not get connection string" -ForegroundColor Yellow
        $storageConn = Read-Host "   Please enter Storage Connection String manually"
    } else {
        Write-Host "   ✓ Connection string retrieved" -ForegroundColor Green
    }
} catch {
    Write-Host "   ⚠️  Error getting storage account: $_" -ForegroundColor Yellow
    $storageAccountName = Read-Host "   Please enter Storage Account Name manually"
    $storageConn = Read-Host "   Please enter Storage Connection String manually"
}

Write-Host ""
Write-Host "5. Updating local.settings.json..." -ForegroundColor Yellow

$localSettingsPath = Join-Path $PSScriptRoot "local.settings.json"

# Read existing file
$localSettings = @{
    IsEncrypted = $false
    Values = @{
        FUNCTIONS_EXTENSION_VERSION = "~4"
        AZURE_FUNCTIONS_ENVIRONMENT = "Development"
        FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"
        AzureWebJobsStorage = "UseDevelopmentStorage=true"
        MainStorageConnection = $storageConn
        AZURE_SQL_SERVER = $sqlServer
        AZURE_SQL_DATABASE = $sqlDatabase
        AZURE_SQL_USER = $sqlAdmin
        AZURE_SQL_PASSWORD = $sqlPasswordPlain
        CsvFieldSeparator = "║"
    }
}

# Convert to JSON and write
$json = $localSettings | ConvertTo-Json -Depth 10
$json | Set-Content -Path $localSettingsPath -Encoding UTF8

Write-Host "   ✓ local.settings.json updated successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  SQL Server: $sqlServer" -ForegroundColor Gray
Write-Host "  SQL Database: $sqlDatabase" -ForegroundColor Gray
Write-Host "  SQL User: $sqlAdmin" -ForegroundColor Gray
Write-Host "  Storage Account: $storageAccountName" -ForegroundColor Gray
Write-Host ""
Write-Host "Done! You can now run func start to test locally." -ForegroundColor Green


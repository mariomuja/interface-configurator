# Simple script to update local.settings.json
# Usage: .\setup-local-settings.ps1

Write-Host "Setting up local.settings.json for local development" -ForegroundColor Cyan
Write-Host ""

$localSettingsPath = Join-Path $PSScriptRoot "local.settings.json"

Write-Host "Please provide the following Azure credentials:" -ForegroundColor Yellow
Write-Host ""

# Get SQL Server details
$sqlServer = Read-Host "SQL Server FQDN (e.g., myserver.database.windows.net)"
$sqlDatabase = Read-Host "SQL Database Name (default: AppDatabase)"
if ([string]::IsNullOrWhiteSpace($sqlDatabase)) {
    $sqlDatabase = "AppDatabase"
}
$sqlUser = Read-Host "SQL Username"
$sqlPassword = Read-Host "SQL Password" -AsSecureString
$sqlPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sqlPassword))

Write-Host ""
Write-Host "Storage Account Connection String:" -ForegroundColor Yellow
Write-Host "  Format: DefaultEndpointsProtocol=https;AccountName=NAME;AccountKey=KEY;EndpointSuffix=core.windows.net" -ForegroundColor Gray
$storageConn = Read-Host "Storage Connection String"

Write-Host ""
Write-Host "Creating local.settings.json..." -ForegroundColor Yellow

$jsonContent = @"
{
  "IsEncrypted": false,
  "Values": {
    "FUNCTIONS_EXTENSION_VERSION": "~4",
    "AZURE_FUNCTIONS_ENVIRONMENT": "Development",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "MainStorageConnection": "$storageConn",
    "AZURE_SQL_SERVER": "$sqlServer",
    "AZURE_SQL_DATABASE": "$sqlDatabase",
    "AZURE_SQL_USER": "$sqlUser",
    "AZURE_SQL_PASSWORD": "$sqlPasswordPlain",
    "CsvFieldSeparator": "║"
  }
}
"@

$jsonContent | Set-Content -Path $localSettingsPath -Encoding UTF8

Write-Host ""
Write-Host "✓ local.settings.json created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "You can now run: func start" -ForegroundColor Cyan


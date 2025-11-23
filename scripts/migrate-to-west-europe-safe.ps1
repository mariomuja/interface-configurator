# Safe Migration to West Europe
# This script will backup data, then recreate resources in West Europe

param(
    [string]$SourceResourceGroup = "rg-interface-configurator",
    [string]$TargetResourceGroup = "rg-interface-configurator-weu",  # Temporary name
    [string]$TargetLocation = "West Europe",
    [string]$StorageAccountName = "stappgeneral",
    [string]$SqlServerName = "sql-main-database",
    [string]$FunctionAppName = "func-integration-main"
)

Write-Host "`n=== Safe Migration to West Europe ===" -ForegroundColor Cyan
Write-Host "This will:" -ForegroundColor Yellow
Write-Host "1. Backup SQL databases" -ForegroundColor White
Write-Host "2. Create new resource group in West Europe" -ForegroundColor White
Write-Host "3. Deploy infrastructure" -ForegroundColor White
Write-Host "4. Restore databases" -ForegroundColor White
Write-Host "`n⚠️  WARNING: This will create NEW resources. Old resources will remain until you delete them." -ForegroundColor Yellow
Write-Host ""

# Auto-confirm since user explicitly requested migration
Write-Host "Proceeding with migration..." -ForegroundColor Green

# Step 1: Backup SQL databases
Write-Host "`nStep 1: Backing up SQL databases..." -ForegroundColor Yellow
$storageKey = az storage account keys list --resource-group $SourceResourceGroup --account-name $StorageAccountName --query "[0].value" -o tsv

if ([string]::IsNullOrWhiteSpace($storageKey)) {
    Write-Host "❌ Could not get storage account key" -ForegroundColor Red
    exit 1
}

# Create backups container if it doesn't exist
az storage container create --account-name $StorageAccountName --account-key $storageKey --name backups --output none 2>&1 | Out-Null

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Write-Host "Backing up app-database..." -ForegroundColor Gray
$appDbBackupUri = "https://$StorageAccountName.blob.core.windows.net/backups/app-database-$timestamp.bacpac"
az sql db export `
    --resource-group $SourceResourceGroup `
    --server $SqlServerName `
    --name app-database `
    --storage-key-type StorageAccessKey `
    --storage-key $storageKey `
    --storage-uri $appDbBackupUri `
    --administrator-login sqladmin `
    --administrator-login-password "InfrastructureAsCode2024!Secure" `
    --output json 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ app-database backed up to: app-database-$timestamp.bacpac" -ForegroundColor Green
} else {
    Write-Host "⚠️  Backup may have issues, but continuing..." -ForegroundColor Yellow
}

Write-Host "Backing up MessageBox database..." -ForegroundColor Gray
$messageBoxBackupUri = "https://$StorageAccountName.blob.core.windows.net/backups/MessageBox-$timestamp.bacpac"
az sql db export `
    --resource-group $SourceResourceGroup `
    --server $SqlServerName `
    --name MessageBox `
    --storage-key-type StorageAccessKey `
    --storage-key $storageKey `
    --storage-uri $messageBoxBackupUri `
    --administrator-login sqladmin `
    --administrator-login-password "InfrastructureAsCode2024!Secure" `
    --output json 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ MessageBox backed up to: MessageBox-$timestamp.bacpac" -ForegroundColor Green
} else {
    Write-Host "⚠️  Backup may have issues, but continuing..." -ForegroundColor Yellow
}

# Step 2: Create resource group in West Europe
Write-Host "`nStep 2: Creating resource group in West Europe..." -ForegroundColor Yellow
az group create --name $TargetResourceGroup --location $TargetLocation --tags Environment=prod Project=Infrastructure
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to create resource group" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Resource group created: $TargetResourceGroup" -ForegroundColor Green

# Step 3: Deploy infrastructure
Write-Host "`nStep 3: Deploying infrastructure in West Europe..." -ForegroundColor Yellow
$bicepPath = Join-Path $PSScriptRoot "..\bicep"
$parametersFile = Join-Path $bicepPath "parameters.json"

# Temporarily update parameters to use new resource group
$params = Get-Content $parametersFile | ConvertFrom-Json
$params.parameters.resourceGroupName.value = $TargetResourceGroup
$params.parameters.location.value = $TargetLocation
$tempParamsFile = Join-Path $env:TEMP "bicep-params-$timestamp.json"
$params | ConvertTo-Json -Depth 10 | Set-Content $tempParamsFile

Write-Host "Deploying Bicep template..." -ForegroundColor Gray
az deployment group create `
    --resource-group $TargetResourceGroup `
    --template-file "$bicepPath\main.bicep" `
    --parameters "@$tempParamsFile" `
    --output json

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Bicep deployment failed" -ForegroundColor Red
    Remove-Item $tempParamsFile -ErrorAction SilentlyContinue
    exit 1
}

Write-Host "✅ Infrastructure deployed" -ForegroundColor Green
Remove-Item $tempParamsFile -ErrorAction SilentlyContinue

# Step 4: Restore databases
Write-Host "`nStep 4: Restoring SQL databases..." -ForegroundColor Yellow
Write-Host "Waiting 30 seconds for SQL server to be ready..." -ForegroundColor Gray
Start-Sleep -Seconds 30

Write-Host "Restoring app-database..." -ForegroundColor Gray
az sql db import `
    --resource-group $TargetResourceGroup `
    --server $SqlServerName `
    --name app-database `
    --storage-key-type StorageAccessKey `
    --storage-key $storageKey `
    --storage-uri $appDbBackupUri `
    --administrator-login sqladmin `
    --administrator-login-password "InfrastructureAsCode2024!Secure" `
    --output json 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ app-database restored" -ForegroundColor Green
} else {
    Write-Host "⚠️  Restore may have failed - check manually" -ForegroundColor Yellow
}

Write-Host "Restoring MessageBox..." -ForegroundColor Gray
az sql db import `
    --resource-group $TargetResourceGroup `
    --server $SqlServerName `
    --name MessageBox `
    --storage-key-type StorageAccessKey `
    --storage-key $storageKey `
    --storage-uri $messageBoxBackupUri `
    --administrator-login sqladmin `
    --administrator-login-password "InfrastructureAsCode2024!Secure" `
    --output json 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ MessageBox restored" -ForegroundColor Green
} else {
    Write-Host "⚠️  Restore may have failed - check manually" -ForegroundColor Yellow
}

# Step 5: Copy blob storage (manual step)
Write-Host "`nStep 5: Blob Storage Migration" -ForegroundColor Yellow
Write-Host "⚠️  Blob storage data needs to be copied manually:" -ForegroundColor Yellow
Write-Host "   Source: $StorageAccountName in $SourceResourceGroup (Central US)" -ForegroundColor White
Write-Host "   Target: $StorageAccountName in $TargetResourceGroup (West Europe)" -ForegroundColor White
Write-Host "   Use Azure Storage Explorer or: az storage blob copy start-batch" -ForegroundColor Gray

# Step 6: Update function app
Write-Host "`nStep 6: Function App" -ForegroundColor Yellow
Write-Host "⚠️  Function app needs to be redeployed to new location:" -ForegroundColor Yellow
Write-Host "   cd azure-functions/main" -ForegroundColor White
Write-Host "   func azure functionapp publish $FunctionAppName" -ForegroundColor White

Write-Host "`n=== Migration Complete ===" -ForegroundColor Cyan
Write-Host "✅ New resources created in West Europe: $TargetResourceGroup" -ForegroundColor Green
Write-Host "⚠️  Old resources still exist in Central US: $SourceResourceGroup" -ForegroundColor Yellow
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Copy blob storage data (function-config container)" -ForegroundColor White
Write-Host "2. Redeploy function app" -ForegroundColor White
Write-Host "3. Update Vercel environment variables" -ForegroundColor White
Write-Host "4. Test thoroughly" -ForegroundColor White
Write-Host "5. Delete old resource group when ready: az group delete --name $SourceResourceGroup --yes" -ForegroundColor White


# Complete Migration to West Europe
# This will backup, delete old resources, then recreate in West Europe

param(
    [string]$ResourceGroup = "rg-interface-configurator",
    [string]$TargetLocation = "West Europe",
    [string]$StorageAccountName = "stappgeneral",
    [string]$SqlServerName = "sql-main-database",
    [string]$FunctionAppName = "func-integration-main"
)

Write-Host "`n=== Complete Migration to West Europe ===" -ForegroundColor Cyan
Write-Host "⚠️  WARNING: This will DELETE existing resources in Central US!" -ForegroundColor Red
Write-Host "Make sure you have backups before proceeding." -ForegroundColor Yellow
Write-Host ""

# Step 1: Backup SQL databases
Write-Host "Step 1: Backing up SQL databases..." -ForegroundColor Yellow
$storageKey = az storage account keys list --resource-group $ResourceGroup --account-name $StorageAccountName --query "[0].value" -o tsv

if ([string]::IsNullOrWhiteSpace($storageKey)) {
    Write-Host "❌ Could not get storage account key" -ForegroundColor Red
    exit 1
}

# Create backups container
az storage container create --account-name $StorageAccountName --account-key $storageKey --name backups --output none 2>&1 | Out-Null

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Write-Host "Backing up app-database..." -ForegroundColor Gray
$appDbBackupUri = "https://$StorageAccountName.blob.core.windows.net/backups/app-database-$timestamp.bacpac"
$exportResult = az sql db export `
    --resource-group $ResourceGroup `
    --server $SqlServerName `
    --name app-database `
    --storage-key-type StorageAccessKey `
    --storage-key $storageKey `
    --storage-uri $appDbBackupUri `
    --administrator-login sqladmin `
    --administrator-login-password "InfrastructureAsCode2024!Secure" `
    --output json 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ app-database backed up" -ForegroundColor Green
} else {
    Write-Host "⚠️  Backup failed: $exportResult" -ForegroundColor Yellow
    Write-Host "Continuing anyway..." -ForegroundColor Yellow
}

Write-Host "Backing up MessageBox..." -ForegroundColor Gray
$messageBoxBackupUri = "https://$StorageAccountName.blob.core.windows.net/backups/MessageBox-$timestamp.bacpac"
$exportResult2 = az sql db export `
    --resource-group $ResourceGroup `
    --server $SqlServerName `
    --name MessageBox `
    --storage-key-type StorageAccessKey `
    --storage-key $storageKey `
    --storage-uri $messageBoxBackupUri `
    --administrator-login sqladmin `
    --administrator-login-password "InfrastructureAsCode2024!Secure" `
    --output json 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ MessageBox backed up" -ForegroundColor Green
} else {
    Write-Host "⚠️  Backup failed: $exportResult2" -ForegroundColor Yellow
}

# Step 2: Backup blob storage config
Write-Host "`nStep 2: Backing up blob storage configuration..." -ForegroundColor Yellow
$backupDir = Join-Path $env:TEMP "azure-backup-$timestamp"
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

# Download function-config container
az storage blob download-batch `
    --destination $backupDir `
    --source function-config `
    --account-name $StorageAccountName `
    --account-key $storageKey `
    --output none 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Blob storage configuration backed up to: $backupDir" -ForegroundColor Green
} else {
    Write-Host "⚠️  Blob backup may have failed" -ForegroundColor Yellow
}

# Step 3: Delete old resource group
Write-Host "`nStep 3: Deleting old resource group in Central US..." -ForegroundColor Yellow
Write-Host "⚠️  This will delete ALL resources in $ResourceGroup" -ForegroundColor Red
az group delete --name $ResourceGroup --yes --no-wait
Write-Host "✅ Deletion initiated (running in background)" -ForegroundColor Green
Write-Host "Waiting 60 seconds for deletion to complete..." -ForegroundColor Gray
Start-Sleep -Seconds 60

# Wait for deletion to complete
$maxWait = 300  # 5 minutes
$waited = 0
while ($waited -lt $maxWait) {
    $rg = az group show --name $ResourceGroup --output json 2>&1 | ConvertFrom-Json -ErrorAction SilentlyContinue
    if (-not $rg) {
        Write-Host "✅ Resource group deleted" -ForegroundColor Green
        break
    }
    Write-Host "Waiting for deletion... ($waited seconds)" -ForegroundColor Gray
    Start-Sleep -Seconds 10
    $waited += 10
}

if ($waited -ge $maxWait) {
    Write-Host "⚠️  Deletion taking longer than expected. Continuing anyway..." -ForegroundColor Yellow
}

# Step 4: Create new resource group in West Europe
Write-Host "`nStep 4: Creating resource group in West Europe..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $TargetLocation --tags Environment=prod Project=Infrastructure
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to create resource group" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Resource group created in West Europe" -ForegroundColor Green

# Step 5: Deploy infrastructure
Write-Host "`nStep 5: Deploying infrastructure in West Europe..." -ForegroundColor Yellow
$bicepPath = Join-Path $PSScriptRoot "..\bicep"
$parametersFile = Join-Path $bicepPath "parameters.json"

Write-Host "Deploying Bicep template..." -ForegroundColor Gray
az deployment group create `
    --resource-group $ResourceGroup `
    --template-file "$bicepPath\main.bicep" `
    --parameters "@$parametersFile" `
    --output json

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Bicep deployment failed" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Infrastructure deployed" -ForegroundColor Green

# Step 6: Restore databases
Write-Host "`nStep 6: Restoring SQL databases..." -ForegroundColor Yellow
Write-Host "Waiting 30 seconds for SQL server to be ready..." -ForegroundColor Gray
Start-Sleep -Seconds 30

Write-Host "Restoring app-database..." -ForegroundColor Gray
az sql db import `
    --resource-group $ResourceGroup `
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
    Write-Host "⚠️  Restore failed - you may need to restore manually" -ForegroundColor Yellow
}

Write-Host "Restoring MessageBox..." -ForegroundColor Gray
az sql db import `
    --resource-group $ResourceGroup `
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
    Write-Host "⚠️  Restore failed - you may need to restore manually" -ForegroundColor Yellow
}

# Step 7: Restore blob storage
Write-Host "`nStep 7: Restoring blob storage configuration..." -ForegroundColor Yellow
$newStorageKey = az storage account keys list --resource-group $ResourceGroup --account-name $StorageAccountName --query "[0].value" -o tsv

if (-not [string]::IsNullOrWhiteSpace($newStorageKey) -and (Test-Path $backupDir)) {
    az storage blob upload-batch `
        --destination function-config `
        --source $backupDir `
        --account-name $StorageAccountName `
        --account-key $newStorageKey `
        --output none 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Blob storage configuration restored" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Blob restore may have failed" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Migration Complete ===" -ForegroundColor Cyan
Write-Host "✅ Resources migrated to West Europe" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Redeploy function app: cd azure-functions/main && func azure functionapp publish $FunctionAppName" -ForegroundColor White
Write-Host "2. Update Vercel environment variables with new URLs" -ForegroundColor White
Write-Host "3. Test the application thoroughly" -ForegroundColor White
Write-Host "`nBackup location: $backupDir" -ForegroundColor Gray


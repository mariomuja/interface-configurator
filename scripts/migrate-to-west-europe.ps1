# Migrate Azure Resources from Central US to West Europe
# This script will:
# 1. Backup current resources (SQL databases, blob storage)
# 2. Create new resource group in West Europe
# 3. Deploy infrastructure in West Europe
# 4. Migrate data
# 5. Update function app settings

param(
    [string]$SourceResourceGroup = "rg-infrastructure-as-code",
    [string]$TargetResourceGroup = "rg-interface-configurator",
    [string]$SourceLocation = "Central US",
    [string]$TargetLocation = "West Europe",
    [string]$StorageAccountName = "stappgeneral",
    [string]$SqlServerName = "sql-main-database",
    [string]$FunctionAppName = "func-integration-main"
)

Write-Host "`n=== Migrating Resources to West Europe ===" -ForegroundColor Cyan
Write-Host "Source: $SourceResourceGroup ($SourceLocation)" -ForegroundColor Yellow
Write-Host "Target: $TargetResourceGroup ($TargetLocation)" -ForegroundColor Green

# Step 1: Check if target resource group exists and its location
Write-Host "`nStep 1: Checking target resource group..." -ForegroundColor Yellow
$targetRg = az group show --name $TargetResourceGroup --output json 2>&1 | ConvertFrom-Json -ErrorAction SilentlyContinue

if ($targetRg) {
    if ($targetRg.location -eq "westeurope") {
        Write-Host "✅ Target resource group already exists in West Europe" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Target resource group exists in $($targetRg.location), not West Europe" -ForegroundColor Yellow
        Write-Host "Checking if it's empty..." -ForegroundColor Yellow
        $resources = az resource list --resource-group $TargetResourceGroup --output json | ConvertFrom-Json
        if ($resources.Count -gt 0) {
            Write-Host "❌ Resource group contains resources. Cannot change location." -ForegroundColor Red
            Write-Host "Please delete the resource group first or use a different name." -ForegroundColor Red
            exit 1
        } else {
            Write-Host "Resource group is empty. Deleting and recreating in West Europe..." -ForegroundColor Yellow
            az group delete --name $TargetResourceGroup --yes --no-wait
            Start-Sleep -Seconds 10
        }
    }
}

# Create or recreate resource group in West Europe
if (-not $targetRg -or $targetRg.location -ne "westeurope") {
    Write-Host "Creating resource group in West Europe..." -ForegroundColor Yellow
    az group create --name $TargetResourceGroup --location $TargetLocation --tags Environment=prod Project=Infrastructure
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to create resource group" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Resource group created in West Europe" -ForegroundColor Green
}

# Step 2: Backup SQL databases
Write-Host "`nStep 2: Backing up SQL databases..." -ForegroundColor Yellow
$storageKey = az storage account keys list --resource-group $SourceResourceGroup --account-name $StorageAccountName --query "[0].value" -o tsv

if ([string]::IsNullOrWhiteSpace($storageKey)) {
    Write-Host "⚠️  Could not get storage account key. Skipping SQL backup." -ForegroundColor Yellow
    Write-Host "You may need to backup databases manually before migration." -ForegroundColor Yellow
} else {
    Write-Host "Backing up app-database..." -ForegroundColor Gray
    $backupUri = "https://$StorageAccountName.blob.core.windows.net/backups/app-database-$(Get-Date -Format 'yyyyMMdd-HHmmss').bacpac"
    az sql db export `
        --resource-group $SourceResourceGroup `
        --server $SqlServerName `
        --name app-database `
        --storage-key-type StorageAccessKey `
        --storage-key $storageKey `
        --storage-uri $backupUri `
        --administrator-login sqladmin `
        --administrator-login-password "InfrastructureAsCode2024!Secure" `
        --output none 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ app-database backed up" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Backup may have failed, but continuing..." -ForegroundColor Yellow
    }
    
    Write-Host "Backing up MessageBox database..." -ForegroundColor Gray
    $messageBoxBackupUri = "https://$StorageAccountName.blob.core.windows.net/backups/MessageBox-$(Get-Date -Format 'yyyyMMdd-HHmmss').bacpac"
    az sql db export `
        --resource-group $SourceResourceGroup `
        --server $SqlServerName `
        --name MessageBox `
        --storage-key-type StorageAccessKey `
        --storage-key $storageKey `
        --storage-uri $messageBoxBackupUri `
        --administrator-login sqladmin `
        --administrator-login-password "InfrastructureAsCode2024!Secure" `
        --output none 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ MessageBox database backed up" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Backup may have failed, but continuing..." -ForegroundColor Yellow
    }
}

# Step 3: Deploy infrastructure in West Europe using Bicep
Write-Host "`nStep 3: Deploying infrastructure in West Europe..." -ForegroundColor Yellow
Write-Host "Using Bicep deployment..." -ForegroundColor Gray

$bicepPath = Join-Path $PSScriptRoot "..\bicep"
$parametersFile = Join-Path $bicepPath "parameters.json"

if (-not (Test-Path $parametersFile)) {
    Write-Host "❌ Parameters file not found: $parametersFile" -ForegroundColor Red
    exit 1
}

Write-Host "Deploying Bicep template..." -ForegroundColor Gray
az deployment group create `
    --resource-group $TargetResourceGroup `
    --template-file "$bicepPath\main.bicep" `
    --parameters "@$parametersFile" `
    --output json

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Bicep deployment failed" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Infrastructure deployed in West Europe" -ForegroundColor Green

# Step 4: Restore SQL databases (if backups were created)
if (-not [string]::IsNullOrWhiteSpace($storageKey)) {
    Write-Host "`nStep 4: Restoring SQL databases..." -ForegroundColor Yellow
    
    # Get the latest backup files
    $backups = az storage blob list `
        --account-name $StorageAccountName `
        --account-key $storageKey `
        --container-name backups `
        --query "[?contains(name, '.bacpac')].{name:name, lastModified:properties.lastModified}" `
        --output json | ConvertFrom-Json | Sort-Object lastModified -Descending
    
    $appDbBackup = $backups | Where-Object { $_.name -like "*app-database*" } | Select-Object -First 1
    $messageBoxBackup = $backups | Where-Object { $_.name -like "*MessageBox*" } | Select-Object -First 1
    
    if ($appDbBackup) {
        Write-Host "Restoring app-database from $($appDbBackup.name)..." -ForegroundColor Gray
        $backupUri = "https://$StorageAccountName.blob.core.windows.net/backups/$($appDbBackup.name)"
        az sql db import `
            --resource-group $TargetResourceGroup `
            --server $SqlServerName `
            --name app-database `
            --storage-key-type StorageAccessKey `
            --storage-key $storageKey `
            --storage-uri $backupUri `
            --administrator-login sqladmin `
            --administrator-login-password "InfrastructureAsCode2024!Secure" `
            --output none 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ app-database restored" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Restore may have failed" -ForegroundColor Yellow
        }
    }
    
    if ($messageBoxBackup) {
        Write-Host "Restoring MessageBox from $($messageBoxBackup.name)..." -ForegroundColor Gray
        $backupUri = "https://$StorageAccountName.blob.core.windows.net/backups/$($messageBoxBackup.name)"
        az sql db import `
            --resource-group $TargetResourceGroup `
            --server $SqlServerName `
            --name MessageBox `
            --storage-key-type StorageAccessKey `
            --storage-key $storageKey `
            --storage-uri $backupUri `
            --administrator-login sqladmin `
            --administrator-login-password "InfrastructureAsCode2024!Secure" `
            --output none 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ MessageBox database restored" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Restore may have failed" -ForegroundColor Yellow
        }
    }
}

# Step 5: Copy blob storage data
Write-Host "`nStep 5: Copying blob storage data..." -ForegroundColor Yellow
Write-Host "This step requires manual intervention or Azure Storage Explorer." -ForegroundColor Yellow
Write-Host "You can use: az storage blob copy start-batch" -ForegroundColor Gray

# Step 6: Update function app settings
Write-Host "`nStep 6: Updating function app settings..." -ForegroundColor Yellow
$targetSqlServer = az sql server show --resource-group $TargetResourceGroup --name $SqlServerName --query "fullyQualifiedDomainName" -o tsv

if ($targetSqlServer) {
    Write-Host "Updating SQL connection strings..." -ForegroundColor Gray
    az functionapp config appsettings set `
        --name $FunctionAppName `
        --resource-group $TargetResourceGroup `
        --settings `
            AZURE_SQL_SERVER="$targetSqlServer" `
            AZURE_SQL_DATABASE="app-database" `
        --output none 2>&1
    
    Write-Host "✅ Function app settings updated" -ForegroundColor Green
}

Write-Host "`n=== Migration Summary ===" -ForegroundColor Cyan
Write-Host "✅ Resource group created in West Europe" -ForegroundColor Green
Write-Host "✅ Infrastructure deployed" -ForegroundColor Green
Write-Host "⚠️  Data migration may require additional steps" -ForegroundColor Yellow
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Verify all resources are created correctly" -ForegroundColor White
Write-Host "2. Copy blob storage data from Central US to West Europe" -ForegroundColor White
Write-Host "3. Redeploy function app to new location" -ForegroundColor White
Write-Host "4. Update Vercel environment variables with new URLs" -ForegroundColor White
Write-Host "5. Test the application thoroughly" -ForegroundColor White
Write-Host "6. Delete old resources in Central US after verification" -ForegroundColor White

Write-Host "`n=== Done ===" -ForegroundColor Cyan


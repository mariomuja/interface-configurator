# Cleanup script to remove duplicate and unnecessary Azure resources

param(
    [string]$ResourceGroup = "rg-interface-configuration",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Azure Resources Cleanup" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if ($WhatIf) {
    Write-Host "WHAT-IF MODE: No resources will be deleted" -ForegroundColor Yellow
    Write-Host ""
}

# Resources to delete (duplicates from bicep deployment with e1mz5h suffix)
$resourcesToDelete = @()

# 1. Duplicate Function App (e1mz5h)
Write-Host "1. Checking duplicate Function App..." -ForegroundColor Yellow
$duplicateFunctionApp = az functionapp show --resource-group $ResourceGroup --name "func-appe1mz5h" --query "name" -o tsv 2>&1
if ($duplicateFunctionApp -and $duplicateFunctionApp -eq "func-appe1mz5h") {
    Write-Host "   Found: func-appe1mz5h" -ForegroundColor Red
    $resourcesToDelete += @{Type="FunctionApp"; Name="func-appe1mz5h"}
} else {
    Write-Host "   Not found (already deleted)" -ForegroundColor Green
}

# 2. Duplicate App Service Plan (e1mz5h)
Write-Host "`n2. Checking duplicate App Service Plan..." -ForegroundColor Yellow
$duplicatePlan = az appservice plan show --resource-group $ResourceGroup --name "plan-funcs-appe1mz5h" --query "name" -o tsv 2>&1
if ($duplicatePlan -and $duplicatePlan -eq "plan-funcs-appe1mz5h") {
    Write-Host "   Found: plan-funcs-appe1mz5h" -ForegroundColor Red
    $resourcesToDelete += @{Type="AppServicePlan"; Name="plan-funcs-appe1mz5h"}
} else {
    Write-Host "   Not found (already deleted)" -ForegroundColor Green
}

# 3. Duplicate SQL Server and Database (e1mz5h)
Write-Host "`n3. Checking duplicate SQL Server..." -ForegroundColor Yellow
$duplicateSqlServer = az sql server show --resource-group $ResourceGroup --name "sql-infrastructuree1mz5h" --query "name" -o tsv 2>&1
if ($duplicateSqlServer -and $duplicateSqlServer -eq "sql-infrastructuree1mz5h") {
    Write-Host "   Found: sql-infrastructuree1mz5h" -ForegroundColor Red
    $resourcesToDelete += @{Type="SqlServer"; Name="sql-infrastructuree1mz5h"}
} else {
    Write-Host "   Not found (already deleted)" -ForegroundColor Green
}

# 4. Duplicate Storage Accounts (e1mz5h)
Write-Host "`n4. Checking duplicate Storage Accounts..." -ForegroundColor Yellow
$duplicateStorage1 = az storage account show --resource-group $ResourceGroup --name "stfuncsappe1mz5h" --query "name" -o tsv 2>&1
if ($duplicateStorage1 -and $duplicateStorage1 -eq "stfuncsappe1mz5h") {
    Write-Host "   Found: stfuncsappe1mz5h" -ForegroundColor Red
    $resourcesToDelete += @{Type="StorageAccount"; Name="stfuncsappe1mz5h"}
} else {
    Write-Host "   Not found (already deleted)" -ForegroundColor Green
}

$duplicateStorage2 = az storage account show --resource-group $ResourceGroup --name "stappe1mz5h" --query "name" -o tsv 2>&1
if ($duplicateStorage2 -and $duplicateStorage2 -eq "stappe1mz5h") {
    Write-Host "   Found: stappe1mz5h" -ForegroundColor Red
    $resourcesToDelete += @{Type="StorageAccount"; Name="stappe1mz5h"}
} else {
    Write-Host "   Not found (already deleted)" -ForegroundColor Green
}

# 5. Managed Identity
Write-Host "`n5. Checking Managed Identity..." -ForegroundColor Yellow
$managedIdentity = az identity show --resource-group $ResourceGroup --name "oidc-msi-bbce" --query "name" -o tsv 2>&1
if ($managedIdentity -and $managedIdentity -eq "oidc-msi-bbce") {
    Write-Host "   Found: oidc-msi-bbce" -ForegroundColor Red
    $resourcesToDelete += @{Type="ManagedIdentity"; Name="oidc-msi-bbce"}
} else {
    Write-Host "   Not found (already deleted)" -ForegroundColor Green
}

# 6. Action Group
Write-Host "`n6. Checking Action Group..." -ForegroundColor Yellow
$actionGroup = az monitor action-group show --resource-group $ResourceGroup --name "Application Insights Smart Detection" --query "name" -o tsv 2>&1
if ($actionGroup -and $actionGroup -like "*Smart Detection*") {
    Write-Host "   Found: Application Insights Smart Detection" -ForegroundColor Red
    $resourcesToDelete += @{Type="ActionGroup"; Name="Application Insights Smart Detection"}
} else {
    Write-Host "   Not found (already deleted)" -ForegroundColor Green
}

# 7. Log Analytics Workspace
Write-Host "`n7. Checking Log Analytics Workspace..." -ForegroundColor Yellow
$logAnalytics = az monitor log-analytics workspace show --resource-group $ResourceGroup --workspace-name "DefaultWorkspace-f1e8e2a3-2bf1-43f0-8f19-37abd624205c-CUS" --query "name" -o tsv 2>&1
if ($logAnalytics) {
    Write-Host "   Found: DefaultWorkspace-f1e8e2a3-2bf1-43f0-8f19-37abd624205c-CUS" -ForegroundColor Red
    $resourcesToDelete += @{Type="LogAnalyticsWorkspace"; Name="DefaultWorkspace-f1e8e2a3-2bf1-43f0-8f19-37abd624205c-CUS"}
} else {
    Write-Host "   Not found (already deleted)" -ForegroundColor Green
}

# 8. Network Watcher
Write-Host "`n8. Checking Network Watcher..." -ForegroundColor Yellow
$networkWatcher = az network watcher show --resource-group $ResourceGroup --name "NetworkWatcher_centralus" --query "name" -o tsv 2>&1
if ($networkWatcher) {
    Write-Host "   Found: NetworkWatcher_centralus" -ForegroundColor Red
    $resourcesToDelete += @{Type="NetworkWatcher"; Name="NetworkWatcher_centralus"}
} else {
    Write-Host "   Not found (already deleted or doesn't exist)" -ForegroundColor Green
}

# Delete resources
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Deletion Summary" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if ($resourcesToDelete.Count -eq 0) {
    Write-Host "No resources to delete!" -ForegroundColor Green
    exit 0
}

Write-Host "Resources to delete: $($resourcesToDelete.Count)" -ForegroundColor Yellow
foreach ($resource in $resourcesToDelete) {
    Write-Host "  - $($resource.Type): $($resource.Name)" -ForegroundColor Gray
}

if ($WhatIf) {
    Write-Host "`nWHAT-IF: Would delete the above resources" -ForegroundColor Yellow
    exit 0
}

Write-Host "`nDeleting resources..." -ForegroundColor Yellow

foreach ($resource in $resourcesToDelete) {
    Write-Host "`nDeleting $($resource.Type): $($resource.Name)..." -ForegroundColor Yellow
    
    try {
        switch ($resource.Type) {
            "FunctionApp" {
                az functionapp delete --resource-group $ResourceGroup --name $resource.Name --output none 2>&1 | Out-Null
            }
            "AppServicePlan" {
                az appservice plan delete --resource-group $ResourceGroup --name $resource.Name --yes --output none 2>&1 | Out-Null
            }
            "SqlServer" {
                az sql server delete --resource-group $ResourceGroup --name $resource.Name --yes --output none 2>&1 | Out-Null
            }
            "StorageAccount" {
                az storage account delete --resource-group $ResourceGroup --name $resource.Name --yes --output none 2>&1 | Out-Null
            }
            "ManagedIdentity" {
                az identity delete --resource-group $ResourceGroup --name $resource.Name --output none 2>&1 | Out-Null
            }
            "ActionGroup" {
                az monitor action-group delete --resource-group $ResourceGroup --name $resource.Name --yes --output none 2>&1 | Out-Null
            }
            "LogAnalyticsWorkspace" {
                az monitor log-analytics workspace delete --resource-group $ResourceGroup --workspace-name $resource.Name --yes --output none 2>&1 | Out-Null
            }
            "NetworkWatcher" {
                az network watcher delete --resource-group $ResourceGroup --name $resource.Name --output none 2>&1 | Out-Null
            }
        }
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] Deleted successfully" -ForegroundColor Green
        } else {
            Write-Host "  [WARNING] Deletion may have failed (check manually)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  [ERROR] Failed to delete: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Cleanup Complete!" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan










# Script to migrate resources from old resource group to new resource group
# Resource Groups cannot be renamed in Azure, so we need to:
# 1. Create the new resource group
# 2. Move all resources to the new resource group
# 3. Update Terraform state

param(
    [string]$OldResourceGroup = "rg-infrastructure-as-code",
    [string]$NewResourceGroup = "rg-interface-configurator",
    [string]$Location = "Central US",
    [string]$SubscriptionId = ""
)

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Resource Group Migration Script" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Old Resource Group: $OldResourceGroup" -ForegroundColor Yellow
Write-Host "New Resource Group: $NewResourceGroup" -ForegroundColor Green
Write-Host "Location: $Location" -ForegroundColor Yellow
Write-Host ""

# Check if Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Azure CLI is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

# Set subscription if provided
if ($SubscriptionId) {
    Write-Host "Setting Azure subscription to: $SubscriptionId" -ForegroundColor Cyan
    az account set --subscription $SubscriptionId
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to set subscription" -ForegroundColor Red
        exit 1
    }
}

# Get current subscription
$currentSub = az account show --query id -o tsv
Write-Host "Current Subscription: $currentSub" -ForegroundColor Cyan
Write-Host ""

# Check if old resource group exists
Write-Host "Checking if old resource group exists..." -ForegroundColor Cyan
$oldRgExists = az group exists --name $OldResourceGroup
if ($oldRgExists -eq "false") {
    Write-Host "WARNING: Old resource group '$OldResourceGroup' does not exist!" -ForegroundColor Yellow
    Write-Host "This might be expected if resources are already migrated or in a different resource group." -ForegroundColor Yellow
    Write-Host ""
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne "y") {
        exit 0
    }
} else {
    Write-Host "Old resource group '$OldResourceGroup' exists." -ForegroundColor Green
}

# Check if new resource group exists
Write-Host "Checking if new resource group exists..." -ForegroundColor Cyan
$newRgExists = az group exists --name $NewResourceGroup
if ($newRgExists -eq "true") {
    Write-Host "WARNING: New resource group '$NewResourceGroup' already exists!" -ForegroundColor Yellow
    Write-Host "This script will move resources into the existing resource group." -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Host "Creating new resource group '$NewResourceGroup'..." -ForegroundColor Cyan
    az group create --name $NewResourceGroup --location $Location
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to create new resource group" -ForegroundColor Red
        exit 1
    }
    Write-Host "New resource group created successfully." -ForegroundColor Green
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Step 1: List resources in old resource group" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

# Get all resources in the old resource group
$resources = az resource list --resource-group $OldResourceGroup --query "[].{Name:name, Type:type, Id:id}" -o json | ConvertFrom-Json

if ($resources.Count -eq 0) {
    Write-Host "No resources found in old resource group." -ForegroundColor Yellow
    Write-Host "Migration complete (nothing to migrate)." -ForegroundColor Green
    exit 0
}

Write-Host "Found $($resources.Count) resources to migrate:" -ForegroundColor Cyan
foreach ($resource in $resources) {
    Write-Host "  - $($resource.Name) ($($resource.Type))" -ForegroundColor Gray
}

Write-Host ""
$confirm = Read-Host "Do you want to proceed with moving these resources? (y/n)"
if ($confirm -ne "y") {
    Write-Host "Migration cancelled." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Step 2: Moving resources to new resource group" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

$successCount = 0
$failedResources = @()

foreach ($resource in $resources) {
    Write-Host "Moving: $($resource.Name)..." -ForegroundColor Cyan
    az resource move --destination-group $NewResourceGroup --ids $resource.Id
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Successfully moved $($resource.Name)" -ForegroundColor Green
        $successCount++
    } else {
        Write-Host "  ✗ Failed to move $($resource.Name)" -ForegroundColor Red
        $failedResources += $resource
    }
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Migration Summary" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Successfully moved: $successCount / $($resources.Count)" -ForegroundColor $(if ($successCount -eq $resources.Count) { "Green" } else { "Yellow" })

if ($failedResources.Count -gt 0) {
    Write-Host ""
    Write-Host "Failed resources:" -ForegroundColor Red
    foreach ($resource in $failedResources) {
        Write-Host "  - $($resource.Name) ($($resource.Type))" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "NOTE: Some resources may need to be moved manually or recreated." -ForegroundColor Yellow
    Write-Host "Check Azure Portal for details." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Step 3: Update Terraform State" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "IMPORTANT: After moving resources, you need to update Terraform state:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Navigate to terraform directory:" -ForegroundColor Cyan
Write-Host "   cd terraform" -ForegroundColor White
Write-Host ""
Write-Host "2. Initialize Terraform (if not already done):" -ForegroundColor Cyan
Write-Host "   terraform init" -ForegroundColor White
Write-Host ""
Write-Host "3. Import the new resource group:" -ForegroundColor Cyan
Write-Host "   terraform import azurerm_resource_group.main /subscriptions/$currentSub/resourceGroups/$NewResourceGroup" -ForegroundColor White
Write-Host ""
Write-Host "4. For each moved resource, update the state:" -ForegroundColor Cyan
Write-Host "   terraform state mv 'azurerm_resource_type.old_name' 'azurerm_resource_type.new_name'" -ForegroundColor White
Write-Host ""
Write-Host "5. Run terraform plan to verify:" -ForegroundColor Cyan
Write-Host "   terraform plan" -ForegroundColor White
Write-Host ""
Write-Host "6. If plan shows no changes, apply:" -ForegroundColor Cyan
Write-Host "   terraform apply" -ForegroundColor White
Write-Host ""

Write-Host "Migration script completed!" -ForegroundColor Green



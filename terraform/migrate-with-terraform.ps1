# Script to migrate resource group using Terraform
# This script will:
# 1. Create the new resource group
# 2. Import existing resources into Terraform state
# 3. Update resource group references

param(
    [string]$OldResourceGroup = "rg-infrastructure-as-code",
    [string]$NewResourceGroup = "rg-interface-configurator",
    [string]$Location = "Central US"
)

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Terraform Resource Group Migration" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Old Resource Group: $OldResourceGroup" -ForegroundColor Yellow
Write-Host "New Resource Group: $NewResourceGroup" -ForegroundColor Green
Write-Host "Location: $Location" -ForegroundColor Yellow
Write-Host ""

# Check if Terraform is installed
if (-not (Get-Command terraform -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Terraform is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

# Check if Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Azure CLI is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

# Get current subscription
$subscriptionId = az account show --query id -o tsv
Write-Host "Current Subscription: $subscriptionId" -ForegroundColor Cyan
Write-Host ""

# Change to terraform directory
Push-Location $PSScriptRoot

try {
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "Step 1: Initialize Terraform" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    
    terraform init
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Terraform init failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "Step 2: Create new resource group in Azure" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    
    # Check if new resource group exists
    $newRgExists = az group exists --name $NewResourceGroup
    if ($newRgExists -eq "false") {
        Write-Host "Creating new resource group '$NewResourceGroup'..." -ForegroundColor Cyan
        az group create --name $NewResourceGroup --location $Location
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Failed to create new resource group" -ForegroundColor Red
            exit 1
        }
        Write-Host "New resource group created." -ForegroundColor Green
    } else {
        Write-Host "Resource group '$NewResourceGroup' already exists." -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "Step 3: Import new resource group into Terraform" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    
    $rgResourceId = "/subscriptions/$subscriptionId/resourceGroups/$NewResourceGroup"
    Write-Host "Importing resource group: $rgResourceId" -ForegroundColor Cyan
    
    terraform import azurerm_resource_group.main $rgResourceId
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: Failed to import resource group (might already be in state)" -ForegroundColor Yellow
    } else {
        Write-Host "Resource group imported successfully." -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "Step 4: Move resources from old to new resource group" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "NOTE: This step requires manual intervention or the migrate-resource-group.ps1 script." -ForegroundColor Yellow
    Write-Host "Run the following command to move resources:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  .\migrate-resource-group.ps1 -OldResourceGroup '$OldResourceGroup' -NewResourceGroup '$NewResourceGroup' -Location '$Location'" -ForegroundColor White
    Write-Host ""
    
    $moveResources = Read-Host "Have you moved all resources to the new resource group? (y/n)"
    if ($moveResources -ne "y") {
        Write-Host "Please run migrate-resource-group.ps1 first to move resources." -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host "Step 5: Update Terraform state for moved resources" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Terraform will detect that resources have moved to a new resource group." -ForegroundColor Cyan
    Write-Host "Running terraform plan to see what needs to be updated..." -ForegroundColor Cyan
    Write-Host ""
    
    terraform plan -out=tfplan
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Terraform plan failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host ""
    Write-Host "Review the plan above. If it looks correct, apply the changes:" -ForegroundColor Yellow
    Write-Host ""
    $apply = Read-Host "Apply Terraform changes? (y/n)"
    
    if ($apply -eq "y") {
        Write-Host ""
        Write-Host "Applying Terraform changes..." -ForegroundColor Cyan
        terraform apply tfplan
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "================================================" -ForegroundColor Green
            Write-Host "Migration completed successfully!" -ForegroundColor Green
            Write-Host "================================================" -ForegroundColor Green
            Write-Host ""
            Write-Host "You can now delete the old resource group if it's empty:" -ForegroundColor Cyan
            Write-Host "  az group delete --name $OldResourceGroup --yes" -ForegroundColor White
        } else {
            Write-Host "ERROR: Terraform apply failed" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "Migration cancelled. Run 'terraform apply tfplan' when ready." -ForegroundColor Yellow
    }
    
} finally {
    Pop-Location
}



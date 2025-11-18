# Azure Bicep Deployment Script
# This script deploys infrastructure using Azure Bicep

param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName = "rg-interface-configuration",
    
    [Parameter(Mandatory = $false)]
    [string]$Location = "Central US",
    
    [Parameter(Mandatory = $false)]
    [string]$ParametersFile = "parameters.json",
    
    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId = "",
    
    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

# Check if Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI is not installed. Please install it from https://aka.ms/installazurecliwindows"
    exit 1
}

# Check if Bicep CLI is installed
if (-not (Get-Command bicep -ErrorAction SilentlyContinue)) {
    Write-Warning "Bicep CLI not found in PATH. Attempting to use Azure CLI's built-in Bicep support..."
    $useBicepCli = $false
} else {
    $useBicepCli = $true
    Write-Host "Using Bicep CLI version: $(bicep --version)" -ForegroundColor Green
}

# Login to Azure if not already logged in
$context = az account show 2>$null | ConvertFrom-Json
if (-not $context) {
    Write-Host "Not logged in to Azure. Please log in..." -ForegroundColor Yellow
    az login
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to log in to Azure"
        exit 1
    }
}

# Set subscription if provided
if ($SubscriptionId -ne "") {
    Write-Host "Setting subscription to: $SubscriptionId" -ForegroundColor Cyan
    az account set --subscription $SubscriptionId
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to set subscription"
        exit 1
    }
}

# Get current subscription
$currentSub = az account show --query id -o tsv
Write-Host "Current subscription: $currentSub" -ForegroundColor Cyan

# Verify parameters file exists
$parametersPath = Join-Path $PSScriptRoot $ParametersFile
if (-not (Test-Path $parametersPath)) {
    Write-Error "Parameters file not found: $parametersPath"
    exit 1
}

Write-Host "Using parameters file: $ParametersFile" -ForegroundColor Cyan

# Create resource group if it doesn't exist
Write-Host "Checking if resource group exists: $ResourceGroupName" -ForegroundColor Cyan
$rgExists = az group exists --name $ResourceGroupName | ConvertFrom-Json
if (-not $rgExists) {
    Write-Host "Creating resource group: $ResourceGroupName in $Location" -ForegroundColor Yellow
    az group create --name $ResourceGroupName --location $Location
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create resource group"
        exit 1
    }
} else {
    Write-Host "Resource group already exists: $ResourceGroupName" -ForegroundColor Green
}

# Deploy Bicep template
$bicepFile = Join-Path $PSScriptRoot "main.bicep"
$deploymentName = "infrastructure-deployment-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

Write-Host "`nDeploying Bicep template..." -ForegroundColor Cyan
Write-Host "Deployment name: $deploymentName" -ForegroundColor Cyan
Write-Host "Bicep file: $bicepFile" -ForegroundColor Cyan
Write-Host "Parameters file: $parametersPath" -ForegroundColor Cyan

if ($WhatIf) {
    Write-Host "`nRunning What-If preview..." -ForegroundColor Yellow
    az deployment group what-if `
        --resource-group $ResourceGroupName `
        --template-file $bicepFile `
        --parameters $parametersPath `
        --name $deploymentName
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "What-If preview failed"
        exit 1
    }
    
    Write-Host "`nWhat-If preview completed. Review the changes above." -ForegroundColor Yellow
    $confirm = Read-Host "Do you want to proceed with the deployment? (y/N)"
    if ($confirm -ne "y" -and $confirm -ne "Y") {
        Write-Host "Deployment cancelled." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host "`nStarting deployment..." -ForegroundColor Green
az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file $bicepFile `
    --parameters $parametersPath `
    --name $deploymentName `
    --verbose

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed"
    exit 1
}

Write-Host "`n✅ Deployment completed successfully!" -ForegroundColor Green

# Show outputs
Write-Host "`nDeployment outputs:" -ForegroundColor Cyan
az deployment group show `
    --resource-group $ResourceGroupName `
    --name $deploymentName `
    --query properties.outputs `
    --output table

Write-Host "`nDeployment completed at: $(Get-Date)" -ForegroundColor Green


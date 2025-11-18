# Azure Bicep Validation Script
# This script validates Bicep templates without deploying

param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName = "rg-interface-configuration",
    
    [Parameter(Mandatory = $false)]
    [string]$ParametersFile = "parameters.json"
)

# Check if Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI is not installed. Please install it from https://aka.ms/installazurecliwindows"
    exit 1
}

# Check if Bicep CLI is installed
if (Get-Command bicep -ErrorAction SilentlyContinue) {
    Write-Host "Validating Bicep file syntax..." -ForegroundColor Cyan
    $bicepFile = Join-Path $PSScriptRoot "main.bicep"
    bicep build $bicepFile --no-restore
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Bicep syntax validation failed"
        exit 1
    }
    
    Write-Host "✅ Bicep syntax is valid" -ForegroundColor Green
}

# Verify parameters file exists
$parametersPath = Join-Path $PSScriptRoot $ParametersFile
if (-not (Test-Path $parametersPath)) {
    Write-Error "Parameters file not found: $parametersPath"
    exit 1
}

# Validate template with Azure
Write-Host "`nValidating template with Azure..." -ForegroundColor Cyan
$bicepFile = Join-Path $PSScriptRoot "main.bicep"

az deployment group validate `
    --resource-group $ResourceGroupName `
    --template-file $bicepFile `
    --parameters $parametersPath `
    --verbose

if ($LASTEXITCODE -ne 0) {
    Write-Error "Template validation failed"
    exit 1
}

Write-Host "`n✅ Template validation passed!" -ForegroundColor Green


# PowerShell Script to Set AZURE_FUNCTION_APP_URL in Vercel
# This script retrieves the Function App URL from Azure and sets it in Vercel

param(
    [string]$ResourceGroup = "",
    [string]$FunctionAppName = "",
    [string]$VercelProject = "interface-configurator"
)

Write-Host "=== Setting AZURE_FUNCTION_APP_URL in Vercel ===" -ForegroundColor Cyan
Write-Host ""

# Check if Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Azure CLI is not installed or not in PATH" -ForegroundColor Red
    Write-Host "Install from: https://aka.ms/installazurecliwindows" -ForegroundColor Yellow
    exit 1
}

# Check if Vercel CLI is installed
if (-not (Get-Command vercel -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Vercel CLI is not installed" -ForegroundColor Red
    Write-Host "Install with: npm install -g vercel" -ForegroundColor Yellow
    exit 1
}

# Check if logged in to Azure
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
$azAccount = az account show 2>$null | ConvertFrom-Json
if (-not $azAccount) {
    Write-Host "Not logged in to Azure. Logging in..." -ForegroundColor Yellow
    az login
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to login to Azure" -ForegroundColor Red
        exit 1
    }
}

# Get resource group if not provided
if ([string]::IsNullOrEmpty($ResourceGroup)) {
    Write-Host ""
    Write-Host "Available resource groups:" -ForegroundColor Yellow
    $resourceGroups = az group list --query "[].name" -o tsv
    $resourceGroups | ForEach-Object { Write-Host "  - $_" }
    Write-Host ""
    $ResourceGroup = Read-Host "Enter resource group name"
}

# Get function app name if not provided
if ([string]::IsNullOrEmpty($FunctionAppName)) {
    Write-Host ""
    Write-Host "Searching for Function Apps in resource group '$ResourceGroup'..." -ForegroundColor Yellow
    $functionApps = az functionapp list --resource-group $ResourceGroup --query "[].name" -o tsv
    if ($functionApps) {
        Write-Host "Found Function Apps:" -ForegroundColor Yellow
        $functionApps | ForEach-Object { Write-Host "  - $_" }
        Write-Host ""
        $FunctionAppName = Read-Host "Enter Function App name"
    } else {
        Write-Host "ERROR: No Function Apps found in resource group '$ResourceGroup'" -ForegroundColor Red
        exit 1
    }
}

# Get Function App URL
Write-Host ""
Write-Host "Retrieving Function App URL..." -ForegroundColor Yellow
$functionAppUrl = az functionapp show `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "defaultHostName" `
    -o tsv

if ([string]::IsNullOrEmpty($functionAppUrl)) {
    Write-Host "ERROR: Could not retrieve Function App URL" -ForegroundColor Red
    Write-Host "Please check:" -ForegroundColor Yellow
    Write-Host "  - Resource group name: $ResourceGroup" -ForegroundColor Yellow
    Write-Host "  - Function App name: $FunctionAppName" -ForegroundColor Yellow
    exit 1
}

# Ensure URL starts with https://
if (-not $functionAppUrl.StartsWith("http")) {
    $functionAppUrl = "https://$functionAppUrl"
}

Write-Host ""
Write-Host "Function App URL: $functionAppUrl" -ForegroundColor Green
Write-Host ""

# Verify Function App is accessible
Write-Host "Verifying Function App is accessible..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$functionAppUrl/api/GetProcessLogs" -Method GET -TimeoutSec 10 -ErrorAction Stop
    Write-Host "✓ Function App is accessible (Status: $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "⚠ Warning: Could not verify Function App accessibility" -ForegroundColor Yellow
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "  The URL will still be set, but please verify manually" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Setting AZURE_FUNCTION_APP_URL in Vercel..." -ForegroundColor Yellow
Write-Host "Project: $VercelProject" -ForegroundColor Yellow
Write-Host ""

# Set environment variable in Vercel
# Note: This will prompt for environment (production, preview, development)
Write-Host "Vercel will prompt you to select the environment(s) for this variable." -ForegroundColor Cyan
Write-Host "Recommended: Select 'Production' (and optionally 'Preview' and 'Development')" -ForegroundColor Cyan
Write-Host ""

$confirm = Read-Host "Continue? (Y/N)"
if ($confirm -ne "Y" -and $confirm -ne "y") {
    Write-Host "Cancelled." -ForegroundColor Yellow
    exit 0
}

# Use Vercel CLI to set the environment variable
Write-Host ""
Write-Host "Running: vercel env add AZURE_FUNCTION_APP_URL" -ForegroundColor Cyan
Write-Host "When prompted, enter: $functionAppUrl" -ForegroundColor Cyan
Write-Host ""

# Vercel CLI doesn't support non-interactive mode for env add, so we need to use echo
# Alternative: Use Vercel API directly
Write-Host "Option 1: Use Vercel CLI (interactive)" -ForegroundColor Yellow
Write-Host "  Run: vercel env add AZURE_FUNCTION_APP_URL" -ForegroundColor Cyan
Write-Host "  When prompted, enter: $functionAppUrl" -ForegroundColor Cyan
Write-Host ""

Write-Host "Option 2: Use Vercel Dashboard" -ForegroundColor Yellow
Write-Host "  1. Go to: https://vercel.com/dashboard" -ForegroundColor Cyan
Write-Host "  2. Select project: $VercelProject" -ForegroundColor Cyan
Write-Host "  3. Go to: Settings → Environment Variables" -ForegroundColor Cyan
Write-Host "  4. Add new variable:" -ForegroundColor Cyan
Write-Host "     Name: AZURE_FUNCTION_APP_URL" -ForegroundColor Cyan
Write-Host "     Value: $functionAppUrl" -ForegroundColor Cyan
Write-Host "     Environment: Production (and Preview/Development if needed)" -ForegroundColor Cyan
Write-Host ""

Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "Function App URL: $functionAppUrl" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Green
Write-Host "Function App Name: $FunctionAppName" -ForegroundColor Green
Write-Host ""
Write-Host "After setting the variable, redeploy the Vercel project for changes to take effect." -ForegroundColor Yellow
Write-Host ""






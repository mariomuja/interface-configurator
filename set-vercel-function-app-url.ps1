# PowerShell Script to Set AZURE_FUNCTION_APP_URL in Vercel
# This script retrieves the Function App URL and sets it in Vercel

param(
    [string]$FunctionAppUrl = "",
    [string]$Environment = "production"
)

Write-Host "=== Set AZURE_FUNCTION_APP_URL in Vercel ===" -ForegroundColor Cyan
Write-Host ""

# Check if Vercel CLI is installed
if (-not (Get-Command vercel -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Vercel CLI is not installed" -ForegroundColor Red
    Write-Host "Install with: npm install -g vercel" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternatively, set the variable manually:" -ForegroundColor Yellow
    Write-Host "  1. Go to: https://vercel.com/dashboard" -ForegroundColor Cyan
    Write-Host "  2. Select project: interface-configuration" -ForegroundColor Cyan
    Write-Host "  3. Go to: Settings -> Environment Variables" -ForegroundColor Cyan
    Write-Host "  4. Add: AZURE_FUNCTION_APP_URL = https://func-integration-main.azurewebsites.net" -ForegroundColor Cyan
    exit 1
}

# Get Function App URL if not provided
if ([string]::IsNullOrEmpty($FunctionAppUrl)) {
    Write-Host "Getting Function App URL from Terraform..." -ForegroundColor Yellow
    
    $terraformDir = Join-Path $PSScriptRoot "terraform"
    if (Test-Path $terraformDir) {
        Push-Location $terraformDir
        try {
            $url = terraform output -raw function_app_url 2>$null
            if ($url -and $url -ne "null" -and $url -ne "" -and $url -notmatch "Error") {
                $FunctionAppUrl = $url
                Write-Host "Found URL from Terraform: $FunctionAppUrl" -ForegroundColor Green
            }
        } catch {
            Write-Host "Could not get URL from Terraform, trying Azure CLI..." -ForegroundColor Yellow
        } finally {
            Pop-Location
        }
    }
    
    # Try Azure CLI if Terraform didn't work
    if ([string]::IsNullOrEmpty($FunctionAppUrl)) {
        if (Get-Command az -ErrorAction SilentlyContinue) {
            Write-Host "Getting Function App URL from Azure..." -ForegroundColor Yellow
            $account = az account show 2>$null | ConvertFrom-Json
            if ($account) {
                $url = az functionapp show `
                    --name func-integration-main `
                    --resource-group rg-interface-configuration `
                    --query "defaultHostName" `
                    -o tsv 2>$null
                
                if ($url) {
                    $FunctionAppUrl = "https://$url"
                    Write-Host "Found URL from Azure: $FunctionAppUrl" -ForegroundColor Green
                }
            }
        }
    }
    
    # Use default if still not found
    if ([string]::IsNullOrEmpty($FunctionAppUrl)) {
        $FunctionAppUrl = "https://func-integration-main.azurewebsites.net"
        Write-Host "Using default URL: $FunctionAppUrl" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Function App URL: $FunctionAppUrl" -ForegroundColor Green
Write-Host "Environment: $Environment" -ForegroundColor Green
Write-Host ""

# Verify URL format
if (-not $FunctionAppUrl.StartsWith("http")) {
    Write-Host "WARNING: URL should start with https://" -ForegroundColor Yellow
    $FunctionAppUrl = "https://$FunctionAppUrl"
    Write-Host "Corrected URL: $FunctionAppUrl" -ForegroundColor Green
}

# Verify Function App is accessible
Write-Host "Verifying Function App is accessible..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$FunctionAppUrl/api/GetProcessLogs" -Method GET -TimeoutSec 10 -ErrorAction Stop
    Write-Host "✓ Function App is accessible (Status: $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "⚠ Warning: Could not verify Function App accessibility" -ForegroundColor Yellow
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "  The URL will still be set, but please verify manually" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Setting AZURE_FUNCTION_APP_URL in Vercel..." -ForegroundColor Yellow
Write-Host "Note: Vercel CLI will prompt you to select environments (Production, Preview, Development)" -ForegroundColor Cyan
Write-Host "Recommended: Select Production (and optionally Preview/Development)" -ForegroundColor Cyan
Write-Host ""

$confirm = Read-Host "Continue? (Y/N)"
if ($confirm -ne "Y" -and $confirm -ne "y") {
    Write-Host "Cancelled." -ForegroundColor Yellow
    exit 0
}

# Set environment variable using Vercel CLI
# Note: Vercel CLI doesn't support non-interactive mode for env add
# So we need to use echo to pipe the value
Write-Host ""
Write-Host "Running: echo '$FunctionAppUrl' | vercel env add AZURE_FUNCTION_APP_URL $Environment" -ForegroundColor Cyan
Write-Host ""

try {
    $FunctionAppUrl | vercel env add AZURE_FUNCTION_APP_URL $Environment
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ AZURE_FUNCTION_APP_URL set successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "  1. Redeploy your Vercel project for changes to take effect" -ForegroundColor Yellow
        Write-Host "  2. Go to: https://vercel.com/dashboard" -ForegroundColor Cyan
        Write-Host "  3. Select project -> Deployments -> Latest -> Redeploy" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Or run: vercel deploy --prod" -ForegroundColor Cyan
    } else {
        Write-Host ""
        Write-Host "⚠ Vercel CLI command may have failed. Please set manually:" -ForegroundColor Yellow
        Write-Host "  1. Go to: https://vercel.com/dashboard" -ForegroundColor Cyan
        Write-Host "  2. Select project: interface-configuration" -ForegroundColor Cyan
        Write-Host "  3. Go to: Settings -> Environment Variables" -ForegroundColor Cyan
        Write-Host "  4. Add new variable:" -ForegroundColor Cyan
        Write-Host "     Name: AZURE_FUNCTION_APP_URL" -ForegroundColor White
        Write-Host "     Value: $FunctionAppUrl" -ForegroundColor White
        Write-Host "     Environment: $Environment" -ForegroundColor White
    }
} catch {
    Write-Host ""
    Write-Host "Error setting environment variable:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Please set manually:" -ForegroundColor Yellow
    Write-Host "  1. Go to: https://vercel.com/dashboard" -ForegroundColor Cyan
    Write-Host "  2. Select project: interface-configuration" -ForegroundColor Cyan
    Write-Host "  3. Go to: Settings -> Environment Variables" -ForegroundColor Cyan
    Write-Host "  4. Add: AZURE_FUNCTION_APP_URL = $FunctionAppUrl" -ForegroundColor Cyan
}



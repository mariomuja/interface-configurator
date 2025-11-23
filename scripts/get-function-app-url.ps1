# Quick script to get Function App URL
# Usage: .\get-function-app-url.ps1 -FromTerraform
# Or: .\get-function-app-url.ps1 -ResourceGroup <rg> -FunctionAppName <name>

param(
    [string]$ResourceGroup = "",
    [string]$FunctionAppName = "",
    [switch]$FromTerraform = $false
)

Write-Host "=== Azure Function App URL Finder ===" -ForegroundColor Cyan
Write-Host ""

# Try Terraform first if requested
if ($FromTerraform) {
    Write-Host "Trying to get URL from Terraform outputs..." -ForegroundColor Yellow
    $terraformDir = Join-Path $PSScriptRoot "terraform"
    if (Test-Path $terraformDir) {
        Push-Location $terraformDir
        try {
            $url = terraform output -raw function_app_url 2>$null
            if ($url -and $url -ne "null" -and $url -ne "" -and $url -notmatch "Error") {
                Write-Host ""
                Write-Host "=== Function App URL (from Terraform) ===" -ForegroundColor Green
                Write-Host $url -ForegroundColor White -BackgroundColor DarkGreen
                Write-Host ""
                Write-Host "Copy this URL and set it as AZURE_FUNCTION_APP_URL in Vercel:" -ForegroundColor Yellow
                Write-Host "  https://vercel.com/dashboard -> Settings -> Environment Variables" -ForegroundColor Cyan
                Write-Host ""
                Write-Host "Quick command:" -ForegroundColor Cyan
                Write-Host "  vercel env add AZURE_FUNCTION_APP_URL production" -ForegroundColor White
                Write-Host ""
                
                # Copy to clipboard if available
                if (Get-Command Set-Clipboard -ErrorAction SilentlyContinue) {
                    $url | Set-Clipboard
                    Write-Host "✓ URL copied to clipboard!" -ForegroundColor Green
                }
                Pop-Location
                exit 0
            }
        } catch {
            Write-Host "Could not get URL from Terraform, trying Azure CLI..." -ForegroundColor Yellow
        } finally {
            Pop-Location
        }
    }
}

# Check Azure CLI
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Azure CLI not found" -ForegroundColor Red
    Write-Host "Install from: https://aka.ms/installazurecliwindows" -ForegroundColor Yellow
    exit 1
}

# Login check
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Logging in to Azure..." -ForegroundColor Yellow
    az login
}

# List all function apps if no parameters provided
if ([string]::IsNullOrEmpty($ResourceGroup) -and [string]::IsNullOrEmpty($FunctionAppName)) {
    Write-Host ""
    Write-Host "All Function Apps in your subscription:" -ForegroundColor Yellow
    az functionapp list --query "[].{Name:name, URL:defaultHostName, ResourceGroup:resourceGroup, State:state}" -o table
    Write-Host ""
    Write-Host "To get a specific Function App URL, run:" -ForegroundColor Cyan
    Write-Host "  .\get-function-app-url.ps1 -ResourceGroup <rg-name> -FunctionAppName <app-name>" -ForegroundColor White
    Write-Host ""
    Write-Host "Or use Terraform output:" -ForegroundColor Cyan
    Write-Host "  .\get-function-app-url.ps1 -FromTerraform" -ForegroundColor White
    exit 0
}

# Get resource group
if ([string]::IsNullOrEmpty($ResourceGroup)) {
    Write-Host "Available resource groups:" -ForegroundColor Yellow
    az group list --query "[].name" -o tsv | ForEach-Object { Write-Host "  - $_" }
    Write-Host ""
    $ResourceGroup = Read-Host "Enter resource group name"
}

# Get function app name
if ([string]::IsNullOrEmpty($FunctionAppName)) {
    Write-Host ""
    Write-Host "Function Apps in '$ResourceGroup':" -ForegroundColor Yellow
    az functionapp list --resource-group $ResourceGroup --query "[].{Name:name, State:state}" -o table
    Write-Host ""
    $FunctionAppName = Read-Host "Enter Function App name"
}

# Get URL
Write-Host ""
Write-Host "Retrieving Function App URL..." -ForegroundColor Yellow
$url = az functionapp show `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "defaultHostName" `
    -o tsv

if ($url) {
    $fullUrl = "https://$url"
    Write-Host ""
    Write-Host "=== Function App URL ===" -ForegroundColor Green
    Write-Host $fullUrl -ForegroundColor White -BackgroundColor DarkGreen
    Write-Host ""
    Write-Host "Copy this URL and set it as AZURE_FUNCTION_APP_URL in Vercel:" -ForegroundColor Yellow
    Write-Host "  https://vercel.com/dashboard -> Settings -> Environment Variables" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Quick command:" -ForegroundColor Cyan
    Write-Host "  vercel env add AZURE_FUNCTION_APP_URL production" -ForegroundColor White
    Write-Host "  (Then paste: $fullUrl)" -ForegroundColor Gray
    Write-Host ""
    
    # Copy to clipboard if available
    if (Get-Command Set-Clipboard -ErrorAction SilentlyContinue) {
        $fullUrl | Set-Clipboard
        Write-Host "✓ URL copied to clipboard!" -ForegroundColor Green
    }
} else {
    Write-Host "ERROR: Could not retrieve Function App URL" -ForegroundColor Red
    Write-Host "Please check:" -ForegroundColor Yellow
    Write-Host "  - Resource group name: $ResourceGroup" -ForegroundColor Yellow
    Write-Host "  - Function App name: $FunctionAppName" -ForegroundColor Yellow
    exit 1
}

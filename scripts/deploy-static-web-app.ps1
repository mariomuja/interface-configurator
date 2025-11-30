# Deploy Angular Frontend to Azure Static Web Apps
# This script builds the frontend and deploys it to Azure Static Web Apps

param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName = "rg-interface-configurator",
    
    [Parameter(Mandatory = $false)]
    [string]$StaticWebAppName = "swa-interface-configurator",
    
    [Parameter(Mandatory = $false)]
    [string]$Environment = "prod"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== Deploying Static Web App ===" -ForegroundColor Cyan
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor White
Write-Host "Static Web App Name: $StaticWebAppName" -ForegroundColor White
Write-Host "Environment: $Environment" -ForegroundColor White

# Check if Static Web App exists
Write-Host "`nChecking if Static Web App exists..." -ForegroundColor Yellow
$swa = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroupName 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Static Web App not found. Creating..." -ForegroundColor Yellow
    
    # Create Static Web App using Bicep
    Write-Host "Deploying Static Web App via Bicep..." -ForegroundColor Yellow
    $bicepParams = if ($Environment -eq "dev") {
        "bicep/parameters.dev.json"
    } else {
        "bicep/parameters.prod.json"
    }
    
    az deployment group create `
        --resource-group $ResourceGroupName `
        --template-file "bicep/main.bicep" `
        --parameters "@$bicepParams" `
        --parameters "enableStaticWebApp=true" `
        --parameters "staticWebAppName=$StaticWebAppName" `
        2>&1 | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to create Static Web App" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Static Web App created" -ForegroundColor Green
} else {
    Write-Host "✅ Static Web App exists" -ForegroundColor Green
}

# Build the frontend
Write-Host "`nBuilding frontend..." -ForegroundColor Yellow
Push-Location "frontend"

try {
    # Install dependencies
    Write-Host "Installing dependencies..." -ForegroundColor Gray
    npm ci 2>&1 | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to install dependencies" -ForegroundColor Red
        exit 1
    }
    
    # Build for production
    Write-Host "Building for production..." -ForegroundColor Gray
    npm run build:prod 2>&1 | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Build completed" -ForegroundColor Green
} finally {
    Pop-Location
}

# Get deployment token
Write-Host "`nGetting deployment token..." -ForegroundColor Yellow
$deploymentToken = az staticwebapp secrets list --name $StaticWebAppName --resource-group $ResourceGroupName --query "properties.apiKey" -o tsv 2>&1

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($deploymentToken)) {
    Write-Host "Getting deployment token from list-secrets..." -ForegroundColor Gray
    $secrets = az staticwebapp secrets list --name $StaticWebAppName --resource-group $ResourceGroupName 2>&1
    $deploymentToken = ($secrets | ConvertFrom-Json).properties.apiKey
    
    if ([string]::IsNullOrWhiteSpace($deploymentToken)) {
        Write-Host "❌ Failed to get deployment token" -ForegroundColor Red
        exit 1
    }
}

# Deploy using SWA CLI or direct API
Write-Host "`nDeploying to Azure Static Web Apps..." -ForegroundColor Yellow

# Check if SWA CLI is installed
$swaCliInstalled = Get-Command "swa" -ErrorAction SilentlyContinue

if ($swaCliInstalled) {
    Write-Host "Using SWA CLI for deployment..." -ForegroundColor Gray
    swa deploy `
        --app-location "frontend" `
        --output-location "frontend/dist/interface-configuration/browser" `
        --deployment-token $deploymentToken `
        --env "production" `
        --no-use-keychain
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Deployment failed" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "SWA CLI not found. Installing..." -ForegroundColor Yellow
    npm install -g @azure/static-web-apps-cli 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Using SWA CLI for deployment..." -ForegroundColor Gray
        swa deploy `
            --app-location "frontend" `
            --output-location "frontend/dist/interface-configuration/browser" `
            --deployment-token $deploymentToken `
            --env "production" `
            --no-use-keychain
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Deployment failed" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "⚠️  SWA CLI installation failed. Using Azure CLI..." -ForegroundColor Yellow
        
        # Alternative: Use Azure CLI to upload files
        Write-Host "Getting Static Web App URL..." -ForegroundColor Gray
        $swaUrl = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroupName --query "properties.defaultHostname" -o tsv
        
        Write-Host "⚠️  Manual deployment required:" -ForegroundColor Yellow
        Write-Host "1. Go to Azure Portal -> Static Web App -> Deployment Center" -ForegroundColor White
        Write-Host "2. Connect your GitHub repository" -ForegroundColor White
        Write-Host "3. Or use: az staticwebapp appsettings set --name $StaticWebAppName --resource-group $ResourceGroupName" -ForegroundColor White
        Write-Host "`nStatic Web App URL: https://$swaUrl" -ForegroundColor Cyan
    }
}

# Get the Static Web App URL
$swaUrl = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroupName --query "properties.defaultHostname" -o tsv

Write-Host "`n✅ Deployment completed!" -ForegroundColor Green
Write-Host "Static Web App URL: https://$swaUrl" -ForegroundColor Cyan
Write-Host "`nNote: If using GitLab CI/CD, deployments will be automatic on push to main branch." -ForegroundColor Gray










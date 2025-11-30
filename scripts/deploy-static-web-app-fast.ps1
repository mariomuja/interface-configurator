# Fast deployment script for Azure Static Web Apps
# Uses Azure CLI directly instead of Bicep for faster resource creation

param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName = "rg-interface-configurator",
    
    [Parameter(Mandatory = $false)]
    [string]$StaticWebAppName = "swa-interface-configurator",
    
    [Parameter(Mandatory = $false)]
    [string]$Location = "Central US",
    
    [Parameter(Mandatory = $false)]
    [string]$Sku = "Free"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== Fast Static Web App Deployment ===" -ForegroundColor Cyan
$startTime = Get-Date

# Step 1: Check if Static Web App exists (fast check)
Write-Host "`n[1/4] Checking if Static Web App exists..." -ForegroundColor Yellow
$swa = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroupName 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "Static Web App not found. Creating with Azure CLI (faster than Bicep)..." -ForegroundColor Yellow
    
    # Create Static Web App directly with Azure CLI (much faster than Bicep)
    az staticwebapp create `
        --name $StaticWebAppName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --sku $Sku `
        --output none 2>&1 | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to create Static Web App" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Static Web App created" -ForegroundColor Green
} else {
    Write-Host "✅ Static Web App already exists" -ForegroundColor Green
}

# Step 2: Build frontend (only if needed)
Write-Host "`n[2/4] Checking if build is needed..." -ForegroundColor Yellow
$buildNeeded = $true
$distPath = "frontend/dist/interface-configuration/browser"

if (Test-Path $distPath) {
    $lastBuild = (Get-Item $distPath).LastWriteTime
    $lastChange = (Get-ChildItem -Path "frontend/src" -Recurse -File | 
                   Sort-Object LastWriteTime -Descending | 
                   Select-Object -First 1).LastWriteTime
    
    if ($lastBuild -gt $lastChange) {
        Write-Host "Build is up to date. Skipping build..." -ForegroundColor Gray
        $buildNeeded = $false
    }
}

if ($buildNeeded) {
    Write-Host "Building frontend..." -ForegroundColor Yellow
    Push-Location "frontend"
    
    try {
        # Check if node_modules exists and is recent
        if (-not (Test-Path "node_modules") -or 
            (Get-Item "package-lock.json").LastWriteTime -gt (Get-Item "node_modules").LastWriteTime) {
            Write-Host "Installing dependencies..." -ForegroundColor Gray
            npm ci --silent 2>&1 | Out-Null
        } else {
            Write-Host "Dependencies are up to date" -ForegroundColor Gray
        }
        
        Write-Host "Building for production..." -ForegroundColor Gray
        npm run build:prod --silent 2>&1 | Out-Null
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Build failed" -ForegroundColor Red
            exit 1
        }
        
        Write-Host "✅ Build completed" -ForegroundColor Green
    } finally {
        Pop-Location
    }
} else {
    Write-Host "✅ Using existing build" -ForegroundColor Green
}

# Step 3: Get deployment token
Write-Host "`n[3/4] Getting deployment token..." -ForegroundColor Yellow
$deploymentToken = az staticwebapp secrets list --name $StaticWebAppName --resource-group $ResourceGroupName --query "properties.apiKey" -o tsv 2>&1

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($deploymentToken)) {
    # Try alternative method
    $secrets = az staticwebapp secrets list --name $StaticWebAppName --resource-group $ResourceGroupName 2>&1
    if ($LASTEXITCODE -eq 0) {
        $secretsJson = $secrets | ConvertFrom-Json
        $deploymentToken = $secretsJson.properties.apiKey
    }
    
    if ([string]::IsNullOrWhiteSpace($deploymentToken)) {
        Write-Host "⚠️  Could not get deployment token automatically" -ForegroundColor Yellow
        Write-Host "Getting deployment token manually..." -ForegroundColor Gray
        $deploymentToken = Read-Host "Please enter the deployment token (or get it from Azure Portal -> Static Web App -> Deployment token)"
    }
}

# Step 4: Deploy using SWA CLI (fastest method)
Write-Host "`n[4/4] Deploying to Azure Static Web Apps..." -ForegroundColor Yellow

# Check if SWA CLI is installed
$swaCliInstalled = Get-Command "swa" -ErrorAction SilentlyContinue

if (-not $swaCliInstalled) {
    Write-Host "Installing SWA CLI..." -ForegroundColor Gray
    npm install -g @azure/static-web-apps-cli --silent 2>&1 | Out-Null
}

if (Get-Command "swa" -ErrorAction SilentlyContinue) {
    Write-Host "Deploying with SWA CLI..." -ForegroundColor Gray
    swa deploy `
        --app-location "frontend" `
        --output-location "frontend/dist/interface-configuration/browser" `
        --deployment-token $deploymentToken `
        --env "production" `
        --no-use-keychain `
        --no-interactive 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Deployment completed!" -ForegroundColor Green
    } else {
        Write-Host "⚠️  SWA CLI deployment had issues. Trying alternative method..." -ForegroundColor Yellow
        # Fallback: Use Azure CLI
        $swaUrl = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroupName --query "properties.defaultHostname" -o tsv
        Write-Host "Please deploy manually via GitLab CI/CD or Azure Portal" -ForegroundColor Yellow
        Write-Host "Static Web App URL: https://$swaUrl" -ForegroundColor Cyan
    }
} else {
    Write-Host "⚠️  SWA CLI not available. Using GitLab CI/CD pipeline..." -ForegroundColor Yellow
    Write-Host "Push to main branch to trigger automatic deployment" -ForegroundColor Gray
}

# Get the Static Web App URL
$swaUrl = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroupName --query "properties.defaultHostname" -o tsv

$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host "`n=== Deployment Summary ===" -ForegroundColor Cyan
Write-Host "Static Web App URL: https://$swaUrl" -ForegroundColor Green
Write-Host "Total time: $([math]::Round($duration.TotalSeconds, 2)) seconds" -ForegroundColor Gray
Write-Host "`n💡 Tip: For fastest deployments, use GitLab CI/CD pipeline (automatic on push)" -ForegroundColor Yellow










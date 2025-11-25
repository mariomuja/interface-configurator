# Build and Push Adapter Docker Images to Azure Container Registry
# This script builds Docker images for each adapter type and pushes them to ACR

param(
    [Parameter(Mandatory=$true)]
    [string]$ContainerRegistryName,
    
    [string]$ResourceGroup = "rg-interface-configuration",
    
    [string]$DockerfilePath = "docker/Dockerfile.adapter",
    
    [string[]]$AdapterTypes = @("csv", "sqlserver", "sap", "dynamics365", "crm", "file", "sftp")
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== Build and Push Adapter Docker Images ===" -ForegroundColor Cyan
Write-Host "Container Registry: $ContainerRegistryName" -ForegroundColor White
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "Dockerfile: $DockerfilePath" -ForegroundColor White
Write-Host "Adapter Types: $($AdapterTypes -join ', ')" -ForegroundColor White

# Step 1: Verify Container Registry exists
Write-Host "`n[1] Verifying Container Registry..." -ForegroundColor Yellow
$acr = az acr show --name $ContainerRegistryName --resource-group $ResourceGroup --query "{Name:name, LoginServer:loginServer, AdminUserEnabled:adminUserEnabled}" -o json 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Container Registry not found: $ContainerRegistryName" -ForegroundColor Red
    Write-Host "Error: $acr" -ForegroundColor Red
    exit 1
}

$acrInfo = $acr | ConvertFrom-Json
$loginServer = $acrInfo.loginServer
Write-Host "✅ Container Registry found: $loginServer" -ForegroundColor Green

# Step 2: Enable admin user if not enabled
if (-not $acrInfo.adminUserEnabled) {
    Write-Host "`n[2] Enabling admin user..." -ForegroundColor Yellow
    az acr update --name $ContainerRegistryName --admin-enabled true | Out-Null
    Write-Host "✅ Admin user enabled" -ForegroundColor Green
} else {
    Write-Host "`n[2] Admin user already enabled" -ForegroundColor Green
}

# Step 3: Login to ACR
Write-Host "`n[3] Logging in to Container Registry..." -ForegroundColor Yellow
az acr login --name $ContainerRegistryName
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to login to Container Registry" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Logged in successfully" -ForegroundColor Green

# Step 4: Verify Dockerfile exists
Write-Host "`n[4] Verifying Dockerfile..." -ForegroundColor Yellow
if (-not (Test-Path $DockerfilePath)) {
    Write-Host "❌ Dockerfile not found: $DockerfilePath" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Dockerfile found" -ForegroundColor Green

# Step 5: Build and push images for each adapter type
Write-Host "`n[5] Building and pushing adapter images..." -ForegroundColor Yellow

$buildContext = Split-Path -Parent $DockerfilePath
if (-not $buildContext) {
    $buildContext = "."
}

foreach ($adapterType in $AdapterTypes) {
    Write-Host "`n  Processing $adapterType adapter..." -ForegroundColor Cyan
    
    $imageName = "$loginServer/${adapterType}-adapter"
    $imageTag = "latest"
    $fullImageName = "${imageName}:${imageTag}"
    
    Write-Host "    Image: $fullImageName" -ForegroundColor White
    
    # Build Docker image
    Write-Host "    Building Docker image..." -ForegroundColor Yellow
    $buildArgs = @(
        "build",
        "-f", $DockerfilePath,
        "-t", $fullImageName,
        $buildContext
    )
    
    docker $buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    ❌ Failed to build image for $adapterType" -ForegroundColor Red
        continue
    }
    Write-Host "    ✅ Image built successfully" -ForegroundColor Green
    
    # Push Docker image
    Write-Host "    Pushing to Container Registry..." -ForegroundColor Yellow
    docker push $fullImageName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    ❌ Failed to push image for $adapterType" -ForegroundColor Red
        continue
    }
    Write-Host "    ✅ Image pushed successfully" -ForegroundColor Green
}

# Step 6: List all images in registry
Write-Host "`n[6] Listing images in Container Registry..." -ForegroundColor Yellow
$images = az acr repository list --name $ContainerRegistryName --output table
Write-Host $images

Write-Host "`n=== Build and Push Complete ===" -ForegroundColor Green
Write-Host "All adapter images have been built and pushed to: $loginServer" -ForegroundColor White


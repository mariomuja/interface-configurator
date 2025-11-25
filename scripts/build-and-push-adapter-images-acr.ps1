# Build and Push Adapter Docker Images using Azure Container Registry Build
# This script uses ACR Build which doesn't require Docker Desktop

param(
    [Parameter(Mandatory=$true)]
    [string]$ContainerRegistryName,
    
    [string]$ResourceGroup = "rg-interface-configuration",
    
    [string]$DockerfilePath = "docker/Dockerfile.adapter",
    
    [string[]]$AdapterTypes = @("csv", "sqlserver", "sap", "dynamics365", "crm", "file", "sftp")
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== Build and Push Adapter Docker Images (ACR Build) ===" -ForegroundColor Cyan
Write-Host "Container Registry: $ContainerRegistryName" -ForegroundColor White
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "Dockerfile: $DockerfilePath" -ForegroundColor White
Write-Host "Adapter Types: $($AdapterTypes -join ', ')" -ForegroundColor White

# Step 1: Verify Container Registry exists
Write-Host "`n[1] Verifying Container Registry..." -ForegroundColor Yellow
$acr = az acr show --name $ContainerRegistryName --resource-group $ResourceGroup --query "{Name:name, LoginServer:loginServer}" -o json 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Container Registry not found: $ContainerRegistryName" -ForegroundColor Red
    Write-Host "Error: $acr" -ForegroundColor Red
    exit 1
}

$acrInfo = $acr | ConvertFrom-Json
$loginServer = $acrInfo.loginServer
Write-Host "✅ Container Registry found: $loginServer" -ForegroundColor Green

# Step 2: Verify Dockerfile exists
Write-Host "`n[2] Verifying Dockerfile..." -ForegroundColor Yellow
if (-not (Test-Path $DockerfilePath)) {
    Write-Host "❌ Dockerfile not found: $DockerfilePath" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Dockerfile found" -ForegroundColor Green

# Step 3: Get build context directory
$buildContext = Split-Path -Parent $DockerfilePath
if (-not $buildContext -or $buildContext -eq "") {
    $buildContext = "."
}
$buildContext = Resolve-Path $buildContext
Write-Host "`n[3] Build context: $buildContext" -ForegroundColor Yellow

# Step 4: Build and push images for each adapter type using ACR Build
Write-Host "`n[4] Building and pushing adapter images using ACR Build..." -ForegroundColor Yellow

foreach ($adapterType in $AdapterTypes) {
    Write-Host "`n  Processing $adapterType adapter..." -ForegroundColor Cyan
    
    $imageName = "${adapterType}-adapter"
    $imageTag = "latest"
    $fullImageName = "${imageName}:${imageTag}"
    
    Write-Host "    Image: $loginServer/$fullImageName" -ForegroundColor White
    
    # Build Docker image using ACR Build
    Write-Host "    Building Docker image with ACR Build..." -ForegroundColor Yellow
    Write-Host "    This may take several minutes..." -ForegroundColor White
    
    # Run ACR build and capture output
    $buildOutput = az acr build `
        --registry $ContainerRegistryName `
        --image $fullImageName `
        --file $DockerfilePath `
        $buildContext 2>&1 | Tee-Object -Variable buildResult
    
    # Check exit code
    if ($LASTEXITCODE -ne 0) {
        Write-Host "    ❌ Failed to build image for $adapterType" -ForegroundColor Red
        Write-Host "    Error output:" -ForegroundColor Red
        $buildResult | Where-Object { $_ -notmatch "WARNING" } | ForEach-Object { Write-Host "      $_" -ForegroundColor Red }
        continue
    }
    
    Write-Host "    ✅ Image built and pushed successfully" -ForegroundColor Green
}

# Step 5: List all images in registry
Write-Host "`n[5] Listing images in Container Registry..." -ForegroundColor Yellow
$repositories = az acr repository list --name $ContainerRegistryName --output table
if ($repositories) {
    Write-Host $repositories
    Write-Host "`n✅ Repositories found in Container Registry" -ForegroundColor Green
} else {
    Write-Host "⚠️  No repositories found yet" -ForegroundColor Yellow
}

Write-Host "`n=== Build and Push Complete ===" -ForegroundColor Green
Write-Host "All adapter images have been built and pushed to: $loginServer" -ForegroundColor White


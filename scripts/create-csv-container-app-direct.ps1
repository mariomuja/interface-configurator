# Create CSV Adapter Container App directly using Azure CLI/REST API
# Times the creation process

param(
    [string]$ResourceGroup = "rg-interface-configurator",
    [string]$ContainerAppEnvironment = "cae-adapter-instances",
    [string]$Location = "centralus",
    [string]$RegistryServer = "acrinterfaceconfig.azurecr.io"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== Create CSV Adapter Container App (Direct) ===" -ForegroundColor Cyan
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "Environment: $ContainerAppEnvironment" -ForegroundColor White
Write-Host ""

# Generate GUID for adapter instance
$adapterInstanceGuid = [System.Guid]::NewGuid()
# Container app name format: ca-{first-24-chars-of-guid-without-dashes}
# Based on ContainerAppService.GetContainerAppName implementation
$guidNoDashes = $adapterInstanceGuid.ToString("N")
$containerAppName = "ca-$($guidNoDashes.Substring(0, [Math]::Min(24, $guidNoDashes.Length)))"
Write-Host "Generated Adapter Instance GUID: $adapterInstanceGuid" -ForegroundColor White
Write-Host "Container App Name: $containerAppName" -ForegroundColor White
Write-Host ""

# Get ACR credentials
Write-Host "[1] Getting ACR credentials..." -ForegroundColor Yellow
$acrCredentials = az acr credential show --name "acrinterfaceconfig" --resource-group $ResourceGroup --query "{username:username, password:passwords[0].value}" -o json 2>&1 | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to get ACR credentials" -ForegroundColor Red
    exit 1
}
Write-Host "✅ ACR credentials retrieved" -ForegroundColor Green
Write-Host ""

# Get storage account connection string (for blob storage)
Write-Host "[2] Getting storage account connection string..." -ForegroundColor Yellow
$storageAccount = az storage account list --resource-group $ResourceGroup --query "[0].{Name:name}" -o json 2>&1 | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or !$storageAccount) {
    Write-Host "❌ Failed to get storage account" -ForegroundColor Red
    exit 1
}
$storageKey = az storage account keys list --resource-group $ResourceGroup --account-name $storageAccount.name --query "[0].value" -o tsv 2>&1
$blobConnectionString = "DefaultEndpointsProtocol=https;AccountName=$($storageAccount.name);AccountKey=$storageKey;EndpointSuffix=core.windows.net"
Write-Host "✅ Storage connection string retrieved" -ForegroundColor Green
Write-Host ""

# Get Service Bus connection string
Write-Host "[3] Getting Service Bus connection string..." -ForegroundColor Yellow
$serviceBusNamespace = az servicebus namespace list --resource-group $ResourceGroup --query "[0].{Name:name}" -o json 2>&1 | ConvertFrom-Json
if ($serviceBusNamespace) {
    $serviceBusKey = az servicebus namespace authorization-rule keys list --resource-group $ResourceGroup --namespace-name $serviceBusNamespace.name --name "RootManageSharedAccessKey" --query "primaryConnectionString" -o tsv 2>&1
    $serviceBusConnectionString = $serviceBusKey
} else {
    $serviceBusConnectionString = ""
    Write-Host "⚠️  Service Bus namespace not found, using empty connection string" -ForegroundColor Yellow
}
Write-Host "✅ Service Bus connection string retrieved" -ForegroundColor Green
Write-Host ""

# Check if container app environment exists, create if it doesn't
Write-Host "[4] Checking/Creating Container App Environment..." -ForegroundColor Yellow
$ErrorActionPreference = "Continue"
$envCheck = az containerapp env show --name $ContainerAppEnvironment --resource-group $ResourceGroup --query "name" -o tsv 2>&1
$ErrorActionPreference = "Stop"
$envExists = $envCheck -match $ContainerAppEnvironment -and $LASTEXITCODE -eq 0

if (-not $envExists) {
    Write-Host "   Environment does not exist. Creating it now..." -ForegroundColor Yellow
    Write-Host "   This may take 2-5 minutes..." -ForegroundColor Gray
    $envCreateStart = Get-Date
    
    $ErrorActionPreference = "Continue"
    $envCreateResult = az containerapp env create `
        --name $ContainerAppEnvironment `
        --resource-group $ResourceGroup `
        --location $Location `
        --query "name" -o tsv 2>&1
    $ErrorActionPreference = "Stop"
    
    if ($LASTEXITCODE -eq 0 -or $envCreateResult -match $ContainerAppEnvironment) {
        $envCreateEnd = Get-Date
        $envCreateDuration = $envCreateEnd - $envCreateStart
        Write-Host "   ✅ Environment created in $([math]::Round($envCreateDuration.TotalSeconds, 2)) seconds" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Failed to create environment" -ForegroundColor Red
        Write-Host "   Error: $envCreateResult" -ForegroundColor Red
        Write-Host "   Continuing anyway - may already exist..." -ForegroundColor Yellow
    }
} else {
    Write-Host "   ✅ Environment exists" -ForegroundColor Green
}
Write-Host ""

# Start timer
Write-Host "[5] Creating Container App..." -ForegroundColor Yellow
Write-Host "⏱️  Timer started..." -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    # Create container app using Azure CLI
    # This will create the container app with all necessary configuration
    $createCommand = @(
        "containerapp", "create",
        "--name", $containerAppName,
        "--resource-group", $ResourceGroup,
        "--environment", $ContainerAppEnvironment,
        "--image", "$RegistryServer/csv-adapter:latest",
        "--registry-server", $RegistryServer,
        "--registry-username", $acrCredentials.username,
        "--registry-password", $acrCredentials.password,
        "--target-port", "8080",
        "--ingress", "internal",
        "--min-replicas", "0",
        "--max-replicas", "1",
        "--cpu", "0.5",
        "--memory", "1.0Gi",
        "--env-vars",
        "ADAPTER_INSTANCE_GUID=$adapterInstanceGuid",
        "ADAPTER_NAME=CSV",
        "ADAPTER_TYPE=Source",
        "INTERFACE_NAME=TestInterface-CSV",
        "INSTANCE_NAME=CSV-Source-Test",
        "BLOB_CONNECTION_STRING=$blobConnectionString",
        "BLOB_CONTAINER_NAME=adapter-$($adapterInstanceGuid.ToString().Replace('-', ''))",
        "ADAPTER_CONFIG_PATH=adapter-config.json",
        "AZURE_SERVICEBUS_CONNECTION_STRING=$serviceBusConnectionString",
        "--query", "properties.provisioningState",
        "-o", "tsv"
    )
    
    $result = & az $createCommand 2>&1
    
    $stopwatch.Stop()
    $endTime = Get-Date
    $duration = $stopwatch.Elapsed
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Container App Creation Initiated!" -ForegroundColor Green
        Write-Host ""
        Write-Host "=== Timing Results ===" -ForegroundColor Cyan
        Write-Host "Start Time:    $($startTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))" -ForegroundColor White
        Write-Host "End Time:      $($endTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))" -ForegroundColor White
        Write-Host "Duration:      $([math]::Round($duration.TotalSeconds, 2)) seconds" -ForegroundColor Green
        Write-Host "               $([math]::Round($duration.TotalMinutes, 2)) minutes" -ForegroundColor White
        Write-Host ""
        Write-Host "Provisioning State: $result" -ForegroundColor White
        Write-Host ""
        Write-Host "=== Container App Details ===" -ForegroundColor Cyan
        Write-Host "Container App Name: $containerAppName" -ForegroundColor White
        Write-Host "Adapter Instance GUID: $adapterInstanceGuid" -ForegroundColor White
        Write-Host ""
        Write-Host "Note: Container app provisioning may take additional time (1-3 minutes)" -ForegroundColor Yellow
        Write-Host "Check status with: az containerapp show --name $containerAppName --resource-group $ResourceGroup" -ForegroundColor Gray
    } else {
        Write-Host "❌ Failed to create Container App" -ForegroundColor Red
        Write-Host "Error output: $result" -ForegroundColor Red
        Write-Host "Duration before failure: $($duration.TotalSeconds) seconds" -ForegroundColor Yellow
        exit 1
    }
    
} catch {
    $stopwatch.Stop()
    Write-Host "❌ Exception creating Container App" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Duration before failure: $($stopwatch.Elapsed.TotalSeconds) seconds" -ForegroundColor Yellow
    exit 1
}


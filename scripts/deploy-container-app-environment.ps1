# Deploy Container App Environment infrastructure and configure ACR credentials

param(
    [string]$ResourceGroup = "rg-interface-configurator",
    [string]$FunctionAppName = "func-integration-main",
    [string]$ContainerRegistryName = "acrinterfaceconfig",
    [string]$BicepTemplateFile = "bicep/main.bicep",
    [string]$ParametersFile = "bicep/parameters.json"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== Deploy Container App Environment Infrastructure ===" -ForegroundColor Cyan
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "Function App: $FunctionAppName" -ForegroundColor White
Write-Host "Container Registry: $ContainerRegistryName" -ForegroundColor White
Write-Host ""

# Step 1: Check if resource group exists
Write-Host "[1] Checking Resource Group..." -ForegroundColor Yellow
$rgExists = az group show --name $ResourceGroup --query "name" -o tsv 2>&1
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($rgExists)) {
    Write-Host "   Resource group does not exist. Creating it..." -ForegroundColor Yellow
    az group create --name $ResourceGroup --location "Central US" --output none
    Write-Host "   ✅ Resource group created" -ForegroundColor Green
} else {
    Write-Host "   ✅ Resource group exists" -ForegroundColor Green
}
Write-Host ""

# Step 2: Deploy Bicep template
Write-Host "[2] Deploying Bicep template..." -ForegroundColor Yellow
Write-Host "   This will create the Container App Environment and update Function App settings..." -ForegroundColor Gray
Write-Host "   ⏱️  This may take 5-10 minutes..." -ForegroundColor Cyan
Write-Host ""

$deployStartTime = Get-Date
$deployStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$ErrorActionPreference = "Continue"
$deploymentOutput = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $BicepTemplateFile `
    --parameters $ParametersFile `
    --name "deploy-container-app-env-$(Get-Date -Format 'yyyyMMdd-HHmmss')" `
    --output json 2>&1

$deployStopwatch.Stop()
$ErrorActionPreference = "Stop"

# Filter out warnings and get JSON output
$jsonOutput = $deploymentOutput | Where-Object { $_ -notmatch "WARNING|UserWarning" } | Out-String

if ($LASTEXITCODE -eq 0) {
    try {
        $deploymentResult = $jsonOutput | ConvertFrom-Json
        if ($deploymentResult.properties.provisioningState -eq "Succeeded") {
            Write-Host "   ✅ Deployment succeeded!" -ForegroundColor Green
            Write-Host "   Duration: $([math]::Round($deployStopwatch.Elapsed.TotalSeconds, 2)) seconds" -ForegroundColor White
        } else {
            Write-Host "   ⚠️  Deployment completed with state: $($deploymentResult.properties.provisioningState)" -ForegroundColor Yellow
        }
    } catch {
        # If JSON parsing fails but exit code is 0, deployment may have succeeded
        Write-Host "   ⚠️  Deployment command completed (could not parse output)" -ForegroundColor Yellow
        Write-Host "   Exit code: $LASTEXITCODE" -ForegroundColor Gray
    }
} else {
    Write-Host "   ❌ Deployment failed!" -ForegroundColor Red
    Write-Host "   Exit code: $LASTEXITCODE" -ForegroundColor Red
    Write-Host "   Output: $deploymentOutput" -ForegroundColor Red
    Write-Host "   Duration before failure: $([math]::Round($deployStopwatch.Elapsed.TotalSeconds, 2)) seconds" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Step 3: Verify Container App Environment was created
Write-Host "[3] Verifying Container App Environment..." -ForegroundColor Yellow
$ErrorActionPreference = "Continue"
$envCheck = az containerapp env show --name "cae-adapter-instances" --resource-group $ResourceGroup --query "name" -o tsv 2>&1
$ErrorActionPreference = "Stop"

if ($envCheck -match "cae-adapter-instances") {
    Write-Host "   ✅ Container App Environment 'cae-adapter-instances' exists" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Container App Environment may not exist yet (checking status...)" -ForegroundColor Yellow
    Write-Host "   Output: $envCheck" -ForegroundColor Gray
}
Write-Host ""

# Step 4: Get ACR credentials and configure Function App
Write-Host "[4] Configuring Container Registry credentials in Function App..." -ForegroundColor Yellow

# Check if ACR exists
$ErrorActionPreference = "Continue"
$acrExists = az acr show --name $ContainerRegistryName --resource-group $ResourceGroup --query "name" -o tsv 2>&1
$ErrorActionPreference = "Stop"

if (-not ($acrExists -match $ContainerRegistryName)) {
    Write-Host "   ⚠️  Container Registry '$ContainerRegistryName' not found in resource group $ResourceGroup" -ForegroundColor Yellow
    Write-Host "   Please check the ACR name and resource group, or create the ACR first." -ForegroundColor Yellow
    Write-Host "   Skipping ACR credential configuration..." -ForegroundColor Gray
} else {
    Write-Host "   ✅ Container Registry found: $ContainerRegistryName" -ForegroundColor Green
    
    # Get ACR credentials
    Write-Host "   Retrieving ACR admin credentials..." -ForegroundColor Gray
    $ErrorActionPreference = "Continue"
    $acrCredentials = az acr credential show --name $ContainerRegistryName --resource-group $ResourceGroup --query "{username:username, password:passwords[0].value}" -o json 2>&1 | ConvertFrom-Json
    $ErrorActionPreference = "Stop"
    
    if ($acrCredentials -and $acrCredentials.username) {
        $acrServer = "${ContainerRegistryName}.azurecr.io"
        $acrUsername = $acrCredentials.username
        $acrPassword = $acrCredentials.password
        
        Write-Host "   ✅ ACR credentials retrieved" -ForegroundColor Green
        
        # Configure Function App settings
        Write-Host "   Setting Function App configuration..." -ForegroundColor Gray
        $ErrorActionPreference = "Continue"
        $settingResult = az functionapp config appsettings set `
            --resource-group $ResourceGroup `
            --name $FunctionAppName `
            --settings `
                ContainerRegistryServer="${acrServer}" `
                ContainerRegistryUsername="${acrUsername}" `
                ContainerRegistryPassword="${acrPassword}" `
            --output none 2>&1
        $ErrorActionPreference = "Stop"
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✅ Container Registry credentials configured in Function App" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️  Failed to set ACR credentials: $settingResult" -ForegroundColor Yellow
            Write-Host "   You may need to set these manually in Azure Portal:" -ForegroundColor Yellow
            Write-Host "   - ContainerRegistryServer: $acrServer" -ForegroundColor Gray
            Write-Host "   - ContainerRegistryUsername: $acrUsername" -ForegroundColor Gray
            Write-Host "   - ContainerRegistryPassword: [hidden]" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ⚠️  Failed to retrieve ACR credentials" -ForegroundColor Yellow
        Write-Host "   You may need to enable admin credentials on ACR first:" -ForegroundColor Yellow
        Write-Host "   az acr update --name $ContainerRegistryName --admin-enabled true" -ForegroundColor Gray
    }
}
Write-Host ""

# Step 5: Verify Function App settings
Write-Host "[5] Verifying Function App configuration..." -ForegroundColor Yellow
$ErrorActionPreference = "Continue"
$appSettings = az functionapp config appsettings list `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "[?contains(name, 'Container') || contains(name, 'ResourceGroup') || contains(name, 'Location') || contains(name, 'ContainerAppEnvironment')].{Name:name, Value:value}" `
    -o json 2>&1 | ConvertFrom-Json
$ErrorActionPreference = "Stop"

if ($appSettings) {
    Write-Host "   ✅ Function App settings:" -ForegroundColor Green
    foreach ($setting in $appSettings) {
        $displayValue = if ($setting.Value -match "password|secret|key" -or $setting.Name -match "Password") { "[HIDDEN]" } else { $setting.Value }
        Write-Host "      $($setting.Name): $displayValue" -ForegroundColor White
    }
} else {
    Write-Host "   ⚠️  Could not retrieve Function App settings" -ForegroundColor Yellow
}
Write-Host ""

# Summary
Write-Host "=== Deployment Summary ===" -ForegroundColor Cyan
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "Container App Environment: cae-adapter-instances" -ForegroundColor White
Write-Host "Function App: $FunctionAppName" -ForegroundColor White
Write-Host "Deployment Duration: $([math]::Round($deployStopwatch.Elapsed.TotalSeconds, 2)) seconds" -ForegroundColor White
Write-Host ""
Write-Host "✅ Infrastructure deployment complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Verify Container App Environment exists:" -ForegroundColor White
Write-Host "   az containerapp env show --name cae-adapter-instances --resource-group $ResourceGroup" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Test creating a container app:" -ForegroundColor White
Write-Host "   .\scripts\test-create-csv-container-app.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Check container app status in Azure Portal:" -ForegroundColor White
Write-Host "   https://portal.azure.com/#@mariomujagmail508.onmicrosoft.com/resource/subscriptions/f1e8e2a3-2bf1-43f0-8f19-37abd624205c/resourceGroups/$ResourceGroup/providers/Microsoft.App/managedEnvironments/cae-adapter-instances" -ForegroundColor Gray

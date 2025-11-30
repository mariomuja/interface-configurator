# Test script to create a CSV adapter container app and measure time
# This uses the Function App API endpoint

param(
    [string]$FunctionAppName = "func-integration-main",
    [string]$ResourceGroup = "rg-interface-configurator",
    [string]$InterfaceName = "TestInterface-CSV-Timed",
    [string]$InstanceName = "CSV-Source-Test"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== Create CSV Adapter Container App (via Function App API) ===" -ForegroundColor Cyan
Write-Host "Function App: $FunctionAppName" -ForegroundColor White
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host ""

# Step 1: Get Function App URL
Write-Host "[1] Getting Function App URL..." -ForegroundColor Yellow
$ErrorActionPreference = "Continue"
$functionAppUrlRaw = az functionapp show --resource-group $ResourceGroup --name $FunctionAppName --query "defaultHostName" -o tsv 2>&1
$ErrorActionPreference = "Stop"
$functionAppUrl = ($functionAppUrlRaw | Where-Object { $_ -match "azurewebsites\.net" } | Select-Object -First 1).ToString().Trim()
if ([string]::IsNullOrWhiteSpace($functionAppUrl)) {
    Write-Host "❌ Failed to get Function App URL" -ForegroundColor Red
    Write-Host "Raw output: $functionAppUrlRaw" -ForegroundColor Red
    exit 1
}
$baseUrl = "https://$functionAppUrl"
Write-Host "✅ Function App URL: $baseUrl" -ForegroundColor Green
Write-Host ""

# Step 2: Check if Container App Environment exists
Write-Host "[2] Checking Container App Environment..." -ForegroundColor Yellow
$ErrorActionPreference = "Continue"
# Use az resource show instead of containerapp env show to avoid permission issues
$envCheck = az resource show --ids "/subscriptions/f1e8e2a3-2bf1-43f0-8f19-37abd624205c/resourceGroups/$ResourceGroup/providers/Microsoft.App/managedEnvironments/cae-adapter-instances" --query "name" -o tsv 2>&1
$ErrorActionPreference = "Stop"
$envExists = $envCheck -match "cae-adapter-instances" -and $LASTEXITCODE -eq 0

if (-not $envExists) {
    Write-Host "⚠️  Container App Environment 'cae-adapter-instances' does not exist!" -ForegroundColor Yellow
    Write-Host "   Please deploy the infrastructure first using Bicep or Terraform." -ForegroundColor Yellow
    Write-Host "   The environment will be created automatically during deployment." -ForegroundColor Gray
    Write-Host ""
    Write-Host "   To deploy:" -ForegroundColor White
    Write-Host "   Bicep: az deployment group create --resource-group $ResourceGroup --template-file bicep/main.bicep --parameters @bicep/parameters.json" -ForegroundColor Gray
    Write-Host "   Terraform: cd terraform && terraform apply" -ForegroundColor Gray
    Write-Host ""
    exit 1
} else {
    Write-Host "✅ Container App Environment exists" -ForegroundColor Green
}
Write-Host ""

# Step 3: Generate adapter instance GUID
$adapterInstanceGuid = [System.Guid]::NewGuid()
Write-Host "[3] Generated Adapter Instance GUID: $adapterInstanceGuid" -ForegroundColor White
Write-Host ""

# Step 4: Prepare request
Write-Host "[4] Preparing request..." -ForegroundColor Yellow
$createRequest = @{
    AdapterInstanceGuid = $adapterInstanceGuid.ToString()
    AdapterName = "CSV"
    AdapterType = "Source"
    InterfaceName = $InterfaceName
    InstanceName = $InstanceName
} | ConvertTo-Json

Write-Host "Request:" -ForegroundColor Gray
Write-Host $createRequest -ForegroundColor DarkGray
Write-Host ""

# Step 5: Create container app and measure time
Write-Host "[5] Creating Container App via API..." -ForegroundColor Yellow
Write-Host "⏱️  Timer started..." -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    $headers = @{
        "Content-Type" = "application/json"
    }
    
    $response = Invoke-RestMethod `
        -Uri "$baseUrl/api/CreateContainerApp" `
        -Method Post `
        -Body $createRequest `
        -Headers $headers `
        -ErrorAction Stop
    
    $stopwatch.Stop()
    $endTime = Get-Date
    $duration = $stopwatch.Elapsed
    
    Write-Host "✅ Container App Created Successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "=== Timing Results ===" -ForegroundColor Cyan
    Write-Host "Start Time:    $($startTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))" -ForegroundColor White
    Write-Host "End Time:      $($endTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))" -ForegroundColor White
    Write-Host "Duration:      $([math]::Round($duration.TotalSeconds, 2)) seconds" -ForegroundColor Green
    Write-Host "               $([math]::Round($duration.TotalMinutes, 2)) minutes" -ForegroundColor White
    Write-Host ""
    
    Write-Host "=== Container App Details ===" -ForegroundColor Cyan
    Write-Host "Container App Name: $($response.ContainerAppName)" -ForegroundColor White
    Write-Host "Adapter Instance GUID: $adapterInstanceGuid" -ForegroundColor White
    Write-Host "Adapter Name: CSV" -ForegroundColor White
    Write-Host "Adapter Type: Source" -ForegroundColor White
    Write-Host "Interface Name: $InterfaceName" -ForegroundColor White
    Write-Host "Instance Name: $InstanceName" -ForegroundColor White
    Write-Host ""
    
    # Step 6: Check container app status (wait a bit first)
    Write-Host "[6] Checking Container App Status..." -ForegroundColor Yellow
    Write-Host "   Waiting 5 seconds for provisioning to start..." -ForegroundColor Gray
    Start-Sleep -Seconds 5
    
    try {
        $statusResponse = Invoke-RestMethod `
            -Uri "$baseUrl/api/GetContainerAppStatus?adapterInstanceGuid=$adapterInstanceGuid" `
            -Method Get `
            -Headers $headers `
            -ErrorAction Stop
        
        Write-Host "   Status: $($statusResponse.Status)" -ForegroundColor $(if ($statusResponse.Status -eq "Running") { "Green" } else { "Yellow" })
        if ($statusResponse.Message) {
            Write-Host "   Message: $($statusResponse.Message)" -ForegroundColor White
        }
        Write-Host ""
        Write-Host "   Note: Container app provisioning may take 1-3 minutes to complete." -ForegroundColor Gray
        Write-Host "   Status may show 'Pending' or 'Provisioning' initially." -ForegroundColor Gray
    } catch {
        Write-Host "   ⚠️  Could not get container app status yet (may still be provisioning)" -ForegroundColor Yellow
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "=== Summary ===" -ForegroundColor Cyan
    Write-Host "Container App creation API call took: $([math]::Round($duration.TotalSeconds, 2)) seconds" -ForegroundColor Green
    Write-Host ""
    Write-Host "Note: The actual container app provisioning in Azure continues in the background." -ForegroundColor Gray
    Write-Host "You can check the status in Azure Portal:" -ForegroundColor White
    Write-Host "https://portal.azure.com/#@mariomujagmail508.onmicrosoft.com/resource/subscriptions/f1e8e2a3-2bf1-43f0-8f19-37abd624205c/resourceGroups/$ResourceGroup/providers/Microsoft.App/containerApps/$($response.ContainerAppName)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Or check status via API:" -ForegroundColor White
    Write-Host "$baseUrl/api/GetContainerAppStatus?adapterInstanceGuid=$adapterInstanceGuid" -ForegroundColor Cyan
    
} catch {
    $stopwatch.Stop()
    $duration = $stopwatch.Elapsed
    
    Write-Host "❌ Failed to create Container App" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host ""
            Write-Host "Response Body:" -ForegroundColor Yellow
            Write-Host $responseBody -ForegroundColor Gray
        } catch {
            # Ignore errors reading response stream
        }
    }
    
    if ($_.ErrorDetails) {
        Write-Host ""
        Write-Host "Error Details:" -ForegroundColor Yellow
        Write-Host $_.ErrorDetails.Message -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "Duration before failure: $([math]::Round($duration.TotalSeconds, 2)) seconds" -ForegroundColor Yellow
    
    if ($_.Exception.Message -match "404") {
        Write-Host ""
        Write-Host "⚠️  The CreateContainerApp API endpoint returned 404 (Not Found)." -ForegroundColor Yellow
        Write-Host "   Possible causes:" -ForegroundColor White
        Write-Host "   1. Function App may not be deployed or the function is missing" -ForegroundColor Gray
        Write-Host "   2. Function route may be incorrect" -ForegroundColor Gray
        Write-Host "   3. Function App may need to be restarted" -ForegroundColor Gray
        Write-Host ""
        Write-Host "   Try:" -ForegroundColor White
        Write-Host "   az functionapp restart --resource-group $ResourceGroup --name $FunctionAppName" -ForegroundColor Gray
    }
    
    exit 1
}


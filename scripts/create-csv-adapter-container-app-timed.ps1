# Create CSV Adapter Container App and Measure Time
# Times the creation process from start to finish

param(
    [string]$FunctionAppName = "func-integration-main",
    [string]$ResourceGroup = "rg-interface-configurator",
    [string]$InterfaceName = "TestInterface-CSV",
    [string]$InstanceName = "CSV-Source-Test"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== Create CSV Adapter Container App (Timed) ===" -ForegroundColor Cyan
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

# Step 2: Generate adapter instance GUID
$adapterInstanceGuid = [System.Guid]::NewGuid()
Write-Host "[2] Generated Adapter Instance GUID: $adapterInstanceGuid" -ForegroundColor White
Write-Host ""

# Step 3: Prepare request
# Note: CreateContainerApp function expects adapterConfiguration but doesn't pass it - using empty object
$adapterConfig = @{
    ReceiveFolder = "csv-incoming"
    FileMask = "*.csv"
    FieldSeparator = "║"
    BatchSize = 1000
    PollingInterval = 60
} | ConvertTo-Json -Compress

$createRequest = @{
    AdapterInstanceGuid = $adapterInstanceGuid.ToString()
    AdapterName = "CSV"
    AdapterType = "Source"
    InterfaceName = $InterfaceName
    InstanceName = $InstanceName
    AdapterConfiguration = $adapterConfig
} | ConvertTo-Json

Write-Host "[3] Request Payload:" -ForegroundColor Yellow
Write-Host $createRequest -ForegroundColor Gray
Write-Host ""

# Step 4: Start timer and create container app
Write-Host "[4] Creating Container App..." -ForegroundColor Yellow
Write-Host "⏱️  Timer started..." -ForegroundColor Cyan
Write-Host ""

$startTime = Get-Date
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    $headers = @{
        "Content-Type" = "application/json"
    }
    
    $createResponse = Invoke-RestMethod `
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
    Write-Host "Duration:      $($duration.TotalSeconds) seconds" -ForegroundColor Green
    Write-Host "               $([math]::Round($duration.TotalSeconds, 2)) seconds" -ForegroundColor Green
    Write-Host "               $([math]::Round($duration.TotalMinutes, 2)) minutes" -ForegroundColor White
    Write-Host ""
    
    Write-Host "=== Container App Details ===" -ForegroundColor Cyan
    Write-Host "Container App Name: $($createResponse.ContainerAppName)" -ForegroundColor White
    Write-Host "Adapter Instance GUID: $adapterInstanceGuid" -ForegroundColor White
    Write-Host "Adapter Name: CSV" -ForegroundColor White
    Write-Host "Adapter Type: Source" -ForegroundColor White
    Write-Host "Interface Name: $InterfaceName" -ForegroundColor White
    Write-Host "Instance Name: $InstanceName" -ForegroundColor White
    Write-Host ""
    
    # Step 5: Check container app status
    Write-Host "[5] Checking Container App Status..." -ForegroundColor Yellow
    Start-Sleep -Seconds 2
    
    try {
        $statusResponse = Invoke-RestMethod `
            -Uri "$baseUrl/api/GetContainerAppStatus?adapterInstanceGuid=$adapterInstanceGuid" `
            -Method Get `
            -Headers $headers `
            -ErrorAction Stop
        
        Write-Host "Status: $($statusResponse.Status)" -ForegroundColor $(if ($statusResponse.Status -eq "Running") { "Green" } else { "Yellow" })
        if ($statusResponse.Message) {
            Write-Host "Message: $($statusResponse.Message)" -ForegroundColor White
        }
    } catch {
        Write-Host "⚠️  Could not get container app status yet (may still be provisioning)" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "=== Summary ===" -ForegroundColor Cyan
    Write-Host "Container App creation took: $([math]::Round($duration.TotalSeconds, 2)) seconds" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can check the container app in Azure Portal:" -ForegroundColor White
    Write-Host "https://portal.azure.com/#@mariomujagmail508.onmicrosoft.com/resource/subscriptions/f1e8e2a3-2bf1-43f0-8f19-37abd624205c/resourceGroups/$ResourceGroup/providers/Microsoft.App/containerApps/$($createResponse.ContainerAppName)" -ForegroundColor Cyan
    
} catch {
    $stopwatch.Stop()
    $duration = $stopwatch.Stop()
    
    Write-Host "❌ Failed to create Container App" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "Duration before failure: $($stopwatch.Elapsed.TotalSeconds) seconds" -ForegroundColor Yellow
    exit 1
}


# Script to create container app for CSV adapter and measure deployment time
# This script:
# 1. Gets the first interface configuration with a CSV source adapter
# 2. Creates a container app for that adapter instance
# 3. Monitors the status until it's running
# 4. Reports the total time taken

param(
    [string]$FunctionAppUrl = "",
    [string]$InterfaceName = ""
)

# Auto-detect Function App URL
if ([string]::IsNullOrEmpty($FunctionAppUrl)) {
    $hostname = $env:COMPUTERNAME
    if ($hostname -like "*localhost*" -or $hostname -like "*127.0.0.1*") {
        $FunctionAppUrl = "http://localhost:7071"
    }
    else {
        $FunctionAppUrl = "https://func-integration-main.azurewebsites.net"
    }
}

$ErrorActionPreference = "Stop"

Write-Host "`n=== CSV Adapter Container App Creation ===" -ForegroundColor Cyan
Write-Host "Function App URL: $FunctionAppUrl" -ForegroundColor White

$startTime = Get-Date

# Step 1: Get interface configurations
Write-Host "`n1. Getting interface configurations..." -ForegroundColor Yellow
try {
    $headers = @{
        "Accept" = "application/json"
    }
    $configsResponse = Invoke-RestMethod -Uri "$FunctionAppUrl/api/GetInterfaceConfigurations" -Method Get -Headers $headers -ErrorAction Stop
    
    if (-not $configsResponse -or ($configsResponse.Count -eq 0 -and $configsResponse -isnot [System.Array])) {
        Write-Host "❌ No interface configurations found" -ForegroundColor Red
        exit 1
    }
    
    $configCount = if ($configsResponse -is [System.Array]) { $configsResponse.Count } else { 1 }
    Write-Host "✅ Found $configCount interface configuration(s)" -ForegroundColor Green
}
catch {
    Write-Host "❌ Error getting interface configurations: $_" -ForegroundColor Red
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "   HTTP Status: $statusCode" -ForegroundColor Red
    }
    Write-Host "`nTrying to use Azure Function App URL..." -ForegroundColor Yellow
    $FunctionAppUrl = "https://func-integration-main.azurewebsites.net"
    Write-Host "Using: $FunctionAppUrl" -ForegroundColor White
    
    try {
        $configsResponse = Invoke-RestMethod -Uri "$FunctionAppUrl/api/GetInterfaceConfigurations" -Method Get -Headers $headers -ErrorAction Stop
        $configCount = if ($configsResponse -is [System.Array]) { $configsResponse.Count } else { 1 }
        Write-Host "✅ Found $configCount interface configuration(s)" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Error: $_" -ForegroundColor Red
        exit 1
    }
}

# Step 2: Find CSV adapter instance
Write-Host "`n2. Finding CSV adapter instance..." -ForegroundColor Yellow
$csvAdapterConfig = $null
$adapterInstanceGuid = $null
$adapterName = "CSV"
$adapterType = "Source"
$interfaceName = ""
$instanceName = ""

if ($InterfaceName) {
    $csvAdapterConfig = $configsResponse | Where-Object { $_.interfaceName -eq $InterfaceName -and $_.sourceAdapterName -eq "CSV" } | Select-Object -First 1
}
else {
    $csvAdapterConfig = $configsResponse | Where-Object { $_.sourceAdapterName -eq "CSV" } | Select-Object -First 1
}

if (-not $csvAdapterConfig) {
    Write-Host "❌ No CSV source adapter found in interface configurations" -ForegroundColor Red
    Write-Host "Available interfaces:" -ForegroundColor Yellow
    $configsResponse | ForEach-Object { Write-Host "  - $($_.interfaceName) (Source: $($_.sourceAdapterName), Destination: $($_.destinationAdapterName))" -ForegroundColor White }
    exit 1
}

# Get adapter instance GUID
if ($csvAdapterConfig.sourceAdapterInstanceGuid) {
    $adapterInstanceGuid = [Guid]::Parse($csvAdapterConfig.sourceAdapterInstanceGuid)
}
else {
    Write-Host "⚠️  No sourceAdapterInstanceGuid found. Creating new GUID..." -ForegroundColor Yellow
    $adapterInstanceGuid = [Guid]::NewGuid()
}

$interfaceName = $csvAdapterConfig.interfaceName
$instanceName = if ($csvAdapterConfig.sourceInstanceName) { $csvAdapterConfig.sourceInstanceName } else { "CSV Source" }

Write-Host "✅ Found CSV adapter:" -ForegroundColor Green
Write-Host "   Interface: $interfaceName" -ForegroundColor White
Write-Host "   Instance Name: $instanceName" -ForegroundColor White
Write-Host "   Adapter Instance GUID: $adapterInstanceGuid" -ForegroundColor White

# Step 3: Create container app
Write-Host "`n3. Creating container app..." -ForegroundColor Yellow
$createRequest = @{
    adapterInstanceGuid = $adapterInstanceGuid.ToString()
    adapterName = $adapterName
    adapterType = $adapterType
    interfaceName = $interfaceName
    instanceName = $instanceName
} | ConvertTo-Json

try {
    $createResponse = Invoke-RestMethod -Uri "$FunctionAppUrl/api/CreateContainerApp" -Method Post -Body $createRequest -ContentType "application/json"
    
    Write-Host "✅ Container app creation initiated:" -ForegroundColor Green
    Write-Host "   Container App Name: $($createResponse.containerAppName)" -ForegroundColor White
    Write-Host "   Status: $($createResponse.status)" -ForegroundColor White
    Write-Host "   Blob Container: $($createResponse.blobContainerName)" -ForegroundColor White
    
    $containerAppName = $createResponse.containerAppName
}
catch {
    Write-Host "❌ Error creating container app: $_" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    exit 1
}

# Step 4: Monitor status until running
Write-Host "`n4. Monitoring container app status..." -ForegroundColor Yellow
$maxWaitTime = 600 # 10 minutes
$checkInterval = 10 # Check every 10 seconds
$elapsed = 0
$status = "Creating"

while ($elapsed -lt $maxWaitTime) {
    Start-Sleep -Seconds $checkInterval
    $elapsed += $checkInterval
    
    try {
        $statusResponse = Invoke-RestMethod -Uri "$FunctionAppUrl/api/GetContainerAppStatus?adapterInstanceGuid=$adapterInstanceGuid" -Method Get -ContentType "application/json"
        $status = $statusResponse.status
        
        $elapsedMinutes = [math]::Round($elapsed / 60, 1)
        Write-Host "   [$elapsedMinutes min] Status: $status" -ForegroundColor $(if ($status -eq "Running") { "Green" } else { "Yellow" })
        
        if ($status -eq "Running") {
            Write-Host "`n✅ Container app is now RUNNING!" -ForegroundColor Green
            break
        }
        elseif ($status -eq "Failed" -or $status -eq "Stopped") {
            Write-Host "`n❌ Container app status: $status" -ForegroundColor Red
            if ($statusResponse.error) {
                Write-Host "   Error: $($statusResponse.error)" -ForegroundColor Red
            }
            exit 1
        }
    }
    catch {
        Write-Host "   ⚠️  Error checking status: $_" -ForegroundColor Yellow
        # Continue monitoring
    }
}

if ($status -ne "Running") {
    Write-Host "`n⚠️  Container app did not reach 'Running' status within $maxWaitTime seconds" -ForegroundColor Yellow
    Write-Host "   Final status: $status" -ForegroundColor Yellow
}

# Step 5: Calculate and report total time
$endTime = Get-Date
$totalDuration = $endTime - $startTime
$totalSeconds = [math]::Round($totalDuration.TotalSeconds, 1)
$totalMinutes = [math]::Round($totalDuration.TotalMinutes, 2)

Write-Host "`n=== Deployment Summary ===" -ForegroundColor Cyan
Write-Host "Container App Name: $containerAppName" -ForegroundColor White
Write-Host "Adapter Instance GUID: $adapterInstanceGuid" -ForegroundColor White
Write-Host "Final Status: $status" -ForegroundColor $(if ($status -eq "Running") { "Green" } else { "Yellow" })
Write-Host "`n⏱️  Total Time: $totalSeconds seconds ($totalMinutes minutes)" -ForegroundColor Cyan
Write-Host "   Started: $($startTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White
Write-Host "   Ended:   $($endTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor White

if ($status -eq "Running") {
    Write-Host "`n✅ Container app is up and running!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "`n⚠️  Container app is not in 'Running' status" -ForegroundColor Yellow
    exit 1
}


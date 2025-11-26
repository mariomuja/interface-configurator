# Test Script for CSV Adapter
# Tests: Container App creation, adapter activation, data simulation, Service Bus verification, cleanup

param(
    [Parameter(Mandatory=$true)]
    [string]$FunctionAppUrl,
    
    [Parameter(Mandatory=$false)]
    [string]$InterfaceName = "TestInterface-CSV",
    
    [Parameter(Mandatory=$false)]
    [int]$MaxWaitSeconds = 300,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipCleanup
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== CSV Adapter Test ===" -ForegroundColor Cyan
Write-Host "Function App URL: $FunctionAppUrl" -ForegroundColor White
Write-Host "Interface Name: $InterfaceName" -ForegroundColor White

# Step 1: Create Interface Configuration with CSV Source Adapter
Write-Host "`n[1] Creating Interface Configuration..." -ForegroundColor Yellow
$interfaceGuid = [Guid]::NewGuid().ToString()
$createInterfaceBody = @{
    interfaceName = $InterfaceName
    sourceAdapterName = "CSV"
    sourceConfiguration = @{
        receiveFolder = "csv-incoming"
        fileMask = "*.csv"
        batchSize = 100
        fieldSeparator = ","
        csvAdapterType = "RAW"
    } | ConvertTo-Json -Compress
} | ConvertTo-Json

try {
    $createResponse = Invoke-RestMethod -Uri "$FunctionAppUrl/api/CreateInterfaceConfiguration" `
        -Method Post `
        -Body $createInterfaceBody `
        -ContentType "application/json"
    
    # Extract adapter instance GUID from response
    if ($createResponse.sourceAdapterInstanceGuid) {
        $adapterInstanceGuid = $createResponse.sourceAdapterInstanceGuid
    } elseif ($createResponse.sources -and $createResponse.sources.PSObject.Properties.Count -gt 0) {
        # Try to get from sources object
        $firstSource = $createResponse.sources.PSObject.Properties | Select-Object -First 1
        $adapterInstanceGuid = $firstSource.Value.adapterInstanceGuid
    } else {
        # Try to parse from full response
        $adapterInstanceGuid = $createResponse | ConvertTo-Json -Depth 10 | Select-String -Pattern 'adapterInstanceGuid["\s]*:["\s]*"([^"]+)"' | ForEach-Object { $_.Matches.Groups[1].Value }
    }
    
    if ([string]::IsNullOrEmpty($adapterInstanceGuid)) {
        Write-Host "❌ Failed to extract adapter instance GUID from response" -ForegroundColor Red
        Write-Host "Response: $($createResponse | ConvertTo-Json -Depth 10)" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "✅ Interface created. Adapter Instance GUID: $adapterInstanceGuid" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to create interface: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Activate Adapter (enabled=true) - This triggers Container App creation
Write-Host "`n[2] Activating Adapter (this will create Container App)..." -ForegroundColor Yellow
try {
    $activateBody = @{
        interfaceName = $InterfaceName
        adapterInstanceGuid = $adapterInstanceGuid
        enabled = $true
    } | ConvertTo-Json
    
    $activateResponse = Invoke-RestMethod -Uri "$FunctionAppUrl/api/UpdateSourceAdapterInstance" `
        -Method Put `
        -Body $activateBody `
        -ContentType "application/json"
    
    Write-Host "✅ Adapter activation request sent" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to activate adapter: $_" -ForegroundColor Red
    exit 1
}

# Step 3: Wait for Container App creation
Write-Host "`n[3] Waiting for Container App creation..." -ForegroundColor Yellow
$containerAppCreated = $false
$waitStartTime = Get-Date
$maxWaitTime = (Get-Date).AddSeconds($MaxWaitSeconds)

while (-not $containerAppCreated -and (Get-Date) -lt $maxWaitTime) {
    try {
        $statusResponse = Invoke-RestMethod -Uri "$FunctionAppUrl/api/GetContainerAppStatus?adapterInstanceGuid=$adapterInstanceGuid" `
            -Method Get
        
        if ($statusResponse.exists -eq $true) {
            if ($statusResponse.status -eq "Running") {
                $containerAppCreated = $true
                Write-Host "✅ Container App is running: $($statusResponse.containerAppName)" -ForegroundColor Green
                break
            } else {
                Write-Host "   Container App status: $($statusResponse.status) (waiting for Running...)" -ForegroundColor Gray
            }
        } else {
            Write-Host "   Container App not found yet (waiting...)" -ForegroundColor Gray
        }
    } catch {
        Write-Host "   Checking status... (waiting...)" -ForegroundColor Gray
    }
    
    Start-Sleep -Seconds 5
}

if (-not $containerAppCreated) {
    Write-Host "❌ Container App was not created within $MaxWaitSeconds seconds" -ForegroundColor Red
    if (-not $SkipCleanup) {
        Write-Host "Cleaning up..." -ForegroundColor Yellow
        # Deactivate adapter
        try {
            $deactivateBody = @{
                interfaceName = $InterfaceName
                adapterInstanceGuid = $adapterInstanceGuid
                enabled = $false
            } | ConvertTo-Json
            Invoke-RestMethod -Uri "$FunctionAppUrl/api/UpdateSourceAdapterInstance" `
                -Method Put `
                -Body $deactivateBody `
                -ContentType "application/json" | Out-Null
        } catch { }
    }
    exit 1
}

Start-Sleep -Seconds 10  # Wait for container app to be fully ready

# Step 4: Simulate CSV Data
Write-Host "`n[4] Simulating CSV Data..." -ForegroundColor Yellow
$csvData = @"
Name,Age,City,Email
John Doe,30,New York,john.doe@example.com
Jane Smith,25,Los Angeles,jane.smith@example.com
Bob Johnson,35,Chicago,bob.johnson@example.com
Alice Williams,28,San Francisco,alice.williams@example.com
Charlie Brown,32,Seattle,charlie.brown@example.com
"@

try {
    $csvDataBody = @{
        interfaceName = $InterfaceName
        csvData = $csvData
    } | ConvertTo-Json
    
    $csvResponse = Invoke-RestMethod -Uri "$FunctionAppUrl/api/UpdateCsvData" `
        -Method Put `
        -Body $csvDataBody `
        -ContentType "application/json"
    
    Write-Host "✅ CSV data uploaded ($($csvData.Split("`n").Count) lines)" -ForegroundColor Green
    Write-Host "   Waiting for processing..." -ForegroundColor Gray
    Start-Sleep -Seconds 30  # Wait for adapter to process
} catch {
    Write-Host "❌ Failed to upload CSV data: $_" -ForegroundColor Red
    exit 1
}

# Step 5: Check Service Bus Messages
Write-Host "`n[5] Checking Service Bus Messages..." -ForegroundColor Yellow
$messagesFound = $false
$maxRetries = 10
$retryCount = 0

while (-not $messagesFound -and $retryCount -lt $maxRetries) {
    try {
        $messagesResponse = Invoke-RestMethod -Uri "$FunctionAppUrl/api/GetServiceBusMessages?interfaceName=$InterfaceName&maxMessages=10" `
            -Method Get
        
        if ($messagesResponse.messages -and $messagesResponse.messages.Count -gt 0) {
            $messagesFound = $true
            Write-Host "✅ Found $($messagesResponse.messages.Count) message(s) in Service Bus" -ForegroundColor Green
            Write-Host "`n--- Service Bus Messages ---" -ForegroundColor Cyan
            
            foreach ($message in $messagesResponse.messages) {
                Write-Host "`nMessage ID: $($message.messageId)" -ForegroundColor Yellow
                Write-Host "Adapter: $($message.adapterName)" -ForegroundColor White
                Write-Host "Role: $($message.adapterRole)" -ForegroundColor White
                Write-Host "Headers: $($message.headers -join ', ')" -ForegroundColor Gray
                Write-Host "Record:" -ForegroundColor Gray
                $message.record.PSObject.Properties | ForEach-Object {
                    Write-Host "  $($_.Name): $($_.Value)" -ForegroundColor White
                }
            }
            break
        } else {
            $retryCount++
            Write-Host "   No messages yet (retry $retryCount/$maxRetries)..." -ForegroundColor Gray
            Start-Sleep -Seconds 5
        }
    } catch {
        $retryCount++
        Write-Host "   Error checking messages (retry $retryCount/$maxRetries): $_" -ForegroundColor Gray
        Start-Sleep -Seconds 5
    }
}

if (-not $messagesFound) {
    Write-Host "⚠️  No messages found in Service Bus after $maxRetries retries" -ForegroundColor Yellow
}

# Step 6: Deactivate Adapter
Write-Host "`n[6] Deactivating Adapter..." -ForegroundColor Yellow
try {
    $deactivateBody = @{
        interfaceName = $InterfaceName
        adapterInstanceGuid = $adapterInstanceGuid
        enabled = $false
    } | ConvertTo-Json
    
    Invoke-RestMethod -Uri "$FunctionAppUrl/api/UpdateSourceAdapterInstance" `
        -Method Put `
        -Body $deactivateBody `
        -ContentType "application/json" | Out-Null
    
    Write-Host "✅ Adapter deactivated" -ForegroundColor Green
    Start-Sleep -Seconds 15  # Wait for container app deletion
} catch {
    Write-Host "❌ Failed to deactivate adapter: $_" -ForegroundColor Red
}

# Step 7: Verify Container App Deletion
Write-Host "`n[7] Verifying Container App Deletion..." -ForegroundColor Yellow
$containerAppDeleted = $false
$waitStartTime = Get-Date
$maxWaitTime = (Get-Date).AddSeconds(60)

while (-not $containerAppDeleted -and (Get-Date) -lt $maxWaitTime) {
    try {
        $statusResponse = Invoke-RestMethod -Uri "$FunctionAppUrl/api/GetContainerAppStatus?adapterInstanceGuid=$adapterInstanceGuid" `
            -Method Get
        
        if ($statusResponse.exists -eq $false) {
            $containerAppDeleted = $true
            Write-Host "✅ Container App was deleted" -ForegroundColor Green
            break
        } else {
            Write-Host "   Container App still exists: $($statusResponse.status) (waiting...)" -ForegroundColor Gray
        }
    } catch {
        # If we get an error, container app might be deleted
        $containerAppDeleted = $true
        Write-Host "✅ Container App appears to be deleted (status check failed)" -ForegroundColor Green
        break
    }
    
    Start-Sleep -Seconds 5
}

if (-not $containerAppDeleted) {
    Write-Host "⚠️  Container App was not deleted within 60 seconds" -ForegroundColor Yellow
}

# Step 8: Cleanup Service Bus (if not skipped)
if (-not $SkipCleanup) {
    Write-Host "`n[8] Cleaning up Service Bus messages..." -ForegroundColor Yellow
    try {
        # Note: Service Bus cleanup would require additional API endpoint
        # For now, we just report remaining messages
        $messagesResponse = Invoke-RestMethod -Uri "$FunctionAppUrl/api/GetServiceBusMessages?interfaceName=$InterfaceName&maxMessages=100" `
            -Method Get
        
        if ($messagesResponse.messages -and $messagesResponse.messages.Count -gt 0) {
            Write-Host "⚠️  $($messagesResponse.messages.Count) message(s) still in Service Bus (manual cleanup may be required)" -ForegroundColor Yellow
        } else {
            Write-Host "✅ Service Bus is clean" -ForegroundColor Green
        }
    } catch {
        Write-Host "⚠️  Could not check Service Bus cleanup: $_" -ForegroundColor Yellow
    }
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Green
if ($messagesFound) {
    Write-Host "✅ Test PASSED: Messages were successfully sent to Service Bus" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Test FAILED: No messages found in Service Bus" -ForegroundColor Red
    exit 1
}


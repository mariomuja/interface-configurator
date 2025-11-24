# End-to-End Test: CSV Source Adapter → Service Bus → SQL Server Destination Adapter
# Testet den kompletten Datenfluss von CSV über Service Bus zu SQL Server

param(
    [Parameter(Mandatory = $false)]
    [string]$FunctionAppName = "func-integration-main",
    
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup = "rg-interface-configurator",
    
    [Parameter(Mandatory = $false)]
    [string]$InterfaceName = "FromCsvToSqlServerExample"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  End-to-End Test: CSV → Service Bus → SQL" -ForegroundColor Cyan
Write-Host "=========================================`n" -ForegroundColor Cyan

# Step 1: Get Function App URL
Write-Host "Step 1: Getting Function App URL..." -ForegroundColor Yellow
$functionAppUrl = az functionapp show `
    --name $FunctionAppName `
    --resource-group $ResourceGroup `
    --query "defaultHostName" `
    --output tsv 2>&1 | Where-Object { $_ -notmatch "UserWarning|cryptography" }

if (-not $functionAppUrl -or $functionAppUrl -match "ERROR") {
    Write-Host "ERROR: Could not get Function App URL" -ForegroundColor Red
    exit 1
}

$baseUrl = "https://$functionAppUrl"
Write-Host "Function App URL: $baseUrl" -ForegroundColor Green

# Step 2: Check if CSV Source Adapter exists and is enabled
Write-Host "`nStep 2: Checking CSV Source Adapter status..." -ForegroundColor Yellow
$interfaceConfig = Invoke-RestMethod -Uri "$baseUrl/api/GetInterfaceConfigurations" -Method Get -ErrorAction SilentlyContinue

if ($interfaceConfig) {
    $config = $interfaceConfig | Where-Object { $_.interfaceName -eq $InterfaceName } | Select-Object -First 1
    if ($config) {
        Write-Host "Interface found: $($config.interfaceName)" -ForegroundColor Green
        Write-Host "  Source Adapter: $($config.sourceAdapterName)" -ForegroundColor White
        Write-Host "  Source Enabled: $($config.sourceIsEnabled)" -ForegroundColor White
        Write-Host "  Source Instance GUID: $($config.sourceAdapterInstanceGuid)" -ForegroundColor White
        
        if (-not $config.sourceIsEnabled) {
            Write-Host "`n⚠️  CSV Source Adapter is not enabled. Enabling now..." -ForegroundColor Yellow
            # Enable source adapter
            $enableBody = @{
                interfaceName = $InterfaceName
                isEnabled = $true
            } | ConvertTo-Json
            
            try {
                $enableResult = Invoke-RestMethod -Uri "$baseUrl/api/UpdateSourceAdapterInstance" `
                    -Method Post `
                    -Body $enableBody `
                    -ContentType "application/json" `
                    -ErrorAction Stop
                Write-Host "✅ Source adapter enabled" -ForegroundColor Green
            } catch {
                Write-Host "⚠️  Could not enable source adapter: $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
    } else {
        Write-Host "⚠️  Interface '$InterfaceName' not found" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠️  Could not retrieve interface configurations" -ForegroundColor Yellow
}

# Step 3: Check Container App for CSV Source Adapter
Write-Host "`nStep 3: Checking Container App for CSV Source Adapter..." -ForegroundColor Yellow
if ($config -and $config.sourceAdapterInstanceGuid) {
    $containerAppName = "ca-$($config.sourceAdapterInstanceGuid -replace '-', '')"
    Write-Host "Expected Container App Name: $containerAppName" -ForegroundColor White
    
    $containerApp = az containerapp show `
        --name $containerAppName `
        --resource-group $ResourceGroup `
        --query "{name:name, provisioningState:properties.provisioningState, runningStatus:properties.runningStatus}" `
        --output json 2>&1 | Where-Object { $_ -notmatch "UserWarning|cryptography" } | ConvertFrom-Json -ErrorAction SilentlyContinue
    
    if ($containerApp) {
        Write-Host "✅ Container App found:" -ForegroundColor Green
        Write-Host "  Name: $($containerApp.name)" -ForegroundColor White
        Write-Host "  Provisioning State: $($containerApp.provisioningState)" -ForegroundColor White
        Write-Host "  Running Status: $($containerApp.runningStatus)" -ForegroundColor White
    } else {
        Write-Host "⚠️  Container App not found. It may be created when adapter is enabled." -ForegroundColor Yellow
    }
}

# Step 4: Upload CSV sample data to blob storage
Write-Host "`nStep 4: Uploading CSV sample data to blob storage..." -ForegroundColor Yellow
$csvContent = @"
OrderID║CustomerName║Product║Quantity║Price║OrderDate
1001║John Doe║Widget A║5║10.50║2024-01-15
1002║Jane Smith║Widget B║3║15.75║2024-01-16
1003║Bob Johnson║Widget C║10║8.25║2024-01-17
1004║Alice Brown║Widget A║2║10.50║2024-01-18
1005║Charlie Wilson║Widget B║7║15.75║2024-01-19
"@

$storageAccountName = az storage account list `
    --resource-group $ResourceGroup `
    --query "[?contains(name, 'stapp') || contains(name, 'storage')].name" `
    --output tsv 2>&1 | Select-Object -First 1 | Where-Object { $_ -notmatch "UserWarning|cryptography" }

if ($storageAccountName) {
    Write-Host "Storage Account: $storageAccountName" -ForegroundColor White
    
    # Get storage account key
    $storageKey = az storage account keys list `
        --account-name $storageAccountName `
        --resource-group $ResourceGroup `
        --query "[0].value" `
        --output tsv 2>&1 | Where-Object { $_ -notmatch "UserWarning|cryptography" }
    
    if ($storageKey) {
        # Upload CSV file
        $fileName = "test-$(Get-Date -Format 'yyyyMMddHHmmss').csv"
        $blobPath = "csv-files/csv-incoming/$fileName"
        
        Write-Host "Uploading CSV file: $blobPath" -ForegroundColor White
        
        $csvBytes = [System.Text.Encoding]::UTF8.GetBytes($csvContent)
        $csvBytes | az storage blob upload `
            --account-name $storageAccountName `
            --account-key $storageKey `
            --container-name "csv-files" `
            --name $blobPath `
            --file - `
            --content-type "text/csv" `
            --output none 2>&1 | Where-Object { $_ -notmatch "UserWarning|cryptography" }
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ CSV file uploaded successfully" -ForegroundColor Green
        } else {
            Write-Host "⚠️  CSV file upload may have failed" -ForegroundColor Yellow
        }
    } else {
        Write-Host "⚠️  Could not get storage account key" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠️  Could not find storage account" -ForegroundColor Yellow
}

# Step 5: Wait for blob trigger to process
Write-Host "`nStep 5: Waiting for blob trigger to process CSV (30 seconds)..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

# Step 6: Check Service Bus messages
Write-Host "`nStep 6: Checking Service Bus messages..." -ForegroundColor Yellow
try {
    $serviceBusMessages = Invoke-RestMethod -Uri "$baseUrl/api/GetServiceBusMessages?interfaceName=$InterfaceName&maxCount=100" `
        -Method Get `
        -ErrorAction Stop
    
    if ($serviceBusMessages -and $serviceBusMessages.Count -gt 0) {
        Write-Host "✅ Found $($serviceBusMessages.Count) Service Bus messages" -ForegroundColor Green
        $serviceBusMessages | Select-Object -First 5 | ForEach-Object {
            Write-Host "  Message ID: $($_.messageId), Adapter: $($_.adapterName), Enqueued: $($_.enqueuedTime)" -ForegroundColor White
        }
    } else {
        Write-Host "⚠️  No Service Bus messages found yet" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  Could not retrieve Service Bus messages: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Step 7: Check if SQL Server Destination Adapter is configured
Write-Host "`nStep 7: Checking SQL Server Destination Adapter..." -ForegroundColor Yellow
if ($config) {
    $destinations = $config.destinationAdapterInstances
    if ($destinations -and $destinations.Count -gt 0) {
        $sqlDest = $destinations | Where-Object { $_.adapterName -eq "SqlServer" } | Select-Object -First 1
        if ($sqlDest) {
            Write-Host "✅ SQL Server Destination Adapter found:" -ForegroundColor Green
            Write-Host "  Instance Name: $($sqlDest.instanceName)" -ForegroundColor White
            Write-Host "  Enabled: $($sqlDest.isEnabled)" -ForegroundColor White
            Write-Host "  Instance GUID: $($sqlDest.adapterInstanceGuid)" -ForegroundColor White
            
            if (-not $sqlDest.isEnabled) {
                Write-Host "`n⚠️  SQL Server Destination Adapter is not enabled. Enabling now..." -ForegroundColor Yellow
                # Enable destination adapter
                $enableDestBody = @{
                    interfaceName = $InterfaceName
                    adapterInstanceGuid = $sqlDest.adapterInstanceGuid
                    isEnabled = $true
                } | ConvertTo-Json
                
                try {
                    $enableDestResult = Invoke-RestMethod -Uri "$baseUrl/api/UpdateDestinationAdapterInstance" `
                        -Method Post `
                        -Body $enableDestBody `
                        -ContentType "application/json" `
                        -ErrorAction Stop
                    Write-Host "✅ Destination adapter enabled" -ForegroundColor Green
                } catch {
                    Write-Host "⚠️  Could not enable destination adapter: $($_.Exception.Message)" -ForegroundColor Yellow
                }
            }
        } else {
            Write-Host "⚠️  No SQL Server Destination Adapter found" -ForegroundColor Yellow
        }
    } else {
        Write-Host "⚠️  No destination adapters configured" -ForegroundColor Yellow
    }
}

# Step 8: Wait for destination adapter to process messages
Write-Host "`nStep 8: Waiting for destination adapter to process messages (30 seconds)..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

# Step 9: Check Process Logs
Write-Host "`nStep 9: Checking Process Logs..." -ForegroundColor Yellow
try {
    $processLogs = Invoke-RestMethod -Uri "$baseUrl/api/GetProcessLogs?interfaceName=$InterfaceName&maxCount=50" `
        -Method Get `
        -ErrorAction Stop
    
    if ($processLogs -and $processLogs.Count -gt 0) {
        Write-Host "✅ Found $($processLogs.Count) process log entries" -ForegroundColor Green
        Write-Host "`nRecent log entries:" -ForegroundColor Cyan
        $processLogs | Select-Object -First 10 | ForEach-Object {
            $level = $_.level
            $color = switch ($level) {
                "Error" { "Red" }
                "Warning" { "Yellow" }
                default { "White" }
            }
            Write-Host "  [$($_.level)] $($_.message)" -ForegroundColor $color
            if ($_.details) {
                Write-Host "    Details: $($_.details)" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "⚠️  No process logs found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  Could not retrieve process logs: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Step 10: Check SQL Server TransportData table
Write-Host "`nStep 10: Checking SQL Server TransportData table..." -ForegroundColor Yellow
$sqlServer = az sql server list `
    --resource-group $ResourceGroup `
    --query "[0].name" `
    --output tsv 2>&1 | Where-Object { $_ -notmatch "UserWarning|cryptography" }

if ($sqlServer) {
    Write-Host "SQL Server: $sqlServer" -ForegroundColor White
    Write-Host "⚠️  Manual SQL query required to verify data in TransportData table" -ForegroundColor Yellow
    Write-Host "  Query: SELECT TOP 10 * FROM TransportData ORDER BY datetime_created DESC" -ForegroundColor Gray
} else {
    Write-Host "⚠️  Could not find SQL Server" -ForegroundColor Yellow
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "  Test Summary" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "✅ Test completed. Check UI for detailed results." -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "  1. Open UI and check Service Bus Messages card" -ForegroundColor White
Write-Host "  2. Check Process Logs table in UI" -ForegroundColor White
Write-Host "  3. Verify data in SQL Server TransportData table" -ForegroundColor White
Write-Host "  4. Check Container App logs in Azure Portal" -ForegroundColor White


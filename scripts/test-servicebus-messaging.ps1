# Test Service Bus Messaging
# Tests the communication between adapters and Azure Service Bus

param(
    [string]$FunctionAppName = "func-integration-main",
    [string]$ResourceGroupName = "rg-interface-configurator",
    [string]$InterfaceName = ""
)

Write-Host "`n=== Testing Service Bus Messaging ===" -ForegroundColor Cyan

# Get Function App URL
Write-Host "`n[1/3] Getting Function App URL..." -ForegroundColor Yellow
$functionAppUrlRaw = az functionapp show --resource-group $ResourceGroupName --name $FunctionAppName --query "defaultHostName" -o tsv 2>&1 | Where-Object { $_ -notmatch "UserWarning|cryptography" }
$functionAppUrl = ($functionAppUrlRaw | Select-String -Pattern "azurewebsites\.net" | Select-Object -First 1).ToString().Trim()
if ([string]::IsNullOrWhiteSpace($functionAppUrl)) {
    Write-Host "❌ Error getting Function App URL" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Function App URL: https://$functionAppUrl" -ForegroundColor Green

# Get interface name if not provided
if ([string]::IsNullOrWhiteSpace($InterfaceName)) {
    Write-Host "`n[2/3] Getting available interfaces..." -ForegroundColor Yellow
    $interfacesResponse = Invoke-RestMethod -Uri "https://$functionAppUrl/api/GetInterfaceConfigurations" -Method Get -ContentType "application/json" -ErrorAction SilentlyContinue
    if ($interfacesResponse -and $interfacesResponse.Count -gt 0) {
        $InterfaceName = $interfacesResponse[0].InterfaceName
        Write-Host "✅ Using interface: $InterfaceName" -ForegroundColor Green
    } else {
        Write-Host "⚠️  No interfaces found. Please provide InterfaceName parameter or create an interface first." -ForegroundColor Yellow
        Write-Host "Usage: .\scripts\test-servicebus-messaging.ps1 -InterfaceName 'YourInterfaceName'" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "`n[2/3] Using provided interface: $InterfaceName" -ForegroundColor Yellow
}

# Test Service Bus messaging
Write-Host "`n[3/3] Testing Service Bus messaging..." -ForegroundColor Yellow
$testBody = @{
    InterfaceName = $InterfaceName
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "https://$functionAppUrl/api/TestServiceBusMessaging" -Method Post -Body $testBody -ContentType "application/json"
    
    Write-Host "`n=== Test Results ===" -ForegroundColor Cyan
    Write-Host "Success: $($response.success)" -ForegroundColor $(if ($response.success) { "Green" } else { "Yellow" })
    Write-Host "Summary: $($response.summary)" -ForegroundColor $(if ($response.success) { "Green" } else { "Yellow" })
    
    Write-Host "`nDetailed Results:" -ForegroundColor Cyan
    foreach ($result in $response.results) {
        $color = if ($result.Success) { "Green" } else { "Red" }
        Write-Host "  [$($result.TestName)]: $($result.Message)" -ForegroundColor $color
        if ($result.Details) {
            Write-Host "    Details: $($result.Details | ConvertTo-Json -Compress)" -ForegroundColor Gray
        }
    }
    
    if ($response.success) {
        Write-Host "`n✅ All tests passed! Service Bus messaging is working correctly." -ForegroundColor Green
    } else {
        Write-Host "`n⚠️  Some tests failed. Please check the details above." -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Error testing Service Bus messaging: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Response: $($_.Exception.Response)" -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Test Completed ===" -ForegroundColor Cyan


# Test Health Check Endpoint
# Tests the /api/health endpoint and displays results

param(
    [string]$FunctionAppName = "func-integration-main",
    [string]$ResourceGroup = "rg-infrastructure-as-code"
)

$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Health Check Endpoint Test" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Get Function App URL
Write-Host "Getting Function App URL..." -ForegroundColor Yellow
$functionApp = az functionapp show `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "{defaultHostName:defaultHostName, state:state}" `
    --output json 2>&1 | ConvertFrom-Json

if (-not $functionApp -or -not $functionApp.defaultHostName) {
    Write-Host "❌ ERROR: Could not retrieve Function App information" -ForegroundColor Red
    exit 1
}

$healthCheckUrl = "https://$($functionApp.defaultHostName)/api/health"
Write-Host "Function App: $FunctionAppName" -ForegroundColor White
Write-Host "State: $($functionApp.state)" -ForegroundColor $(if ($functionApp.state -eq "Running") { "Green" } else { "Yellow" })
Write-Host "Health Check URL: $healthCheckUrl" -ForegroundColor Cyan
Write-Host ""

# Test Health Check Endpoint
Write-Host "Testing health check endpoint..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri $healthCheckUrl -Method GET -TimeoutSec 10 -ErrorAction Stop
    
    Write-Host "✅ Health Check Response:" -ForegroundColor Green
    Write-Host ""
    Write-Host "Overall Status: $($response.Status)" -ForegroundColor $(if ($response.Status -eq "Healthy") { "Green" } else { "Red" })
    Write-Host "Timestamp: $($response.Timestamp)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Individual Checks:" -ForegroundColor Cyan
    
    foreach ($check in $response.Checks) {
        $statusColor = switch ($check.Status) {
            "Healthy" { "Green" }
            "Degraded" { "Yellow" }
            "Unhealthy" { "Red" }
            default { "White" }
        }
        
        Write-Host "  [$($check.Status)] $($check.Name)" -ForegroundColor $statusColor
        Write-Host "      $($check.Message)" -ForegroundColor Gray
    }
    
    Write-Host ""
    if ($response.Status -eq "Healthy") {
        Write-Host "✅ All systems operational!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "⚠️  Some checks failed. Review details above." -ForegroundColor Yellow
        exit 1
    }
}
catch {
    Write-Host "❌ ERROR: Failed to call health check endpoint" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "HTTP Status Code: $statusCode" -ForegroundColor Red
        
        if ($statusCode -eq 404) {
            Write-Host ""
            Write-Host "Possible causes:" -ForegroundColor Yellow
            Write-Host "  - Health check endpoint not deployed yet" -ForegroundColor Gray
            Write-Host "  - Function App still starting up" -ForegroundColor Gray
            Write-Host "  - Check deployment logs" -ForegroundColor Gray
        }
    }
    
    exit 1
}


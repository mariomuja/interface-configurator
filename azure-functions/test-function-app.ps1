# Testet die neue Function App

param(
    [string]$ResourceGroup = "rg-interface-configuration",
    [string]$FunctionAppName = "func-integration-main"
)

$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Function App Test" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "1. Pruefe Function App Status..." -ForegroundColor Yellow
$functionApp = az functionapp show `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "{state:state, defaultHostName:defaultHostName}" `
    --output json 2>&1 | Where-Object { $_ -match '^\s*\{' } | ConvertFrom-Json

if ($functionApp) {
    Write-Host "   Status: $($functionApp.state)" -ForegroundColor $(if ($functionApp.state -eq "Running") { "Green" } else { "Red" })
    Write-Host "   URL: https://$($functionApp.defaultHostName)" -ForegroundColor Cyan
} else {
    Write-Host "   FEHLER: Function App nicht gefunden!" -ForegroundColor Red
    exit 1
}

Write-Host "`n2. Pruefe App Settings..." -ForegroundColor Yellow
$packageUrl = az functionapp config appsettings list `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "[?name=='WEBSITE_RUN_FROM_PACKAGE'].value" `
    -o tsv 2>&1 | Select-Object -Last 1

if ($packageUrl -and $packageUrl -like "http*") {
    Write-Host "   WEBSITE_RUN_FROM_PACKAGE: Gesetzt" -ForegroundColor Green
} else {
    Write-Host "   WEBSITE_RUN_FROM_PACKAGE: Nicht gesetzt!" -ForegroundColor Red
    Write-Host "   Bitte deployen Sie ein Package zuerst" -ForegroundColor Yellow
}

Write-Host "`n3. Teste Function Endpoint..." -ForegroundColor Yellow
$testUrl = "https://$($functionApp.defaultHostName)/api/SimpleTestFunction"
Write-Host "   URL: $testUrl" -ForegroundColor Gray

try {
    $response = Invoke-WebRequest -Uri $testUrl -Method GET -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop
    Write-Host "   ✅ SUCCESS: Function antwortet!" -ForegroundColor Green
    Write-Host "   Status Code: $($response.StatusCode)" -ForegroundColor Cyan
    Write-Host "   Response: $($response.Content)" -ForegroundColor Gray
} catch {
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "   Status Code: $statusCode" -ForegroundColor $(if ($statusCode -eq 404) { "Yellow" } elseif ($statusCode -eq 503) { "Yellow" } else { "Red" })
        
        if ($statusCode -eq 404) {
            Write-Host "   ⚠️  Function noch nicht geladen" -ForegroundColor Yellow
            Write-Host "   Moegliche Ursachen:" -ForegroundColor Yellow
            Write-Host "     - Function App startet noch (warte 1-2 Minuten)" -ForegroundColor Gray
            Write-Host "     - Package nicht korrekt deployed" -ForegroundColor Gray
            Write-Host "     - WEBSITE_RUN_FROM_PACKAGE nicht gesetzt" -ForegroundColor Gray
        } elseif ($statusCode -eq 503) {
            Write-Host "   ⚠️  Service Unavailable - Function App startet noch" -ForegroundColor Yellow
            Write-Host "   Bitte warten Sie weitere 1-2 Minuten" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n4. Liste Functions..." -ForegroundColor Yellow
$functions = az rest --method GET `
    --uri "https://management.azure.com/subscriptions/$(az account show --query id -o tsv 2>&1 | Select-Object -Last 1)/resourceGroups/$ResourceGroup/providers/Microsoft.Web/sites/$FunctionAppName/functions?api-version=2022-03-01" `
    --output json 2>&1 | Where-Object { $_ -match '^\s*\{' -or $_ -match '^\s*\[' }

if ($functions) {
    $funcList = $functions | ConvertFrom-Json
    if ($funcList.value -and $funcList.value.Count -gt 0) {
        Write-Host "   Gefundene Functions:" -ForegroundColor Green
        foreach ($func in $funcList.value) {
            Write-Host "     - $($func.name)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "   ⚠️  Keine Functions gefunden" -ForegroundColor Yellow
        Write-Host "   Die Function App startet noch oder Package wurde nicht deployed" -ForegroundColor Gray
    }
} else {
    Write-Host "   ⚠️  Konnte Functions nicht auflisten" -ForegroundColor Yellow
}

Write-Host ""







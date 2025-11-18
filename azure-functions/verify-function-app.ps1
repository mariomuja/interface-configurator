# Verifiziert dass die Azure Function App korrekt konfiguriert ist

param(
    [string]$ResourceGroup = "rg-interface-configuration",
    [string]$FunctionAppName = "func-apprigklebtsay2o"
)

$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Azure Function App Verification" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Unterdruecke Warnungen
$env:PYTHONWARNINGS = "ignore"

Write-Host "1. Pruefe Function App Status..." -ForegroundColor Yellow
$functionAppJson = az functionapp show `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --output json 2>&1 | Where-Object { $_ -match '^\s*\{' -or $_ -match '^\s*"' }

if ($functionAppJson) {
    $functionApp = $functionAppJson | ConvertFrom-Json
    Write-Host "   Status: $($functionApp.state)" -ForegroundColor $(if ($functionApp.state -eq "Running") { "Green" } else { "Red" })
    Write-Host "   URL: https://$($functionApp.defaultHostName)" -ForegroundColor Cyan
} else {
    Write-Host "   FEHLER: Function App Status konnte nicht abgerufen werden" -ForegroundColor Red
    exit 1
}

Write-Host "`n2. Pruefe kritische App Settings..." -ForegroundColor Yellow
$appSettingsJson = az functionapp config appsettings list `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --output json 2>&1 | Where-Object { $_ -match '^\s*\{' -or $_ -match '^\s*\[' }

if ($appSettingsJson) {
    $settings = $appSettingsJson | ConvertFrom-Json
    
    $criticalSettings = @{
        "FUNCTIONS_WORKER_RUNTIME" = "node"
        "AzureWebJobsStorage" = $null  # Muss gesetzt sein
        "WEBSITE_NODE_DEFAULT_VERSION" = "~20"
        "WEBSITE_USE_PLACEHOLDER" = "0"
        "WEBSITE_RUN_FROM_PACKAGE" = $null  # Muss gesetzt sein
    }
    
    $settingsDict = @{}
    foreach ($setting in $settings) {
        if ($setting.value) {
            $settingsDict[$setting.name] = $setting.value
        }
    }
    
    $allOk = $true
    foreach ($key in $criticalSettings.Keys) {
        if ($criticalSettings[$key] -eq $null) {
            # Muss nur vorhanden sein
            if ($settingsDict.ContainsKey($key) -and $settingsDict[$key]) {
                $displayValue = if ($key -like "*PASSWORD*" -or $key -like "*KEY*" -or $key -like "*STORAGE*") { "***" } else { $settingsDict[$key].Substring(0, [Math]::Min(50, $settingsDict[$key].Length)) }
                Write-Host "   [$key]: OK ($displayValue...)" -ForegroundColor Green
            } else {
                Write-Host "   [$key]: FEHLT!" -ForegroundColor Red
                $allOk = $false
            }
        } else {
            # Muss bestimmten Wert haben
            if ($settingsDict.ContainsKey($key) -and $settingsDict[$key] -eq $criticalSettings[$key]) {
                Write-Host "   [$key]: OK" -ForegroundColor Green
            } else {
                Write-Host "   [$key]: FEHLT oder falscher Wert!" -ForegroundColor Red
                $allOk = $false
            }
        }
    }
    
    if (-not $allOk) {
        Write-Host "`n   WARNUNG: Einige kritische Settings fehlen!" -ForegroundColor Yellow
    }
}

Write-Host "`n3. Teste Function App Endpoint..." -ForegroundColor Yellow
$functionUrl = "https://$($functionApp.defaultHostName)/api/health"
try {
    $response = Invoke-WebRequest -Uri $functionUrl -Method GET -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
    Write-Host "   Function antwortet!" -ForegroundColor Green
    Write-Host "   Status Code: $($response.StatusCode)" -ForegroundColor Cyan
    Write-Host "   Response: $($response.Content)" -ForegroundColor Gray
} catch {
    Write-Host "   Function antwortet nicht oder Fehler:" -ForegroundColor Yellow
    Write-Host "   $($_.Exception.Message)" -ForegroundColor Gray
    
    if ($_.Exception.Response) {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "   Status Code: $statusCode" -ForegroundColor Yellow
        
        if ($statusCode -eq 503 -or $statusCode -eq 502) {
            Write-Host "   Moegliche Ursachen:" -ForegroundColor Yellow
            Write-Host "     - Function App startet noch" -ForegroundColor Gray
            Write-Host "     - Package nicht korrekt deployed" -ForegroundColor Gray
            Write-Host "     - WEBSITE_RUN_FROM_PACKAGE nicht gesetzt" -ForegroundColor Gray
        }
    }
}

Write-Host "`n4. Pruefe Storage Account..." -ForegroundColor Yellow
if ($settingsDict.ContainsKey("AzureWebJobsStorage")) {
    $storageConn = $settingsDict["AzureWebJobsStorage"]
    if ($storageConn -match "AccountName=([^;]+)") {
        $storageAccountName = $matches[1]
        
        $storageJson = az storage account show `
            --resource-group $ResourceGroup `
            --name $storageAccountName `
            --query "{statusOfPrimary:statusOfPrimary}" `
            --output json 2>&1 | Where-Object { $_ -match '^\s*\{' }
        
        if ($storageJson) {
            $storage = $storageJson | ConvertFrom-Json
            if ($storage.statusOfPrimary -eq "available") {
                Write-Host "   Storage Account: $storageAccountName - Verfuegbar" -ForegroundColor Green
            } else {
                Write-Host "   Storage Account: $storageAccountName - Status: $($storage.statusOfPrimary)" -ForegroundColor Red
            }
        }
    }
}

Write-Host "`n5. Empfohlene Aktionen:" -ForegroundColor Cyan
Write-Host "   - Wenn Function nicht antwortet:" -ForegroundColor Yellow
Write-Host "     1. Pruefe Logs: az functionapp log tail --resource-group $ResourceGroup --name $FunctionAppName" -ForegroundColor Gray
Write-Host "     2. Stelle sicher, dass WEBSITE_RUN_FROM_PACKAGE gesetzt ist" -ForegroundColor Gray
Write-Host "     3. Starte Function App neu: az functionapp restart --resource-group $ResourceGroup --name $FunctionAppName" -ForegroundColor Gray
Write-Host "     4. Warte 30-60 Sekunden nach Neustart" -ForegroundColor Gray

Write-Host ""










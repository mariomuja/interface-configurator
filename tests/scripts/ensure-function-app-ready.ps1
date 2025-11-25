# Stellt sicher, dass die Azure Function App bereit ist und alle Services verfuegbar sind

param(
    [string]$ResourceGroup = "rg-interface-configuration",
    [string]$FunctionAppName = "func-apprigklebtsay2o"
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Azure Function App Service Check" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "1. Pruefe Function App Status..." -ForegroundColor Yellow
$functionApp = az functionapp show `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --output json 2>&1 | ConvertFrom-Json

if ($functionApp.state -eq "Running") {
    Write-Host "   Status: Running" -ForegroundColor Green
} else {
    Write-Host "   Status: $($functionApp.state)" -ForegroundColor Red
    Write-Host "   Starte Function App..." -ForegroundColor Yellow
    az functionapp start --resource-group $ResourceGroup --name $FunctionAppName --output none
    Start-Sleep -Seconds 10
}

Write-Host "`n2. Pruefe App Settings..." -ForegroundColor Yellow
$appSettings = az functionapp config appsettings list `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --output json 2>&1 | ConvertFrom-Json

$settingsDict = @{}
foreach ($setting in $appSettings) {
    if ($setting.value) {
        $settingsDict[$setting.name] = $setting.value
    }
}

# Pruefe kritische Settings
$criticalSettings = @(
    "FUNCTIONS_WORKER_RUNTIME",
    "AzureWebJobsStorage",
    "WEBSITE_NODE_DEFAULT_VERSION"
)

$missingSettings = @()
foreach ($setting in $criticalSettings) {
    if ($settingsDict.ContainsKey($setting) -and $settingsDict[$setting]) {
        Write-Host "   [$setting]: OK" -ForegroundColor Green
    } else {
        Write-Host "   [$setting]: FEHLT!" -ForegroundColor Red
        $missingSettings += $setting
    }
}

if ($missingSettings.Count -gt 0) {
    Write-Host "`n   Setze fehlende Settings..." -ForegroundColor Yellow
    
    $settingsToSet = @{}
    if ($missingSettings -contains "FUNCTIONS_WORKER_RUNTIME") {
        $settingsToSet["FUNCTIONS_WORKER_RUNTIME"] = "node"
    }
    if ($missingSettings -contains "WEBSITE_NODE_DEFAULT_VERSION") {
        $settingsToSet["WEBSITE_NODE_DEFAULT_VERSION"] = "~20"
    }
    
    if ($settingsToSet.Count -gt 0) {
        $settingsString = ($settingsToSet.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join " "
        az functionapp config appsettings set `
            --resource-group $ResourceGroup `
            --name $FunctionAppName `
            --settings $settingsString `
            --output none 2>&1 | Out-Null
        Write-Host "   Settings aktualisiert" -ForegroundColor Green
    }
}

Write-Host "`n3. Pruefe Storage Account..." -ForegroundColor Yellow
if ($settingsDict.ContainsKey("AzureWebJobsStorage")) {
    $storageConn = $settingsDict["AzureWebJobsStorage"]
    if ($storageConn -match "AccountName=([^;]+)") {
        $storageAccountName = $matches[1]
        
        $storageStatus = az storage account show `
            --resource-group $ResourceGroup `
            --name $storageAccountName `
            --query "{statusOfPrimary:statusOfPrimary}" `
            --output json 2>&1 | ConvertFrom-Json
        
        if ($storageStatus.statusOfPrimary -eq "available") {
            Write-Host "   Storage Account: $storageAccountName - Verfuegbar" -ForegroundColor Green
        } else {
            Write-Host "   Storage Account: $storageAccountName - Status: $($storageStatus.statusOfPrimary)" -ForegroundColor Red
        }
    }
} else {
    Write-Host "   FEHLER: AzureWebJobsStorage nicht konfiguriert!" -ForegroundColor Red
}

Write-Host "`n4. Pruefe WEBSITE_RUN_FROM_PACKAGE..." -ForegroundColor Yellow
if ($settingsDict.ContainsKey("WEBSITE_RUN_FROM_PACKAGE") -and $settingsDict["WEBSITE_RUN_FROM_PACKAGE"]) {
    $packageUrl = $settingsDict["WEBSITE_RUN_FROM_PACKAGE"]
    if ($packageUrl -like "http*") {
        Write-Host "   WEBSITE_RUN_FROM_PACKAGE: Gesetzt (Blob URL)" -ForegroundColor Green
    } elseif ($packageUrl -eq "1") {
        Write-Host "   WEBSITE_RUN_FROM_PACKAGE: Aktiviert (Run from Package)" -ForegroundColor Green
    } else {
        Write-Host "   WEBSITE_RUN_FROM_PACKAGE: $packageUrl" -ForegroundColor Yellow
    }
} else {
    Write-Host "   WARNUNG: WEBSITE_RUN_FROM_PACKAGE nicht gesetzt!" -ForegroundColor Yellow
    Write-Host "   Setze auf '1' (Run from Package)..." -ForegroundColor Yellow
    az functionapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $FunctionAppName `
        --settings "WEBSITE_RUN_FROM_PACKAGE=1" `
        --output none 2>&1 | Out-Null
    Write-Host "   WEBSITE_RUN_FROM_PACKAGE auf '1' gesetzt" -ForegroundColor Green
}

Write-Host "`n5. Stelle sicher, dass WEBSITE_USE_PLACEHOLDER=0..." -ForegroundColor Yellow
if ($settingsDict.ContainsKey("WEBSITE_USE_PLACEHOLDER") -and $settingsDict["WEBSITE_USE_PLACEHOLDER"] -eq "0") {
    Write-Host "   WEBSITE_USE_PLACEHOLDER: 0 (OK)" -ForegroundColor Green
} else {
    Write-Host "   Setze WEBSITE_USE_PLACEHOLDER auf 0..." -ForegroundColor Yellow
    az functionapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $FunctionAppName `
        --settings "WEBSITE_USE_PLACEHOLDER=0" `
        --output none 2>&1 | Out-Null
    Write-Host "   WEBSITE_USE_PLACEHOLDER auf 0 gesetzt" -ForegroundColor Green
}

Write-Host "`n6. Starte Function App neu..." -ForegroundColor Yellow
az functionapp restart `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --output none 2>&1 | Out-Null

Write-Host "   Function App wird neu gestartet..." -ForegroundColor Green
Write-Host "   Warte 20 Sekunden..." -ForegroundColor Gray
Start-Sleep -Seconds 20

Write-Host "`n7. Pruefe Function App Health..." -ForegroundColor Yellow
$health = az functionapp show `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "{state:state, defaultHostName:defaultHostName}" `
    --output json 2>&1 | ConvertFrom-Json

if ($health.state -eq "Running") {
    Write-Host "   Status: Running" -ForegroundColor Green
    Write-Host "   URL: https://$($health.defaultHostName)" -ForegroundColor Cyan
} else {
    Write-Host "   Status: $($health.state)" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Fertig!" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Versuchen Sie jetzt, die Functions aufzulisten:" -ForegroundColor Yellow
Write-Host "  az functionapp function list --resource-group $ResourceGroup --name $FunctionAppName" -ForegroundColor Cyan
Write-Host ""










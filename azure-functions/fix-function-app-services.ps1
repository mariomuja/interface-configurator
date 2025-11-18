# Stellt sicher, dass alle benoetigten Services fuer die Azure Function App verfuegbar sind

param(
    [string]$ResourceGroup = "rg-interface-configuration",
    [string]$FunctionAppName = "func-apprigklebtsay2o"
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Azure Function App Service Fix" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "1. Pruefe Storage Account..." -ForegroundColor Yellow
$storageSettings = az functionapp config appsettings list `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "[?name=='AzureWebJobsStorage'].value" `
    -o tsv 2>&1

if ($storageSettings -match "AccountName=([^;]+)") {
    $storageAccountName = $matches[1]
    Write-Host "   Storage Account: $storageAccountName" -ForegroundColor Green
    
    $storageStatus = az storage account show `
        --resource-group $ResourceGroup `
        --name $storageAccountName `
        --query "{provisioningState:provisioningState, statusOfPrimary:statusOfPrimary}" `
        --output json 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        $storage = $storageStatus | ConvertFrom-Json
        if ($storage.statusOfPrimary -ne "available") {
            Write-Host "   WARNUNG: Storage Account ist nicht verfuegbar!" -ForegroundColor Red
            Write-Host "   Status: $($storage.statusOfPrimary)" -ForegroundColor Red
        } else {
            Write-Host "   Status: Verfuegbar" -ForegroundColor Green
        }
    } else {
        Write-Host "   FEHLER: Storage Account nicht gefunden!" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "   FEHLER: AzureWebJobsStorage nicht konfiguriert!" -ForegroundColor Red
    exit 1
}

Write-Host "`n2. Pruefe SQL Server Verbindung..." -ForegroundColor Yellow
$sqlServer = az functionapp config appsettings list `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "[?name=='AZURE_SQL_SERVER'].value" `
    -o tsv 2>&1

if ($sqlServer) {
    $sqlServerName = if ($sqlServer -match "^([^.]+)") { $matches[1] } else { "" }
    
    if ($sqlServerName) {
        Write-Host "   SQL Server: $sqlServerName" -ForegroundColor Green
        
        $sqlStatus = az sql server show `
            --resource-group $ResourceGroup `
            --name $sqlServerName `
            --query "{state:state}" `
            --output json 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            $sql = $sqlStatus | ConvertFrom-Json
            if ($sql.state -eq "Ready") {
                Write-Host "   Status: Ready" -ForegroundColor Green
            } else {
                Write-Host "   WARNUNG: SQL Server Status: $($sql.state)" -ForegroundColor Yellow
            }
        } else {
            Write-Host "   WARNUNG: SQL Server nicht gefunden (kann normal sein wenn nicht verwendet)" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "   WARNUNG: AZURE_SQL_SERVER nicht konfiguriert (kann normal sein)" -ForegroundColor Yellow
}

Write-Host "`n3. Pruefe WEBSITE_RUN_FROM_PACKAGE..." -ForegroundColor Yellow
$packageUrl = az functionapp config appsettings list `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "[?name=='WEBSITE_RUN_FROM_PACKAGE'].value" `
    -o tsv 2>&1

if ($packageUrl -and $packageUrl -like "http*") {
    Write-Host "   WEBSITE_RUN_FROM_PACKAGE: Gesetzt" -ForegroundColor Green
    Write-Host "   URL: $($packageUrl.Substring(0, [Math]::Min(60, $packageUrl.Length)))..." -ForegroundColor Gray
} else {
    Write-Host "   WARNUNG: WEBSITE_RUN_FROM_PACKAGE nicht gesetzt!" -ForegroundColor Yellow
    Write-Host "   Dies kann zu ServiceUnavailable Fehlern fuehren." -ForegroundColor Yellow
}

Write-Host "`n4. Stelle sicher, dass alle App Settings korrekt sind..." -ForegroundColor Yellow
$requiredSettings = @{
    "FUNCTIONS_WORKER_RUNTIME" = "node"
    "WEBSITE_NODE_DEFAULT_VERSION" = "~20"
    "WEBSITE_USE_PLACEHOLDER" = "0"
}

foreach ($setting in $requiredSettings.GetEnumerator()) {
    $currentValue = az functionapp config appsettings list `
        --resource-group $ResourceGroup `
        --name $FunctionAppName `
        --query "[?name=='$($setting.Key)'].value" `
        -o tsv 2>&1
    
    if ($currentValue -eq $setting.Value) {
        Write-Host "   [$($setting.Key)]: OK" -ForegroundColor Green
    } else {
        Write-Host "   [$($setting.Key)]: Setze auf '$($setting.Value)'..." -ForegroundColor Yellow
        az functionapp config appsettings set `
            --resource-group $ResourceGroup `
            --name $FunctionAppName `
            --settings "$($setting.Key)=$($setting.Value)" `
            --output none 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   [$($setting.Key)]: Aktualisiert" -ForegroundColor Green
        } else {
            Write-Host "   [$($setting.Key)]: FEHLER beim Aktualisieren" -ForegroundColor Red
        }
    }
}

Write-Host "`n5. Starte Function App neu..." -ForegroundColor Yellow
az functionapp restart `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --output none 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "   Function App wird neu gestartet..." -ForegroundColor Green
    Write-Host "   Warte 15 Sekunden..." -ForegroundColor Gray
    Start-Sleep -Seconds 15
} else {
    Write-Host "   FEHLER: Function App konnte nicht neu gestartet werden!" -ForegroundColor Red
    exit 1
}

Write-Host "`n6. Pruefe Function App Status..." -ForegroundColor Yellow
$functionAppStatus = az functionapp show `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "{state:state, defaultHostName:defaultHostName}" `
    --output json 2>&1

if ($LASTEXITCODE -eq 0) {
    $status = $functionAppStatus | ConvertFrom-Json
    Write-Host "   Status: $($status.state)" -ForegroundColor $(if ($status.state -eq "Running") { "Green" } else { "Red" })
    Write-Host "   URL: https://$($status.defaultHostName)" -ForegroundColor Cyan
}

Write-Host "`n7. Liste Functions..." -ForegroundColor Yellow
$functions = az functionapp list-functions `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --output json 2>&1

if ($LASTEXITCODE -eq 0) {
    $funcList = $functions | ConvertFrom-Json
    if ($funcList.Count -gt 0) {
        Write-Host "   Gefundene Functions:" -ForegroundColor Green
        foreach ($func in $funcList) {
            Write-Host "     - $($func.name)" -ForegroundColor Cyan
        }
    } else {
        Write-Host "   WARNUNG: Keine Functions gefunden!" -ForegroundColor Yellow
        Write-Host "   Moegliche Ursachen:" -ForegroundColor Yellow
        Write-Host "     - WEBSITE_RUN_FROM_PACKAGE nicht gesetzt" -ForegroundColor Gray
        Write-Host "     - Package nicht korrekt deployed" -ForegroundColor Gray
        Write-Host "     - Function App benoetigt mehr Zeit zum Starten" -ForegroundColor Gray
    }
} else {
    Write-Host "   FEHLER: Functions konnten nicht aufgelistet werden!" -ForegroundColor Red
    Write-Host $functions -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Fertig!" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan










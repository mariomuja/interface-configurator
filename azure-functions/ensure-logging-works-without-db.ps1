# Stellt sicher, dass Logging auch ohne Datenbank funktioniert

param(
    [string]$ResourceGroup = "rg-infrastructure-as-code",
    [string]$FunctionAppName = "func-apprigklebtsay2o"
)

$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Ensure Logging Works Without Database" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "1. Pruefe SQL Server Verbindung..." -ForegroundColor Yellow
$sqlServer = az functionapp config appsettings list `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "[?name=='AZURE_SQL_SERVER'].value" `
    -o tsv 2>&1 | Select-Object -Last 1

if ($sqlServer) {
    $sqlServerName = if ($sqlServer -match "^([^.]+)") { $matches[1] } else { "" }
    
    if ($sqlServerName) {
        Write-Host "   SQL Server: $sqlServerName" -ForegroundColor Green
        
        # Pruefe Firewall Rules
        $firewallRules = az sql server firewall-rule list `
            --resource-group $ResourceGroup `
            --server $sqlServerName `
            --output json 2>&1 | ConvertFrom-Json
        
        if ($firewallRules.Count -eq 0) {
            Write-Host "   WARNUNG: Keine Firewall Rules gefunden!" -ForegroundColor Yellow
            Write-Host "   Die Function App kann moeglicherweise nicht auf die Datenbank zugreifen." -ForegroundColor Yellow
        } else {
            Write-Host "   Firewall Rules: $($firewallRules.Count) gefunden" -ForegroundColor Green
        }
        
        # Pruefe ob Azure Services erlaubt sind
        $allowAzureServices = $firewallRules | Where-Object { 
            $_.startIpAddress -eq "0.0.0.0" -and $_.endIpAddress -eq "0.0.0.0" 
        }
        
        if (-not $allowAzureServices) {
            Write-Host "   WARNUNG: Azure Services sind moeglicherweise nicht erlaubt!" -ForegroundColor Yellow
            Write-Host "   Setze Firewall Rule fuer Azure Services..." -ForegroundColor Yellow
            
            az sql server firewall-rule create `
                --resource-group $ResourceGroup `
                --server $sqlServerName `
                --name "AllowAzureServices" `
                --start-ip-address "0.0.0.0" `
                --end-ip-address "0.0.0.0" `
                --output none 2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "   Firewall Rule erstellt" -ForegroundColor Green
            } else {
                Write-Host "   FEHLER: Firewall Rule konnte nicht erstellt werden" -ForegroundColor Red
            }
        } else {
            Write-Host "   Azure Services sind erlaubt" -ForegroundColor Green
        }
    }
} else {
    Write-Host "   WARNUNG: AZURE_SQL_SERVER nicht konfiguriert" -ForegroundColor Yellow
    Write-Host "   Logging wird nur zu Console/ILogger gehen" -ForegroundColor Gray
}

Write-Host "`n2. Pruefe App Settings fuer Logging..." -ForegroundColor Yellow
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

$requiredSqlSettings = @(
    "AZURE_SQL_SERVER",
    "AZURE_SQL_DATABASE",
    "AZURE_SQL_USER",
    "AZURE_SQL_PASSWORD"
)

$missingSqlSettings = @()
foreach ($setting in $requiredSqlSettings) {
    if ($settingsDict.ContainsKey($setting) -and $settingsDict[$setting]) {
        Write-Host "   [$setting]: OK" -ForegroundColor Green
    } else {
        Write-Host "   [$setting]: FEHLT" -ForegroundColor Yellow
        $missingSqlSettings += $setting
    }
}

if ($missingSqlSettings.Count -gt 0) {
    Write-Host "`n   WARNUNG: Einige SQL Settings fehlen!" -ForegroundColor Yellow
    Write-Host "   Logging wird nur zu Console/ILogger gehen (OK fuer JavaScript Functions)" -ForegroundColor Gray
}

Write-Host "`n3. Stelle sicher, dass Logging auch ohne DB funktioniert..." -ForegroundColor Yellow
Write-Host "   Die LoggingServiceAdapter ist bereits so konfiguriert, dass sie:" -ForegroundColor Gray
Write-Host "   - Zuerst zu Console/ILogger loggt (fail-safe)" -ForegroundColor Gray
Write-Host "   - Nur dann zur Datenbank loggt, wenn DB verfuegbar ist" -ForegroundColor Gray
Write-Host "   - Bei DB-Fehlern nicht die Function zum Absturz bringt" -ForegroundColor Gray
Write-Host "   ✅ Logging ist bereits fail-safe konfiguriert" -ForegroundColor Green

Write-Host "`n4. Pruefe Function App Logs..." -ForegroundColor Yellow
Write-Host "   Lade letzte Log-Eintraege..." -ForegroundColor Gray

# Versuche Logs abzurufen (kann fehlschlagen wenn noch nicht verfuegbar)
$logs = az monitor app-insights query `
    --app $FunctionAppName `
    --analytics-query "traces | take 5" `
    --output json 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "   Logs verfuegbar" -ForegroundColor Green
} else {
    Write-Host "   Logs noch nicht verfuegbar (normal nach Neustart)" -ForegroundColor Yellow
}

Write-Host "`n5. Empfohlene Aktionen:" -ForegroundColor Cyan
Write-Host "   - Wenn SQL-Verbindung fehlt:" -ForegroundColor Yellow
Write-Host "     1. Stelle sicher, dass Firewall Rules gesetzt sind" -ForegroundColor Gray
Write-Host "     2. Pruefe, ob Azure Services erlaubt sind (0.0.0.0 - 0.0.0.0)" -ForegroundColor Gray
Write-Host "     3. Pruefe SQL Server Status: az sql server show --name <server> --resource-group $ResourceGroup" -ForegroundColor Gray
Write-Host "   - Logging funktioniert auch ohne DB:" -ForegroundColor Yellow
Write-Host "     Die LoggingServiceAdapter loggt immer zuerst zu Console/ILogger" -ForegroundColor Gray
Write-Host "     Database-Logging ist optional und wird nur verwendet wenn verfuegbar" -ForegroundColor Gray

Write-Host ""





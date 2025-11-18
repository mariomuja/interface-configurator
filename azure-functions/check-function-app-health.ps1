# Prueft die Gesundheit der Azure Function App und aller Dependencies

param(
    [string]$ResourceGroup = "rg-interface-configuration",
    [string]$FunctionAppName = ""
)

$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Azure Function App Health Check" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Finde Function App Name falls nicht angegeben
if ([string]::IsNullOrEmpty($FunctionAppName)) {
    Write-Host "Suche Function App..." -ForegroundColor Yellow
    $functionApps = az functionapp list --resource-group $ResourceGroup --query "[].name" -o tsv 2>&1
    
    if ($LASTEXITCODE -eq 0 -and $functionApps) {
        $FunctionAppName = $functionApps | Select-Object -First 1
        Write-Host "Gefunden: $FunctionAppName" -ForegroundColor Green
    } else {
        Write-Host "Keine Function App gefunden!" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n1. Pruefe Function App Status..." -ForegroundColor Yellow
$functionAppStatus = az functionapp show `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --query "{state:state, hostNames:defaultHostName, kind:kind, runtime:siteConfig.linuxFxVersion, provisioningState:provisioningState}" `
    --output json 2>&1

if ($LASTEXITCODE -eq 0) {
    $status = $functionAppStatus | ConvertFrom-Json
    Write-Host "   Status: $($status.state)" -ForegroundColor $(if ($status.state -eq "Running") { "Green" } else { "Red" })
    Write-Host "   Runtime: $($status.runtime)" -ForegroundColor Cyan
    Write-Host "   Host: $($status.hostNames)" -ForegroundColor Cyan
    Write-Host "   Provisioning: $($status.provisioningState)" -ForegroundColor Cyan
} else {
    Write-Host "   FEHLER: Function App nicht gefunden oder nicht erreichbar!" -ForegroundColor Red
    Write-Host $functionAppStatus -ForegroundColor Red
}

Write-Host "`n2. Pruefe App Settings..." -ForegroundColor Yellow
$appSettings = az functionapp config appsettings list `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --output json 2>&1

if ($LASTEXITCODE -eq 0) {
    $settings = $appSettings | ConvertFrom-Json
    
    $requiredSettings = @(
        "FUNCTIONS_WORKER_RUNTIME",
        "AzureWebJobsStorage",
        "WEBSITE_NODE_DEFAULT_VERSION",
        "AZURE_SQL_SERVER",
        "AZURE_SQL_DATABASE",
        "AZURE_SQL_USER",
        "AZURE_SQL_PASSWORD"
    )
    
    $settingsDict = @{}
    foreach ($setting in $settings) {
        $settingsDict[$setting.name] = $setting.value
    }
    
    foreach ($required in $requiredSettings) {
        if ($settingsDict.ContainsKey($required)) {
            $value = $settingsDict[$required]
            $displayValue = if ($required -like "*PASSWORD*" -or $required -like "*KEY*") { 
                "***" 
            } else { 
                $value 
            }
            Write-Host "   [$required]: $displayValue" -ForegroundColor Green
        } else {
            Write-Host "   [$required]: FEHLT!" -ForegroundColor Red
        }
    }
    
    # Pruefe WEBSITE_RUN_FROM_PACKAGE
    if ($settingsDict.ContainsKey("WEBSITE_RUN_FROM_PACKAGE")) {
        $packageUrl = $settingsDict["WEBSITE_RUN_FROM_PACKAGE"]
        Write-Host "   [WEBSITE_RUN_FROM_PACKAGE]: Gesetzt" -ForegroundColor Green
        if ($packageUrl -like "http*") {
            Write-Host "      URL: $($packageUrl.Substring(0, [Math]::Min(80, $packageUrl.Length)))..." -ForegroundColor Gray
        }
    } else {
        Write-Host "   [WEBSITE_RUN_FROM_PACKAGE]: FEHLT!" -ForegroundColor Yellow
    }
} else {
    Write-Host "   FEHLER: App Settings konnten nicht abgerufen werden!" -ForegroundColor Red
}

Write-Host "`n3. Pruefe Storage Account..." -ForegroundColor Yellow
$storageConn = $settingsDict["AzureWebJobsStorage"]
if ($storageConn) {
    $storageAccount = if ($storageConn -match "AccountName=([^;]+)") { $matches[1] } else { "" }
    
    if ($storageAccount) {
        $storageStatus = az storage account show `
            --resource-group $ResourceGroup `
            --name $storageAccount `
            --query "{provisioningState:provisioningState, statusOfPrimary:statusOfPrimary, kind:kind}" `
            --output json 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            $storage = $storageStatus | ConvertFrom-Json
            Write-Host "   Storage Account: $storageAccount" -ForegroundColor Green
            Write-Host "   Status: $($storage.statusOfPrimary)" -ForegroundColor $(if ($storage.statusOfPrimary -eq "available") { "Green" } else { "Red" })
            Write-Host "   Provisioning: $($storage.provisioningState)" -ForegroundColor Cyan
        } else {
            Write-Host "   FEHLER: Storage Account nicht gefunden!" -ForegroundColor Red
        }
    } else {
        Write-Host "   WARNUNG: Storage Account Name konnte nicht extrahiert werden" -ForegroundColor Yellow
    }
} else {
    Write-Host "   FEHLER: AzureWebJobsStorage nicht konfiguriert!" -ForegroundColor Red
}

Write-Host "`n4. Pruefe SQL Server..." -ForegroundColor Yellow
$sqlServer = $settingsDict["AZURE_SQL_SERVER"]
if ($sqlServer) {
    $sqlServerName = if ($sqlServer -match "^([^.]+)") { $matches[1] } else { "" }
    
    if ($sqlServerName) {
        $sqlStatus = az sql server show `
            --resource-group $ResourceGroup `
            --name $sqlServerName `
            --query "{state:state, fullyQualifiedDomainName:fullyQualifiedDomainName}" `
            --output json 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            $sql = $sqlStatus | ConvertFrom-Json
            Write-Host "   SQL Server: $sqlServerName" -ForegroundColor Green
            Write-Host "   Status: $($sql.state)" -ForegroundColor $(if ($sql.state -eq "Ready") { "Green" } else { "Red" })
            Write-Host "   FQDN: $($sql.fullyQualifiedDomainName)" -ForegroundColor Cyan
        } else {
            Write-Host "   FEHLER: SQL Server nicht gefunden!" -ForegroundColor Red
        }
    } else {
        Write-Host "   WARNUNG: SQL Server Name konnte nicht extrahiert werden" -ForegroundColor Yellow
    }
} else {
    Write-Host "   WARNUNG: AZURE_SQL_SERVER nicht konfiguriert" -ForegroundColor Yellow
}

Write-Host "`n5. Pruefe Function App Logs..." -ForegroundColor Yellow
Write-Host "   Lade letzte Log-Eintraege..." -ForegroundColor Gray
$logs = az functionapp log tail `
    --resource-group $ResourceGroup `
    --name $FunctionAppName `
    --output json 2>&1 | Select-Object -First 5

if ($LASTEXITCODE -eq 0 -and $logs) {
    Write-Host "   Logs verfuegbar" -ForegroundColor Green
} else {
    Write-Host "   WARNUNG: Logs konnten nicht abgerufen werden" -ForegroundColor Yellow
}

Write-Host "`n6. Empfohlene Aktionen:" -ForegroundColor Cyan
Write-Host "   - Function App neu starten: az functionapp restart --resource-group $ResourceGroup --name $FunctionAppName" -ForegroundColor Yellow
Write-Host "   - Logs anzeigen: az functionapp log tail --resource-group $ResourceGroup --name $FunctionAppName" -ForegroundColor Yellow
Write-Host "   - App Settings pruefen: az functionapp config appsettings list --resource-group $ResourceGroup --name $FunctionAppName" -ForegroundColor Yellow

Write-Host ""










# PowerShell Script zum Deployment der Azure Functions
# Dieses Skript deployt die Functions direkt über Azure CLI

param(
    [string]$FunctionAppName = "func-appe1mz5h",
    [string]$ResourceGroup = "rg-interface-configuration",
    [string]$PackagePath = "azure-functions\ProcessCsvBlobTrigger\function-app.zip"
)

Write-Host "=== Azure Functions Deployment ===" -ForegroundColor Cyan
Write-Host "Function App: $FunctionAppName" -ForegroundColor White
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "Package: $PackagePath" -ForegroundColor White
Write-Host ""

# Prüfe ob ZIP existiert
if (-not (Test-Path $PackagePath)) {
    Write-Host "ERROR: ZIP-Datei nicht gefunden: $PackagePath" -ForegroundColor Red
    Write-Host "Erstelle ZIP-Datei..." -ForegroundColor Yellow
    
    $publishPath = "azure-functions\ProcessCsvBlobTrigger\publish"
    if (-not (Test-Path $publishPath)) {
        Write-Host "ERROR: Publish-Verzeichnis nicht gefunden. Führe 'dotnet publish' aus." -ForegroundColor Red
        exit 1
    }
    
    # Erstelle ZIP
    Push-Location $publishPath
    Compress-Archive -Path * -DestinationPath "..\function-app.zip" -Force
    Pop-Location
    $PackagePath = "azure-functions\ProcessCsvBlobTrigger\function-app.zip"
    Write-Host "ZIP erstellt: $PackagePath" -ForegroundColor Green
}

# Prüfe Function App Status
Write-Host "`nPrüfe Function App Status..." -ForegroundColor Cyan
$appState = az functionapp show --name $FunctionAppName --resource-group $ResourceGroup --query "state" --output tsv
Write-Host "Function App Status: $appState" -ForegroundColor $(if ($appState -eq "Running") { "Green" } else { "Yellow" })

# Prüfe App Settings
Write-Host "`nPrüfe App Settings..." -ForegroundColor Cyan
$workerRuntime = az functionapp config appsettings list --name $FunctionAppName --resource-group $ResourceGroup --query "[?name=='FUNCTIONS_WORKER_RUNTIME'].value" --output tsv
Write-Host "FUNCTIONS_WORKER_RUNTIME: $workerRuntime" -ForegroundColor $(if ($workerRuntime -eq "dotnet-isolated") { "Green" } else { "Red" })

if ($workerRuntime -ne "dotnet-isolated") {
    Write-Host "WARNING: FUNCTIONS_WORKER_RUNTIME ist nicht 'dotnet-isolated'!" -ForegroundColor Yellow
    Write-Host "Setze FUNCTIONS_WORKER_RUNTIME auf 'dotnet-isolated'..." -ForegroundColor Yellow
    az functionapp config appsettings set --name $FunctionAppName --resource-group $ResourceGroup --settings FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
}

# Deploy
Write-Host "`nStarte Deployment..." -ForegroundColor Cyan
Write-Host "Dies kann einige Minuten dauern..." -ForegroundColor Yellow

$zipSize = (Get-Item $PackagePath).Length / 1MB
Write-Host "ZIP-Größe: $([math]::Round($zipSize, 2)) MB" -ForegroundColor White

# Deaktiviere PowerShell Timeouts für Web Requests
$ProgressPreference = 'SilentlyContinue'
[System.Net.ServicePointManager]::Expect100Continue = $false
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

$maxRetries = 3
$retryCount = 0
$deploymentSuccess = $false

while ($retryCount -lt $maxRetries -and -not $deploymentSuccess) {
    $retryCount++
    Write-Host "`nVersuch $retryCount von $maxRetries..." -ForegroundColor Cyan
    
    try {
        # Versuche zuerst Kudu API (robuster für große Dateien)
        Write-Host "Versuche Deployment über Kudu ZIP Deploy API..." -ForegroundColor Yellow
        
        # Hole Publishing Credentials
        $publishCreds = az functionapp deployment list-publishing-profiles `
            --name $FunctionAppName `
            --resource-group $ResourceGroup `
            --query "[?publishMethod=='MSDeploy'].{userName:userName, userPWD:publishPassword}" `
            --output json | ConvertFrom-Json | Select-Object -First 1
        
        if (-not $publishCreds) {
            throw "Konnte Publishing Credentials nicht abrufen"
        }
        
        $kuduUrl = "https://$FunctionAppName.scm.azurewebsites.net/api/zipdeploy"
        $base64Auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($publishCreds.userName):$($publishCreds.userPWD)"))
        
        $headers = @{
            "Authorization" = "Basic $base64Auth"
            "Content-Type" = "application/zip"
        }
        
        Write-Host "Upload läuft (kein Timeout-Limit)..." -ForegroundColor Yellow
        Write-Host "ZIP-Größe: $zipSize MB - Dies kann mehrere Minuten dauern..." -ForegroundColor White
        
        # Verwende WebClient ohne Timeout-Limit
        $webClient = New-Object System.Net.WebClient
        $webClient.Headers.Add("Authorization", "Basic $base64Auth")
        $webClient.Headers.Add("Content-Type", "application/zip")
        # Kein Timeout gesetzt - verwendet Standard ohne Limit
        
        # Upload mit Progress
        $zipPath = Resolve-Path $PackagePath
        $uri = New-Object System.Uri($kuduUrl)
        
        Write-Host "Starte Upload..." -ForegroundColor Yellow
        $response = $webClient.UploadFile($uri, $zipPath)
        $webClient.Dispose()
        
        Write-Host "✅ Deployment über Kudu API erfolgreich!" -ForegroundColor Green
        $deploymentSuccess = $true
        
    } catch {
        Write-Host "❌ Versuch $retryCount fehlgeschlagen: $($_.Exception.Message)" -ForegroundColor Red
        
        if ($retryCount -lt $maxRetries) {
            $waitTime = [math]::Min(30 * $retryCount, 120)  # Exponential backoff, max 2 Minuten
            Write-Host "Warte $waitTime Sekunden vor erneutem Versuch..." -ForegroundColor Yellow
            Start-Sleep -Seconds $waitTime
        } else {
            Write-Host "`nAlle Versuche fehlgeschlagen. Versuche alternativen Ansatz..." -ForegroundColor Yellow
            
            # Fallback: Azure CLI ohne Timeout-Parameter
            Write-Host "Versuche Azure CLI Deployment ohne Timeout..." -ForegroundColor Yellow
            $deploymentResult = az functionapp deployment source config-zip `
                --name $FunctionAppName `
                --resource-group $ResourceGroup `
                --src $PackagePath `
                --output json 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Deployment über Azure CLI erfolgreich!" -ForegroundColor Green
                $deploymentSuccess = $true
            } else {
                throw "Alle Deployment-Methoden fehlgeschlagen: $deploymentResult"
            }
        }
    }
}

if (-not $deploymentSuccess) {
    Write-Host "`n❌ Deployment fehlgeschlagen!" -ForegroundColor Red
    exit 1
}

Write-Host "`nWarte 30 Sekunden auf Function-Registrierung..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

Write-Host "`nPrüfe deployed Functions..." -ForegroundColor Cyan
$functions = az functionapp function list --name $FunctionAppName --resource-group $ResourceGroup --output json | ConvertFrom-Json

if ($functions.Count -gt 0) {
    Write-Host "✅ Gefundene Functions:" -ForegroundColor Green
    $functions | ForEach-Object {
        Write-Host "  - $($_.name)" -ForegroundColor White
    }
} else {
    Write-Host "⚠️  Keine Functions gefunden. Dies kann einige Minuten dauern." -ForegroundColor Yellow
    Write-Host "Prüfe die Function App im Azure Portal." -ForegroundColor Yellow
}

Write-Host "`n=== Deployment abgeschlossen ===" -ForegroundColor Cyan


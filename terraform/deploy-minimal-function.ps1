# Minimales Deployment für Azure Function App
# Schrittweise: Erst essentiell, dann prüfen, dann weitere Dateien

param(
    [string]$ResourceGroup = "rg-interface-configuration",
    [string]$FunctionAppName = "func-integration",
    [string]$PublishPath = "azure-functions/main/publish"
)

Write-Host "`n=== Minimales Function App Deployment ===" -ForegroundColor Cyan
Write-Host "Function App: $FunctionAppName" -ForegroundColor White

# Schritt 1: Erstelle minimales ZIP mit nur essentiellen Dateien
Write-Host "`n[Schritt 1] Erstelle minimales ZIP..." -ForegroundColor Yellow
$tempDir = Join-Path $env:TEMP "func-minimal-$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

# Essentielle Dateien
$essentialFiles = @(
    "host.json",
    "functions.metadata",
    "extensions.json",
    "worker.config.json",
    "ProcessCsvBlobTrigger.dll",
    "ProcessCsvBlobTrigger.deps.json",
    "ProcessCsvBlobTrigger.runtimeconfig.json"
)

Write-Host "Kopiere essentielle Dateien..." -ForegroundColor White
foreach ($file in $essentialFiles) {
    $sourcePath = Join-Path $PublishPath $file
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath -Destination (Join-Path $tempDir $file) -Force
        Write-Host "  ✅ $file" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  $file nicht gefunden" -ForegroundColor Yellow
    }
}

# Kopiere .azurefunctions Verzeichnis
$azureFunctionsDir = Join-Path $PublishPath ".azurefunctions"
if (Test-Path $azureFunctionsDir) {
    Copy-Item $azureFunctionsDir -Destination (Join-Path $tempDir ".azurefunctions") -Recurse -Force
    Write-Host "  ✅ .azurefunctions" -ForegroundColor Green
}

# Kopiere nur die wichtigsten DLLs (Azure Functions Runtime)
Write-Host "Kopiere wichtige DLLs..." -ForegroundColor White
$importantDlls = Get-ChildItem (Join-Path $PublishPath "*.dll") | 
    Where-Object { $_.Name -match "^(Microsoft\.Azure\.Functions\.|Azure\.|Microsoft\.EntityFrameworkCore\.|Microsoft\.Extensions\.|System\.)" } |
    Select-Object -First 30

foreach ($dll in $importantDlls) {
    Copy-Item $dll.FullName -Destination (Join-Path $tempDir $dll.Name) -Force
    Write-Host "  ✅ $($dll.Name)" -ForegroundColor Green
}

# Erstelle ZIP
$zipPath = Join-Path $PublishPath "function-app-minimal.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Erstelle ZIP..." -ForegroundColor White
Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath -Force
$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "✅ Minimales ZIP erstellt: $zipSize MB" -ForegroundColor Green

# Aufräumen
Remove-Item $tempDir -Recurse -Force

# Schritt 2: Deploye über Azure CLI (kleineres ZIP sollte funktionieren)
Write-Host "`n[Schritt 2] Deploye über Azure CLI..." -ForegroundColor Yellow
Write-Host "Upload läuft ($zipSize MB)..." -ForegroundColor White

$deploymentResult = az functionapp deployment source config-zip `
    --name $FunctionAppName `
    --resource-group $ResourceGroup `
    --src (Resolve-Path $zipPath) `
    --timeout 300 `
    --output json 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Deployment erfolgreich!" -ForegroundColor Green
} else {
    Write-Host "❌ Fehler beim Deployment:" -ForegroundColor Red
    Write-Host $deploymentResult -ForegroundColor Red
    
    # Fallback: Versuche Kudu API mit Invoke-WebRequest
    Write-Host "`nVersuche Fallback über Kudu API..." -ForegroundColor Yellow
    $publishCreds = az functionapp deployment list-publishing-profiles `
        --name $FunctionAppName `
        --resource-group $ResourceGroup `
        --query "[?publishMethod=='MSDeploy'].{userName:userName, userPWD:publishPassword}" `
        --output json | ConvertFrom-Json | Select-Object -First 1
    
    if ($publishCreds) {
        $kuduUrl = "https://$FunctionAppName.scm.azurewebsites.net/api/zipdeploy"
        $base64Auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($publishCreds.userName):$($publishCreds.userPWD)"))
        
        $ProgressPreference = 'SilentlyContinue'
        $headers = @{
            "Authorization" = "Basic $base64Auth"
            "Content-Type" = "application/zip"
        }
        
        try {
            $fileBytes = [System.IO.File]::ReadAllBytes((Resolve-Path $zipPath))
            $response = Invoke-WebRequest -Uri $kuduUrl -Method Post -Headers $headers -Body $fileBytes -TimeoutSec 600
            Write-Host "✅ Deployment über Kudu erfolgreich!" -ForegroundColor Green
        } catch {
            Write-Host "❌ Auch Kudu Deployment fehlgeschlagen: $($_.Exception.Message)" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "❌ Konnte Publishing Credentials nicht abrufen" -ForegroundColor Red
        exit 1
    }
}

# Schritt 3: Warte und prüfe Functions
Write-Host "`n[Schritt 3] Warte 30 Sekunden auf Function-Registrierung..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

Write-Host "Prüfe deployed Functions..." -ForegroundColor Yellow
$functions = az functionapp function list --name $FunctionAppName --resource-group $ResourceGroup --output json | ConvertFrom-Json

if ($functions.Count -gt 0) {
    Write-Host "`n✅ Funktionen gefunden:" -ForegroundColor Green
    $functions | ForEach-Object {
        Write-Host "  - $($_.name)" -ForegroundColor White
    }
    Write-Host "`n✅ Minimales Deployment erfolgreich!" -ForegroundColor Green
} else {
    Write-Host "`n⚠️  Keine Functions gefunden" -ForegroundColor Yellow
    Write-Host "Prüfe Logs im Azure Portal oder warte weitere 1-2 Minuten." -ForegroundColor Yellow
}

Write-Host "`n=== Deployment abgeschlossen ===" -ForegroundColor Cyan


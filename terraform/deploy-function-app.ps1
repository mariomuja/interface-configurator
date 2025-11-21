# Deploy Azure Function App using WEBSITE_RUN_FROM_PACKAGE
# This script uploads the Function App ZIP to Blob Storage and sets WEBSITE_RUN_FROM_PACKAGE

param(
    [string]$ResourceGroup = "rg-interface-configuration",
    [string]$FunctionAppName = "func-integration",
    [string]$StorageAccountName = "stfuncsappe1mz5h",
    [string]$ContainerName = "function-releases",
    [string]$ZipPath = "azure-functions/main/publish/function-app.zip"
)

Write-Host "`n=== Azure Function App Deployment ===" -ForegroundColor Cyan
Write-Host "Function App: $FunctionAppName" -ForegroundColor White
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "Storage Account: $StorageAccountName" -ForegroundColor White

# Step 1: Check ZIP file
Write-Host "`n1. Prüfe ZIP-Datei..." -ForegroundColor Yellow
$fullZipPath = Resolve-Path $ZipPath -ErrorAction SilentlyContinue
if (-not $fullZipPath) {
    Write-Host "❌ ZIP nicht gefunden: $ZipPath" -ForegroundColor Red
    Write-Host "Erstelle neues ZIP..." -ForegroundColor Yellow
    Push-Location "azure-functions/main"
    dotnet publish --no-self-contained --configuration Release --output ./publish
    Pop-Location
    $fullZipPath = Resolve-Path $ZipPath
}

$zipSize = [math]::Round((Get-Item $fullZipPath).Length / 1MB, 2)
Write-Host "✅ ZIP gefunden: $zipSize MB" -ForegroundColor Green
Write-Host "   Pfad: $fullZipPath" -ForegroundColor White

# Step 2: Get Storage Account Key
Write-Host "`n2. Hole Storage Account Key..." -ForegroundColor Yellow
$storageKey = az storage account keys list --resource-group $ResourceGroup --account-name $StorageAccountName --query "[0].value" -o tsv
if (-not $storageKey) {
    Write-Host "❌ Storage Key nicht gefunden" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Storage Key erhalten" -ForegroundColor Green

# Step 3: Create container if not exists
Write-Host "`n3. Erstelle Container falls nicht vorhanden..." -ForegroundColor Yellow
az storage container create --account-name $StorageAccountName --account-key $storageKey --name $ContainerName --public-access off --output none 2>&1 | Out-Null
Write-Host "✅ Container bereit" -ForegroundColor Green

# Step 4: Upload ZIP to Blob Storage
Write-Host "`n4. Upload ZIP zu Blob Storage..." -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$blobName = "$timestamp-function-app.zip"
Write-Host "   Blob Name: $blobName" -ForegroundColor White

$uploadResult = az storage blob upload --account-name $StorageAccountName --account-key $storageKey --container-name $ContainerName --name $blobName --file $fullZipPath --overwrite 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Upload fehlgeschlagen:" -ForegroundColor Red
    Write-Host $uploadResult
    exit 1
}
Write-Host "✅ ZIP erfolgreich hochgeladen" -ForegroundColor Green

# Step 5: Generate SAS URL
Write-Host "`n5. Generiere SAS URL..." -ForegroundColor Yellow
$sasExpiry = (Get-Date).AddYears(10).ToString("yyyy-MM-ddTHH:mm:ssZ")
Write-Host "   Ablauf: $sasExpiry" -ForegroundColor White

$sasToken = az storage blob generate-sas --account-name $StorageAccountName --account-key $storageKey --container-name $ContainerName --name $blobName --permissions r --expiry $sasExpiry --output tsv
if ($LASTEXITCODE -ne 0 -or -not $sasToken) {
    Write-Host "❌ SAS Token Generierung fehlgeschlagen" -ForegroundColor Red
    exit 1
}

$blobUrl = "https://$StorageAccountName.blob.core.windows.net/$ContainerName/$blobName" + "?" + $sasToken
Write-Host "✅ SAS URL generiert" -ForegroundColor Green
Write-Host "   URL (gekürzt): $($blobUrl.Substring(0, [Math]::Min(100, $blobUrl.Length)))..." -ForegroundColor White

# Step 6: Set WEBSITE_RUN_FROM_PACKAGE
Write-Host "`n6. Setze WEBSITE_RUN_FROM_PACKAGE..." -ForegroundColor Yellow
Write-Host "   URL Länge: $($blobUrl.Length) Zeichen" -ForegroundColor White

# Use JSON file to avoid PowerShell escaping issues
$settingsJson = @{
    WEBSITE_RUN_FROM_PACKAGE = $blobUrl
} | ConvertTo-Json -Compress

$tempSettingsFile = [System.IO.Path]::GetTempFileName()
$settingsJson | Out-File -FilePath $tempSettingsFile -Encoding UTF8 -NoNewline

try {
    $settingResult = az functionapp config appsettings set --resource-group $ResourceGroup --name $FunctionAppName --settings "@$tempSettingsFile" --output json 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Fehler beim Setzen:" -ForegroundColor Red
        Write-Host $settingResult
        Remove-Item $tempSettingsFile -ErrorAction SilentlyContinue
        exit 1
    }
    Write-Host "✅ WEBSITE_RUN_FROM_PACKAGE gesetzt" -ForegroundColor Green
} finally {
    Remove-Item $tempSettingsFile -ErrorAction SilentlyContinue
}

# Step 7: Verify setting
Write-Host "`n7. Prüfe Einstellung..." -ForegroundColor Yellow
Start-Sleep -Seconds 3
$packageUrl = az functionapp config appsettings list --resource-group $ResourceGroup --name $FunctionAppName --query "[?name=='WEBSITE_RUN_FROM_PACKAGE'].value" -o tsv
if ($packageUrl -like "*blob.core.windows.net*") {
    Write-Host "✅ WEBSITE_RUN_FROM_PACKAGE korrekt gesetzt" -ForegroundColor Green
} else {
    Write-Host "⚠️  URL könnte unvollständig sein" -ForegroundColor Yellow
    Write-Host "   Gesetzt: $packageUrl" -ForegroundColor White
}

# Step 8: Restart Function App
Write-Host "`n8. Starte Function App neu..." -ForegroundColor Yellow
az functionapp restart --resource-group $ResourceGroup --name $FunctionAppName --output none 2>&1 | Out-Null
Write-Host "✅ Function App neu gestartet" -ForegroundColor Green

# Step 9: Wait for initialization
Write-Host "`n⏳ Warte 120 Sekunden für Function App Initialisierung..." -ForegroundColor Yellow
Write-Host "   (Die Function App muss das ZIP aus Blob Storage laden und initialisieren)" -ForegroundColor White
$progress = 0
while ($progress -lt 120) {
    Start-Sleep -Seconds 15
    $progress += 15
    Write-Host "   ... $progress Sekunden gewartet" -ForegroundColor Gray
}

# Step 10: Check Functions
Write-Host "`n9. Prüfe Functions..." -ForegroundColor Cyan
$functionsOutput = az functionapp function list --resource-group $ResourceGroup --name $FunctionAppName -o json 2>&1
if ($LASTEXITCODE -eq 0) {
    $functionsJson = $functionsOutput | Select-String -Pattern '^\s*\[|\{' | Out-String
    if ($functionsJson) {
        try {
            $functions = $functionsJson | ConvertFrom-Json
            if ($functions -and $functions.Count -gt 0) {
                Write-Host "`n✅ Functions erfolgreich erkannt:" -ForegroundColor Green
                $functions | Select-Object name, language, scriptRootPath | Format-Table -AutoSize
                Write-Host "`n🎉 Deployment erfolgreich!" -ForegroundColor Green
            } else {
                Write-Host "`n⚠️  Keine Functions gefunden (leeres Array)" -ForegroundColor Yellow
                Write-Host "   Prüfen Sie die Function App im Azure Portal" -ForegroundColor White
            }
        } catch {
            Write-Host "`n⚠️  Fehler beim Parsen der Functions" -ForegroundColor Red
            Write-Host "Error: $_" -ForegroundColor Yellow
        }
    } else {
        Write-Host "`n⚠️  Keine JSON-Antwort erhalten" -ForegroundColor Yellow
        Write-Host "Output: $functionsOutput" -ForegroundColor White
    }
} else {
    Write-Host "`n⚠️  Fehler beim Abrufen der Functions" -ForegroundColor Red
    Write-Host "Output: $functionsOutput" -ForegroundColor Yellow
}

# Step 11: Check Function App Status
Write-Host "`n10. Prüfe Function App Status..." -ForegroundColor Cyan
$status = az functionapp show --resource-group $ResourceGroup --name $FunctionAppName --query "{state:state, enabled:enabled, defaultHostName:defaultHostName}" -o json | ConvertFrom-Json
Write-Host "Status: $($status.state)" -ForegroundColor $(if ($status.state -eq "Running") { "Green" } else { "Yellow" })
Write-Host "Enabled: $($status.enabled)" -ForegroundColor White
Write-Host "Host: $($status.defaultHostName)" -ForegroundColor White

Write-Host "`n=== Deployment abgeschlossen ===" -ForegroundColor Cyan
Write-Host "Function App: $FunctionAppName" -ForegroundColor White
Write-Host "Blob URL: $blobUrl" -ForegroundColor White
Write-Host "`nNächste Schritte:" -ForegroundColor Yellow
Write-Host "1. Prüfen Sie die Functions im Azure Portal" -ForegroundColor White
Write-Host "2. Testen Sie die Function mit einem Blob-Upload" -ForegroundColor White
Write-Host "3. Prüfen Sie die Logs bei Bedarf" -ForegroundColor White


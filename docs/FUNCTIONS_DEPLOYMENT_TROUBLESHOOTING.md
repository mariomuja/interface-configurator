# Azure Functions Deployment - Troubleshooting

## Problem: Functions sind nicht in Azure sichtbar

### Mögliche Ursachen:

1. **Deployment nicht erfolgreich**
   - Das ZIP-Paket wurde nicht korrekt hochgeladen
   - Timeout beim Deployment
   - GitHub Actions Deployment fehlgeschlagen

2. **Function App Konfiguration**
   - `FUNCTIONS_WORKER_RUNTIME` nicht korrekt gesetzt
   - App Settings fehlen

3. **Functions werden nicht erkannt**
   - `functions.metadata` fehlt oder ist falsch
   - `host.json` fehlt oder ist falsch
   - DLLs fehlen im Deployment-Paket

## Lösungsansätze:

### 1. GitHub Actions Deployment prüfen

1. Gehe zu GitHub → Actions
2. Prüfe ob der Workflow `Deploy Azure Functions` erfolgreich gelaufen ist
3. Prüfe die Logs auf Fehler

### 2. Manuelles Deployment über Azure Portal

1. Gehe zu Azure Portal → Function App → Deployment Center
2. Wähle "Local Git" oder "External Git"
3. Lade das ZIP-Paket manuell hoch

### 3. Deployment über Azure Functions Core Tools

```powershell
# Installiere Azure Functions Core Tools
winget install Microsoft.AzureFunctionsCoreTools

# Deploy
cd azure-functions\ProcessCsvBlobTrigger
func azure functionapp publish func-appe1mz5h --dotnet-isolated
```

### 4. Prüfe Function App Status

```powershell
# Prüfe Function App Status
az functionapp show --name func-appe1mz5h --resource-group rg-interface-configuration --query "state"

# Prüfe App Settings
az functionapp config appsettings list --name func-appe1mz5h --resource-group rg-interface-configuration --query "[?name=='FUNCTIONS_WORKER_RUNTIME']"

# Liste Functions
az functionapp function list --name func-appe1mz5h --resource-group rg-interface-configuration
```

### 5. Prüfe Deployment-Paket

Das ZIP-Paket muss folgende Dateien enthalten:
- `host.json` (im Root)
- `functions.metadata` (im Root)
- `ProcessCsvBlobTrigger.dll` (im Root)
- `extensions.json` (im Root)
- Alle benötigten DLLs

### 6. Alternative: Deployment über Blob Storage

Wenn das direkte Deployment nicht funktioniert:

1. Lade das ZIP in Blob Storage hoch
2. Setze `WEBSITE_RUN_FROM_PACKAGE` App Setting auf die Blob URL

```powershell
# Erstelle SAS URL für Blob
$blobUrl = az storage blob generate-sas --account-name stfuncsappe1mz5h --container-name function-releases --name function-app.zip --permissions r --expiry (Get-Date).AddHours(24).ToString("yyyy-MM-ddTHH:mm:ssZ") --full-uri --output tsv

# Setze App Setting
az functionapp config appsettings set --name func-appe1mz5h --resource-group rg-interface-configuration --settings "WEBSITE_RUN_FROM_PACKAGE=$blobUrl"
```

## Nächste Schritte:

1. **Prüfe GitHub Actions**: Gehe zu GitHub → Actions und prüfe ob das Deployment erfolgreich war
2. **Prüfe Azure Portal**: Gehe zu Function App → Functions und prüfe ob Functions sichtbar sind
3. **Prüfe Logs**: Gehe zu Function App → Log stream und prüfe auf Fehler
4. **Manuelles Deployment**: Verwende `deploy-functions.ps1` oder Azure Functions Core Tools

## Wichtige Hinweise:

- Functions können einige Minuten brauchen, um nach dem Deployment sichtbar zu werden
- Bei dotnet-isolated Functions muss `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated` gesetzt sein
- Das Deployment-Paket muss alle benötigten DLLs enthalten
- Bei großen ZIP-Dateien kann das Deployment länger dauern (15 MB sollte aber kein Problem sein)


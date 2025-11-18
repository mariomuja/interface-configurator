# Azure Function App Troubleshooting - ServiceUnavailable Fehler

## Problem

Beim Auflisten der Azure Functions tritt der Fehler auf:
```
Encountered an error (ServiceUnavailable) from host runtime.
```

## Ursachen

Dieser Fehler tritt auf, wenn:

1. **WEBSITE_RUN_FROM_PACKAGE nicht gesetzt** - Die Function App weiß nicht, wo das Package liegt
2. **Storage Account nicht verfügbar** - AzureWebJobsStorage Connection String ist ungültig
3. **App Settings fehlen** - Kritische Settings wie FUNCTIONS_WORKER_RUNTIME fehlen
4. **Function App startet noch** - Nach Neustart benötigt die App Zeit zum Initialisieren

## Lösung

### Schritt 1: App Settings über Terraform aktualisieren

```bash
cd terraform
terraform apply -target=azurerm_linux_function_app.main
```

Dies stellt sicher, dass alle App Settings korrekt gesetzt sind:
- `FUNCTIONS_WORKER_RUNTIME=node`
- `WEBSITE_NODE_DEFAULT_VERSION=~20`
- `AzureWebJobsStorage` (Connection String)
- `WEBSITE_USE_PLACEHOLDER=0`
- SQL Server Settings (falls benötigt)

### Schritt 2: Package deployen

Das Package muss über GitHub Actions deployed werden, oder manuell:

```powershell
# 1. Package erstellen
cd azure-functions
Compress-Archive -Path "SimpleTestFunction", "host.json", "package.json" -DestinationPath "function-app.zip" -Force

# 2. Upload zu Blob Storage
$storageKey = az storage account keys list --resource-group rg-interface-configuration --account-name stfuncsapprigklebtsay2o --query "[0].value" -o tsv
az storage blob upload --account-name stfuncsapprigklebtsay2o --account-key $storageKey --container-name function-releases --name "function-app.zip" --file function-app.zip --overwrite

# 3. SAS Token generieren
$expiry = (Get-Date).AddYears(10).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$sasToken = az storage container generate-sas --name function-releases --account-name stfuncsapprigklebtsay2o --account-key $storageKey --permissions r --expiry $expiry -o tsv

# 4. WEBSITE_RUN_FROM_PACKAGE setzen
$packageUrl = "https://stfuncsapprigklebtsay2o.blob.core.windows.net/function-releases/function-app.zip?$sasToken"
az functionapp config appsettings set --resource-group rg-interface-configuration --name func-apprigklebtsay2o --settings "WEBSITE_RUN_FROM_PACKAGE=$packageUrl"
```

### Schritt 3: Function App neu starten

```bash
az functionapp restart --resource-group rg-interface-configuration --name func-apprigklebtsay2o
```

**WICHTIG:** Warten Sie 30-60 Sekunden nach dem Neustart, bevor Sie die Functions auflisten.

### Schritt 4: Services verifizieren

Stellen Sie sicher, dass alle Services verfügbar sind:

1. **Storage Account**: Muss `statusOfPrimary: "available"` haben
2. **SQL Server**: Muss `state: "Ready"` haben (falls verwendet)
3. **Function App**: Muss `state: "Running"` haben

## Verifizierung

### Function App Status prüfen

```bash
az functionapp show --resource-group rg-interface-configuration --name func-apprigklebtsay2o --query "{state:state, defaultHostName:defaultHostName}"
```

### Functions auflisten

```bash
# Über REST API (zuverlässiger)
az rest --method GET \
  --uri "https://management.azure.com/subscriptions/$(az account show --query id -o tsv)/resourceGroups/rg-interface-configuration/providers/Microsoft.Web/sites/func-apprigklebtsay2o/functions?api-version=2022-03-01" \
  --output json
```

### Function direkt testen

```bash
curl https://func-apprigklebtsay2o.azurewebsites.net/api/SimpleTestFunction
```

## Häufige Probleme

### Problem: App Settings werden auf null gesetzt

**Ursache:** Terraform und CLI überschreiben sich gegenseitig

**Lösung:** Verwenden Sie Terraform für alle App Settings, außer `WEBSITE_RUN_FROM_PACKAGE` (wird vom Deployment Workflow gesetzt)

### Problem: Functions werden nicht erkannt

**Ursache:** Package-Struktur ist falsch oder `host.json` fehlt

**Lösung:** Stellen Sie sicher, dass das Package enthält:
- `host.json` (im Root)
- `package.json` (im Root)
- `SimpleTestFunction/function.json`
- `SimpleTestFunction/index.js`

### Problem: ServiceUnavailable nach Neustart

**Ursache:** Function App benötigt Zeit zum Initialisieren

**Lösung:** Warten Sie 30-60 Sekunden nach dem Neustart

## Automatisierung

Verwenden Sie das Script `ensure-function-app-ready.ps1`:

```powershell
cd azure-functions
.\ensure-function-app-ready.ps1
```

Dieses Script:
1. Prüft alle kritischen App Settings
2. Setzt fehlende Settings
3. Verifiziert Storage Account und SQL Server
4. Startet die Function App neu
5. Prüft den Status

## Weitere Hilfe

- **Azure Portal**: Prüfen Sie die Function App Logs im Portal
- **Kudu Console**: `https://func-apprigklebtsay2o.scm.azurewebsites.net`
- **Application Insights**: Falls konfiguriert, prüfen Sie die Telemetrie










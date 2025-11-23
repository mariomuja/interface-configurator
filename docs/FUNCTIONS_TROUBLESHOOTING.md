# Azure Functions Troubleshooting Guide

## Problem: Functions werden nicht geladen - "Einige Funktionen konnten aufgrund von Fehlern nicht geladen werden"

### Ursache
Bei `dotnet-isolated` Azure Functions sollten **keine Extension Bundles** verwendet werden, da die Extensions über NuGet-Pakete bereitgestellt werden.

### Lösung

#### 1. host.json korrigieren
Die `host.json` sollte **keine** `extensionBundle` Konfiguration enthalten:

```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "maxTelemetryItemsPerSecond": 20
      }
    },
    "logLevel": {
      "default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

**NICHT verwenden:**
```json
{
  "extensionBundle": {
    "id": "Microsoft.Azure.Functions.ExtensionBundle",
    "version": "[4.*, 5.0.0)"
  }
}
```

#### 2. Blob Trigger Connection explizit setzen
Im Function Code sollte die Connection explizit angegeben werden:

```csharp
[BlobTrigger("csv-uploads/{name}", Connection = "AzureWebJobsStorage")] byte[] blobContent
```

#### 3. App Settings prüfen
Stellen Sie sicher, dass folgende App Settings in Azure gesetzt sind:
- `FUNCTIONS_WORKER_RUNTIME` = `dotnet-isolated`
- `AzureWebJobsStorage` = Connection String zum Storage Account
- `WEBSITE_RUN_FROM_PACKAGE` = wird automatisch von Azure gesetzt

### Verifizierung nach Deployment

1. **Azure Portal prüfen:**
   - Function App → Functions
   - Die Funktion sollte jetzt sichtbar sein

2. **Logs prüfen:**
   - Function App → Log stream
   - Nach Fehlermeldungen suchen

3. **GitHub Actions Logs:**
   - Repository → Actions → Latest Workflow Run
   - Prüfen ob `host.json` korrekt ist (keine Extension Bundles)

### Häufige Fehlerquellen

1. **Extension Bundles bei dotnet-isolated** ✅ Behoben
2. **Fehlende Connection Strings** - Prüfen Sie die App Settings
3. **Falsche Runtime-Version** - Muss `dotnet-isolated` sein
4. **Deployment-Probleme** - Prüfen Sie die GitHub Actions Logs

### Weitere Hilfe

- [Azure Functions dotnet-isolated Dokumentation](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Troubleshooting Azure Functions](https://learn.microsoft.com/en-us/azure/azure-functions/functions-recover-storage-account)


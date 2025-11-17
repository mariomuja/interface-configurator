# Zusammenfassung: Azure Function App ServiceUnavailable Fehler beheben

## Problem
Beim Auflisten der Azure Functions tritt der Fehler auf:
```
Encountered an error (ServiceUnavailable) from host runtime.
```

## Durchgefuehrte Massnahmen

### 1. App Settings gesetzt
Alle kritischen App Settings wurden gesetzt:
- ✅ `FUNCTIONS_WORKER_RUNTIME=node`
- ✅ `WEBSITE_NODE_DEFAULT_VERSION=~20`
- ✅ `WEBSITE_USE_PLACEHOLDER=0`
- ✅ `AzureWebJobsStorage` (Connection String)
- ✅ `WEBSITE_RUN_FROM_PACKAGE` (Blob URL mit SAS Token)

### 2. Package deployed
- Package erstellt: `deploy-package.zip`
- Hochgeladen zu: `stfuncsapprigklebtsay2o.blob.core.windows.net/function-releases/`
- `WEBSITE_RUN_FROM_PACKAGE` auf Blob URL gesetzt

### 3. Services verifiziert
- ✅ Storage Account: `stfuncsapprigklebtsay2o` - Verfügbar
- ✅ SQL Server: `sql-infrastructurerigklebtsay2o` - Ready
- ✅ Function App: `func-apprigklebtsay2o` - Running

### 4. Function App neu gestartet
Die Function App wurde neu gestartet und sollte jetzt verfügbar sein.

## Naechste Schritte

1. **Warten Sie 30-60 Sekunden** nach dem Neustart
2. **Pruefen Sie die Functions**:
   ```bash
   # Im Azure Portal
   # Oder ueber REST API
   az rest --method GET \
     --uri "https://management.azure.com/subscriptions/$(az account show --query id -o tsv)/resourceGroups/rg-infrastructure-as-code/providers/Microsoft.Web/sites/func-apprigklebtsay2o/functions?api-version=2022-03-01"
   ```

3. **Testen Sie die Function direkt**:
   ```bash
   curl https://func-apprigklebtsay2o.azurewebsites.net/api/SimpleTestFunction
   ```

## Falls das Problem weiterhin besteht

1. Pruefen Sie die Logs im Azure Portal
2. Stellen Sie sicher, dass das Package korrekt strukturiert ist:
   - `host.json` (im Root)
   - `package.json` (im Root)
   - `SimpleTestFunction/function.json`
   - `SimpleTestFunction/index.js`
3. Pruefen Sie, ob `WEBSITE_RUN_FROM_PACKAGE` eine gueltige Blob URL ist
4. Warten Sie laenger (manchmal dauert es bis zu 2 Minuten)

## Wichtige Hinweise

- **WEBSITE_RUN_FROM_PACKAGE** muss eine gueltige Blob URL mit SAS Token sein
- **WEBSITE_USE_PLACEHOLDER=0** ist wichtig, damit Functions geladen werden
- Nach Neustart benoetigt die Function App Zeit zum Initialisieren
- Das Package muss die korrekte Struktur haben





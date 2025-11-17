# Function Visibility Issue - BadGateway Error

## Problem

Nach erfolgreichem Deployment werden die Funktionen nicht im Azure Portal angezeigt:
- Fehlermeldung: "Einige Funktionen in der Liste konnten aufgrund von Fehlern nicht geladen werden"
- Details: "Encountered an error (BadGateway) from host runtime"

## Aktueller Status

- ✅ Function App läuft (state: "Running")
- ✅ Deployment erfolgreich (keine Fehler im Workflow)
- ✅ `FUNCTIONS_WORKER_RUNTIME` = "dotnet-isolated" (korrekt)
- ✅ `AzureWebJobsStorage` gesetzt (korrekt)
- ❌ `WEBSITE_RUN_FROM_PACKAGE` nicht gesetzt (nach Terraform Apply entfernt)

## Mögliche Ursachen

### 1. WEBSITE_RUN_FROM_PACKAGE fehlt

Nach dem Deployment sollte Azure automatisch `WEBSITE_RUN_FROM_PACKAGE` auf die Blob Storage URL setzen. Wenn das nicht passiert, können die Funktionen nicht geladen werden.

**Lösung**: Prüfe nach dem nächsten Deployment, ob `WEBSITE_RUN_FROM_PACKAGE` gesetzt ist.

### 2. Package-Struktur

Das ZIP-Package muss korrekt strukturiert sein:
- `ProcessCsvBlobTrigger.dll` (Haupt-DLL)
- `host.json`
- `worker.config.json`
- `functions.metadata`
- Alle Abhängigkeiten

**Lösung**: Der Workflow prüft jetzt die Package-Struktur.

### 3. Function App Runtime kann Package nicht laden

Wenn `WEBSITE_RUN_FROM_PACKAGE` auf eine URL gesetzt ist, muss die Function App das Package von Blob Storage laden können.

**Lösung**: Prüfe die Function App Logs im Azure Portal.

## Debugging-Schritte

### 1. Prüfe WEBSITE_RUN_FROM_PACKAGE nach Deployment

```bash
az functionapp config appsettings list \
  --resource-group rg-infrastructure-as-code \
  --name func-appe1mz5h \
  --query "[?name=='WEBSITE_RUN_FROM_PACKAGE']"
```

### 2. Prüfe Function App Logs

Im Azure Portal:
- Function App → Monitoring → Log stream
- Oder: Function App → Development Tools → Kudu → Debug console → LogFiles

### 3. Prüfe ob Package hochgeladen wurde

```bash
az storage blob list \
  --account-name stfuncsappe1mz5h \
  --container-name function-releases \
  --query "[].{Name: name, LastModified: properties.lastModified}" \
  -o table
```

### 4. Manuell WEBSITE_RUN_FROM_PACKAGE setzen

Falls Azure es nicht automatisch setzt:

1. Finde die Blob URL aus dem Storage Account
2. Setze manuell:
   ```bash
   az functionapp config appsettings set \
     --resource-group rg-infrastructure-as-code \
     --name func-appe1mz5h \
     --settings WEBSITE_RUN_FROM_PACKAGE="<blob-url>"
   ```
3. Function App neu starten

## Nächste Schritte

1. Warte auf den nächsten Workflow-Run
2. Prüfe ob `WEBSITE_RUN_FROM_PACKAGE` nach dem Deployment gesetzt ist
3. Prüfe die Function App Logs
4. Falls weiterhin Probleme: Manuell `WEBSITE_RUN_FROM_PACKAGE` setzen

## Hinweis zur Testversion

Die Azure Testversion sollte **nicht** das Problem sein. Azure Functions im Consumption Plan funktionieren auch mit Free Tier / Testversionen. Das Problem ist wahrscheinlich technisch (Package-Loading oder Konfiguration).






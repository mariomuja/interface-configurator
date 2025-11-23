# Azure Functions nicht sichtbar - Lösungsansätze

## Problem
Die Functions wurden erfolgreich deployed, aber sie erscheinen nicht in der Functions-Liste im Azure Portal mit der Fehlermeldung:
"Einige Funktionen in der Liste konnten aufgrund von Fehlern nicht geladen werden."

## Mögliche Ursachen und Lösungen

### 1. Extension Bundle Problem
Bei dotnet-isolated Functions kann es sein, dass die Extension Bundle nicht richtig geladen wird.

**Lösung**: Prüfe die `host.json`:
```json
{
  "version": "2.0",
  "extensionBundle": {
    "id": "Microsoft.Azure.Functions.ExtensionBundle",
    "version": "[4.*, 5.0.0)"
  }
}
```

### 2. Storage Connection Problem
Die Functions benötigen eine funktionierende Storage Connection.

**Prüfen**:
```powershell
az functionapp config appsettings list --name func-appe1mz5h --resource-group rg-interface-configuration --query "[?name=='AzureWebJobsStorage']"
```

**Lösung**: Stelle sicher, dass `AzureWebJobsStorage` korrekt gesetzt ist.

### 3. Functions müssen neu registriert werden
Manchmal müssen die Functions nach dem Deployment neu registriert werden.

**Lösung**:
1. Function App neu starten: `az functionapp restart --name func-appe1mz5h --resource-group rg-interface-configuration`
2. Warte 2-3 Minuten
3. Prüfe erneut die Functions-Liste

### 4. functions.metadata Problem
Die `functions.metadata` Datei muss korrekt sein.

**Prüfen**: Die Datei sollte im Root des Deployment-Verzeichnisses sein und folgendes Format haben:
```json
[
  {
    "name": "ProcessCsvBlobTrigger",
    "scriptFile": "ProcessCsvBlobTrigger.dll",
    "entryPoint": "ProcessCsvBlobTrigger.ProcessCsvBlobTriggerFunction.Run",
    "language": "dotnet-isolated"
  }
]
```

### 5. Worker Runtime Problem
Stelle sicher, dass `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated` gesetzt ist.

**Prüfen**:
```powershell
az functionapp config appsettings list --name func-appe1mz5h --resource-group rg-interface-configuration --query "[?name=='FUNCTIONS_WORKER_RUNTIME']"
```

### 6. Application Insights Problem
Manchmal kann Application Insights die Functions-Erkennung beeinträchtigen.

**Lösung**: Prüfe die Logs im Azure Portal:
- Function App → Log stream
- Function App → Monitoring → Logs

### 7. Deployment-Verzeichnis Problem
Stelle sicher, dass alle Dateien im richtigen Verzeichnis sind:
- `host.json` im Root
- `functions.metadata` im Root
- `ProcessCsvBlobTrigger.dll` im Root
- Alle benötigten DLLs vorhanden

## Debugging-Schritte

1. **Prüfe die Logs**:
   ```powershell
   az functionapp log tail --name func-appe1mz5h --resource-group rg-interface-configuration
   ```

2. **Prüfe die deployed Dateien** über Kudu:
   - Gehe zu: `https://func-appe1mz5h.scm.azurewebsites.net`
   - Navigiere zu: Debug Console → CMD → site → wwwroot
   - Prüfe ob alle Dateien vorhanden sind

3. **Prüfe die Function App Status**:
   ```powershell
   az functionapp show --name func-appe1mz5h --resource-group rg-interface-configuration --query "state"
   ```

4. **Teste die Function direkt**:
   - Versuche einen Blob in den Container `csv-uploads` hochzuladen
   - Prüfe die Logs, ob die Function getriggert wird

## Häufige Lösung
Oft hilft es, die Function App einfach neu zu starten und 2-3 Minuten zu warten, bis die Functions registriert sind.


# Troubleshooting: Funktionen werden nicht angezeigt

## Problem

Nach erfolgreichem Deployment werden die Funktionen nicht im Azure Portal angezeigt. Fehlermeldung:
- "Einige Funktionen in der Liste konnten aufgrund von Fehlern nicht geladen werden"
- "Encountered an error (BadGateway) from host runtime"

## Mögliche Ursachen

### 1. Run from Package Modus

Wenn `WEBSITE_RUN_FROM_PACKAGE` auf eine URL gesetzt ist (nicht "1"), kann es Probleme geben.

**Lösung**: Der Workflow deaktiviert jetzt temporär `WEBSITE_RUN_FROM_PACKAGE` während des Deployments.

### 2. Function App muss neu gestartet werden

Nach dem Deployment muss die Function App neu gestartet werden.

**Lösung**: Der Workflow startet die Function App automatisch neu.

### 3. ZIP-Package Struktur

Das ZIP-Package muss folgende Dateien enthalten:
- `ProcessCsvBlobTrigger.dll`
- `host.json`
- `worker.config.json`
- `functions.metadata`
- Alle Abhängigkeiten

**Lösung**: Der Workflow prüft jetzt die Package-Struktur.

## Debugging-Schritte

### 1. Function App Logs prüfen

```bash
# Im Azure Portal:
# Function App → Monitoring → Log stream
```

### 2. Function App Status prüfen

```bash
az functionapp show \
  --resource-group rg-interface-configuration \
  --name func-appe1mz5h \
  --query "{state: state, hostNames: defaultHostName}"
```

### 3. App Settings prüfen

```bash
az functionapp config appsettings list \
  --resource-group rg-interface-configuration \
  --name func-appe1mz5h \
  --query "[?name=='WEBSITE_RUN_FROM_PACKAGE']"
```

### 4. Function App neu starten

```bash
az functionapp restart \
  --resource-group rg-interface-configuration \
  --name func-appe1mz5h
```

## Alternative: Run from Package deaktivieren

Falls das Problem weiterhin besteht, kannst du `WEBSITE_RUN_FROM_PACKAGE` komplett entfernen:

1. In `terraform/main.tf` die Zeile entfernen:
   ```hcl
   "WEBSITE_RUN_FROM_PACKAGE" = "1"
   ```

2. Terraform anwenden:
   ```bash
   terraform apply
   ```

3. Function App neu starten

## Workflow-Verbesserungen

Der aktualisierte Workflow:
- ✅ Prüft die Package-Struktur vor dem Deployment
- ✅ Deaktiviert temporär `WEBSITE_RUN_FROM_PACKAGE` während des Deployments
- ✅ Aktiviert `WEBSITE_RUN_FROM_PACKAGE` wieder nach dem Deployment
- ✅ Startet die Function App automatisch neu
- ✅ Zeigt detaillierte Status-Informationen

## Nächste Schritte

1. Warte auf den nächsten Workflow-Run
2. Prüfe die Logs im GitHub Actions
3. Prüfe die Function App Logs im Azure Portal
4. Falls das Problem weiterhin besteht, entferne `WEBSITE_RUN_FROM_PACKAGE` komplett











# Sync Trigger Fix - Deployment Summary

## Problem
Sync Trigger schlägt fehl bei Function App Deployment; die Funktionen werden nicht gelistet.

## Root Cause
Bei .NET Isolated Worker Functions versucht Azure während des Deployments, die Functions neu zu bauen, was zu Sync-Trigger-Fehlern führt. Die Functions werden nicht korrekt erkannt.

## Lösung

### 1. App-Einstellungen in Bicep-Template (`bicep/main.bicep`)
Folgende kritische App-Einstellungen wurden hinzugefügt:
- `FUNCTIONS_EXTENSION_VERSION=~4` - Stellt sicher, dass Functions v4 Runtime verwendet wird
- `SCM_DO_BUILD_DURING_DEPLOYMENT=false` - Verhindert Rebuild während Deployment
- `ENABLE_ORYX_BUILD=false` - Deaktiviert Oryx Build-System

### 2. GitHub Actions Workflow (`.github/workflows/deploy-functions.yml`)
- Setzt die neuen App-Einstellungen automatisch während Deployment
- Verifiziert, dass `functions.metadata` generiert wird
- Verbesserter Sync-Trigger-Schritt mit alternativen Methoden
- Verifiziert App-Einstellungen nach dem Setzen

### 3. PowerShell Deployment-Skript (`terraform/deploy-function-app.ps1`)
- Setzt die neuen App-Einstellungen während Deployment
- Neuer Sync-Trigger-Schritt mit Azure CLI und Admin API
- Verbesserte Fehlerbehandlung

### 4. Build-Konfiguration (`azure-functions/main/main.csproj`)
- `GenerateFunctionsMetadata=true` hinzugefügt, um sicherzustellen, dass `functions.metadata` generiert wird

### 5. Neues Fix-Skript (`azure-functions/fix-sync-trigger-settings.ps1`)
Standalone-Skript zum manuellen Setzen der App-Einstellungen, falls das Bicep-Template noch nicht deployed wurde.

## Verwendung

### Option 1: Bicep-Template neu deployen
```bash
az deployment group create \
  --resource-group <resource-group> \
  --template-file bicep/main.bicep \
  --parameters bicep/parameters.json
```

### Option 2: Fix-Skript ausführen (wenn Bicep noch nicht deployed)
```powershell
.\azure-functions\fix-sync-trigger-settings.ps1 `
  -ResourceGroup "rg-interface-configuration" `
  -FunctionAppName "func-integration-main"
```

### Option 3: GitHub Actions Deployment
Die Änderungen werden automatisch beim nächsten Push angewendet.

## Erwartetes Ergebnis
Nach dem Deployment sollten:
- ✅ Alle Functions korrekt erkannt und gelistet werden
- ✅ Sync Trigger erfolgreich durchlaufen
- ✅ Functions im Azure Portal sichtbar sein
- ✅ HTTP-Endpunkte erreichbar sein

## Troubleshooting
Falls Functions immer noch nicht erkannt werden:
1. Function App neu starten: `az functionapp restart --resource-group <rg> --name <app>`
2. 2-3 Minuten warten für Initialisierung
3. Sync Trigger manuell ausführen mit `fix-sync-trigger-settings.ps1`
4. Azure Portal prüfen: Function App → Functions

## Technische Details
- **Runtime**: .NET Isolated Worker (dotnet-isolated)
- **Functions Version**: v4
- **Deployment-Methode**: Run from Package (WEBSITE_RUN_FROM_PACKAGE)
- **Build**: Framework-dependent (--no-self-contained)


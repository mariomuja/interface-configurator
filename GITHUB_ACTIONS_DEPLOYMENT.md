# GitHub Actions Deployment für Azure Functions

## Übersicht

Die Azure Functions werden automatisch über GitHub Actions deployed. Terraform erstellt nur die Infrastruktur (Function App, Storage Account, etc.), während GitHub Actions den Code automatisch bei jedem Push zum `main` Branch deployed.

## Setup

### 1. GitHub Secret konfigurieren

1. Gehe zu GitHub Repository → Settings → Secrets and variables → Actions
2. Füge ein neues Secret hinzu:
   - **Name**: `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`
   - **Wert**: Publish Profile der Function App

### 2. Publish Profile abrufen

```bash
# Mit Azure CLI
az functionapp deployment list-publishing-profiles \
  --resource-group rg-infrastructure-as-code \
  --name func-appe1mz5h \
  --xml
```

Kopiere den gesamten XML-Inhalt und füge ihn als Secret `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` in GitHub ein.

**Alternative**: Im Azure Portal:
1. Function App → Deployment Center → Deployment Center
2. Klicke auf "Get publish profile"
3. Kopiere den Inhalt und füge ihn als GitHub Secret ein

### 3. Function App Name aktualisieren

Der Function App Name ist aktuell hardcoded im Workflow. Um ihn dynamisch zu machen:

1. Hole den Namen aus Terraform Outputs:
   ```bash
   cd terraform
   terraform output function_app_name
   ```

2. Aktualisiere `.github/workflows/deploy-azure-functions.yml`:
   ```yaml
   env:
     AZURE_FUNCTIONAPP_NAME: <output-value>
   ```

   Oder verwende GitHub Secrets für dynamische Werte.

## Workflow

Der Workflow `.github/workflows/deploy-azure-functions.yml` wird automatisch ausgelöst wenn:

- Code in `azure-functions/**` geändert wird
- Push zum `main` Branch
- Manuell über "Run workflow" Button

## Deployment-Prozess

1. **Checkout**: Code wird aus GitHub gecheckt
2. **Build**: .NET Function App wird gebaut (`dotnet build`)
3. **Publish**: Code wird für Deployment vorbereitet (`dotnet publish`)
4. **Optimize**: Unnötige Dateien werden entfernt (CodeAnalysis DLLs, etc.)
5. **Package**: ZIP-Archiv wird erstellt
6. **Deploy**: ZIP wird zu Azure Function App deployed

## Vorteile

- ✅ **Automatisches Deployment** bei jedem Push
- ✅ **Keine Timeout-Probleme** - GitHub Actions läuft in der Cloud
- ✅ **Terraform bleibt schlank** - nur Infrastruktur, kein Code-Deployment
- ✅ **CI/CD Integration** - Teil des normalen Git-Workflows
- ✅ **Build-Logs** in GitHub sichtbar

## Troubleshooting

### Deployment schlägt fehl

1. Prüfe GitHub Actions Logs: Repository → Actions → Failed Workflow
2. Prüfe ob `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` Secret korrekt gesetzt ist
3. Prüfe ob Function App Name im Workflow korrekt ist

### Function App Name ändern

Wenn die Function App neu erstellt wird (z.B. durch Terraform), muss der Name im Workflow aktualisiert werden:

```bash
cd terraform
terraform output function_app_name
```

Dann `.github/workflows/deploy-azure-functions.yml` aktualisieren.


# GitHub Actions Deployment für Azure Functions

## Übersicht

Die Azure Functions werden automatisch über GitHub Actions deployed. Terraform erstellt nur die Infrastruktur (Function App, Storage Account, etc.), während GitHub Actions den Code automatisch bei jedem Push zum `main` Branch deployed.

**Wichtig**: Wir verwenden die **"Run from Package"** Methode, die von Microsoft empfohlen wird.

## Schnellstart

### Automatisches Setup (Empfohlen)

```powershell
# Windows
.\setup-github-secrets.ps1
```

```bash
# Linux/Mac
./setup-github-secrets.sh
```

Siehe auch: [SETUP_GITHUB_SECRETS.md](./SETUP_GITHUB_SECRETS.md)

### Manuelles Setup

1. **Service Principal erstellen**:
   ```bash
   az ad sp create-for-rbac \
     --name "github-actions-functions" \
     --role contributor \
     --scopes /subscriptions/{subscription-id}/resourceGroups/rg-interface-configuration \
     --sdk-auth
   ```

2. **GitHub Secrets setzen** (https://github.com/mariomuja/interface-configuration/settings/secrets/actions):
   - `AZURE_CREDENTIALS`: JSON-Output aus Schritt 1
   - `AZURE_RESOURCE_GROUP`: `rg-interface-configuration`
   - `AZURE_FUNCTIONAPP_NAME`: `terraform output function_app_name`

## Workflow

Der Workflow `.github/workflows/deploy-functions.yml` wird automatisch ausgelöst wenn:

- Code in `azure-functions/**` geändert wird
- Push zum `main` Branch
- Manuell über "Run workflow" Button

## Deployment-Prozess

1. **Checkout**: Code wird aus GitHub gecheckt
2. **Setup .NET**: .NET 8.0 SDK wird installiert
3. **Restore**: NuGet-Pakete werden wiederhergestellt
4. **Build**: .NET Function App wird gebaut (`dotnet build`)
5. **Publish**: Code wird für Deployment vorbereitet (`dotnet publish`)
6. **Package**: ZIP-Archiv wird erstellt
7. **Azure Login**: Authentifizierung mit Service Principal
8. **Deploy**: ZIP wird via `az functionapp deployment source config-zip` deployed (Run from Package)
9. **Verify**: Deployment wird verifiziert

## Vorteile

- ✅ **Automatisches Deployment** bei jedem Push
- ✅ **Run from Package** - Microsoft's empfohlene Methode
- ✅ **Keine Timeout-Probleme** - GitHub Actions läuft in der Cloud
- ✅ **Terraform bleibt schlank** - nur Infrastruktur, kein Code-Deployment
- ✅ **CI/CD Integration** - Teil des normalen Git-Workflows
- ✅ **Build-Logs** in GitHub sichtbar
- ✅ **Schnellere Deployments** - Code läuft direkt aus ZIP

## Run from Package Modus

Die Function App ist mit `WEBSITE_RUN_FROM_PACKAGE=1` konfiguriert:
- Funktionen laufen direkt aus dem ZIP-Archiv
- Keine Entpackung auf dem Dateisystem
- Bessere Performance und schnellere Deployments

## Troubleshooting

### Deployment schlägt fehl

1. Prüfe GitHub Actions Logs: Repository → Actions → Failed Workflow
2. Prüfe ob alle drei Secrets korrekt gesetzt sind:
   - `AZURE_CREDENTIALS`
   - `AZURE_RESOURCE_GROUP`
   - `AZURE_FUNCTIONAPP_NAME`
3. Prüfe ob Service Principal Berechtigungen korrekt sind
4. Prüfe ob Function App existiert: `terraform output function_app_name`

### Function App Name ändern

Wenn die Function App neu erstellt wird (z.B. durch Terraform), aktualisiere das Secret:

```bash
cd terraform
terraform output function_app_name
```

Dann GitHub Secret `AZURE_FUNCTIONAPP_NAME` aktualisieren oder das Setup-Script erneut ausführen.



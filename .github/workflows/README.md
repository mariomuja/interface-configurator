# GitHub Actions Workflow - Azure Functions Deployment

## Übersicht

Dieser Workflow deployed automatisch Azure Functions mit der **"Run from Package"** Methode, die von Microsoft empfohlen wird.

## Workflow: `deploy-functions.yml`

### Trigger

- **Automatisch**: Bei Push zum `main` Branch, wenn Dateien in `azure-functions/**` geändert werden
- **Manuell**: Über den "Run workflow" Button in GitHub Actions

### Deployment-Prozess

1. **Checkout**: Code wird aus dem Repository gecheckt
2. **Setup .NET**: .NET 8.0 SDK wird installiert
3. **Restore**: NuGet-Pakete werden wiederhergestellt
4. **Build**: Function App wird im Release-Modus gebaut
5. **Publish**: Code wird für Deployment vorbereitet
6. **Create Package**: ZIP-Archiv wird erstellt
7. **Azure Login**: Authentifizierung mit Service Principal
8. **Deploy**: ZIP wird via `az functionapp deployment source config-zip` deployed
9. **Verify**: Deployment wird verifiziert

### Erforderliche GitHub Secrets

Folgende Secrets müssen in GitHub konfiguriert werden:

#### 1. `AZURE_CREDENTIALS`
Service Principal Credentials im JSON-Format.

**Erstellen:**
```bash
az ad sp create-for-rbac \
  --name "github-actions-functions" \
  --role contributor \
  --scopes /subscriptions/{subscription-id}/resourceGroups/{resource-group} \
  --sdk-auth
```

**Output kopieren** und als Secret `AZURE_CREDENTIALS` in GitHub einfügen.

#### 2. `AZURE_RESOURCE_GROUP`
Name der Resource Group (z.B. `rg-interface-configuration`)

**Abrufen:**
```bash
cd terraform
terraform output resource_group_name
```

#### 3. `AZURE_FUNCTIONAPP_NAME`
Name der Function App

**Abrufen:**
```bash
cd terraform
terraform output function_app_name
```

### GitHub Secrets konfigurieren

1. Gehe zu: **Repository → Settings → Secrets and variables → Actions**
2. Klicke auf **"New repository secret"**
3. Füge die drei Secrets hinzu:
   - `AZURE_CREDENTIALS`
   - `AZURE_RESOURCE_GROUP`
   - `AZURE_FUNCTIONAPP_NAME`

### Run from Package Modus

Die Function App ist mit `WEBSITE_RUN_FROM_PACKAGE=1` konfiguriert. Dies bedeutet:

- ✅ Funktionen laufen direkt aus dem ZIP-Archiv
- ✅ Keine Entpackung auf dem Dateisystem
- ✅ Schnellere Deployments
- ✅ Bessere Performance
- ✅ Empfohlene Methode von Microsoft

### Troubleshooting

#### Deployment schlägt fehl

1. **Prüfe GitHub Secrets**: Alle drei Secrets müssen korrekt gesetzt sein
2. **Prüfe Service Principal Berechtigungen**: Muss Contributor-Rolle auf Resource Group haben
3. **Prüfe Function App Status**: Im Azure Portal prüfen ob Function App läuft
4. **Prüfe Workflow Logs**: In GitHub Actions → Workflow run → Logs

#### Function App Name ändern

Wenn sich der Function App Name ändert (z.B. nach Terraform Apply):

1. Hole neuen Namen: `terraform output function_app_name`
2. Aktualisiere GitHub Secret `AZURE_FUNCTIONAPP_NAME`

### Terraform Integration

Die Function App wird von Terraform erstellt und verwaltet. Der Workflow deployed nur den Code:

- **Terraform**: Erstellt/verwaltet Infrastruktur (Function App, Storage, etc.)
- **GitHub Actions**: Deployed den Code in die bestehende Function App

### Weitere Informationen

- [Azure Functions Run from Package](https://learn.microsoft.com/en-us/azure/azure-functions/run-functions-from-deployment-package)
- [Azure Functions Deployment](https://learn.microsoft.com/en-us/azure/azure-functions/functions-deployment-technologies)











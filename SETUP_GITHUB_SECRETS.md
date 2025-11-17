# GitHub Secrets Setup - Automatisch

Dieses Script richtet automatisch alle benötigten GitHub Secrets für das Azure Functions Deployment ein.

## Voraussetzungen

1. **Azure CLI** installiert und eingeloggt:
   ```bash
   az login
   ```

2. **GitHub CLI** installiert und eingeloggt:
   ```bash
   gh auth login
   ```

3. **Terraform** muss bereits ausgeführt worden sein (`terraform apply`), damit die Function App existiert

## Windows (PowerShell)

```powershell
.\setup-github-secrets.ps1
```

## Linux/Mac (Bash)

```bash
chmod +x setup-github-secrets.sh
./setup-github-secrets.sh
```

## Was das Script macht

1. ✅ Prüft ob Azure CLI und GitHub CLI installiert sind
2. ✅ Holt die Azure Subscription ID
3. ✅ Liest den Function App Namen aus Terraform Outputs
4. ✅ Erstellt einen Service Principal mit Contributor-Rolle
5. ✅ Setzt die folgenden GitHub Secrets:
   - `AZURE_CREDENTIALS` - Service Principal Credentials (JSON)
   - `AZURE_RESOURCE_GROUP` - Resource Group Name
   - `AZURE_FUNCTIONAPP_NAME` - Function App Name

## Manuelle Alternative

Falls das Script nicht funktioniert, kannst du die Secrets auch manuell setzen:

### 1. Service Principal erstellen

```bash
az ad sp create-for-rbac \
  --name "github-actions-functions" \
  --role contributor \
  --scopes /subscriptions/{subscription-id}/resourceGroups/rg-infrastructure-as-code \
  --sdk-auth
```

Kopiere den JSON-Output.

### 2. GitHub Secrets setzen

Gehe zu: https://github.com/mariomuja/infrastructure-as-code/settings/secrets/actions

Setze folgende Secrets:

- **AZURE_CREDENTIALS**: Der komplette JSON-Output aus Schritt 1
- **AZURE_RESOURCE_GROUP**: `rg-infrastructure-as-code`
- **AZURE_FUNCTIONAPP_NAME**: Hole mit `terraform output function_app_name`

## Nach dem Setup

Der GitHub Actions Workflow wird automatisch ausgelöst wenn:
- Code in `azure-functions/**` geändert wird
- Du manuell den Workflow startest: https://github.com/mariomuja/infrastructure-as-code/actions

## Troubleshooting

### "Function App Name nicht gefunden"
- Führe zuerst `terraform apply` aus
- Oder gib den Namen manuell ein, wenn das Script danach fragt

### "GitHub CLI nicht eingeloggt"
```bash
gh auth login
```

### "Nicht bei Azure eingeloggt"
```bash
az login
```






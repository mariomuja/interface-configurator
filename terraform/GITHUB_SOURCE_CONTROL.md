# GitHub Source Control für Azure Functions

## Übersicht

Die Function App kann so konfiguriert werden, dass sie Code direkt aus einem GitHub-Repository zieht und automatisch deployed. Dies ersetzt die manuelle ZIP-Deployment-Methode.

## Konfiguration

### 1. Terraform Variablen setzen

In `terraform.tfvars`:

```hcl
enable_function_app = true
github_repo_url     = "https://github.com/mariomuja/infrastructure-as-code"
github_branch       = "main"
github_repo_path    = "azure-functions/ProcessCsvBlobTrigger"
```

### 2. Terraform Apply

```bash
cd terraform
terraform plan
terraform apply
```

### 3. GitHub Authentifizierung

Nach dem `terraform apply` musst du die GitHub-Authentifizierung im Azure Portal konfigurieren:

1. Gehe zu Azure Portal → Function App → Deployment Center
2. Klicke auf "Authorize" oder "Edit" bei GitHub
3. Folge den Anweisungen zur GitHub-Authentifizierung
4. Wähle das Repository und den Branch

**Alternativ**: Die Authentifizierung kann auch über Azure CLI erfolgen:

```bash
az functionapp deployment source config \
  --name func-appe1mz5h \
  --resource-group rg-infrastructure-as-code \
  --repo-url https://github.com/mariomuja/infrastructure-as-code \
  --branch main \
  --manual-integration
```

## Funktionsweise

- **Automatisches Deployment**: Bei jedem Push zum konfigurierten Branch wird automatisch deployed
- **Build**: Azure baut die .NET Function App automatisch
- **Keine ZIP-Dateien**: Code wird direkt aus GitHub gezogen

## Vorteile

- ✅ **Keine Timeout-Probleme** - Deployment läuft in Azure
- ✅ **Automatisches Deployment** bei jedem Push
- ✅ **Keine manuellen ZIP-Uploads** nötig
- ✅ **Einfache Konfiguration** über Terraform

## Hinweise

- Für .NET Isolated Functions baut Azure den Code automatisch
- Stelle sicher, dass das Repository öffentlich ist oder die GitHub-Authentifizierung korrekt konfiguriert ist
- Der `github_repo_path` muss auf das Verzeichnis zeigen, das die `.csproj` Datei enthält

## Alternative: GitHub Actions

Wenn `github_repo_url` leer gelassen wird, wird stattdessen GitHub Actions für das Deployment verwendet (siehe `.github/workflows/deploy-azure-functions.yml`).


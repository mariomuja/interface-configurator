# Deployment Checklist - Azure Functions

## Vorbereitung

- [ ] Azure CLI installiert und eingeloggt (`az login`)
- [ ] Terraform installiert (>= 1.0)
- [ ] GitHub CLI installiert (optional, für automatisches Setup)
- [ ] Terraform Variablen konfiguriert (`terraform.tfvars`)

## Schritt 1: Infrastructure deployen

```bash
cd terraform
terraform init
terraform plan
terraform apply
```

- [ ] Terraform Apply erfolgreich
- [ ] Function App wurde erstellt
- [ ] Notiere Function App Name: `terraform output function_app_name`
- [ ] Notiere Resource Group: `terraform output resource_group_name`

## Schritt 2: GitHub Secrets konfigurieren

### Option A: Automatisch (Empfohlen)

```powershell
# Windows
.\setup-github-secrets.ps1
```

```bash
# Linux/Mac
./setup-github-secrets.sh
```

- [ ] Script erfolgreich ausgeführt
- [ ] Alle drei Secrets wurden gesetzt

### Option B: Manuell

1. Service Principal erstellen:
   ```bash
   az ad sp create-for-rbac \
     --name "github-actions-functions" \
     --role contributor \
     --scopes /subscriptions/{subscription-id}/resourceGroups/rg-infrastructure-as-code \
     --sdk-auth
   ```

2. GitHub Secrets setzen: https://github.com/mariomuja/infrastructure-as-code/settings/secrets/actions
   - [ ] `AZURE_CREDENTIALS` (JSON aus Schritt 1)
   - [ ] `AZURE_RESOURCE_GROUP` (`rg-infrastructure-as-code`)
   - [ ] `AZURE_FUNCTIONAPP_NAME` (aus `terraform output function_app_name`)

## Schritt 3: Workflow testen

### Option A: Automatisch (bei Push)

- [ ] Änderung in `azure-functions/**` gemacht
- [ ] Zu `main` Branch gepusht
- [ ] Workflow startet automatisch

### Option B: Manuell

- [ ] Gehe zu: https://github.com/mariomuja/infrastructure-as-code/actions
- [ ] Klicke auf "Deploy Azure Functions"
- [ ] Klicke auf "Run workflow"

## Schritt 4: Deployment verifizieren

- [ ] Workflow erfolgreich abgeschlossen
- [ ] Function App zeigt deployed Code
- [ ] Funktionen sind sichtbar im Azure Portal
- [ ] Teste eine Function (z.B. Blob Trigger)

## Troubleshooting

### Workflow schlägt fehl

1. Prüfe GitHub Actions Logs
2. Prüfe ob alle Secrets gesetzt sind
3. Prüfe Service Principal Berechtigungen
4. Prüfe ob Function App existiert

### Function App zeigt keine Funktionen

1. Prüfe `WEBSITE_RUN_FROM_PACKAGE=1` ist gesetzt
2. Prüfe Function App Logs im Azure Portal
3. Prüfe ob ZIP korrekt deployed wurde

## Wichtige Links

- GitHub Secrets: https://github.com/mariomuja/infrastructure-as-code/settings/secrets/actions
- GitHub Actions: https://github.com/mariomuja/infrastructure-as-code/actions
- Azure Portal: https://portal.azure.com

## Nächste Schritte

Nach erfolgreichem Deployment:
- [ ] Datenbank initialisieren (falls noch nicht geschehen)
- [ ] Storage Container konfigurieren
- [ ] Event Grid Subscriptions einrichten (falls benötigt)






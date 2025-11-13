# Terraform Memory - Wichtige Regeln

## Priorität: Terraform hat IMMER Priorität über CLI

**WICHTIG**: Alle Azure-Ressourcen müssen über Terraform verwaltet werden. CLI-Befehle sollten nur für:
- Einmalige Initialisierung
- Debugging
- Manuelle Tests

verwendet werden.

## Function App Deployment

Die Azure Functions werden automatisch über Terraform deployed:

1. **ZIP-Archiv wird erstellt**: `data.archive_file.function_app` erstellt ein ZIP aus dem publish-Ordner
2. **Deployment über Azure CLI**: `null_resource.deploy_function_app` verwendet `az functionapp deployment source config-zip`
3. **Trigger**: Deployment wird ausgelöst wenn:
   - Function App erstellt/geändert wird
   - ZIP-Datei sich ändert (Hash-basiert)

## Ressourcen-Management

- **NICHT** manuell über Azure Portal ändern
- **NICHT** manuell über Azure CLI ändern (außer für Debugging)
- **IMMER** Änderungen in Terraform Config machen
- **IMMER** `terraform plan` vor `terraform apply` ausführen

## Datenbank-Name

Die Datenbank heißt standardmäßig `app_database` (aus `variables.tf`).
Falls die App `csvtransportdb` erwartet, muss `sql_database_name` in `terraform.tfvars` angepasst werden.



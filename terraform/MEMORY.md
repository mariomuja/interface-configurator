# Terraform Memory - Wichtige Regeln

## Priorität: Terraform hat IMMER Priorität über CLI

**WICHTIG**: Alle Azure-Ressourcen müssen über Terraform verwaltet werden. CLI-Befehle sollten nur für:
- Einmalige Initialisierung
- Debugging
- Manuelle Tests

verwendet werden.

## Naming Conventions

**IMPORTANT:** Always use descriptive, self-documenting names for Azure resources that explain what the resource does.

### Problem
Generic names with random suffixes (e.g., `func-apprigklebtsay2o`) make it difficult to understand resource purpose and manage deployments.

### Solution
Use descriptive naming patterns: `{project}-{env}-{resource-type}-{purpose}`

**Examples:**
- Function Apps: `infra-prod-func-csv-processor` (not `func-apprigklebtsay2o`)
- Storage Accounts: `infraprodstcsvblobs` (not `stapprigklebtsay2o`)
- SQL Servers: `infra-prod-sql-main` (not `sql-infrastructurerigklebtsay2o`)

See `NAMING_CONVENTIONS.md` in the project root for detailed naming guidelines.

## Coding Standards

**CRITICAL**: Siehe [CODING_STANDARDS.md](../CODING_STANDARDS.md) für wichtige Regeln:
- ⚠️ **NIEMALS** leere catch-Blöcke verwenden (`catch { }`)
- Alle Exceptions müssen geloggt werden
- Logging-Fehler müssen ebenfalls geloggt werden

## Function App Deployment

Die Azure Functions werden über **GitHub Actions** deployed:

1. **Terraform** erstellt nur die Infrastruktur (Function App, Storage, etc.)
2. **GitHub Actions** deployed den Code automatisch bei jedem Push
3. **Run from Package**: `WEBSITE_RUN_FROM_PACKAGE=1` aktiviert die empfohlene Deployment-Methode
4. **Workflow**: `.github/workflows/deploy-functions.yml` baut und deployed den Code

Siehe auch: [GITHUB_ACTIONS_DEPLOYMENT.md](../GITHUB_ACTIONS_DEPLOYMENT.md)

## Ressourcen-Management

- **NICHT** manuell über Azure Portal ändern
- **NICHT** manuell über Azure CLI ändern (außer für Debugging)
- **IMMER** Änderungen in Terraform Config machen
- **IMMER** `terraform plan` vor `terraform apply` ausführen

## Datenbank-Name

Die Datenbank heißt standardmäßig `app_database` (aus `variables.tf`).
Falls die App `csvtransportdb` erwartet, muss `sql_database_name` in `terraform.tfvars` angepasst werden.



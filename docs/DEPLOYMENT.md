# Deployment Guide

## Voraussetzungen

1. Azure Account mit aktivem Subscription
2. Terraform installiert (>= 1.0)
3. Azure CLI installiert und eingeloggt: `az login`
4. Vercel CLI installiert: `npm i -g vercel`

## Schritt 1: Azure Infrastructure deployen

1. Wechsle in das Terraform-Verzeichnis:
   ```bash
   cd terraform
   ```

2. Kopiere `terraform.tfvars.example` zu `terraform.tfvars` und fülle die Werte aus:
   ```bash
   cp terraform.tfvars.example terraform.tfvars
   ```

3. Initialisiere Terraform:
   ```bash
   terraform init
   ```

4. Plane die Deployment:
   ```bash
   terraform plan
   ```

5. Deploye die Infrastructure:
   ```bash
   terraform apply
   ```

6. Notiere die Outputs (speichere sie für Schritt 2):
   - `storage_account_connection_string`
   - `sql_server_fqdn`
   - `sql_database_name`

## Schritt 2: Datenbank initialisieren

1. Führe das SQL-Script aus:
   ```bash
   sqlcmd -S <sql_server_fqdn> -d <sql_database_name> -U <sql_admin_login> -P <sql_admin_password> -i init-database.sql -N
   ```

## Schritt 3: Azure Functions deployen

1. Wechsle in das Azure Functions Verzeichnis:
   ```bash
   cd ../azure-functions
   ```

2. Installiere Dependencies:
   ```bash
   npm install
   ```

3. Deploye die Functions (mit Azure Functions Core Tools):
   ```bash
   func azure functionapp publish <function_app_name>
   ```

## Schritt 4: Vercel Deployment

1. Wechsle zurück zum Root-Verzeichnis:
   ```bash
   cd ..
   ```

2. Installiere API Dependencies:
   ```bash
   cd api
   npm install
   cd ..
   ```

3. Baue das Frontend:
   ```bash
   cd frontend
   npm install
   npm run build:prod
   cd ..
   ```

4. Deploye nach Vercel:
   ```bash
   vercel deploy --prod
   ```

5. Setze die Umgebungsvariablen in Vercel:
   - Gehe zu Vercel Dashboard → Project Settings → Environment Variables
   - Füge alle Variablen aus `.env.example` hinzu

## Schritt 5: Event Grid Subscription konfigurieren

Die Event Grid Subscription sollte automatisch durch Terraform erstellt werden. Falls nicht:

1. Gehe zum Azure Portal
2. Navigiere zu Event Grid → System Topics
3. Wähle dein Topic aus
4. Überprüfe, dass die Subscription auf die Azure Function zeigt

## Verifikation

1. Öffne die Vercel-URL
2. Klicke auf "Transport starten"
3. Überprüfe die Logs in der SQL-Tabelle `ProcessLogs`
4. Überprüfe, dass Daten in der `TransportData` Tabelle erscheinen




# Vercel Environment Variables Configuration

Diese Datei dokumentiert alle Umgebungsvariablen, die in Vercel für die Interface Configurator App konfiguriert werden müssen.

## Azure SQL Database Konfiguration

Die folgenden Umgebungsvariablen sind **erforderlich** für die Datenbankverbindung:

### Erforderliche Variablen

- `AZURE_SQL_SERVER` - Der vollständige SQL Server Name (z.B. `sql-infrastructurexxxxx.database.windows.net`)
  - **Hinweis**: Verwende den vollständigen FQDN, nicht nur den Servernamen
  - Finde den Wert in Azure Portal → SQL Server → Übersicht → Servername
  
- `AZURE_SQL_DATABASE` - Der Name der Datenbank (z.B. `app_database`)
  - Standardwert aus Terraform: `app_database`
  
- `AZURE_SQL_USER` - Der SQL Server Administrator Benutzername
  - Standardwert aus Terraform: `sql_admin` (oder der Wert aus `terraform.tfvars`)
  
- `AZURE_SQL_PASSWORD` - Das SQL Server Administrator Passwort
  - **Wichtig**: Verwende das Passwort, das in `terraform.tfvars` konfiguriert wurde

### Beispiel-Werte

```
AZURE_SQL_SERVER=sql-infrastructureabc123.database.windows.net
AZURE_SQL_DATABASE=app_database
AZURE_SQL_USER=sql_admin
AZURE_SQL_PASSWORD=DeinSicheresPasswort123!
```

## Azure Storage Konfiguration

Die folgenden Umgebungsvariablen sind **erforderlich** für Blob Storage:

### Option 1: Connection String (empfohlen)

- `AZURE_STORAGE_CONNECTION_STRING` - Die vollständige Connection String für Azure Storage
  - Format: `DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net`
  - Finde den Wert in Azure Portal → Storage Account → Access Keys → Connection string

### Option 2: Account Name und Key (Alternative)

- `AZURE_STORAGE_ACCOUNT_NAME` - Der Name des Storage Accounts
- `AZURE_STORAGE_ACCOUNT_KEY` - Der Access Key des Storage Accounts

### Optional

- `AZURE_STORAGE_CONTAINER` - Der Name des Containers für CSV-Dateien
  - Standardwert: `csv-uploads`
  - Wird automatisch erstellt, falls nicht vorhanden

## So setzt du die Variablen in Vercel

### Über das Vercel Dashboard

1. Gehe zu [vercel.com](https://vercel.com) und logge dich ein
2. Wähle dein Projekt `infrastructure-as-code`
3. Gehe zu **Settings** → **Environment Variables**
4. Füge jede Variable hinzu:
   - **Key**: z.B. `AZURE_SQL_SERVER`
   - **Value**: Der entsprechende Wert
   - **Environment**: Wähle `Production`, `Preview` und/oder `Development`
5. Klicke auf **Save**
6. **Wichtig**: Nach dem Hinzufügen neuer Variablen muss ein neues Deployment ausgelöst werden

### Über Vercel CLI

```bash
vercel env add AZURE_SQL_SERVER
vercel env add AZURE_SQL_DATABASE
vercel env add AZURE_SQL_USER
vercel env add AZURE_SQL_PASSWORD
vercel env add AZURE_STORAGE_CONNECTION_STRING
```

## Überprüfung der Konfiguration

Nach dem Setzen der Variablen:

1. **Trigger ein neues Deployment**:
   ```bash
   git commit --allow-empty -m "Trigger deployment"
   git push origin main
   ```

2. **Teste die API-Endpunkte**:
   - Öffne die Browser-Konsole (F12)
   - Klicke auf "Transport starten"
   - Prüfe die Netzwerk-Tab für Fehler
   - Die API sollte jetzt detaillierte Fehlermeldungen zurückgeben, falls etwas fehlt

3. **Prüfe die Vercel Logs**:
   - Gehe zu Vercel Dashboard → Deployments → Wähle das neueste Deployment → Logs
   - Suche nach Fehlermeldungen bezüglich fehlender Umgebungsvariablen

## Häufige Probleme

### Problem: "Database configuration incomplete"

**Lösung**: Eine oder mehrere SQL-Umgebungsvariablen fehlen. Überprüfe alle vier Variablen in Vercel.

### Problem: "Cannot connect to Azure SQL Server"

**Mögliche Ursachen**:
1. Firewall-Regel blockiert Vercel IPs
   - **Lösung**: Füge eine Firewall-Regel hinzu, die Azure Services erlaubt (0.0.0.0 - 0.0.0.0)
2. Falscher Servername
   - **Lösung**: Verwende den vollständigen FQDN (z.B. `sql-infrastructurexxxxx.database.windows.net`)

### Problem: "Authentication failed"

**Lösung**: Überprüfe Benutzername und Passwort. Stelle sicher, dass sie mit den Terraform-Werten übereinstimmen.

### Problem: "SQL query failed. Check if tables exist"

**Lösung**: Die Datenbank-Tabellen wurden noch nicht initialisiert. Führe das Datenbank-Initialisierungs-Script aus (siehe DEPLOYMENT.md).

## Terraform Outputs verwenden

Nach dem Ausführen von `terraform apply` kannst du die Outputs verwenden:

```bash
cd terraform
terraform output
```

Die Outputs zeigen:
- `sql_server_fqdn` → Verwende für `AZURE_SQL_SERVER`
- `sql_database_name` → Verwende für `AZURE_SQL_DATABASE`
- `storage_account_connection_string` → Verwende für `AZURE_STORAGE_CONNECTION_STRING`

## Sicherheit

⚠️ **Wichtig**: 
- Niemals Umgebungsvariablen mit echten Werten in Git committen
- Verwende starke Passwörter
- Rotiere regelmäßig die Storage Account Keys
- Überprüfe regelmäßig die Vercel Environment Variables auf veraltete Werte




# Datenbank-Initialisierung - Jetzt ausführen

## Problem
Die SQL-Datenbank enthält noch keine Tabellen (`TransportData` und `ProcessLogs`).

## Lösung

### Option 1: PowerShell-Script (Empfohlen)

Führen Sie das PowerShell-Script aus:

```powershell
cd C:\Users\mario\interface-configuration
.\terraform\init-database.ps1 -SqlPassword "IhrSQLPasswort"
```

**Hinweis:** Ersetzen Sie `IhrSQLPasswort` mit dem tatsächlichen SQL-Admin-Passwort aus `terraform.tfvars`.

### Option 2: Azure Portal Query Editor

1. Öffnen Sie das [Azure Portal](https://portal.azure.com)
2. Navigieren Sie zu **SQL-Datenbanken** → `app_database`
3. Klicken Sie auf **Query editor (preview)** im linken Menü
4. Melden Sie sich mit SQL-Authentifizierung an:
   - Login: `sqladmin`
   - Password: (aus `terraform.tfvars`)
5. Öffnen Sie die Datei `terraform/init-database.sql`
6. Kopieren Sie den gesamten Inhalt
7. Fügen Sie ihn in den Query Editor ein
8. Klicken Sie auf **Run**

### Option 3: VS Code mit MSSQL Extension

1. Öffnen Sie VS Code
2. Verbinden Sie sich mit der SQL-Datenbank (siehe `VS_CODE_MSSQL_CONNECTION.md`)
3. Öffnen Sie `terraform/init-database.sql`
4. Führen Sie das gesamte Script aus (F5 oder Rechtsklick → Execute Query)

### Option 4: SQL Server Management Studio (SSMS)

1. Öffnen Sie SSMS
2. Verbinden Sie sich mit:
   - Server: `sql-infrastructuree1mz5h.database.windows.net`
   - Authentication: SQL Server Authentication
   - Login: `sqladmin`
   - Password: (aus `terraform.tfvars`)
3. Öffnen Sie `terraform/init-database.sql`
4. Führen Sie das Script aus (F5)

## Nach der Initialisierung

Prüfen Sie, ob die Tabellen erstellt wurden:

```sql
SELECT TABLE_NAME 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_TYPE = 'BASE TABLE' 
AND TABLE_SCHEMA = 'dbo' 
ORDER BY TABLE_NAME;
```

Sie sollten sehen:
- `ProcessLogs`
- `TransportData`

## Nächste Schritte

Nach der erfolgreichen Initialisierung:
1. Die Azure Function App sollte die Tabellen beim ersten Blob-Trigger automatisch verwenden können
2. Die Vercel API-Endpunkte (`/api/sql-data`, `/api/process-logs`) sollten funktionieren
3. Sie können den Transport-Prozess starten


# Datenbank Initialisierung

## Prüfen, ob Tabellen bereits existieren

### Option 1: Azure Portal Query Editor

1. Gehe zu [Azure Portal](https://portal.azure.com)
2. Navigiere zu **SQL Server** → `sql-csvtransportud3e1cem`
3. Klicke auf **Query editor (preview)**
4. Logge dich ein mit:
   - **Login**: `sqladmin`
   - **Password**: `InfrastructureAsCode2024!Secure`
5. Wähle die Datenbank `csvtransportdb`
6. Führe diese Abfrage aus:

```sql
SELECT 
    CASE WHEN EXISTS (SELECT * FROM sys.tables WHERE name = 'TransportData')
        THEN 'TransportData EXISTS'
        ELSE 'TransportData DOES NOT EXIST'
    END AS TransportDataStatus,
    CASE WHEN EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessLogs')
        THEN 'ProcessLogs EXISTS'
        ELSE 'ProcessLogs DOES NOT EXIST'
    END AS ProcessLogsStatus;
```

### Option 2: sqlcmd (Command Line)

```bash
sqlcmd -S sql-csvtransportud3e1cem.database.windows.net `
       -d csvtransportdb `
       -U sqladmin `
       -P "InfrastructureAsCode2024!Secure" `
       -i check-database-tables.sql
```

## Initialisierung ausführen

**Falls die Tabellen NICHT existieren**, führe das Initialisierungs-Script aus:

### Option 1: Azure Portal Query Editor

1. Öffne Query Editor (siehe oben)
2. Kopiere den Inhalt von `init-database.sql`
3. Führe das Script aus

### Option 2: sqlcmd

```bash
sqlcmd -S sql-csvtransportud3e1cem.database.windows.net `
       -d csvtransportdb `
       -U sqladmin `
       -P "InfrastructureAsCode2024!Secure" `
       -i init-database.sql
```

### Option 3: Azure CLI

```bash
az sql db execute-query \
  --server sql-csvtransportud3e1cem \
  --database csvtransportdb \
  --admin-user sqladmin \
  --admin-password "InfrastructureAsCode2024!Secure" \
  --file-path init-database.sql
```

## Was wird erstellt?

Das Script erstellt zwei Tabellen:

1. **TransportData** - Speichert die CSV-Daten
   - Spalten: Id, Name, Email, Age, City, Salary, CreatedAt
   - Index auf CreatedAt

2. **ProcessLogs** - Speichert Prozess-Logs
   - Spalten: Id, Timestamp, Level, Message, Details
   - Indizes auf Timestamp und Level

## Hinweis

Die Azure Function verwendet `EnsureDatabaseCreatedAsync()`, was die Tabellen automatisch erstellen **könnte**, aber es ist sicherer, sie manuell zu initialisieren, bevor die Vercel API-Endpunkte verwendet werden.

## Nach der Initialisierung

1. Teste die API-Endpunkte:
   - `/api/sql-data` sollte eine leere Liste zurückgeben (keine Fehler)
   - `/api/process-logs` sollte eine leere Liste zurückgeben (keine Fehler)

2. Teste den Transport:
   - Klicke auf "Transport starten" in der App
   - Die CSV-Datei sollte in Blob Storage hochgeladen werden
   - Die Azure Function sollte die Daten verarbeiten und in die Datenbank schreiben




# SQL Database Management Tools - Aktuelle Alternativen

Da Azure Data Studio deprecated ist, hier sind aktuelle Alternativen für die Verwaltung der Azure SQL-Datenbank:

## Option 1: VS Code MSSQL Extension (Empfohlen)

**Status**: Bereits installiert, benötigt VS Code Neustart

**Verbindungsdaten**:
- Server: `sql-csvtransportud3e1cem.database.windows.net`
- Database: `csvtransportdb`
- Username: `sqladmin`
- Password: `InfrastructureAsCode2024!Secure`
- Encrypt: `true`

**Nach VS Code Neustart**:
1. `Ctrl+Shift+P` → `mssql.connect`
2. Oder erstelle eine `.sql` Datei und klicke auf "Connect"

## Option 2: SQL Server Management Studio (SSMS)

**Download**: https://aka.ms/ssms

**Vorteile**:
- Offizielles Microsoft-Tool
- Vollständige SQL Server-Verwaltung
- Sehr stabil und zuverlässig

**Verbindung**:
1. Öffne SSMS
2. Server name: `sql-csvtransportud3e1cem.database.windows.net`
3. Authentication: SQL Server Authentication
4. Login: `sqladmin`
5. Password: `InfrastructureAsCode2024!Secure`
6. Klicke auf "Connect"

## Option 3: DBeaver (Open Source)

**Download**: https://dbeaver.io

**Vorteile**:
- Kostenlos und Open Source
- Unterstützt viele Datenbanktypen
- Gute UI und Features

**Verbindung**:
1. Öffne DBeaver
2. New Database Connection → SQL Server
3. Server Host: `sql-csvtransportud3e1cem.database.windows.net`
4. Port: `1433`
5. Database: `csvtransportdb`
6. Username: `sqladmin`
7. Password: `InfrastructureAsCode2024!Secure`
8. SSL: Enable
9. Klicke auf "Test Connection" → "Finish"

## Option 4: Azure Portal Query Editor

**Vorteile**:
- Keine Installation nötig
- Direkt im Browser verfügbar
- Einfach zu verwenden

**Verwendung**:
1. Gehe zu [Azure Portal](https://portal.azure.com)
2. Navigiere zu SQL Server → `sql-csvtransportud3e1cem`
3. Klicke auf "Query editor (preview)"
4. Login: `sqladmin` / Password: `InfrastructureAsCode2024!Secure`
5. Wähle Datenbank: `csvtransportdb`
6. Führe SQL-Queries aus

## Option 5: sqlcmd (Command Line)

**Bereits verfügbar** auf deinem System!

**Beispiele**:
```powershell
# Einfache Query
sqlcmd -S sql-csvtransportud3e1cem.database.windows.net `
       -d csvtransportdb `
       -U sqladmin `
       -P "InfrastructureAsCode2024!Secure" `
       -Q "SELECT * FROM TransportData"

# Script ausführen
sqlcmd -S sql-csvtransportud3e1cem.database.windows.net `
       -d csvtransportdb `
       -U sqladmin `
       -P "InfrastructureAsCode2024!Secure" `
       -i script.sql
```

## Empfehlung

**Für VS Code Nutzer**: VS Code MSSQL Extension (nach Neustart)
**Für umfangreiche Verwaltung**: SSMS
**Für einfache Queries**: Azure Portal Query Editor
**Für Command Line**: sqlcmd

## Verbindungsdetails (für alle Tools)

```
Server: sql-csvtransportud3e1cem.database.windows.net
Database: csvtransportdb
Username: sqladmin
Password: InfrastructureAsCode2024!Secure
Port: 1433
Encrypt: true
```

## Firewall-Regel

Deine IP-Adresse (`79.196.254.230`) ist bereits in den Firewall-Regeln erlaubt.

Falls sich deine IP ändert, füge eine neue Regel hinzu:

```powershell
$ip = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing).Content
az sql server firewall-rule create `
  --server sql-csvtransportud3e1cem `
  --resource-group rg-infrastructure-as-code `
  --name "AllowMyIP_$(Get-Date -Format 'yyyyMMddHHmmss')" `
  --start-ip-address $ip `
  --end-ip-address $ip
```



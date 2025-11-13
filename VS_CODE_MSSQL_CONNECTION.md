# VS Code MSSQL Extension Verbindungsprobleme beheben

## Häufige Fehler und Lösungen

### Problem 1: Firewall blockiert die Verbindung

**Fehler**: "Cannot connect to server" oder "Login failed"

**Lösung**: Füge deine aktuelle IP-Adresse zu den Firewall-Regeln hinzu:

```powershell
# Hole deine aktuelle IP-Adresse
$ip = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing).Content

# Füge Firewall-Regel hinzu
az sql server firewall-rule create `
  --server sql-csvtransportud3e1cem `
  --resource-group rg-infrastructure-as-code `
  --name "AllowVSCodeConnection" `
  --start-ip-address $ip `
  --end-ip-address $ip
```

### Problem 2: SSL/TLS Zertifikatsfehler

**Fehler**: "Certificate validation failed" oder "Encryption not supported"

**Lösung**: Stelle sicher, dass in der Connection String `Encrypt=true` gesetzt ist:

```
Server=sql-csvtransportud3e1cem.database.windows.net;Database=csvtransportdb;User Id=sqladmin;Password=InfrastructureAsCode2024!Secure;Encrypt=true;TrustServerCertificate=false;
```

### Problem 3: Falsche Verbindungsdaten

**Überprüfe diese Werte**:

- **Server**: `sql-csvtransportud3e1cem.database.windows.net` (mit `.database.windows.net`)
- **Database**: `csvtransportdb`
- **Username**: `sqladmin`
- **Password**: `InfrastructureAsCode2024!Secure`
- **Port**: `1433` (Standard für Azure SQL)

### Problem 4: VS Code Extension benötigt zusätzliche Konfiguration

1. Öffne VS Code Settings (`Ctrl+,`)
2. Suche nach `mssql`
3. Stelle sicher, dass diese Einstellungen aktiviert sind:
   - `mssql.enableSqlCmd` sollte aktiviert sein
   - `mssql.enableIntelliSense` sollte aktiviert sein

## Schritt-für-Schritt Verbindung

### Schritt 1: Firewall-Regel hinzufügen

```powershell
# Hole deine IP-Adresse
$ip = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing).Content
Write-Host "Deine IP: $ip"

# Füge Firewall-Regel hinzu
az sql server firewall-rule create `
  --server sql-csvtransportud3e1cem `
  --resource-group rg-infrastructure-as-code `
  --name "AllowVSCode_$(Get-Date -Format 'yyyyMMddHHmmss')" `
  --start-ip-address $ip `
  --end-ip-address $ip
```

### Schritt 2: Verbindung in VS Code testen

1. Öffne VS Code
2. Drücke `Ctrl+Shift+P`
3. Tippe `MS SQL: Connect`
4. Wähle "Create Connection Profile"
5. Gib ein:
   - **Server**: `sql-csvtransportud3e1cem.database.windows.net`
   - **Database**: `csvtransportdb`
   - **Username**: `sqladmin`
   - **Password**: `InfrastructureAsCode2024!Secure`
   - **Encrypt**: `true`
   - **Trust Server Certificate**: `false`

### Schritt 3: Alternative - Connection String verwenden

Erstelle eine Datei `.vscode/settings.json` im Projekt-Root:

```json
{
  "mssql.connections": [
    {
      "server": "sql-csvtransportud3e1cem.database.windows.net",
      "database": "csvtransportdb",
      "authenticationType": "SqlLogin",
      "user": "sqladmin",
      "password": "InfrastructureAsCode2024!Secure",
      "encrypt": true,
      "trustServerCertificate": false
    }
  ]
}
```

## Verbindung testen mit sqlcmd

Falls VS Code weiterhin Probleme hat, teste die Verbindung zuerst mit sqlcmd:

```powershell
sqlcmd -S sql-csvtransportud3e1cem.database.windows.net `
       -d csvtransportdb `
       -U sqladmin `
       -P "InfrastructureAsCode2024!Secure" `
       -Q "SELECT @@VERSION"
```

Wenn das funktioniert, ist das Problem bei der VS Code Extension. Wenn nicht, ist es ein Firewall- oder Netzwerkproblem.

## Firewall-Regeln anzeigen

```powershell
az sql server firewall-rule list `
  --server sql-csvtransportud3e1cem `
  --resource-group rg-infrastructure-as-code `
  --output table
```

## Firewall-Regel löschen (falls nötig)

```powershell
az sql server firewall-rule delete `
  --server sql-csvtransportud3e1cem `
  --resource-group rg-infrastructure-as-code `
  --name "AllowVSCodeConnection"
```

## Support

Falls das Problem weiterhin besteht:
1. Prüfe die VS Code Output-Konsole (`View` → `Output` → wähle "MSSQL")
2. Prüfe die Azure SQL Server Logs im Azure Portal
3. Stelle sicher, dass der SQL Server Status "Online" ist




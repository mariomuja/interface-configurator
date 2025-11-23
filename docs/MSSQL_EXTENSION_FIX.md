# MSSQL Extension Installation beheben

## Problem
Die MSSQL Extension ist installiert, aber der Command "MS SQL: Connect" funktioniert nicht.

## Lösungsschritte

### Schritt 1: VS Code neu starten
1. Schließe VS Code komplett
2. Starte VS Code neu
3. Die Extension sollte jetzt aktiv sein

### Schritt 2: Extension aktivieren
1. Öffne VS Code
2. Gehe zu `View` → `Extensions` (oder `Ctrl+Shift+X`)
3. Suche nach "SQL Server (mssql)"
4. Stelle sicher, dass die Extension aktiviert ist (nicht deaktiviert)
5. Klicke auf "Reload" falls verfügbar

### Schritt 3: Alternative Commands probieren
Öffne Command Palette (`Ctrl+Shift+P`) und probiere diese Commands:

- `MS SQL: Connect`
- `SQL: Connect`
- `mssql.connect`
- `SQL: New Query`
- `MS SQL: New Query`

### Schritt 4: Extension manuell über Extensions-Panel installieren
1. Öffne Extensions (`Ctrl+Shift+X`)
2. Suche nach "SQL Server (mssql)" von Microsoft
3. Falls nicht installiert, klicke auf "Install"
4. Falls installiert, klicke auf "Reload" oder "Disable" → "Enable"

### Schritt 5: Prüfe Output-Logs
1. Öffne `View` → `Output`
2. Wähle "MSSQL" aus dem Dropdown
3. Prüfe auf Fehlermeldungen

### Schritt 6: Alternative Tools (falls VS Code nicht funktioniert)

**SQL Server Management Studio (SSMS)** - Offizielles Microsoft-Tool:
- Download: https://aka.ms/ssms
- Vollständige SQL Server-Verwaltung
- Sehr stabil und zuverlässig

**DBeaver** - Open Source Alternative:
- Download: https://dbeaver.io
- Kostenlos und universell für viele Datenbanktypen

**Azure Portal Query Editor**:
- Direkt im Browser verfügbar
- Keine Installation nötig
- Gehe zu Azure Portal → SQL Server → Query Editor

### Schritt 7: Manuelle Verbindung über Settings
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
      "trustServerCertificate": false,
      "emptyPasswordInput": false
    }
  ]
}
```

### Schritt 8: Prüfe VS Code Version
Die MSSQL Extension benötigt VS Code Version 1.60.0 oder höher:

```bash
code --version
```

Falls die Version zu alt ist, aktualisiere VS Code.

## Alternative: Azure Data Studio verwenden

Falls VS Code weiterhin Probleme macht, verwende Azure Data Studio:

1. Installiere Azure Data Studio: https://aka.ms/azuredatastudio
2. Öffne Azure Data Studio
3. Klicke auf "New Connection"
4. Gib die Verbindungsdaten ein:
   - **Server**: `sql-csvtransportud3e1cem.database.windows.net`
   - **Database**: `csvtransportdb`
   - **Authentication**: SQL Login
   - **Username**: `sqladmin`
   - **Password**: `InfrastructureAsCode2024!Secure`
   - **Encrypt**: true

## Verbindung testen ohne Extension

Du kannst auch direkt SQL-Abfragen mit sqlcmd ausführen:

```powershell
sqlcmd -S sql-csvtransportud3e1cem.database.windows.net `
       -d csvtransportdb `
       -U sqladmin `
       -P "InfrastructureAsCode2024!Secure" `
       -Q "SELECT * FROM TransportData"
```

## Bekannte Probleme

1. **Extension lädt nicht**: VS Code muss neu gestartet werden
2. **Command nicht gefunden**: Die Extension ist möglicherweise nicht aktiviert
3. **Verbindungsfehler**: Firewall-Regel muss gesetzt sein (bereits erledigt)

## Nächste Schritte

1. Starte VS Code neu
2. Prüfe, ob die Extension in der Extensions-Liste aktiviert ist
3. Probiere die verschiedenen Commands aus
4. Falls nichts funktioniert, verwende Azure Data Studio als Alternative



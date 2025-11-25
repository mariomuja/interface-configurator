# minime MCP Server - Setup und Problembehebung

## Problem-Analyse

Der minime MCP Server wird in Cursor Settings nicht grün angezeigt, weil:

1. **MCP Proxy läuft nicht**: Der Proxy-Server auf Port 8001 muss laufen, damit Cursor mit minime kommunizieren kann
2. **Kein automatischer Start**: Nach einem Rechner-Neustart startet der Proxy nicht automatisch
3. **Docker Container**: Der minime Docker Container läuft, aber der Proxy muss separat gestartet werden

## Lösung

### Automatisches Setup

Führen Sie das Setup-Skript aus:

```powershell
.\scripts\setup-minime-mcp-server.ps1 -Setup
```

Dieses Skript:
- ✅ Prüft die aktuelle Konfiguration
- ✅ Startet den Docker Container (falls nicht läuft)
- ✅ Startet den MCP Proxy auf Port 8001
- ✅ Korrigiert die MCP-Konfiguration
- ✅ Erstellt einen Startup-Shortcut für automatischen Start

### Manuelle Schritte

Falls das automatische Setup nicht funktioniert:

1. **Starte MCP Proxy manuell:**
   ```powershell
   cd C:\Users\mario\minime-mcp\install
   .\start-mcp-proxy.ps1
   ```

2. **Erstelle Startup-Shortcut:**
   - Öffne: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`
   - Erstelle Shortcut zu: `C:\Users\mario\minime-mcp\install\start-minime-on-boot.ps1`
   - Oder verwende das Setup-Skript: `.\scripts\setup-minime-mcp-server.ps1 -InstallService`

3. **Prüfe Konfiguration:**
   ```powershell
   .\scripts\setup-minime-mcp-server.ps1 -Diagnose
   ```

## Verifizierung

Nach dem Setup sollten folgende Komponenten laufen:

1. **Docker Container**: `docker ps --filter "name=minimemcp"` sollte "Up" zeigen
2. **MCP Proxy**: `Invoke-WebRequest -Uri 'http://localhost:8001/health'` sollte `{"status":"ok"}` zurückgeben
3. **MCP Config**: `C:\Users\mario\.cursor\mcp.json` sollte minime mit `"transport": "http"` enthalten

## Cursor neu starten

**WICHTIG**: Nach dem Setup müssen Sie Cursor **komplett neu starten**:

1. Schließen Sie ALLE Cursor-Fenster
2. Warten Sie 5 Sekunden
3. Öffnen Sie Cursor erneut
4. Prüfen Sie Settings > MCP Servers - minime sollte jetzt grün sein

## Startup-Konfiguration

Der automatische Start funktioniert über:

- **Startup-Shortcut**: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\minime-mcp-server.lnk`
- **oder** geplante Aufgabe `minime-mcp-server-startup` (falls mit Admin-Rechten erstellt)
- **Startup-Script**: `C:\Users\mario\minime-mcp\install\start-minime-on-boot.ps1`

### Was das neue Autostart-Script erledigt

1. **Docker Desktop prüfen**  
   - Startet den Windows-Dienst `com.docker.service`, falls vorhanden  
   - Startet alternativ `Docker Desktop.exe`, falls der Dienst nicht verfügbar ist
2. **Wartet auf Docker Engine** (Health-Check via `docker version`)
3. **Container `minimemcp` starten** und Status erneut prüfen
4. **MCP Proxy starten** via `start-mcp-proxy.ps1` samt Health-Check `http://localhost:8001/health`
5. **Logging**: Alle Aktionen landen in `%LOCALAPPDATA%\minime-mcp\startup.log`

Damit stehen nach jedem Rechner-Neustart automatisch Docker, Container und Proxy bereit, bevor Cursor gestartet wird.

## Troubleshooting

### Problem: Cursor zeigt minime immer noch nicht grün

**Lösung:**
1. Prüfe ob Proxy läuft: `netstat -ano | findstr ":8001"`
2. Prüfe Proxy Health: `Invoke-WebRequest -Uri 'http://localhost:8001/health'`
3. Restart Cursor komplett (alle Fenster schließen, 5 Sekunden warten)
4. Prüfe Cursor Output Logs (View > Output > MCP)

### Problem: Proxy startet nicht beim Systemstart

**Lösung:**
1. Prüfe Startup-Shortcut: `Test-Path "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\minime-mcp-server.lnk"`
2. Prüfe ob PowerShell ExecutionPolicy erlaubt: `Get-ExecutionPolicy`
3. Führe Setup erneut aus: `.\scripts\setup-minime-mcp-server.ps1 -Setup`

### Problem: Docker Container startet nicht

**Lösung:**
1. Prüfe Docker: `docker ps -a --filter "name=minimemcp"`
2. Starte Container: `docker start minimemcp`
3. Prüfe Logs: `docker logs minimemcp --tail 20`

## Verfügbare Befehle

```powershell
# Diagnose
.\scripts\setup-minime-mcp-server.ps1 -Diagnose

# Vollständiges Setup
.\scripts\setup-minime-mcp-server.ps1 -Setup

# Nur Server starten
.\scripts\setup-minime-mcp-server.ps1 -Start

# Nur Konfiguration korrigieren
.\scripts\setup-minime-mcp-server.ps1 -FixConfig

# Nur Startup-Task installieren
.\scripts\setup-minime-mcp-server.ps1 -InstallService
```

## Dateien

- **Setup-Script**: `scripts\setup-minime-mcp-server.ps1`
- **Startup-Script**: `C:\Users\mario\minime-mcp\install\start-minime-on-boot.ps1`
- **Proxy-Script**: `C:\Users\mario\minime-mcp\install\start-mcp-proxy.ps1`
- **MCP Config**: `C:\Users\mario\.cursor\mcp.json`
- **Startup-Shortcut**: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\minime-mcp-server.lnk`


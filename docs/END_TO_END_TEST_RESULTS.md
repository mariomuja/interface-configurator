# End-to-End Test Ergebnisse: CSV Source → Service Bus → SQL Server Destination

## Test-Ziel

Vollständiger Test des Datenflusses:
1. CSV Source Adapter aktivieren → Container App erstellen
2. CSV-Beispieldaten im Blob Container ablegen
3. Blob Trigger → CSV-Daten chunkweise auf Service Bus legen
4. Service Bus Messages im UI anzeigen
5. SQL Server Destination Adapter → Service Bus abonnieren → TransportData Tabelle
6. Process Logs im UI anzeigen

## Test-Durchführung

**Datum:** $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
**Tester:** Browser-basierter UI-Test
**Umgebung:** Production (Vercel)

## Test-Schritte

### ✅ Schritt 1: CSV Source Adapter aktivieren

**Status:** ✅ Erfolgreich
**Aktion:** Toggle Switch für Source CSV Adapter aktiviert
**Ergebnis:** 
- Source Adapter wurde aktiviert
- Container App sollte erstellt werden (muss in Azure Portal verifiziert werden)

**Screenshot:** `test-02-source-enabled.png`

### ⏳ Schritt 2: CSV-Daten eingeben

**Status:** Ausstehend
**Hinweis:** CSV-Daten werden im RAW-Modus direkt in der Source Card eingegeben
**Erwartetes Verhalten:**
- CSV-Daten werden automatisch in Blob Storage hochgeladen (csv-incoming)
- Blob Trigger wird ausgelöst
- CSV-Daten werden chunkweise auf Service Bus gelegt
**Screenshot:** `test-03-source-card-opened.png`

### ⏳ Schritt 3: Container App Status prüfen

**Status:** Ausstehend
**Zu prüfen:**
- Container App wurde erstellt (Name: `ca-{adapterInstanceGuid}`)
- Container App läuft
- Blob Container für Container App existiert
- CSV-Daten wurden im Blob Container abgelegt

### ⏳ Schritt 4: Service Bus Messages prüfen

**Status:** Ausstehend
**Zu prüfen:**
- Service Bus Messages Card zeigt Nachrichten an
- Nachrichten enthalten CSV-Daten
- Nachrichten haben korrekte Properties (AdapterName, MessageId, etc.)

### ✅ Schritt 5: SQL Server Destination Adapter hinzufügen

**Status:** Teilweise abgeschlossen
**Aktionen:**
1. ✅ "Add Destination Adapter" Button geklickt
2. ✅ SQL Server Adapter ausgewählt
3. ⏳ SQL Server Verbindungsdaten konfigurieren (Settings Dialog muss geöffnet werden)
4. ⏳ Adapter aktivieren
5. ⏳ Service Bus Subscription erstellen
**Screenshots:** 
- `test-05-add-destination-dialog.png` - Adapter-Auswahl
- `test-06-sql-destination-settings.png` - Nach Auswahl

### ⏳ Schritt 6: Daten in TransportData Tabelle prüfen

**Status:** Ausstehend
**Zu prüfen:**
- Daten wurden in SQL Server TransportData Tabelle geschrieben
- Alle CSV-Zeilen wurden verarbeitet
- Daten sind korrekt strukturiert

### ⏳ Schritt 7: Process Logs prüfen

**Status:** Ausstehend
**Zu prüfen:**
- Process Logs Tabelle zeigt alle Verarbeitungsschritte
- Logs enthalten:
  - CSV-Daten Upload
  - Blob Trigger Aktivierung
  - Service Bus Message Erstellung
  - SQL Server Schreibvorgänge
  - Erfolgs-/Fehlermeldungen

## Nächste Schritte

1. CSV-Daten in Source Card eingeben
2. Warten auf Blob Trigger Verarbeitung (30-60 Sekunden)
3. Service Bus Messages Card aktualisieren
4. SQL Server Destination Adapter hinzufügen und konfigurieren
5. Warten auf Destination Adapter Verarbeitung (30-60 Sekunden)
6. Process Logs prüfen
7. SQL Server TransportData Tabelle prüfen

## Bekannte Probleme

- Keine bekannt

## Screenshots

- `test-01-main-page.png` - Hauptseite nach Login
- `test-02-source-enabled.png` - Source Adapter aktiviert
- `test-03-source-card-opened.png` - Source Card geöffnet
- `test-04-source-settings-dialog.png` - Settings Dialog geöffnet


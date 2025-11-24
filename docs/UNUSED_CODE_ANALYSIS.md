# Unused Code Analysis

## Zusammenfassung

Diese Analyse identifiziert nicht mehr verwendeten Code, der entfernt oder aufgeräumt werden kann.

## 1. Obsolete Methoden die noch verwendet werden

### `WriteRecordsToMessageBoxAsync` (Obsolete)

**Status:** ⚠️ **Noch verwendet** - Sollte zu `WriteRecordsToServiceBusAsync` migriert werden

**Vorkommen:**
- `azure-functions/main/Adapters/SqlServerAdapter.cs:242`
- `azure-functions/main/Adapters/CsvAdapter.cs:183, 404`
- `azure-functions/main/Adapters/CrmAdapter.cs:237`
- `azure-functions/main/Adapters/Dynamics365Adapter.cs:220`
- `azure-functions/main/Adapters/FileAdapter.cs:422`
- `azure-functions/main/Adapters/SapAdapter.cs:189`

**Empfehlung:** Alle Aufrufe sollten zu `WriteRecordsToServiceBusAsync` geändert werden.

### `ReadMessagesFromMessageBoxAsync` (Obsolete)

**Status:** ✅ **Nur als Compatibility Shim** - Wird nur in `AdapterBase.cs` als Wrapper verwendet

**Vorkommen:**
- `azure-functions/main/Adapters/AdapterBase.cs:318` - Nur Compatibility Shim

**Empfehlung:** Kann entfernt werden, wenn keine Legacy-Code mehr darauf zugreift.

### `ProcessChunksAsync` und `InsertChunkAsync` (Deprecated)

**Status:** ⚠️ **Deprecated aber noch vorhanden**

**Vorkommen:**
- `azure-functions/main/Services/DataServiceAdapter.cs:108, 186` - `ProcessChunksAsync` ruft `InsertChunkAsync` auf
- `azure-functions/main/Services/DataServiceAdapter.cs:186` - `InsertChunkAsync` wirft `NotSupportedException`
- `azure-functions/main/Services/DataServiceAdapterV2.cs:108` - `ProcessChunksAsync` wirft `NotSupportedException`

**Empfehlung:** 
- Prüfen ob `ProcessChunksAsync` noch aufgerufen wird
- Wenn nicht, beide Methoden entfernen
- Wenn ja, alle Aufrufe zu `InsertRowsAsync` migrieren

## 2. Auskommentierte Modelle (MessageBox Migration)

### `MessageBoxMessage`, `MessageSubscription`, `AdapterSubscription`, `MessageProcessing`

**Status:** ✅ **Nicht mehr verwendet** - Tabellen wurden entfernt, Migration zu Service Bus abgeschlossen

**Dateien:**
- `azure-functions/main.Core/Models/MessageBoxMessage.cs` - ✅ Model existiert noch (wird für Compatibility Shim verwendet)
- `azure-functions/main.Core/Models/MessageSubscription.cs` - ⚠️ **Kann entfernt werden**
- `azure-functions/main.Core/Models/AdapterSubscription.cs` - ⚠️ **Kann entfernt werden**
- `azure-functions/main.Core/Models/MessageProcessing.cs` - ⚠️ **Kann entfernt werden**

**Prüfung in MessageBoxDbContext:**
- Diese Modelle sollten aus `MessageBoxDbContext.cs` entfernt sein (wurde bereits gemacht)

**Empfehlung:** 
- `MessageSubscription.cs`, `AdapterSubscription.cs`, `MessageProcessing.cs` können gelöscht werden
- `MessageBoxMessage.cs` behalten für Compatibility Shim in `ReadMessagesFromMessageBoxAsync`

## 3. Deprecated Properties (Backward Compatibility)

### `InterfaceConfiguration.cs` - Deprecated Properties

**Status:** ✅ **Noch verwendet für Backward Compatibility** - Sollte beibehalten werden

**Vorkommen:**
- Viele `[Obsolete]` Properties in `InterfaceConfiguration.cs`
- Werden noch für Migration und Backward Compatibility verwendet

**Empfehlung:** 
- Beibehalten bis Migration vollständig abgeschlossen
- Dann schrittweise entfernen

## 4. Veraltete Dokumentation

### `terraform/GITHUB_SOURCE_CONTROL.md`

**Status:** ✅ **Als veraltet markiert** - Kann entfernt werden

**Inhalt:** Dokumentiert alte GitHub Source Control Methode, die nicht mehr verwendet wird

**Empfehlung:** Datei löschen oder in Archive verschieben

## 5. Test-Dateien

### Test-Funktionen die möglicherweise nicht mehr benötigt werden

**Status:** ⚠️ **Prüfen ob noch verwendet**

**Dateien:**
- `azure-functions/main/TestCsvAdapterMessageBox.cs` - Test für MessageBox (nicht mehr verwendet?)
- `azure-functions/main/TestBlobTriggerLogic.cs` - Prüfen ob noch relevant
- `azure-functions/main/TestSourceAdapterProcess.cs` - Prüfen ob noch relevant
- `azure-functions/main/TestCsvProcessing.cs` - Prüfen ob noch relevant
- `azure-functions/main/TestConfigLoading.cs` - Prüfen ob noch relevant
- `azure-functions/main/TestMessageBoxService.cs` - Test für MessageBox (nicht mehr verwendet?)
- `azure-functions/main/TestMessageBoxWrite.cs` - Test für MessageBox (nicht mehr verwendet?)

**Empfehlung:** 
- Prüfen welche Tests noch relevant sind
- MessageBox-bezogene Tests können entfernt werden
- Andere Tests prüfen ob sie noch funktionieren

## 6. Nicht mehr verwendete Services/Interfaces

### `IMessageBoxService` / `MessageBoxService`

**Status:** ⚠️ **Noch teilweise verwendet** - Wird noch in `AdapterFactory.cs` verwendet

**Vorkommen:**
- `azure-functions/main/Services/AdapterFactory.cs` - Verwendet `IMessageBoxService` für `EnsureAdapterInstanceAsync`
- `azure-functions/main/Adapters/CsvAdapter.cs` - Verwendet `_messageBoxService` für Logging

**Empfehlung:** 
- Prüfen ob `EnsureAdapterInstanceAsync` noch benötigt wird
- Wenn nicht, `IMessageBoxService` Abhängigkeiten entfernen

## 7. Kommentierte Code-Blöcke

### Keine größeren kommentierten Code-Blöcke gefunden

**Status:** ✅ **Sauber** - Keine großen kommentierten Code-Blöcke gefunden

## Empfohlene Aktionen

### Sofort entfernen (sicher):
1. ✅ `azure-functions/main.Core/Models/MessageSubscription.cs`
2. ✅ `azure-functions/main.Core/Models/AdapterSubscription.cs`
3. ✅ `azure-functions/main.Core/Models/MessageProcessing.cs`
4. ✅ `terraform/GITHUB_SOURCE_CONTROL.md`

### Migrieren und dann entfernen:
1. ⚠️ Alle `WriteRecordsToMessageBoxAsync` Aufrufe zu `WriteRecordsToServiceBusAsync` ändern
2. ⚠️ `ReadMessagesFromMessageBoxAsync` entfernen (nach Migration)
3. ⚠️ `ProcessChunksAsync` und `InsertChunkAsync` entfernen (nach Migration)

### Prüfen und ggf. entfernen:
1. ⚠️ MessageBox-bezogene Test-Dateien
2. ⚠️ `IMessageBoxService` Abhängigkeiten in `AdapterFactory.cs`

### Beibehalten (Backward Compatibility):
1. ✅ Deprecated Properties in `InterfaceConfiguration.cs`
2. ✅ `MessageBoxMessage.cs` (für Compatibility Shim)


# VS Code Test Explorer Setup

Diese Anleitung zeigt, wie Sie den VS Code Test Explorer für .NET-Tests einrichten und verwenden.

## Voraussetzungen

1. **VS Code** installiert
2. **.NET SDK 8.0** installiert
3. **C# Extension** für VS Code

## Installation der Extensions

### Option 1: Automatisch (empfohlen)

VS Code sollte automatisch die empfohlenen Extensions vorschlagen, wenn Sie das Projekt öffnen.

### Option 2: Manuell

1. Öffnen Sie VS Code
2. Drücken Sie `Ctrl+Shift+X` (Extensions View)
3. Installieren Sie folgende Extensions:
   - **C#** (`ms-dotnettools.csharp`)
   - **.NET Test Explorer** (`formulahendry.dotnet-test-explorer`) - Optional, aber hilfreich

## Test Explorer öffnen

### Methode 1: Über das Testing Icon

1. Öffnen Sie VS Code im `azure-functions` Verzeichnis
2. Klicken Sie auf das **Testing Icon** (Flask-Symbol) in der linken Seitenleiste
   - Oder drücken Sie `Ctrl+Shift+T`
3. Die Tests werden automatisch erkannt und angezeigt

### Methode 2: Über Command Palette

1. Drücken Sie `Ctrl+Shift+P`
2. Tippen Sie `Test: Focus on Test View`
3. Die Test-Ansicht wird geöffnet

## Test Explorer Features

### Test-Ansicht

Der Test Explorer zeigt:

```
📁 main.Core.Tests
  📁 Services
    📁 CsvProcessingServiceTests
      ✓ ParseCsv_ValidCsv_ReturnsRecords
      ✓ ParseCsv_EmptyContent_ReturnsEmptyList
      ✓ CreateChunks_ValidRecords_CreatesChunks
      ...
    📁 DataServiceAdapterTests
      ...
  📁 Processors
    📁 CsvProcessorTests
      ...
```

### Test ausführen

#### Alle Tests ausführen

1. Klicken Sie auf **▶ Run All Tests** oben im Test Explorer
2. Oder drücken Sie `Ctrl+Shift+P` → `Test: Run All Tests`

#### Einzelne Tests ausführen

1. Klicken Sie auf das **▶** Symbol neben einem Test
2. Oder rechtsklicken Sie auf einen Test → **Run Test**

#### Tests einer Klasse ausführen

1. Klicken Sie auf das **▶** Symbol neben einer Test-Klasse
2. Oder rechtsklicken Sie auf eine Klasse → **Run Tests**

### Test debuggen

1. Setzen Sie einen Breakpoint in Ihrem Test-Code
2. Rechtsklicken Sie auf den Test → **Debug Test**
3. Der Debugger startet und stoppt am Breakpoint

### Code Lens

In den Test-Dateien sehen Sie **Code Lens** direkt über jeder Test-Methode:

```csharp
[Fact]
public void ParseCsv_ValidCsv_ReturnsRecords()  // ▶ Run Test | 🐛 Debug Test
{
    // Test code...
}
```

Klicken Sie auf:
- **▶ Run Test** - Führt den Test aus
- **🐛 Debug Test** - Startet den Debugger

### Test-Status

Tests werden farbcodiert angezeigt:

- ✅ **Grün** - Test bestanden
- ❌ **Rot** - Test fehlgeschlagen
- ⏸️ **Grau** - Test nicht ausgeführt
- ⚠️ **Gelb** - Test übersprungen

### Test-Ergebnisse anzeigen

1. Nach dem Ausführen von Tests sehen Sie die Ergebnisse direkt im Test Explorer
2. Klicken Sie auf einen fehlgeschlagenen Test, um Details zu sehen
3. Die Fehlermeldung wird im Output-Panel angezeigt

## Konfiguration

Die Test Explorer Konfiguration befindet sich in `.vscode/settings.json`:

```json
{
  "dotnet.testWindow.codeLens": true,
  "dotnet.testWindow.showCodeLens": true,
  "testExplorer.useNativeTesting": true,
  "testExplorer.codeLens": true,
  "testExplorer.showDuration": true
}
```

### Wichtige Einstellungen

- `dotnet.testWindow.codeLens` - Zeigt Code Lens über Tests
- `testExplorer.showDuration` - Zeigt Test-Dauer
- `testExplorer.showFailCount` - Zeigt Anzahl fehlgeschlagener Tests

## Troubleshooting

### Tests werden nicht erkannt

1. **Projekt neu laden:**
   - `Ctrl+Shift+P` → `Developer: Reload Window`

2. **OmniSharp neu starten:**
   - `Ctrl+Shift+P` → `OmniSharp: Restart OmniSharp`

3. **Projekt bauen:**
   ```powershell
   cd azure-functions
   dotnet build main.Core.Tests/main.Core.Tests.csproj
   ```

### Code Lens wird nicht angezeigt

1. Stellen Sie sicher, dass `dotnet.testWindow.codeLens` auf `true` gesetzt ist
2. Laden Sie VS Code neu
3. Prüfen Sie, ob die C# Extension aktiviert ist

### Tests laufen nicht

1. Prüfen Sie, ob das Test-Projekt gebaut wurde:
   ```powershell
   dotnet build main.Core.Tests/main.Core.Tests.csproj
   ```

2. Prüfen Sie die Output-Panel für Fehlermeldungen:
   - View → Output → Wählen Sie "Test Explorer" oder ".NET Test Log"

### Test Explorer zeigt keine Tests

1. Öffnen Sie eine `.cs` Datei im Test-Projekt
2. Warten Sie, bis OmniSharp das Projekt analysiert hat (siehe Status-Bar)
3. Klicken Sie auf "Refresh" im Test Explorer

## Keyboard Shortcuts

- `Ctrl+Shift+T` - Test Explorer öffnen/fokussieren
- `Ctrl+Shift+P` → `Test: Run All Tests` - Alle Tests ausführen
- `Ctrl+Shift+P` → `Test: Run Test` - Test am Cursor ausführen
- `Ctrl+Shift+P` → `Test: Debug Test` - Test am Cursor debuggen

## Best Practices

1. **Regelmäßig Tests ausführen:** Führen Sie Tests aus, bevor Sie Code committen
2. **Code Lens nutzen:** Verwenden Sie Code Lens für schnellen Zugriff auf Tests
3. **Debugging:** Nutzen Sie den Debugger für komplexe Test-Szenarien
4. **Test Explorer:** Behalten Sie den Test Explorer im Blick für schnellen Überblick

## Weitere Ressourcen

- [VS Code Testing Documentation](https://code.visualstudio.com/docs/editor/testing)
- [.NET Test Explorer Extension](https://marketplace.visualstudio.com/items?itemName=formulahendry.dotnet-test-explorer)
- [C# Extension Documentation](https://code.visualstudio.com/docs/languages/csharp)










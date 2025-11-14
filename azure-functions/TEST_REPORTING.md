# Test-Reporting in der Entwicklungsumgebung

Dieses Dokument beschreibt die verschiedenen Möglichkeiten, Unit-Test-Ergebnisse übersichtlich darzustellen.

## Übersicht

Es gibt mehrere Möglichkeiten, Test-Ergebnisse übersichtlich anzuzeigen:

1. **PowerShell-Script** (`list-tests.ps1`) - **EMPFOHLEN** - Übersichtliche Console-Ausgabe mit gruppierten Tests
2. **Einfaches Script** (`show-test-results.ps1`) - Standard-Ausgabe mit HTML-Report
3. **HTML Report Generator** (`generate-test-report.ps1`) - Professionelle HTML-Reports mit Code Coverage
4. **VS Code Test Explorer** - Integrierte Test-Ansicht
5. **Console-Output** - Standard dotnet test Ausgabe

## Option 1: PowerShell-Script für Console-Ausgabe (EMPFOHLEN ⭐)

### Verwendung

```powershell
cd azure-functions
.\run-tests.ps1
```

### Features

- ✅ Übersichtliche Zusammenfassung (Gesamt, Bestanden, Fehlgeschlagen)
- ✅ Gruppierte Test-Liste nach Klassen
- ✅ Farbcodierte Status-Anzeige (✓ grün, ✗ rot, ⊘ gelb)
- ✅ Dauer pro Test
- ✅ Automatisches Öffnen des HTML-Reports

### Beispiel-Ausgabe

```
========================================
  Unit Test Uebersicht
========================================

========================================
  Zusammenfassung
========================================

Gesamt:     40 Tests
Bestanden:  40 Tests
Fehlgeschlagen: 0 Tests

========================================
  Test-Details (nach Klasse)
========================================

[ProcessCsvBlobTrigger.Core.Tests.Services.CsvProcessingServiceTests]
  [PASS] ParseCsv_ValidCsv_ReturnsRecords (6 ms)
  [PASS] ParseCsv_EmptyContent_ReturnsEmptyList (< 1 ms)
  [PASS] CreateChunks_ValidRecords_CreatesChunks (< 1 ms)
  ...

========================================
  Test-Reports
========================================

HTML Report: C:\...\TestResults\test-results.html
```

### Alternative: Einfaches Script

```powershell
.\show-test-results.ps1 -OpenHtml
```

Zeigt Standard-Ausgabe und öffnet HTML-Report automatisch.

## Option 2: HTML Report mit Code Coverage

### Verwendung

```powershell
cd azure-functions
.\generate-test-report.ps1 -OpenReport
```

### Features

- ✅ Professioneller HTML-Report
- ✅ Code Coverage Visualisierung
- ✅ Detaillierte Test-Ergebnisse
- ✅ Filterbare Ansichten
- ✅ Badges für Code Coverage

### Report-Verzeichnisse

- `TestResults/coverage/` - Coverage-Daten
- `TestResults/report/` - HTML Report (öffne `index.html`)

## Option 3: VS Code Test Explorer

### Setup

1. Installiere die **.NET Test Explorer** Extension in VS Code
2. Öffne das Test-Projekt in VS Code
3. Die Tests werden automatisch erkannt

### Features

- ✅ Integrierte Test-Ansicht in VS Code
- ✅ Einzelne Tests ausführen
- ✅ Debugging von Tests
- ✅ Test-Status direkt im Editor

### Verwendung

1. Öffne VS Code im `azure-functions` Verzeichnis
2. Öffne die Test Explorer Ansicht (View → Testing)
3. Tests werden automatisch erkannt
4. Klicke auf ▶️ um Tests auszuführen

## Option 4: Standard Console-Output

### Verwendung

```powershell
cd azure-functions
dotnet test ProcessCsvBlobTrigger.Core.Tests/ProcessCsvBlobTrigger.Core.Tests.csproj --verbosity normal
```

### Erweiterte Optionen

```powershell
# Mit TRX Report (für CI/CD)
dotnet test --logger "trx;LogFileName=test-results.trx"

# Mit HTML Report
dotnet test --logger "html;LogFileName=test-results.html"

# Mit Code Coverage
dotnet test --collect:"XPlat Code Coverage"

# Alle zusammen
dotnet test `
    --logger "trx;LogFileName=test-results.trx" `
    --logger "html;LogFileName=test-results.html" `
    --collect:"XPlat Code Coverage" `
    --verbosity detailed
```

## Empfohlene Workflows

### Für schnelle Entwicklung (EMPFOHLEN)

```powershell
.\list-tests.ps1
```

Schnelle, übersichtliche Ausgabe direkt in der Console mit gruppierten Tests.

Oder mit automatischem Öffnen des HTML-Reports:

```powershell
.\list-tests.ps1 -OpenHtml
```

### Für detaillierte Analyse

```powershell
.\generate-test-report.ps1 -OpenReport
```

Öffnet einen detaillierten HTML-Report mit Code Coverage.

### Für CI/CD Integration

```powershell
dotnet test `
    --logger "trx;LogFileName=test-results.trx" `
    --logger "html;LogFileName=test-results.html" `
    --collect:"XPlat Code Coverage" `
    --results-directory TestResults
```

Generiert Reports für CI/CD-Pipelines.

## VS Code Tasks

Die folgenden Tasks sind in `.vscode/tasks.json` definiert:

1. **Run Tests** - Standard Test-Ausführung
2. **Run Tests with Report** - Mit übersichtlichem Report
3. **Generate Test Report** - HTML Report mit Code Coverage

Verwendung:
- `Ctrl+Shift+P` → "Tasks: Run Task" → Wähle Task

## GitHub Actions Integration

Tests können auch in GitHub Actions ausgeführt werden:

```yaml
- name: Run Unit Tests
  run: |
    cd azure-functions
    dotnet test --logger "trx;LogFileName=test-results.trx" --logger "html"
    
- name: Publish Test Results
  uses: dorny/test-reporter@v1
  if: always()
  with:
    name: Unit Tests
    path: azure-functions/TestResults/*.trx
    reporter: dotnet-trx
```

## Troubleshooting

### Tests werden nicht gefunden

```powershell
# Stelle sicher, dass das Test-Projekt gebaut wurde
dotnet build ProcessCsvBlobTrigger.Core.Tests/ProcessCsvBlobTrigger.Core.Tests.csproj
```

### ReportGenerator nicht gefunden

```powershell
# Installiere ReportGenerator global
dotnet tool install -g dotnet-reportgenerator-globaltool
```

### PowerShell Execution Policy Fehler

```powershell
# Setze Execution Policy (nur für aktuellen Benutzer)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## Best Practices

1. **Vor jedem Commit**: Führe `.\run-tests.ps1` aus
2. **Vor größeren Änderungen**: Generiere HTML Report mit Code Coverage
3. **Bei CI/CD**: Verwende TRX Format für Test-Reporter
4. **Für Debugging**: Nutze VS Code Test Explorer

## Weitere Ressourcen

- [xUnit Documentation](https://xunit.net/)
- [.NET Test Documentation](https://learn.microsoft.com/dotnet/core/tools/dotnet-test)
- [ReportGenerator](https://github.com/danielpalme/ReportGenerator)


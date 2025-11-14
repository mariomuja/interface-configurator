# Zeigt eine detaillierte Liste aller Tests mit Zuordnung zu getesteten Objekten/Methoden
# Strukturiert nach: Getestete Klasse -> Getestete Methode -> Test-Methoden

param(
    [switch]$OpenHtml
)

$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Detaillierte Test-Uebersicht" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$testProject = Join-Path $PSScriptRoot "ProcessCsvBlobTrigger.Core.Tests\ProcessCsvBlobTrigger.Core.Tests.csproj"
$testResultsDir = Join-Path $PSScriptRoot "TestResults"

if (-not (Test-Path $testResultsDir)) {
    New-Item -ItemType Directory -Path $testResultsDir | Out-Null
}

# Führe Tests aus und sammle Output
Write-Host "Fuehre Tests aus...`n" -ForegroundColor Yellow

$output = dotnet test $testProject `
    --logger "console;verbosity=normal" `
    --logger "html;LogFileName=test-results.html" `
    --logger "trx;LogFileName=test-results.trx" `
    --results-directory $testResultsDir 2>&1

# Parse Test-Ergebnisse
$testResults = @()

foreach ($line in $output) {
    if ($line -match "Bestanden\s+(.+?)\s+\[(.+?)\]") {
        $testName = $matches[1].Trim()
        $duration = $matches[2].Trim()
        
        # Parse Test-Name: Full.Qualified.Namespace.TestClass.TestMethod
        # Beispiel: ProcessCsvBlobTrigger.Core.Tests.Services.CsvProcessingServiceTests.ParseCsv_ValidCsv_ReturnsRecords
        # Extrahiere Test-Klasse und Test-Methode
        $parts = $testName -split '\.'
        if ($parts.Count -ge 2) {
            $testMethod = $parts[-1]  # Letzter Teil = Test-Methode
            $testClass = $parts[-2]   # Vorletzter Teil = Test-Klasse
            
            # Finde Namespace-Teil (alles vor TestClass)
            $namespaceIndex = 0
            for ($i = 0; $i -lt $parts.Count; $i++) {
                if ($parts[$i] -eq "Tests") {
                    $namespaceIndex = $i
                    break
                }
            }
            
            # Extrahiere getestete Klasse aus Test-Klassen-Namen
            # Konvention: TestClass = GetesteteKlasseTests
            $testedClass = ""
            if ($testClass -match "^(.+?)Tests$") {
                $testedClass = $matches[1]
            }
            
            # Extrahiere getestete Methode aus Test-Methoden-Namen
            # Konvention: TestMethodName = GetesteteMethode_Szenario_Erwartung
            $testedMethod = ""
            if ($testMethod -match "^(.+?)_") {
                $testedMethod = $matches[1]
            }
            
            $testResults += [PSCustomObject]@{
                FullName = $testName
                TestClass = $testClass
                TestMethod = $testMethod
                TestedClass = $testedClass
                TestedMethod = $testedMethod
                Status = "PASSED"
                Duration = $duration
            }
        }
    }
    elseif ($line -match "Fehlgeschlagen\s+(.+?)\s+\[(.+?)\]") {
        $testName = $matches[1].Trim()
        $duration = $matches[2].Trim()
        
        # Parse Test-Name ähnlich wie oben
        $parts = $testName -split '\.'
        if ($parts.Count -ge 2) {
            $testMethod = $parts[-1]
            $testClass = $parts[-2]
            
            $testedClass = ""
            if ($testClass -match "^(.+?)Tests$") {
                $testedClass = $matches[1]
            }
            
            $testedMethod = ""
            if ($testMethod -match "^(.+?)_") {
                $testedMethod = $matches[1]
            }
            
            $testResults += [PSCustomObject]@{
                FullName = $testName
                TestClass = $testClass
                TestMethod = $testMethod
                TestedClass = $testedClass
                TestedMethod = $testedMethod
                Status = "FAILED"
                Duration = $duration
            }
        }
    }
}

# Zeige Zusammenfassung
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Zusammenfassung" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$passed = ($testResults | Where-Object { $_.Status -eq "PASSED" }).Count
$failed = ($testResults | Where-Object { $_.Status -eq "FAILED" }).Count
$total = $testResults.Count

Write-Host "Gesamt:     " -NoNewline -ForegroundColor White
Write-Host "$total Tests" -ForegroundColor White

Write-Host "Bestanden:  " -NoNewline -ForegroundColor White
Write-Host "$passed" -ForegroundColor Green -NoNewline
Write-Host " Tests" -ForegroundColor Green

if ($failed -gt 0) {
    Write-Host "Fehlgeschlagen: " -NoNewline -ForegroundColor White
    Write-Host "$failed" -ForegroundColor Red -NoNewline
    Write-Host " Tests" -ForegroundColor Red
}

# Zeige getestete Klassen
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Getestete Klassen" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$testedClasses = $testResults | Where-Object { $_.TestedClass -ne "" } | 
    Select-Object -Unique TestedClass | 
    Sort-Object TestedClass

foreach ($class in $testedClasses) {
    $classTests = $testResults | Where-Object { $_.TestedClass -eq $class.TestedClass }
    $passedCount = ($classTests | Where-Object { $_.Status -eq "PASSED" }).Count
    $totalCount = $classTests.Count
    
    Write-Host "[$($class.TestedClass)]" -ForegroundColor Magenta
    Write-Host "  Tests: $totalCount (Bestanden: $passedCount)" -ForegroundColor Gray
    
    # Gruppiere nach getesteten Methoden
    $methods = $classTests | Select-Object -Unique TestedMethod | Sort-Object TestedMethod
    foreach ($method in $methods) {
        if ($method.TestedMethod -ne "") {
            $methodTests = $classTests | Where-Object { $_.TestedMethod -eq $method.TestedMethod }
            $methodPassed = ($methodTests | Where-Object { $_.Status -eq "PASSED" }).Count
            
            Write-Host "    -> $($method.TestedMethod)()" -ForegroundColor Yellow
            Write-Host "       $methodPassed/$($methodTests.Count) Tests bestanden" -ForegroundColor Gray
            
            # Zeige einzelne Test-Szenarien
            foreach ($test in $methodTests | Sort-Object TestMethod) {
                $status = if ($test.Status -eq "PASSED") { "[PASS]" } else { "[FAIL]" }
                $color = if ($test.Status -eq "PASSED") { "Green" } else { "Red" }
                
                # Extrahiere Szenario aus Test-Methoden-Namen
                $scenario = $test.TestMethod -replace "^$($test.TestedMethod)_", ""
                Write-Host "         $status $scenario ($($test.Duration))" -ForegroundColor $color
            }
        }
    }
    Write-Host ""
}

# Zeige Tests ohne zugeordnete Klasse
$unmappedTests = $testResults | Where-Object { $_.TestedClass -eq "" }
if ($unmappedTests.Count -gt 0) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Weitere Tests" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
    
    foreach ($test in $unmappedTests | Sort-Object TestClass, TestMethod) {
        $status = if ($test.Status -eq "PASSED") { "[PASS]" } else { "[FAIL]" }
        $color = if ($test.Status -eq "PASSED") { "Green" } else { "Red" }
        
        Write-Host "$status " -NoNewline -ForegroundColor $color
        Write-Host "$($test.TestClass).$($test.TestMethod) ($($test.Duration))" -ForegroundColor White
    }
    Write-Host ""
}

# Zeige Reports
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Test-Reports" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$htmlFile = Join-Path $testResultsDir "test-results.html"
if (Test-Path $htmlFile) {
    Write-Host "HTML Report: " -NoNewline -ForegroundColor White
    Write-Host $htmlFile -ForegroundColor Cyan
    
    if ($OpenHtml) {
        Write-Host "`nOeffne HTML Report..." -ForegroundColor Yellow
        Start-Process $htmlFile
    }
}

Write-Host ""


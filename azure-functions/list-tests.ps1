# Zeigt eine übersichtliche Liste aller Tests mit Status
# Verwendet dotnet test und parst die Ausgabe

param(
    [switch]$OpenHtml
)

$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Unit Test Uebersicht" -ForegroundColor Cyan
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
$inTestSection = $false

foreach ($line in $output) {
    if ($line -match "Bestanden\s+(.+?)\s+\[(.+?)\]") {
        $testName = $matches[1].Trim()
        $duration = $matches[2].Trim()
        $testResults += [PSCustomObject]@{
            Name = $testName
            Status = "PASSED"
            Duration = $duration
        }
    }
    elseif ($line -match "Fehlgeschlagen\s+(.+?)\s+\[(.+?)\]") {
        $testName = $matches[1].Trim()
        $duration = $matches[2].Trim()
        $testResults += [PSCustomObject]@{
            Name = $testName
            Status = "FAILED"
            Duration = $duration
        }
    }
    elseif ($line -match "Gesamtzahl Tests:\s+(\d+)") {
        $totalTests = [int]$matches[1]
    }
    elseif ($line -match "Bestanden:\s+(\d+)") {
        $passedTests = [int]$matches[1]
    }
    elseif ($line -match "fehlgeschlagen:\s+(\d+)") {
        $failedTests = [int]$matches[1]
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

# Gruppiere nach Test-Klasse
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test-Details (nach Klasse)" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$grouped = $testResults | Group-Object {
    if ($_.Name -match "^(.+?)\.(.+?)\.") {
        $matches[1] + "." + $matches[2]
    } else {
        "Other"
    }
} | Sort-Object Name

foreach ($group in $grouped) {
    Write-Host "[$($group.Name)]" -ForegroundColor Magenta
    
    foreach ($test in $group.Group | Sort-Object Name) {
        $status = if ($test.Status -eq "PASSED") { "[PASS]" } else { "[FAIL]" }
        $color = if ($test.Status -eq "PASSED") { "Green" } else { "Red" }
        
        $methodName = $test.Name -replace "^.+\.", ""
        Write-Host "  $status " -NoNewline -ForegroundColor $color
        Write-Host $methodName -ForegroundColor White -NoNewline
        Write-Host " ($($test.Duration))" -ForegroundColor Gray
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


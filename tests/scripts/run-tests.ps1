# PowerShell Script für übersichtliche Test-Ausgabe
# Zeigt eine übersichtliche Liste aller Tests mit Status

param(
    [Parameter(Mandatory = $false)]
    [switch]$Detailed,
    
    [Parameter(Mandatory = $false)]
    [switch]$Html,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "TestResults"
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Unit Test Report" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Wechsle zum Functions-Verzeichnis
$originalLocation = Get-Location
$testProjectPath = Join-Path $PSScriptRoot "main.Core.Tests"

if (-not (Test-Path $testProjectPath)) {
    Write-Host "Test-Projekt nicht gefunden: $testProjectPath" -ForegroundColor Red
    exit 1
}

Set-Location $testProjectPath

# Erstelle Output-Verzeichnis
$outputDir = Join-Path $PSScriptRoot $OutputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# Führe Tests aus und sammle Ergebnisse
Write-Host "Führe Tests aus..." -ForegroundColor Yellow
$testResults = dotnet test --logger "console;verbosity=detailed" --logger "trx;LogFileName=test-results.trx" --logger "html;LogFileName=test-results.html" --results-directory $outputDir 2>&1

# Parse Test-Ergebnisse
$passedTests = @()
$failedTests = @()
$skippedTests = @()

$currentTest = $null
$inTestOutput = $false

foreach ($line in $testResults) {
    if ($line -match "Bestanden\s+(.+?)\s+\[(\d+)\s+ms\]") {
        $testName = $matches[1].Trim()
        $duration = $matches[2]
        $passedTests += [PSCustomObject]@{
            Name = $testName
            Status = "PASSED"
            Duration = "$duration ms"
        }
    }
    elseif ($line -match "Fehlgeschlagen\s+(.+?)\s+\[(\d+)\s+ms\]") {
        $testName = $matches[1].Trim()
        $duration = $matches[2]
        $failedTests += [PSCustomObject]@{
            Name = $testName
            Status = "FAILED"
            Duration = "$duration ms"
        }
    }
    elseif ($line -match "Übersprungen\s+(.+?)") {
        $testName = $matches[1].Trim()
        $skippedTests += [PSCustomObject]@{
            Name = $testName
            Status = "SKIPPED"
            Duration = "N/A"
        }
    }
}

# Zeige Zusammenfassung
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test-Zusammenfassung" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$totalTests = $passedTests.Count + $failedTests.Count + $skippedTests.Count
$passedCount = $passedTests.Count
$failedCount = $failedTests.Count
$skippedCount = $skippedTests.Count

Write-Host "Gesamt:     " -NoNewline -ForegroundColor White
Write-Host "$totalTests Tests" -ForegroundColor White

Write-Host "Bestanden:  " -NoNewline -ForegroundColor White
Write-Host "$passedCount" -ForegroundColor Green -NoNewline
Write-Host " Tests" -ForegroundColor Green

if ($failedCount -gt 0) {
    Write-Host "Fehlgeschlagen: " -NoNewline -ForegroundColor White
    Write-Host "$failedCount" -ForegroundColor Red -NoNewline
    Write-Host " Tests" -ForegroundColor Red
}

if ($skippedCount -gt 0) {
    Write-Host "Übersprungen:  " -NoNewline -ForegroundColor White
    Write-Host "$skippedCount" -ForegroundColor Yellow -NoNewline
    Write-Host " Tests" -ForegroundColor Yellow
}

# Zeige detaillierte Liste
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test-Details" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Gruppiere Tests nach Klasse
$allTests = $passedTests + $failedTests + $skippedTests
$groupedTests = $allTests | Group-Object { 
    if ($_.Name -match "^(.+?)\.(.+?)\.") {
        $matches[1] + "." + $matches[2]
    } else {
        "Other"
    }
}

foreach ($group in $groupedTests | Sort-Object Name) {
    Write-Host "`n[$($group.Name)]" -ForegroundColor Magenta
    
    foreach ($test in $group.Group | Sort-Object Name) {
        $statusSymbol = switch ($test.Status) {
            "PASSED" { "[PASS]" }
            "FAILED" { "[FAIL]" }
            "SKIPPED" { "[SKIP]" }
            default { "[?]" }
        }
        
        $statusColor = switch ($test.Status) {
            "PASSED" { "Green" }
            "FAILED" { "Red" }
            "SKIPPED" { "Yellow" }
            default { "White" }
        }
        
        $testMethodName = $test.Name -replace "^.+\.", ""
        Write-Host "  $statusSymbol " -NoNewline -ForegroundColor $statusColor
        Write-Host $testMethodName -ForegroundColor White -NoNewline
        Write-Host " ($($test.Duration))" -ForegroundColor Gray
    }
}

# Zeige fehlgeschlagene Tests mit Details
if ($failedTests.Count -gt 0) {
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host "  Fehlgeschlagene Tests" -ForegroundColor Red
    Write-Host "========================================`n" -ForegroundColor Red
    
    foreach ($test in $failedTests) {
        Write-Host "[FAIL] $($test.Name)" -ForegroundColor Red
    }
}

# Zeige Dateipfade
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test-Reports" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$trxFile = Join-Path $outputDir "test-results.trx"
$htmlFile = Join-Path $outputDir "test-results.html"

if (Test-Path $trxFile) {
    Write-Host "TRX Report:  " -NoNewline -ForegroundColor White
    Write-Host $trxFile -ForegroundColor Cyan
}

if (Test-Path $htmlFile) {
    Write-Host "HTML Report: " -NoNewline -ForegroundColor White
    Write-Host $htmlFile -ForegroundColor Cyan
    Write-Host "`nOeffne HTML Report..." -ForegroundColor Yellow
    Start-Process $htmlFile
}

# Exit-Code basierend auf Test-Ergebnissen
Set-Location $originalLocation

if ($failedCount -gt 0) {
    exit 1
} else {
    exit 0
}


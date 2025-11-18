# Generiert einen übersichtlichen HTML Test-Report mit Code Coverage
# Verwendet ReportGenerator für professionelle Reports

param(
    [Parameter(Mandatory = $false)]
    [switch]$OpenReport,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputDir = "TestResults"
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test Report Generator" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$originalLocation = Get-Location
$testProjectPath = Join-Path $PSScriptRoot "main.Core.Tests"
$outputBaseDir = Join-Path $PSScriptRoot $OutputDir
$coverageDir = Join-Path $outputBaseDir "coverage"
$reportDir = Join-Path $outputBaseDir "report"

Set-Location $testProjectPath

# Erstelle Verzeichnisse
if (-not (Test-Path $coverageDir)) {
    New-Item -ItemType Directory -Path $coverageDir -Force | Out-Null
}
if (-not (Test-Path $reportDir)) {
    New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
}

# Führe Tests mit Code Coverage aus
Write-Host "Führe Tests mit Code Coverage aus..." -ForegroundColor Yellow
dotnet test `
    --collect:"XPlat Code Coverage" `
    --results-directory $coverageDir `
    --logger "trx;LogFileName=test-results.trx" `
    --logger "html;LogFileName=test-results.html;Verbosity=Detailed" `
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests fehlgeschlagen!" -ForegroundColor Red
    Set-Location $originalLocation
    exit $LASTEXITCODE
}

# Finde Coverage-Datei
$coverageFile = Get-ChildItem -Path $coverageDir -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1

if ($coverageFile) {
    Write-Host "`nGeneriere HTML Report mit Code Coverage..." -ForegroundColor Yellow
    
    # Verwende ReportGenerator falls verfügbar
    $reportGenPath = Join-Path $testProjectPath "packages\reportgenerator\*\tools\ReportGenerator.exe"
    if (Test-Path $reportGenPath) {
        & $reportGenPath `
            -reports:"$($coverageFile.FullName)" `
            -targetdir:"$reportDir" `
            -reporttypes:"Html;Badges" `
            -classfilters:"-*Tests*"
    } else {
        Write-Host "ReportGenerator nicht gefunden. Installiere..." -ForegroundColor Yellow
        dotnet tool install -g dotnet-reportgenerator-globaltool
        reportgenerator `
            -reports:"$($coverageFile.FullName)" `
            -targetdir:"$reportDir" `
            -reporttypes:"Html;Badges" `
            -classfilters:"-*Tests*"
    }
    
    Write-Host "`n[OK] Report generiert!" -ForegroundColor Green
    Write-Host "Report-Verzeichnis: $reportDir" -ForegroundColor Cyan
    
    $indexFile = Join-Path $reportDir "index.html"
    if (Test-Path $indexFile) {
        Write-Host "HTML Report: $indexFile" -ForegroundColor Cyan
        
        if ($OpenReport) {
            Start-Process $indexFile
        }
    }
} else {
    Write-Host "Coverage-Datei nicht gefunden. Generiere einfachen HTML Report..." -ForegroundColor Yellow
}

# Zeige HTML Test-Report
$htmlTestFile = Join-Path $coverageDir "test-results.html"
if (Test-Path $htmlTestFile) {
    Write-Host "`nTest HTML Report: $htmlTestFile" -ForegroundColor Cyan
    if ($OpenReport) {
        Start-Process $htmlTestFile
    }
}

Set-Location $originalLocation

Write-Host "`n[OK] Fertig!" -ForegroundColor Green


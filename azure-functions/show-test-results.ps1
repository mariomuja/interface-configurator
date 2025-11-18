# Einfaches Script zur Anzeige von Test-Ergebnissen
# Verwendet dotnet test mit HTML-Output

param(
    [switch]$OpenHtml
)

$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Unit Test Report" -ForegroundColor Cyan  
Write-Host "========================================`n" -ForegroundColor Cyan

# Erstelle TestResults Verzeichnis
$testResultsDir = Join-Path $PSScriptRoot "TestResults"
if (-not (Test-Path $testResultsDir)) {
    New-Item -ItemType Directory -Path $testResultsDir | Out-Null
}

# Führe Tests aus
$testProject = Join-Path $PSScriptRoot "main.Core.Tests\main.Core.Tests.csproj"

Write-Host "Fuehre Tests aus..." -ForegroundColor Yellow
Write-Host ""

dotnet test $testProject `
    --logger "console;verbosity=normal" `
    --logger "html;LogFileName=test-results.html;Verbosity=Detailed" `
    --logger "trx;LogFileName=test-results.trx" `
    --results-directory $testResultsDir

$exitCode = $LASTEXITCODE

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Test-Reports" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$htmlFile = Join-Path $testResultsDir "test-results.html"
$trxFile = Join-Path $testResultsDir "test-results.trx"

if (Test-Path $htmlFile) {
    Write-Host "HTML Report: " -NoNewline -ForegroundColor White
    Write-Host $htmlFile -ForegroundColor Cyan
    
    if ($OpenHtml) {
        Write-Host "Oeffne HTML Report..." -ForegroundColor Yellow
        Start-Process $htmlFile
    }
}

if (Test-Path $trxFile) {
    Write-Host "TRX Report:  " -NoNewline -ForegroundColor White
    Write-Host $trxFile -ForegroundColor Cyan
}

Write-Host ""

exit $exitCode










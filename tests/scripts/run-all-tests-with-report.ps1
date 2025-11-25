# Comprehensive test execution script with Visual Studio-like reporting
# Generates test reports and stores them for UI access

param(
    [Parameter(Mandatory = $false)]
    [switch]$OpenReport,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputDir = "TestResults"
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Comprehensive Test Execution" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$originalLocation = Get-Location
$testProjectPath = Join-Path $PSScriptRoot "main.Core.Tests"
$outputBaseDir = Join-Path $PSScriptRoot $OutputDir
$coverageDir = Join-Path $outputBaseDir "coverage"
$reportDir = Join-Path $outputBaseDir "report"
$latestReportDir = Join-Path $outputBaseDir "latest"

Set-Location $testProjectPath

# Create directories
@($coverageDir, $reportDir, $latestReportDir) | ForEach-Object {
    if (-not (Test-Path $_)) {
        New-Item -ItemType Directory -Path $_ -Force | Out-Null
    }
}

# Run tests with coverage
Write-Host "Running all tests with code coverage..." -ForegroundColor Yellow
$testOutput = dotnet test `
    --collect:"XPlat Code Coverage" `
    --results-directory $coverageDir `
    --logger "trx;LogFileName=test-results.trx" `
    --logger "html;LogFileName=test-results.html;Verbosity=Detailed" `
    --verbosity normal `
    2>&1

$testOutput | Write-Host

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nTests completed with errors (Exit Code: $LASTEXITCODE)" -ForegroundColor Yellow
}

# Find latest TRX file
$latestTrxFile = Get-ChildItem -Path $coverageDir -Recurse -Filter "*.trx" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1

if ($latestTrxFile) {
    Write-Host "`nFound test results: $($latestTrxFile.FullName)" -ForegroundColor Green
    
    # Copy to latest directory
    Copy-Item $latestTrxFile.FullName -Destination (Join-Path $latestReportDir "test-results.trx") -Force
    
    # Generate Visual Studio-like report
    Write-Host "`nGenerating Visual Studio-like test report..." -ForegroundColor Yellow
    $report = GenerateVisualStudioLikeReport -TrxFile $latestTrxFile.FullName
    
    # Save report as JSON
    $reportJson = $report | ConvertTo-Json -Depth 10
    $reportJsonPath = Join-Path $latestReportDir "test-report.json"
    $reportJson | Out-File -FilePath $reportJsonPath -Encoding UTF8
    
    Write-Host "`nTest Report Summary:" -ForegroundColor Cyan
    Write-Host "  Total Tests: $($report.TotalTests)" -ForegroundColor White
    Write-Host "  Passed: $($report.PassedTests)" -ForegroundColor Green
    Write-Host "  Failed: $($report.FailedTests)" -ForegroundColor Red
    Write-Host "  Skipped: $($report.SkippedTests)" -ForegroundColor Yellow
    Write-Host "  Pass Rate: $([math]::Round($report.PassRate, 2))%" -ForegroundColor $(if ($report.PassRate -ge 80) { "Green" } else { "Yellow" })
    Write-Host "  Report Path: $reportJsonPath" -ForegroundColor Cyan
}

# Generate coverage report if available
$coverageFile = Get-ChildItem -Path $coverageDir -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1
if ($coverageFile) {
    Write-Host "`nGenerating code coverage report..." -ForegroundColor Yellow
    
    # Try to use ReportGenerator
    $reportGenCmd = Get-Command reportgenerator -ErrorAction SilentlyContinue
    if (-not $reportGenCmd) {
        Write-Host "Installing ReportGenerator..." -ForegroundColor Yellow
        dotnet tool install -g dotnet-reportgenerator-globaltool --quiet
    }
    
    reportgenerator `
        -reports:"$($coverageFile.FullName)" `
        -targetdir:"$reportDir" `
        -reporttypes:"Html;Badges" `
        -classfilters:"-*Tests*" `
        2>&1 | Out-Null
    
    Write-Host "Coverage report generated: $reportDir\index.html" -ForegroundColor Green
}

Set-Location $originalLocation

Write-Host "`n[OK] Test execution completed!" -ForegroundColor Green
Write-Host "Latest report available at: $latestReportDir" -ForegroundColor Cyan

if ($OpenReport) {
    $htmlFile = Join-Path $coverageDir "test-results.html"
    if (Test-Path $htmlFile) {
        Start-Process $htmlFile
    }
}

function GenerateVisualStudioLikeReport {
    param([string]$TrxFile)
    
    $xml = [xml](Get-Content $TrxFile)
    $report = @{
        ReportPath = $TrxFile
        GeneratedAt = (Get-Item $TrxFile).LastWriteTime
        TotalTests = 0
        PassedTests = 0
        FailedTests = 0
        SkippedTests = 0
        TestResults = @()
    }
    
    $testResults = $xml.SelectNodes("//UnitTestResult")
    foreach ($result in $testResults) {
        $testName = $result.testName
        $outcome = $result.outcome
        $duration = $result.duration
        $startTime = $result.startTime
        
        $testResult = @{
            TestName = $testName
            Outcome = $outcome
            Duration = $duration
            StartTime = [DateTime]::Parse($startTime)
            ErrorMessage = $result.SelectSingleNode(".//Message")?.InnerText
            StackTrace = $result.SelectSingleNode(".//StackTrace")?.InnerText
        }
        
        $report.TestResults += $testResult
        $report.TotalTests++
        
        switch ($outcome) {
            "Passed" { $report.PassedTests++ }
            "Failed" { $report.FailedTests++ }
            "NotExecuted" { $report.SkippedTests++ }
        }
    }
    
    $report.PassRate = if ($report.TotalTests -gt 0) { 
        [math]::Round(($report.PassedTests / $report.TotalTests) * 100, 2) 
    } else { 0 }
    
    return $report
}


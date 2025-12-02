# CI/CD Test Integration Script (PowerShell)
# This script runs tests in CI/CD environments

param(
    [string]$TestType = "all",
    [int]$CoverageThreshold = 70,
    [switch]$Parallel = $true
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting CI/CD Test Execution" -ForegroundColor Green

# Function to run unit tests
function Run-UnitTests {
    Write-Host "📦 Running Unit Tests..." -ForegroundColor Cyan
    Set-Location frontend
    
    try {
        if ($Parallel) {
            npm test -- --code-coverage --browsers=ChromeHeadless --watch=false
        } else {
            npm test -- --code-coverage --browsers=ChromeHeadless --watch=false --single-run
        }
        
        # Check coverage
        $coveragePath = "coverage/interface-configurator/coverage-summary.json"
        if (Test-Path $coveragePath) {
            $coverage = Get-Content $coveragePath | ConvertFrom-Json
            $linesCoverage = $coverage.total.lines.pct
            
            if ($linesCoverage -lt $CoverageThreshold) {
                Write-Host "✗ Coverage ${linesCoverage}% is below threshold ${CoverageThreshold}%" -ForegroundColor Red
                exit 1
            } else {
                Write-Host "✓ Coverage ${linesCoverage}% meets threshold ${CoverageThreshold}%" -ForegroundColor Green
            }
        }
    } finally {
        Set-Location ..
    }
}

# Function to run E2E tests
function Run-E2ETests {
    Write-Host "🌐 Running E2E Tests..." -ForegroundColor Cyan
    
    # Install Playwright browsers if needed
    npx playwright install --with-deps chromium
    
    # Run E2E tests
    npm run test:e2e
    
    Write-Host "✓ E2E tests completed" -ForegroundColor Green
}

# Function to generate test report
function Generate-TestReport {
    Write-Host "📊 Generating Test Report..." -ForegroundColor Cyan
    
    # Create reports directory
    New-Item -ItemType Directory -Force -Path test-reports | Out-Null
    
    # Copy coverage reports
    if (Test-Path "frontend/coverage") {
        Copy-Item -Path "frontend/coverage" -Destination "test-reports/coverage" -Recurse -Force
    }
    
    # Copy Playwright reports
    if (Test-Path "test-results") {
        Copy-Item -Path "test-results" -Destination "test-reports/test-results" -Recurse -Force
    }
    
    Write-Host "✓ Test report generated in test-reports/" -ForegroundColor Green
}

# Main execution
try {
    switch ($TestType) {
        "unit" {
            Run-UnitTests
        }
        "e2e" {
            Run-E2ETests
        }
        "all" {
            Run-UnitTests
            Run-E2ETests
        }
        default {
            Write-Host "✗ Unknown test type: $TestType" -ForegroundColor Red
            Write-Host "⚠ Valid types: unit, e2e, all" -ForegroundColor Yellow
            exit 1
        }
    }
    
    Generate-TestReport
    
    Write-Host "✓ All tests completed successfully!" -ForegroundColor Green
} catch {
    Write-Host "✗ Test execution failed: $_" -ForegroundColor Red
    exit 1
}

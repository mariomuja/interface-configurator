# Verifiziert die Package-Struktur fuer JavaScript Functions

param(
    [string]$PackagePath = "deploy-package.zip"
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Package Structure Verification" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if (-not (Test-Path $PackagePath)) {
    Write-Host "ERROR: Package not found: $PackagePath" -ForegroundColor Red
    exit 1
}

$tempDir = Join-Path $env:TEMP "verify-package-$(Get-Random)"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    Write-Host "Extracting package..." -ForegroundColor Yellow
    Expand-Archive -Path $PackagePath -DestinationPath $tempDir -Force
    
    Write-Host "`nPackage Structure:" -ForegroundColor Cyan
    Get-ChildItem -Path $tempDir -Recurse | ForEach-Object {
        $relativePath = $_.FullName.Replace($tempDir, "").TrimStart('\')
        $icon = if ($_.PSIsContainer) { "[DIR]" } else { "[FILE]" }
        Write-Host "  $icon $relativePath" -ForegroundColor $(if ($_.PSIsContainer) { "Yellow" } else { "White" })
    }
    
    Write-Host "`nRequired Files Check:" -ForegroundColor Cyan
    
    $requiredFiles = @(
        "host.json",
        "package.json",
        "SimpleTestFunction\function.json",
        "SimpleTestFunction\index.js"
    )
    
    $allPresent = $true
    foreach ($file in $requiredFiles) {
        $fullPath = Join-Path $tempDir $file
        if (Test-Path $fullPath) {
            Write-Host "  [OK] $file" -ForegroundColor Green
        } else {
            Write-Host "  [MISSING] $file" -ForegroundColor Red
            $allPresent = $false
        }
    }
    
    Write-Host "`nChecking for unwanted files:" -ForegroundColor Cyan
    
    # Check for C# DLLs
    $dlls = Get-ChildItem -Path $tempDir -Recurse -Filter "*.dll" -ErrorAction SilentlyContinue
    if ($dlls) {
        Write-Host "  [WARNING] Found C# DLLs (should not be in JavaScript function package):" -ForegroundColor Yellow
        foreach ($dll in $dlls) {
            $relativePath = $dll.FullName.Replace($tempDir, "").TrimStart('\')
            Write-Host "     - $relativePath" -ForegroundColor Gray
        }
    } else {
        Write-Host "  [OK] No C# DLLs found" -ForegroundColor Green
    }
    
    # Check host.json
    $hostJsonPath = Join-Path $tempDir "host.json"
    if (Test-Path $hostJsonPath) {
        Write-Host "`nhost.json content:" -ForegroundColor Cyan
        $hostJson = Get-Content $hostJsonPath -Raw | ConvertFrom-Json
        Write-Host "  Version: $($hostJson.version)" -ForegroundColor Gray
        Write-Host "  Extension Bundle: $($hostJson.extensionBundle.id) $($hostJson.extensionBundle.version)" -ForegroundColor Gray
        
        if ($hostJson.extensionBundle) {
            Write-Host "  [OK] Extension Bundle configured (required for JavaScript)" -ForegroundColor Green
        } else {
            Write-Host "  [WARNING] Extension Bundle missing (may cause issues)" -ForegroundColor Yellow
        }
    }
    
    # Check function.json
    $functionJsonPath = Join-Path $tempDir "SimpleTestFunction\function.json"
    if (Test-Path $functionJsonPath) {
        Write-Host "`nfunction.json content:" -ForegroundColor Cyan
        $functionJson = Get-Content $functionJsonPath -Raw | ConvertFrom-Json
        Write-Host "  Script File: $($functionJson.scriptFile)" -ForegroundColor Gray
        Write-Host "  Bindings: $($functionJson.bindings.Count)" -ForegroundColor Gray
        
        $httpTrigger = $functionJson.bindings | Where-Object { $_.type -eq "httpTrigger" }
        if ($httpTrigger) {
            Write-Host "  [OK] HTTP Trigger configured" -ForegroundColor Green
        } else {
            Write-Host "  [ERROR] HTTP Trigger missing" -ForegroundColor Red
        }
    }
    
    if ($allPresent) {
        Write-Host "`n[OK] Package structure is correct!" -ForegroundColor Green
    } else {
        Write-Host "`n[ERROR] Package structure has issues!" -ForegroundColor Red
        exit 1
    }
    
} finally {
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""


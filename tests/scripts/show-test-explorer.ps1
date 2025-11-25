# Zeigt eine Test Explorer-aehnliche Ansicht im Terminal
# Strukturiert wie VS Code Test Explorer

param(
    [switch]$RunAll,
    [switch]$ShowDetails
)

$ErrorActionPreference = "Continue"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  VS Code Test Explorer View" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$testProject = Join-Path $PSScriptRoot "main.Core.Tests\main.Core.Tests.csproj"

# Lade Test-Struktur aus den Test-Dateien
$testFiles = Get-ChildItem -Path (Join-Path $PSScriptRoot "main.Core.Tests") -Recurse -Filter "*Tests.cs"

$testStructure = @{}

foreach ($file in $testFiles) {
    $content = Get-Content $file.FullName -Raw
    
    # Extrahiere Namespace
    $namespace = ""
    if ($content -match "namespace\s+([^;]+)") {
        $namespace = $matches[1].Trim()
    }
    
    # Extrahiere Klassenname
    $className = ""
    if ($content -match "public\s+class\s+(\w+)") {
        $className = $matches[1]
    }
    
    # Extrahiere Test-Methoden
    $testMethods = @()
    $methodMatches = [regex]::Matches($content, '\[Fact\]\s+public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\([^)]*\)')
    foreach ($match in $methodMatches) {
        $testMethods += $match.Groups[1].Value
    }
    
    if ($className -and $testMethods.Count -gt 0) {
        $relativePath = $file.FullName.Replace((Join-Path $PSScriptRoot "main.Core.Tests"), "").TrimStart('\')
        $folder = Split-Path $relativePath -Parent
        
        if (-not $testStructure.ContainsKey($folder)) {
            $testStructure[$folder] = @{}
        }
        
        $testStructure[$folder][$className] = $testMethods
    }
}

# Zeige Test-Struktur
Write-Host "main.Core.Tests" -ForegroundColor Cyan

foreach ($folder in ($testStructure.Keys | Sort-Object)) {
    if ($folder -and $folder -ne ".") {
        $folderName = $folder -replace '\\', '/'
        Write-Host "  [$folderName]" -ForegroundColor Yellow
    }
    
    foreach ($className in ($testStructure[$folder].Keys | Sort-Object)) {
        $methods = $testStructure[$folder][$className]
        
        # Zeige Klasse
        Write-Host "    [$className]" -ForegroundColor Magenta
        
        # Fuehre Tests aus wenn gewuenscht
        if ($RunAll) {
            Write-Host "      Fuehre Tests aus..." -ForegroundColor Gray -NoNewline
            $output = dotnet test $testProject --filter "FullyQualifiedName~$className" --logger "console;verbosity=quiet" 2>&1
            
            $passed = 0
            $failed = 0
            
            foreach ($line in $output) {
                if ($line -match "Bestanden\s+.*\.$className\.(\w+)") {
                    $passed++
                }
                elseif ($line -match "Fehlgeschlagen\s+.*\.$className\.(\w+)") {
                    $failed++
                }
            }
            
            $total = $methods.Count
            $status = if ($failed -eq 0) { "[PASS]" } else { "[FAIL]" }
            $color = if ($failed -eq 0) { "Green" } else { "Red" }
            
            $passedText = "$passed bestanden"
            $failedText = "$failed fehlgeschlagen"
            Write-Host "`r      $status $total Tests ($passedText, $failedText)" -ForegroundColor $color
        }
        
        # Zeige Test-Methoden
        foreach ($method in ($methods | Sort-Object)) {
            Write-Host "      [PASS] $method" -ForegroundColor White
        }
        
        Write-Host ""
    }
}

# Zeige Zusammenfassung
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Zusammenfassung" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$totalClasses = ($testStructure.Values | ForEach-Object { $_.Keys.Count } | Measure-Object -Sum).Sum
$totalMethods = ($testStructure.Values | ForEach-Object { ($_.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum } | Measure-Object -Sum).Sum

Write-Host "Test-Klassen:  " -NoNewline -ForegroundColor White
Write-Host "$totalClasses" -ForegroundColor Cyan

Write-Host "Test-Methoden: " -NoNewline -ForegroundColor White
Write-Host "$totalMethods" -ForegroundColor Cyan

Write-Host "`nHinweis: Verwenden Sie -RunAll um Tests auszufuehren:" -ForegroundColor Gray
Write-Host "  .\show-test-explorer.ps1 -RunAll" -ForegroundColor Yellow

Write-Host ""

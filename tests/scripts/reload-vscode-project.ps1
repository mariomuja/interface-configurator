# Script to help reload VS Code project and clear OmniSharp cache
# Run this if VS Code shows errors but dotnet build succeeds

Write-Host "Reloading VS Code project..." -ForegroundColor Cyan
Write-Host ""

# Clean and rebuild
Write-Host "1. Cleaning project..." -ForegroundColor Yellow
Set-Location "C:\Users\mario\interface-configurator\azure-functions\main"
dotnet clean | Out-Null

Write-Host "2. Restoring packages..." -ForegroundColor Yellow
dotnet restore | Out-Null

Write-Host "3. Building project..." -ForegroundColor Yellow
$buildOutput = dotnet build 2>&1
$errors = $buildOutput | Select-String -Pattern "error"

if ($errors) {
    Write-Host "Build errors found:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host $_ -ForegroundColor Red }
} else {
    Write-Host "✅ Build successful - no errors!" -ForegroundColor Green
    Write-Host ""
    Write-Host "If VS Code still shows errors, try:" -ForegroundColor Yellow
    Write-Host "  1. Reload VS Code window: Ctrl+Shift+P -> 'Developer: Reload Window'" -ForegroundColor Cyan
    Write-Host "  2. Restart OmniSharp: Ctrl+Shift+P -> 'OmniSharp: Restart OmniSharp'" -ForegroundColor Cyan
    Write-Host "  3. Clear OmniSharp cache: Delete .omnisharp folder in workspace root" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green


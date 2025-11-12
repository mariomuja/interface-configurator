# Terraform Installation Script for Windows
# Downloads and installs Terraform

$terraformVersion = "1.6.6"
$downloadUrl = "https://releases.hashicorp.com/terraform/${terraformVersion}/terraform_${terraformVersion}_windows_amd64.zip"
$installPath = "$env:LOCALAPPDATA\terraform"
$zipPath = "$env:TEMP\terraform.zip"

Write-Host "Downloading Terraform ${terraformVersion}..." -ForegroundColor Green
Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath

Write-Host "Extracting Terraform..." -ForegroundColor Green
if (-not (Test-Path $installPath)) {
    New-Item -ItemType Directory -Path $installPath | Out-Null
}
Expand-Archive -Path $zipPath -DestinationPath $installPath -Force

Write-Host "Adding Terraform to PATH..." -ForegroundColor Green
$currentPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($currentPath -notlike "*$installPath*") {
    [Environment]::SetEnvironmentVariable("Path", "$currentPath;$installPath", "User")
    $env:Path += ";$installPath"
}

Write-Host "Terraform installed successfully!" -ForegroundColor Green
Write-Host "Please restart your terminal or run: `$env:Path += ';$installPath'" -ForegroundColor Yellow

# Verify installation
& "$installPath\terraform.exe" version




# Migriert Azure-Ressourcen zu neuen beschreibenden Namen

param(
    [string]$ResourceGroup = "rg-interface-configuration",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  Azure Resource Migration" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if ($WhatIf) {
    Write-Host "WHAT-IF MODE: Keine Ressourcen werden geloescht oder erstellt" -ForegroundColor Yellow
    Write-Host ""
}

# Alte Ressourcen (mit random suffix)
$oldResources = @{
    "FunctionApp" = "func-apprigklebtsay2o"
    "AppServicePlan" = "plan-funcs-apprigklebtsay2o"
    "SqlServer" = "sql-infrastructurerigklebtsay2o"
    "FunctionsStorage" = "stfuncsapprigklebtsay2o"
}

# Neue Ressourcen (beschreibende Namen)
$newResources = @{
    "FunctionApp" = "func-integration"
    "AppServicePlan" = "plan-func-csv-processor"
    "SqlServer" = "sql-main-database"
    "FunctionsStorage" = "stfunc-csv-processor"
}

Write-Host "Alte Ressourcen (werden geloescht):" -ForegroundColor Yellow
foreach ($key in $oldResources.Keys) {
    Write-Host "  - $key : $($oldResources[$key])" -ForegroundColor Gray
}

Write-Host "`nNeue Ressourcen (werden erstellt):" -ForegroundColor Green
foreach ($key in $newResources.Keys) {
    Write-Host "  - $key : $($newResources[$key])" -ForegroundColor Gray
}

if ($WhatIf) {
    Write-Host "`nWHAT-IF: Wuerde alte Ressourcen loeschen und neue mit Terraform erstellen" -ForegroundColor Yellow
    exit 0
}

Write-Host "`nWICHTIG: Diese Migration loescht die alten Ressourcen!" -ForegroundColor Red
Write-Host "Stelle sicher, dass:" -ForegroundColor Yellow
Write-Host "  1. Alle Daten gesichert sind" -ForegroundColor Gray
Write-Host "  2. Terraform Config aktualisiert ist" -ForegroundColor Gray
Write-Host "  3. GitHub Secrets aktualisiert sind" -ForegroundColor Gray
Write-Host ""

$confirm = Read-Host "Moechten Sie fortfahren? (yes/no)"
if ($confirm -ne "yes") {
    Write-Host "Migration abgebrochen" -ForegroundColor Yellow
    exit 0
}

Write-Host "`nSchritt 1: Loesche alte Ressourcen..." -ForegroundColor Yellow

# Loesche Function App
Write-Host "  Loesche Function App: $($oldResources.FunctionApp)..." -ForegroundColor Gray
az functionapp delete --resource-group $ResourceGroup --name $oldResources.FunctionApp --output none 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "    [OK] Function App geloescht" -ForegroundColor Green
} else {
    Write-Host "    [WARNING] Function App konnte nicht geloescht werden (moeglicherweise bereits geloescht)" -ForegroundColor Yellow
}

# Loesche App Service Plan
Write-Host "  Loesche App Service Plan: $($oldResources.AppServicePlan)..." -ForegroundColor Gray
az appservice plan delete --resource-group $ResourceGroup --name $oldResources.AppServicePlan --yes --output none 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "    [OK] App Service Plan geloescht" -ForegroundColor Green
} else {
    Write-Host "    [WARNING] App Service Plan konnte nicht geloescht werden" -ForegroundColor Yellow
}

# Loesche SQL Server (loescht auch die Datenbanken)
Write-Host "  Loesche SQL Server: $($oldResources.SqlServer)..." -ForegroundColor Gray
Write-Host "    WARNUNG: Dies loescht auch alle Datenbanken auf diesem Server!" -ForegroundColor Red
az sql server delete --resource-group $ResourceGroup --name $oldResources.SqlServer --yes --output none 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "    [OK] SQL Server geloescht" -ForegroundColor Green
} else {
    Write-Host "    [WARNING] SQL Server konnte nicht geloescht werden" -ForegroundColor Yellow
}

# Loesche Storage Account
Write-Host "  Loesche Storage Account: $($oldResources.FunctionsStorage)..." -ForegroundColor Gray
az storage account delete --resource-group $ResourceGroup --name $oldResources.FunctionsStorage --yes --output none 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "    [OK] Storage Account geloescht" -ForegroundColor Green
} else {
    Write-Host "    [WARNING] Storage Account konnte nicht geloescht werden" -ForegroundColor Yellow
}

Write-Host "`nSchritt 2: Erstelle neue Ressourcen mit Terraform..." -ForegroundColor Yellow
Write-Host "  Fuehre 'terraform apply' aus..." -ForegroundColor Gray
Write-Host ""
Write-Host "Naechste Schritte:" -ForegroundColor Cyan
Write-Host "  1. cd terraform" -ForegroundColor Gray
Write-Host "  2. terraform plan  # Pruefe die Aenderungen" -ForegroundColor Gray
Write-Host "  3. terraform apply # Erstelle neue Ressourcen" -ForegroundColor Gray
Write-Host "  4. Aktualisiere GitHub Secret AZURE_FUNCTIONAPP_NAME auf: func-integration" -ForegroundColor Gray
Write-Host ""







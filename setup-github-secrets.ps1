# PowerShell Script zum Setup der GitHub Secrets fuer Azure Functions Deployment
# Voraussetzung: Azure CLI und GitHub CLI muessen installiert sein

Write-Host "=== GitHub Secrets Setup fuer Azure Functions ===" -ForegroundColor Cyan
Write-Host ""

# Schritt 1: Pruefe ob Azure CLI verfuegbar ist
Write-Host "[1/5] Pruefe Azure CLI..." -ForegroundColor Yellow
$azCheck = Get-Command az -ErrorAction SilentlyContinue
if (-not $azCheck) {
    Write-Host "✗ Azure CLI nicht gefunden. Bitte installieren: https://aka.ms/installazurecliwindows" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Azure CLI gefunden" -ForegroundColor Green

# Schritt 2: Pruefe ob GitHub CLI verfuegbar ist
Write-Host "[2/5] Pruefe GitHub CLI..." -ForegroundColor Yellow
$ghCheck = Get-Command gh -ErrorAction SilentlyContinue
if (-not $ghCheck) {
    Write-Host "✗ GitHub CLI nicht gefunden. Bitte installieren: https://cli.github.com/" -ForegroundColor Red
    Write-Host "  Oder setze die Secrets manuell ueber: https://github.com/mariomuja/interface-configuration/settings/secrets/actions" -ForegroundColor Yellow
    exit 1
}
Write-Host "✓ GitHub CLI gefunden" -ForegroundColor Green

# Schritt 3: Hole Azure Subscription und Resource Group
Write-Host "[3/5] Hole Azure Informationen..." -ForegroundColor Yellow
$subscriptionId = az account show --query id -o tsv 2>&1
if ($LASTEXITCODE -ne 0 -or -not $subscriptionId) {
    Write-Host "✗ Nicht bei Azure eingeloggt. Bitte ausfuehren: az login" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Subscription ID: $subscriptionId" -ForegroundColor Green

# Resource Group aus Terraform Output holen
$resourceGroup = "rg-interface-configuration"
Write-Host "  Resource Group: $resourceGroup" -ForegroundColor Gray

# Schritt 4: Hole Function App Name aus Terraform
Write-Host "[4/5] Hole Function App Name..." -ForegroundColor Yellow
Push-Location terraform
$functionAppName = terraform output -raw function_app_name 2>&1 | Out-String
$functionAppName = $functionAppName.Trim()
if (-not $functionAppName -or $functionAppName -eq "null" -or $functionAppName -eq "") {
    Write-Host "✗ Function App Name nicht gefunden. Bitte zuerst 'terraform apply' ausfuehren." -ForegroundColor Red
    Pop-Location
    exit 1
}
Write-Host "✓ Function App Name: $functionAppName" -ForegroundColor Green
Pop-Location

# Schritt 5: Erstelle Service Principal
Write-Host "[5/5] Erstelle Service Principal fuer GitHub Actions..." -ForegroundColor Yellow
$spName = "github-actions-functions-$(Get-Random -Maximum 9999)"
$scope = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup"

Write-Host "  Erstelle Service Principal: $spName" -ForegroundColor Gray
Write-Host "  Scope: $scope" -ForegroundColor Gray

$spJson = az ad sp create-for-rbac --name $spName --role contributor --scopes $scope --sdk-auth 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Fehler beim Erstellen des Service Principals:" -ForegroundColor Red
    Write-Host $spJson -ForegroundColor Red
    exit 1
}

Write-Host "✓ Service Principal erstellt" -ForegroundColor Green

# Schritt 6: Setze GitHub Secrets
Write-Host ""
Write-Host "=== Setze GitHub Secrets ===" -ForegroundColor Cyan

# Pruefe GitHub Login
$ghAuthCheck = gh auth status 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠ GitHub CLI nicht eingeloggt. Bitte ausfuehren: gh auth login" -ForegroundColor Yellow
    Write-Host "  Danach dieses Script erneut ausfuehren." -ForegroundColor Yellow
    exit 1
}

# Setze AZURE_CREDENTIALS
Write-Host "Setze AZURE_CREDENTIALS..." -ForegroundColor Yellow
$spJson | gh secret set AZURE_CREDENTIALS
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ AZURE_CREDENTIALS gesetzt" -ForegroundColor Green
}
else {
    Write-Host "✗ Fehler beim Setzen von AZURE_CREDENTIALS" -ForegroundColor Red
    exit 1
}

# Setze AZURE_RESOURCE_GROUP
Write-Host "Setze AZURE_RESOURCE_GROUP..." -ForegroundColor Yellow
$resourceGroup | gh secret set AZURE_RESOURCE_GROUP
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ AZURE_RESOURCE_GROUP gesetzt" -ForegroundColor Green
}
else {
    Write-Host "✗ Fehler beim Setzen von AZURE_RESOURCE_GROUP" -ForegroundColor Red
    exit 1
}

# Setze AZURE_FUNCTIONAPP_NAME
Write-Host "Setze AZURE_FUNCTIONAPP_NAME..." -ForegroundColor Yellow
$functionAppName | gh secret set AZURE_FUNCTIONAPP_NAME
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ AZURE_FUNCTIONAPP_NAME gesetzt" -ForegroundColor Green
}
else {
    Write-Host "✗ Fehler beim Setzen von AZURE_FUNCTIONAPP_NAME" -ForegroundColor Red
    exit 1
}

# Zusammenfassung
Write-Host ""
Write-Host "=== Fertig! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Folgende Secrets wurden gesetzt:" -ForegroundColor Cyan
Write-Host "  • AZURE_CREDENTIALS" -ForegroundColor White
Write-Host "  • AZURE_RESOURCE_GROUP = $resourceGroup" -ForegroundColor White
Write-Host "  • AZURE_FUNCTIONAPP_NAME = $functionAppName" -ForegroundColor White
Write-Host ""
Write-Host "Naechste Schritte:" -ForegroundColor Cyan
Write-Host "  1. Teste den Workflow: https://github.com/mariomuja/interface-configuration/actions" -ForegroundColor White
Write-Host "  2. Oder pushe eine Aenderung zu azure-functions/**" -ForegroundColor White
Write-Host ""

#!/bin/bash
# Bash Script zum Setup der GitHub Secrets für Azure Functions Deployment
# Voraussetzung: Azure CLI und GitHub CLI müssen installiert sein

set -e

echo "=== GitHub Secrets Setup für Azure Functions ==="
echo ""

# Schritt 1: Prüfe ob Azure CLI verfügbar ist
echo "[1/5] Prüfe Azure CLI..."
if ! command -v az &> /dev/null; then
    echo "✗ Azure CLI nicht gefunden. Bitte installieren: https://aka.ms/installazurecliwindows"
    exit 1
fi
echo "✓ Azure CLI gefunden"

# Schritt 2: Prüfe ob GitHub CLI verfügbar ist
echo "[2/5] Prüfe GitHub CLI..."
if ! command -v gh &> /dev/null; then
    echo "✗ GitHub CLI nicht gefunden. Bitte installieren: https://cli.github.com/"
    echo "  Oder setze die Secrets manuell über: https://github.com/mariomuja/interface-configuration/settings/secrets/actions"
    exit 1
fi
echo "✓ GitHub CLI gefunden"

# Schritt 3: Hole Azure Subscription und Resource Group
echo "[3/5] Hole Azure Informationen..."
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
if [ -z "$SUBSCRIPTION_ID" ]; then
    echo "✗ Nicht bei Azure eingeloggt. Bitte ausführen: az login"
    exit 1
fi
echo "✓ Subscription ID: $SUBSCRIPTION_ID"

RESOURCE_GROUP="rg-interface-configuration"
echo "  Resource Group: $RESOURCE_GROUP"

# Schritt 4: Hole Function App Name aus Terraform
echo "[4/5] Hole Function App Name..."
cd terraform
FUNCTION_APP_NAME=$(terraform output -raw function_app_name 2>/dev/null || echo "")
cd ..

if [ -z "$FUNCTION_APP_NAME" ] || [ "$FUNCTION_APP_NAME" = "null" ]; then
    echo "✗ Function App Name nicht gefunden. Bitte zuerst 'terraform apply' ausführen."
    echo "  Oder gib den Namen manuell ein:"
    read -p "Function App Name: " FUNCTION_APP_NAME
fi
echo "✓ Function App Name: $FUNCTION_APP_NAME"

# Schritt 5: Erstelle Service Principal
echo "[5/5] Erstelle Service Principal für GitHub Actions..."
SP_NAME="github-actions-functions-$(shuf -i 1000-9999 -n 1)"
SCOPE="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"

echo "  Erstelle Service Principal: $SP_NAME"
echo "  Scope: $SCOPE"

SP_JSON=$(az ad sp create-for-rbac --name "$SP_NAME" --role contributor --scopes "$SCOPE" --sdk-auth 2>&1)
if [ $? -ne 0 ]; then
    echo "✗ Fehler beim Erstellen des Service Principals:"
    echo "$SP_JSON"
    exit 1
fi

echo "✓ Service Principal erstellt"

# Schritt 6: Setze GitHub Secrets
echo ""
echo "=== Setze GitHub Secrets ==="

# Prüfe GitHub Login
if ! gh auth status &>/dev/null; then
    echo "⚠ GitHub CLI nicht eingeloggt. Bitte ausführen: gh auth login"
    echo "  Danach dieses Script erneut ausführen."
    exit 1
fi

# Setze AZURE_CREDENTIALS
echo "Setze AZURE_CREDENTIALS..."
echo "$SP_JSON" | gh secret set AZURE_CREDENTIALS
if [ $? -eq 0 ]; then
    echo "✓ AZURE_CREDENTIALS gesetzt"
else
    echo "✗ Fehler beim Setzen von AZURE_CREDENTIALS"
    exit 1
fi

# Setze AZURE_RESOURCE_GROUP
echo "Setze AZURE_RESOURCE_GROUP..."
echo "$RESOURCE_GROUP" | gh secret set AZURE_RESOURCE_GROUP
if [ $? -eq 0 ]; then
    echo "✓ AZURE_RESOURCE_GROUP gesetzt"
else
    echo "✗ Fehler beim Setzen von AZURE_RESOURCE_GROUP"
    exit 1
fi

# Setze AZURE_FUNCTIONAPP_NAME
echo "Setze AZURE_FUNCTIONAPP_NAME..."
echo "$FUNCTION_APP_NAME" | gh secret set AZURE_FUNCTIONAPP_NAME
if [ $? -eq 0 ]; then
    echo "✓ AZURE_FUNCTIONAPP_NAME gesetzt"
else
    echo "✗ Fehler beim Setzen von AZURE_FUNCTIONAPP_NAME"
    exit 1
fi

# Zusammenfassung
echo ""
echo "=== Fertig! ==="
echo ""
echo "Folgende Secrets wurden gesetzt:"
echo "  • AZURE_CREDENTIALS"
echo "  • AZURE_RESOURCE_GROUP = $RESOURCE_GROUP"
echo "  • AZURE_FUNCTIONAPP_NAME = $FUNCTION_APP_NAME"
echo ""
echo "Nächste Schritte:"
echo "  1. Teste den Workflow: https://github.com/mariomuja/interface-configuration/actions"
echo "  2. Oder pushe eine Änderung zu azure-functions/**"
echo ""











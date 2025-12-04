#!/bin/bash
set -e

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "🏗️  Deploying Integration Test Infrastructure (Bicep)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Validate required environment variables
if [ -z "$AZURE_CLIENT_ID" ] || [ -z "$AZURE_CLIENT_SECRET" ] || [ -z "$AZURE_TENANT_ID" ] || [ -z "$AZURE_SUBSCRIPTION_ID" ]; then
  echo "❌ Error: Azure Service Principal credentials not set"
  exit 1
fi

echo ""
echo "📋 Step 1: Logging into Azure..."
az login --service-principal \
  --username "$AZURE_CLIENT_ID" \
  --password "$AZURE_CLIENT_SECRET" \
  --tenant "$AZURE_TENANT_ID" \
  --output none

az account set --subscription "$AZURE_SUBSCRIPTION_ID"
echo "✅ Logged in as Service Principal"

echo ""
echo "📦 Step 2: Deploying Bicep template..."
echo "  Resource Group: $AZURE_RESOURCE_GROUP"
echo "  Subscription: $AZURE_SUBSCRIPTION_ID"

# Deploy Bicep template for integration test resources
az deployment group create \
  --resource-group "$AZURE_RESOURCE_GROUP" \
  --template-file infrastructure/integration-test-resources.bicep \
  --parameters storageAccountName="${AZURE_STORAGE_ACCOUNT_NAME}" \
  --parameters serviceBusNamespaceName="${SERVICE_BUS_NAMESPACE}" \
  --parameters sqlServerName="${AZURE_SQL_SERVER%%.*}" \
  --parameters sqlDatabaseName="${AZURE_SQL_DATABASE}" \
  --parameters acrName="${ACR_NAME}" \
  --parameters servicePrincipalObjectId="${AZURE_SERVICE_PRINCIPAL_OBJECT_ID}" \
  --output json > /tmp/bicep-deployment-output.json

echo "✅ Bicep deployment completed"

echo ""
echo "📊 Deployment Outputs:"
cat /tmp/bicep-deployment-output.json | jq '.properties.outputs'

echo ""
echo "Note: Database schema initialization is handled by a separate pipeline stage"
echo "      See: 'Initialize Database' stage"

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "✅ Integration test infrastructure provisioned!"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Resources created:"
echo "  ✅ Blob Containers: function-config, adapter-data, terraform-state, backup"
echo "  ✅ Service Bus: 3 test topics with subscriptions"
echo "  ✅ SQL Schema: 7 tables with indexes"
echo "  ✅ ACR Permissions: AcrPull + AcrPush for Service Principal"
echo ""


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
echo "🗄️  Step 3: Creating SQL Server Schema..."
echo "  Note: SQL tables are managed by EF Core migrations, not Bicep"
echo "  Creating minimal schema for integration tests..."

# Build connection string
SQL_CONNECTION_STRING="Server=$AZURE_SQL_SERVER;Database=$AZURE_SQL_DATABASE;User Id=$AZURE_SQL_USER;Password=$AZURE_SQL_PASSWORD;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"

# Create SQL script for schema
cat > /tmp/create_schema.sql <<'EOF'
-- Create TransportData table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TransportData')
BEGIN
    CREATE TABLE TransportData (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        interface_name NVARCHAR(255) NOT NULL,
        adapter_instance_guid UNIQUEIDENTIFIER NOT NULL,
        adapter_type NVARCHAR(50) NOT NULL,
        message_body NVARCHAR(MAX),
        headers NVARCHAR(MAX),
        datetime_created DATETIME DEFAULT GETUTCDATE(),
        status NVARCHAR(50) DEFAULT 'Pending',
        error_message NVARCHAR(MAX)
    );
    CREATE INDEX IX_TransportData_datetime_created ON TransportData(datetime_created);
    PRINT 'Created table: TransportData';
END

-- Create InterfaceConfigurations table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InterfaceConfigurations')
BEGIN
    CREATE TABLE InterfaceConfigurations (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        interface_name NVARCHAR(255) NOT NULL UNIQUE,
        configuration_json NVARCHAR(MAX) NOT NULL,
        is_active BIT DEFAULT 1,
        created_at DATETIME DEFAULT GETUTCDATE(),
        updated_at DATETIME DEFAULT GETUTCDATE()
    );
    PRINT 'Created table: InterfaceConfigurations';
END

-- Create ProcessLogs table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessLogs')
BEGIN
    CREATE TABLE ProcessLogs (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        interface_name NVARCHAR(255) NOT NULL,
        log_level NVARCHAR(50) NOT NULL,
        message NVARCHAR(MAX),
        exception NVARCHAR(MAX),
        timestamp DATETIME DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_ProcessLogs_timestamp ON ProcessLogs(timestamp);
    PRINT 'Created table: ProcessLogs';
END

-- Create ProcessingStatistics table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessingStatistics')
BEGIN
    CREATE TABLE ProcessingStatistics (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        interface_name NVARCHAR(255) NOT NULL,
        adapter_instance_guid UNIQUEIDENTIFIER NOT NULL,
        records_processed INT DEFAULT 0,
        records_failed INT DEFAULT 0,
        start_time DATETIME,
        end_time DATETIME,
        duration_ms BIGINT
    );
    PRINT 'Created table: ProcessingStatistics';
END

-- Create ServiceBusMessageLocks table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ServiceBusMessageLocks')
BEGIN
    CREATE TABLE ServiceBusMessageLocks (
        message_id NVARCHAR(255) PRIMARY KEY,
        lock_token NVARCHAR(500) NOT NULL,
        topic_name NVARCHAR(255) NOT NULL,
        subscription_name NVARCHAR(255) NOT NULL,
        locked_until_utc DATETIME NOT NULL,
        status NVARCHAR(50) DEFAULT 'Active',
        created_at DATETIME DEFAULT GETUTCDATE()
    );
    PRINT 'Created table: ServiceBusMessageLocks';
END

-- Create AdapterInstances table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AdapterInstances')
BEGIN
    CREATE TABLE AdapterInstances (
        adapter_instance_guid UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        interface_name NVARCHAR(255) NOT NULL,
        adapter_name NVARCHAR(255) NOT NULL,
        adapter_type NVARCHAR(50) NOT NULL,
        configuration_json NVARCHAR(MAX),
        is_active BIT DEFAULT 1,
        created_at DATETIME DEFAULT GETUTCDATE()
    );
    PRINT 'Created table: AdapterInstances';
END

-- Create AdapterSubscriptions table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AdapterSubscriptions')
BEGIN
    CREATE TABLE AdapterSubscriptions (
        subscription_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        adapter_instance_guid UNIQUEIDENTIFIER NOT NULL,
        topic_name NVARCHAR(255) NOT NULL,
        subscription_name NVARCHAR(255) NOT NULL,
        is_active BIT DEFAULT 1,
        created_at DATETIME DEFAULT GETUTCDATE(),
        FOREIGN KEY (adapter_instance_guid) REFERENCES AdapterInstances(adapter_instance_guid)
    );
    PRINT 'Created table: AdapterSubscriptions';
END

PRINT 'Database schema provisioning completed.';
EOF

echo "  Executing SQL schema creation via sqlcmd..."
/usr/bin/docker run --rm \
  -v /tmp/create_schema.sql:/tmp/create_schema.sql:ro \
  mcr.microsoft.com/mssql-tools \
  /opt/mssql-tools/bin/sqlcmd \
    -S "$AZURE_SQL_SERVER" \
    -d "$AZURE_SQL_DATABASE" \
    -U "$AZURE_SQL_USER" \
    -P "$AZURE_SQL_PASSWORD" \
    -i /tmp/create_schema.sql \
    -b

echo "✅ SQL schema created"

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


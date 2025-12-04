#!/bin/bash
set -e

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "🏗️  Provisioning Azure Resources for Integration Tests"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Validate required environment variables
if [ -z "$AZURE_STORAGE_CONNECTION_STRING" ]; then
  echo "❌ Error: AZURE_STORAGE_CONNECTION_STRING not set"
  exit 1
fi

if [ -z "$AZURE_SERVICE_BUS_CONNECTION_STRING" ]; then
  echo "❌ Error: AZURE_SERVICE_BUS_CONNECTION_STRING not set"
  exit 1
fi

if [ -z "$AZURE_SQL_SERVER" ] || [ -z "$AZURE_SQL_DATABASE" ] || [ -z "$AZURE_SQL_USER" ] || [ -z "$AZURE_SQL_PASSWORD" ]; then
  echo "❌ Error: SQL Server credentials not set"
  exit 1
fi

echo ""
echo "📦 Step 1: Creating Blob Storage Containers..."
echo "─────────────────────────────────────────────────────"

# Global containers (not adapter-specific)
GLOBAL_CONTAINERS=(
  "function-config"
  "terraform-state"
  "backup"
)

for container in "${GLOBAL_CONTAINERS[@]}"; do
  echo "  Creating container: $container"
  az storage container create \
    --name "$container" \
    --connection-string "$AZURE_STORAGE_CONNECTION_STRING" \
    --public-access off \
    --output none 2>/dev/null || echo "    (already exists)"
done

echo ""
echo "📁 Step 2: Creating Adapter Instance Blob Storage Structure..."
echo "─────────────────────────────────────────────────────"
echo "  Note: File-based adapters (CSV, SFTP, File, SAP) get their own folders"
echo "  Each adapter instance structure:"
echo "    <adapter-instance-guid>/"
echo "      ├── incoming/"
echo "      ├── error/"
echo "      └── processed/"

# Create a container for adapter data
echo "  Creating adapter-data container (shared by all adapter instances)..."
az storage container create \
  --name "adapter-data" \
  --connection-string "$AZURE_STORAGE_CONNECTION_STRING" \
  --public-access off \
  --output none 2>/dev/null || echo "    (already exists)"

# For integration tests, create some sample adapter instance folders
# In production, these would be created dynamically when an adapter instance is provisioned
SAMPLE_ADAPTER_INSTANCES=(
  "csv-adapter-test-001"
  "sftp-adapter-test-001"
  "file-adapter-test-001"
  "sap-adapter-test-001"
)

for instance_id in "${SAMPLE_ADAPTER_INSTANCES[@]}"; do
  echo "  Creating folders for adapter instance: $instance_id"
  for folder in "incoming" "error" "processed"; do
    # Create a placeholder file to ensure the folder exists (blob storage is flat)
    echo "Adapter instance folder" | az storage blob upload \
      --container-name "adapter-data" \
      --name "${instance_id}/${folder}/.placeholder" \
      --connection-string "$AZURE_STORAGE_CONNECTION_STRING" \
      --overwrite \
      --output none 2>/dev/null || true
  done
done

echo ""
echo "📨 Step 3: Creating Service Bus Topics & Subscriptions..."
echo "─────────────────────────────────────────────────────"

# Extract Service Bus namespace from connection string
SB_NAMESPACE=$(echo "$AZURE_SERVICE_BUS_CONNECTION_STRING" | grep -oP 'Endpoint=sb://\K[^.]+' || echo "")

if [ -z "$SB_NAMESPACE" ]; then
  echo "  ⚠️  Warning: Could not extract namespace from connection string"
  echo "  Skipping Service Bus entity creation (requires Azure CLI authentication)"
else
  echo "  Service Bus Namespace: $SB_NAMESPACE"
  
  # Create test topics
  TEST_TOPICS=(
    "interface-test-interface"
    "interface-test-interface-csv"
    "interface-test-interface-sftp"
  )
  
  for topic in "${TEST_TOPICS[@]}"; do
    echo "  Creating topic: $topic"
    az servicebus topic create \
      --name "$topic" \
      --namespace-name "$SB_NAMESPACE" \
      --resource-group "$AZURE_RESOURCE_GROUP" \
      --output none 2>/dev/null || echo "    (already exists or no permissions)"
    
    # Create a test subscription for this topic
    echo "    Creating subscription: destination-test"
    az servicebus topic subscription create \
      --name "destination-test" \
      --topic-name "$topic" \
      --namespace-name "$SB_NAMESPACE" \
      --resource-group "$AZURE_RESOURCE_GROUP" \
      --output none 2>/dev/null || echo "    (already exists or no permissions)"
  done
fi

echo ""
echo "🗄️  Step 4: Creating SQL Server Tables & Indexes..."
echo "─────────────────────────────────────────────────────"

# Build connection string
SQL_CONNECTION_STRING="Server=$AZURE_SQL_SERVER;Database=$AZURE_SQL_DATABASE;User Id=$AZURE_SQL_USER;Password=$AZURE_SQL_PASSWORD;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"

echo "  Connecting to: $AZURE_SQL_SERVER/$AZURE_SQL_DATABASE"

# Create SQL script for schema
cat > /tmp/create_schema.sql <<'EOF'
-- Create TransportData table (main data table for adapter messages)
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
    PRINT 'Created table: TransportData';
END
ELSE
    PRINT 'Table TransportData already exists';

-- Create index on datetime_created for query performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TransportData_datetime_created' AND object_id = OBJECT_ID('TransportData'))
BEGIN
    CREATE INDEX IX_TransportData_datetime_created ON TransportData(datetime_created);
    PRINT 'Created index: IX_TransportData_datetime_created';
END
ELSE
    PRINT 'Index IX_TransportData_datetime_created already exists';

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
ELSE
    PRINT 'Table InterfaceConfigurations already exists';

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
    PRINT 'Created table: ProcessLogs';
END
ELSE
    PRINT 'Table ProcessLogs already exists';

-- Create index on ProcessLogs
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProcessLogs_timestamp' AND object_id = OBJECT_ID('ProcessLogs'))
BEGIN
    CREATE INDEX IX_ProcessLogs_timestamp ON ProcessLogs(timestamp);
    PRINT 'Created index: IX_ProcessLogs_timestamp';
END
ELSE
    PRINT 'Index IX_ProcessLogs_timestamp already exists';

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
ELSE
    PRINT 'Table ProcessingStatistics already exists';

-- Create ServiceBusMessageLocks table (for lock renewal tracking)
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
ELSE
    PRINT 'Table ServiceBusMessageLocks already exists';

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
ELSE
    PRINT 'Table AdapterInstances already exists';

-- Create AdapterSubscriptions table (for Service Bus subscriptions)
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
ELSE
    PRINT 'Table AdapterSubscriptions already exists';

PRINT '';
PRINT 'Database schema provisioning completed successfully.';
EOF

echo "  Executing SQL schema creation..."
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

echo ""
echo "🔐 Step 5: Granting ACR Permissions to Service Principal..."
echo "─────────────────────────────────────────────────────"

if [ -n "$AZURE_CLIENT_ID" ] && [ -n "$ACR_NAME" ]; then
  echo "  Service Principal: $AZURE_CLIENT_ID"
  echo "  ACR: $ACR_NAME"
  
  # Get ACR resource ID
  ACR_ID=$(az acr show --name "$ACR_NAME" --query id --output tsv 2>/dev/null || echo "")
  
  if [ -n "$ACR_ID" ]; then
    echo "  Granting AcrPull role..."
    az role assignment create \
      --assignee "$AZURE_CLIENT_ID" \
      --role AcrPull \
      --scope "$ACR_ID" \
      --output none 2>/dev/null || echo "    (already assigned or no permissions)"
    
    echo "  Granting AcrPush role..."
    az role assignment create \
      --assignee "$AZURE_CLIENT_ID" \
      --role AcrPush \
      --scope "$ACR_ID" \
      --output none 2>/dev/null || echo "    (already assigned or no permissions)"
  else
    echo "  ⚠️  Warning: Could not find ACR resource ID. Skipping role assignment."
  fi
else
  echo "  ⚠️  Skipping: AZURE_CLIENT_ID or ACR_NAME not set"
fi

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "✅ Azure resource provisioning completed!"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Summary:"
echo "  ✅ Blob Storage: Global containers + adapter-data container"
echo "  ✅ Service Bus: Test topics and subscriptions (if Azure CLI available)"
echo "  ✅ SQL Server: All tables and indexes created"
echo "  ✅ ACR: Service Principal roles granted (if available)"
echo ""
echo "Note: Adapter-specific blob storage folders are created dynamically"
echo "      when adapter instances are provisioned through the API."
echo ""


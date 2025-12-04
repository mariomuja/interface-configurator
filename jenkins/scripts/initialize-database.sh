#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "🗄️  Initializing Database Schema"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Validate SQL Server credentials
if [ -z "$AZURE_SQL_SERVER" ] || [ -z "$AZURE_SQL_DATABASE" ] || [ -z "$AZURE_SQL_USER" ] || [ -z "$AZURE_SQL_PASSWORD" ]; then
  echo "❌ Error: SQL Server credentials not set"
  exit 1
fi

echo ""
echo "📋 Database: $AZURE_SQL_SERVER/$AZURE_SQL_DATABASE"
echo ""

# Create SQL script for schema initialization
cat > /tmp/init_database.sql <<'EOF'
-- =============================================================================
-- Interface Configurator - Database Schema Initialization
-- =============================================================================
-- This script creates all tables, indexes, and constraints needed for:
--   - Transport data storage
--   - Interface configurations
--   - Adapter instances
--   - Process logging and statistics
--   - Service Bus lock tracking
-- =============================================================================

PRINT 'Starting database schema initialization...';
PRINT '';

-- TransportData: Main data table for messages flowing through adapters
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
    CREATE INDEX IX_TransportData_interface_name ON TransportData(interface_name);
    CREATE INDEX IX_TransportData_adapter_instance_guid ON TransportData(adapter_instance_guid);
    CREATE INDEX IX_TransportData_status ON TransportData(status);
    PRINT '✅ Created table: TransportData (with 4 indexes)';
END
ELSE
    PRINT '   Table TransportData already exists';

PRINT '';

-- InterfaceConfigurations: Stores interface definitions and settings
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
    CREATE INDEX IX_InterfaceConfigurations_is_active ON InterfaceConfigurations(is_active);
    PRINT '✅ Created table: InterfaceConfigurations (with 1 index)';
END
ELSE
    PRINT '   Table InterfaceConfigurations already exists';

PRINT '';

-- ProcessLogs: Application logs and errors
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
    CREATE INDEX IX_ProcessLogs_log_level ON ProcessLogs(log_level);
    PRINT '✅ Created table: ProcessLogs (with 2 indexes)';
END
ELSE
    PRINT '   Table ProcessLogs already exists';

PRINT '';

-- ProcessingStatistics: Performance metrics and monitoring
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
    CREATE INDEX IX_ProcessingStatistics_interface_name ON ProcessingStatistics(interface_name);
    PRINT '✅ Created table: ProcessingStatistics (with 1 index)';
END
ELSE
    PRINT '   Table ProcessingStatistics already exists';

PRINT '';

-- ServiceBusMessageLocks: Track message locks for renewal
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
    CREATE INDEX IX_ServiceBusMessageLocks_locked_until_utc ON ServiceBusMessageLocks(locked_until_utc);
    CREATE INDEX IX_ServiceBusMessageLocks_status ON ServiceBusMessageLocks(status);
    PRINT '✅ Created table: ServiceBusMessageLocks (with 2 indexes)';
END
ELSE
    PRINT '   Table ServiceBusMessageLocks already exists';

PRINT '';

-- AdapterInstances: Registry of all adapter instances
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AdapterInstances')
BEGIN
    CREATE TABLE AdapterInstances (
        adapter_instance_guid UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        interface_name NVARCHAR(255) NOT NULL,
        adapter_name NVARCHAR(255) NOT NULL,
        adapter_type NVARCHAR(50) NOT NULL,
        configuration_json NVARCHAR(MAX),
        blob_storage_path NVARCHAR(500) NULL, -- Only populated for file-based adapters
        is_active BIT DEFAULT 1,
        created_at DATETIME DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_AdapterInstances_interface_name ON AdapterInstances(interface_name);
    CREATE INDEX IX_AdapterInstances_adapter_type ON AdapterInstances(adapter_type);
    CREATE INDEX IX_AdapterInstances_is_active ON AdapterInstances(is_active);
    PRINT '✅ Created table: AdapterInstances (with 3 indexes)';
END
ELSE
    PRINT '   Table AdapterInstances already exists';

PRINT '';

-- AdapterSubscriptions: Service Bus subscriptions for destination adapters
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AdapterSubscriptions')
BEGIN
    CREATE TABLE AdapterSubscriptions (
        subscription_id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        adapter_instance_guid UNIQUEIDENTIFIER NOT NULL,
        topic_name NVARCHAR(255) NOT NULL,
        subscription_name NVARCHAR(255) NOT NULL,
        is_active BIT DEFAULT 1,
        created_at DATETIME DEFAULT GETUTCDATE(),
        CONSTRAINT FK_AdapterSubscriptions_AdapterInstances 
            FOREIGN KEY (adapter_instance_guid) 
            REFERENCES AdapterInstances(adapter_instance_guid)
            ON DELETE CASCADE
    );
    CREATE INDEX IX_AdapterSubscriptions_adapter_instance_guid ON AdapterSubscriptions(adapter_instance_guid);
    PRINT '✅ Created table: AdapterSubscriptions (with 1 index)';
END
ELSE
    PRINT '   Table AdapterSubscriptions already exists';

PRINT '';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '✅ Database schema initialization completed successfully!';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '';
PRINT 'Tables created: 7';
PRINT 'Indexes created: 13';
PRINT '';
EOF

echo "Executing database schema initialization..."
/usr/bin/docker run --rm \
  -v /tmp/init_database.sql:/tmp/init_database.sql:ro \
  mcr.microsoft.com/mssql-tools \
  /opt/mssql-tools/bin/sqlcmd \
    -S "$AZURE_SQL_SERVER" \
    -d "$AZURE_SQL_DATABASE" \
    -U "$AZURE_SQL_USER" \
    -P "$AZURE_SQL_PASSWORD" \
    -i /tmp/init_database.sql \
    -b

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "✅ Database initialization completed!"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo "Tables:"
echo "  ✅ TransportData (main message table)"
echo "  ✅ InterfaceConfigurations (interface definitions)"
echo "  ✅ ProcessLogs (application logs)"
echo "  ✅ ProcessingStatistics (metrics)"
echo "  ✅ ServiceBusMessageLocks (lock tracking)"
echo "  ✅ AdapterInstances (adapter registry)"
echo "  ✅ AdapterSubscriptions (Service Bus subscriptions)"
echo ""
echo "Indexes: 13 total"
echo ""


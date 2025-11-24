# Container App Adapter Instance Settings Management

## Overview

Each container app instance maintains its own complete copy of the adapter instance settings. This ensures complete isolation between adapter instances and allows each container app to operate independently with its own configuration.

## Architecture

### Configuration Storage

When a container app is created for an adapter instance:

1. **Blob Storage**: A dedicated blob storage account and container are created for each adapter instance
2. **Configuration File**: The adapter instance configuration is serialized to JSON and stored as `adapter-config.json` in the container app's blob storage
3. **Environment Variable**: The container app receives an `ADAPTER_CONFIG_PATH` environment variable pointing to `adapter-config.json`

### Settings Included

All adapter instance properties are stored in the configuration file, including:

#### CSV Adapter Settings
- `SourceReceiveFolder` / `DestinationReceiveFolder`
- `SourceFileMask` / `DestinationFileMask`
- `SourceBatchSize`
- `SourceFieldSeparator`
- `CsvData`
- `CsvAdapterType` (RAW, FILE, SFTP)
- `CsvPollingInterval`

#### SQL Server Adapter Settings
- `SqlServerName`
- `SqlDatabaseName`
- `SqlUserName`
- `SqlPassword`
- `SqlIntegratedSecurity`
- `SqlResourceGroup`
- `SqlTableName`
- `SqlUseTransaction`
- `SqlBatchSize`
- `SqlCommandTimeout`
- `SqlFailOnBadStatement`
- `SqlPollingStatement` (for source adapters)
- `SqlPollingInterval` (for source adapters)
- `InsertStatement` (for destination adapters)
- `UpdateStatement` (for destination adapters)
- `DeleteStatement` (for destination adapters)

#### SFTP Adapter Settings
- `SftpHost`
- `SftpPort`
- `SftpUsername`
- `SftpPassword`
- `SftpSshKey`
- `SftpFolder`
- `SftpFileMask`
- `SftpMaxConnectionPoolSize`
- `SftpFileBufferSize`

#### SAP Adapter Settings
- `SapApplicationServer`
- `SapSystemNumber`
- `SapClient`
- `SapUsername`
- `SapPassword`
- `SapLanguage`
- `SapIdocType`
- `SapIdocMessageType`
- `SapIdocFilter` (for source adapters)
- `SapReceiverPort` (for destination adapters)
- `SapReceiverPartner` (for destination adapters)
- `SapPollingInterval` (for source adapters)
- `SapBatchSize`
- `SapConnectionTimeout`
- `SapUseRfc`
- `SapRfcDestination`
- `SapRfcFunctionModule`
- `SapRfcParameters`
- `SapODataServiceUrl`
- `SapRestApiEndpoint`
- `SapUseOData`
- `SapUseRestApi`

#### Dynamics 365 Adapter Settings
- `Dynamics365TenantId`
- `Dynamics365ClientId`
- `Dynamics365ClientSecret`
- `Dynamics365InstanceUrl`
- `Dynamics365EntityName`
- `Dynamics365ODataFilter` (for source adapters)
- `Dynamics365PollingInterval` (for source adapters)
- `Dynamics365BatchSize`
- `Dynamics365PageSize` (for source adapters)
- `Dynamics365UseBatch` (for destination adapters)

#### Microsoft CRM Adapter Settings
- `CrmOrganizationUrl`
- `CrmUsername`
- `CrmPassword`
- `CrmEntityName`
- `CrmFetchXml` (for source adapters)
- `CrmPollingInterval` (for source adapters)
- `CrmBatchSize`
- `CrmUseBatch` (for destination adapters)

#### General Settings
- `AdapterInstanceGuid`
- `InstanceName`
- `AdapterName`
- `IsEnabled`
- `Configuration` (JSON string)
- `JQScriptFile` (for destination adapters)
- `SourceAdapterSubscription` (for destination adapters)
- `CreatedAt`
- `UpdatedAt`

## How It Works

### 1. Container App Creation

When a new adapter instance is created:

```csharp
// Full adapter instance configuration is retrieved
var config = await _configService.GetConfigurationAsync(interfaceName, cancellationToken);
var adapterInstance = config.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == guid);

// Container app is created with full configuration
var containerAppInfo = await _containerAppService.CreateContainerAppAsync(
    adapterInstanceGuid,
    adapterName,
    adapterType,
    interfaceName,
    instanceName,
    adapterInstance, // Full configuration passed here
    cancellationToken);
```

**Process:**
1. Blob storage account and container are created
2. Adapter configuration is serialized to JSON
3. JSON is stored as `adapter-config.json` in blob storage
4. Container app is created with `ADAPTER_CONFIG_PATH` environment variable
5. Container app can read its configuration on startup

### 2. Configuration Updates

When adapter instance settings are changed in the UI:

```csharp
// Settings are updated in the database
await _configService.UpdateDestinationAdapterInstanceAsync(
    interfaceName,
    adapterInstanceGuid,
    instanceName,
    isEnabled,
    configuration,
    cancellationToken);

// Container app configuration is automatically updated
var config = await _configService.GetConfigurationAsync(interfaceName, cancellationToken);
var updatedInstance = config.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == guid);

await _containerAppService.UpdateContainerAppConfigurationAsync(
    adapterInstanceGuid,
    updatedInstance,
    cancellationToken);
```

**Process:**
1. Settings are updated in the database
2. Full updated configuration is retrieved
3. Configuration is serialized to JSON
4. `adapter-config.json` is updated in blob storage
5. Container app can reload configuration (if implemented)

### 3. Configuration Storage Implementation

The configuration is stored using the `StoreAdapterConfigurationAsync` method:

```csharp
private async Task StoreAdapterConfigurationAsync(
    string connectionString,
    string containerName,
    object adapterConfiguration,
    CancellationToken cancellationToken)
{
    var blobServiceClient = new BlobServiceClient(connectionString);
    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

    var blobClient = containerClient.GetBlobClient("adapter-config.json");
    var configJson = System.Text.Json.JsonSerializer.Serialize(
        adapterConfiguration, 
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    await blobClient.UploadAsync(
        new BinaryData(configJson),
        overwrite: true,
        cancellationToken: cancellationToken);
}
```

## API Endpoints

### Create Container App (Automatic)

Container apps are automatically created when adapter instances are added. The configuration is included automatically.

**Endpoint**: `POST /api/AddDestinationAdapterInstance`

**Request Body**:
```json
{
  "interfaceName": "FromCsvToSqlServerExample",
  "adapterName": "SqlServer",
  "instanceName": "Destination",
  "configuration": "{\"destination\": \"TransportData\"}"
}
```

**Response**: Adapter instance is created and container app creation is initiated asynchronously.

### Update Container App Configuration

Manually update container app configuration (also happens automatically when settings change).

**Endpoint**: `POST /api/UpdateContainerAppConfiguration`

**Request Body**:
```json
{
  "adapterInstanceGuid": "guid-here",
  "interfaceName": "FromCsvToSqlServerExample",
  "adapterType": "Destination"
}
```

**Response**:
```json
{
  "success": true
}
```

**Process**:
1. Retrieves the latest adapter instance configuration from the database
2. Updates `adapter-config.json` in the container app's blob storage
3. Container app can reload the configuration

### Update Adapter Instance Settings

When adapter instance settings are updated, the container app configuration is automatically synced.

**Endpoint**: `PUT /api/UpdateDestinationAdapterInstance`

**Request Body**:
```json
{
  "interfaceName": "FromCsvToSqlServerExample",
  "adapterInstanceGuid": "guid-here",
  "instanceName": "Updated Destination",
  "isEnabled": true,
  "configuration": "{\"destination\": \"UpdatedTransportData\"}"
}
```

**Response**:
```json
{
  "message": "Destination adapter instance 'guid-here' updated successfully. Container app configuration update initiated.",
  "interfaceName": "FromCsvToSqlServerExample",
  "adapterInstanceGuid": "guid-here"
}
```

**Process**:
1. Updates settings in the database
2. Automatically triggers container app configuration update (asynchronously)
3. Container app configuration is synced to blob storage

## Container App Implementation

### Environment Variables

Each container app receives the following environment variables:

- `ADAPTER_INSTANCE_GUID`: Unique identifier for the adapter instance
- `ADAPTER_NAME`: Type of adapter (CSV, SqlServer, etc.)
- `ADAPTER_TYPE`: Source or Destination
- `INTERFACE_NAME`: Interface name this adapter belongs to
- `INSTANCE_NAME`: User-friendly instance name
- `BLOB_CONNECTION_STRING`: Connection string to blob storage (secret reference)
- `BLOB_CONTAINER_NAME`: Name of the blob container
- `ADAPTER_CONFIG_PATH`: Path to configuration file (`adapter-config.json`)

### Reading Configuration

The container app should:

1. **On Startup**:
   - Read `ADAPTER_CONFIG_PATH` environment variable
   - Connect to blob storage using `BLOB_CONNECTION_STRING`
   - Load `adapter-config.json` from `BLOB_CONTAINER_NAME`
   - Deserialize JSON to adapter configuration object
   - Apply configuration settings

2. **On Configuration Update** (Optional):
   - Periodically check if `adapter-config.json` has changed
   - Reload configuration if updated
   - Apply new settings without restart (if supported)

### Example Configuration File Structure

```json
{
  "AdapterInstanceGuid": "12345678-1234-1234-1234-123456789abc",
  "InstanceName": "Destination",
  "AdapterName": "SqlServer",
  "IsEnabled": true,
  "Configuration": "{\"destination\": \"TransportData\"}",
  "SqlServerName": "myserver.database.windows.net",
  "SqlDatabaseName": "MyDatabase",
  "SqlUserName": "sqladmin",
  "SqlPassword": "***",
  "SqlIntegratedSecurity": false,
  "SqlTableName": "TransportData",
  "SqlUseTransaction": true,
  "SqlBatchSize": 1000,
  "SqlCommandTimeout": 30,
  "SqlFailOnBadStatement": false,
  "CreatedAt": "2024-01-01T00:00:00Z",
  "UpdatedAt": "2024-01-02T00:00:00Z"
}
```

## Benefits

### 1. Complete Isolation
- Each container app instance has its own settings
- Changes to one instance don't affect others
- No shared configuration state

### 2. Independent Operation
- Each container app can be configured independently
- Settings can be updated per instance
- Container apps can be scaled independently

### 3. Version Control
- Configuration is stored as JSON files
- Can be versioned and tracked
- Easy to backup and restore

### 4. Hot Reload Support
- Configuration can be updated without restarting container apps
- Container apps can poll for configuration changes
- Enables dynamic configuration updates

### 5. Audit Trail
- Configuration changes are tracked via `UpdatedAt` timestamp
- Can be logged and audited
- Easy to identify when settings changed

## Implementation Status

✅ **Backend Implementation Complete**
- Container app creation with configuration storage
- Automatic configuration updates on settings change
- API endpoints for manual configuration updates
- Blob storage per container app instance

⏳ **Container App Implementation Required**
- Docker images need to read `adapter-config.json` on startup
- Apply configuration settings from JSON
- Optional: Implement hot-reload for configuration changes

## Related Documentation

- [Container App Isolation](./CONTAINER_APP_ISOLATION.md) - Overview of container app architecture
- [Adapter Configuration Service](../azure-functions/main/Services/AdapterConfigurationService.cs) - Adapter settings management
- [Container App Service](../azure-functions/main/Services/ContainerAppService.cs) - Container app lifecycle management


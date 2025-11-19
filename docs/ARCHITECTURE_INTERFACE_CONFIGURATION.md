# Interface Configuration System

## Overview

The Interface Configuration system enables **configuration-based integration** by allowing users to define interfaces without writing code. Each interface configuration specifies:
- Source adapter and its configuration
- Destination adapter and its configuration
- Enable/disable flags for independent control
- Instance names for UI display
- Adapter instance GUIDs for tracking
- **Adapter properties**: Fine-grained configuration for each adapter (connection strings, polling intervals, file masks, etc.)

## Configuration Model

```csharp
public class InterfaceConfiguration
{
    public string InterfaceName { get; set; }
    public string SourceAdapterName { get; set; }
    public string SourceConfiguration { get; set; } // JSON
    public string DestinationAdapterName { get; set; }
    public string DestinationConfiguration { get; set; } // JSON
    
    // Instance Management
    public string SourceInstanceName { get; set; }
    public string DestinationInstanceName { get; set; }
    public Guid? SourceAdapterInstanceGuid { get; set; }
    public Guid? DestinationAdapterInstanceGuid { get; set; }
    
    // Process Control
    public bool SourceIsEnabled { get; set; }
    public bool DestinationIsEnabled { get; set; }
    
    // CSV-Specific Properties
    public string? SourceReceiveFolder { get; set; }
    public string? SourceFileMask { get; set; }
    public int? SourceBatchSize { get; set; }
    public string? SourceFieldSeparator { get; set; }
    public string? CsvAdapterType { get; set; } // "RAW", "FILE", "SFTP"
    public string? CsvData { get; set; } // For RAW adapter type
    public string? SftpHost { get; set; }
    public int? SftpPort { get; set; }
    public string? SftpUsername { get; set; }
    public string? SftpPassword { get; set; }
    public string? SftpSshKey { get; set; }
    public string? SftpFolder { get; set; }
    public string? SftpFileMask { get; set; }
    public int? CsvPollingInterval { get; set; }
    public string? DestinationReceiveFolder { get; set; }
    public string? DestinationFileMask { get; set; }
    
    // SQL Server-Specific Properties
    public string? SqlServerName { get; set; }
    public string? SqlDatabaseName { get; set; }
    public string? SqlUserName { get; set; }
    public string? SqlPassword { get; set; }
    public bool SqlIntegratedSecurity { get; set; }
    public string? SqlResourceGroup { get; set; }
    public string? SqlPollingStatement { get; set; }
    public int SqlPollingInterval { get; set; }
    public bool SqlUseTransaction { get; set; }
    public int? SqlBatchSize { get; set; }
    
    // Metadata
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

## Storage

### JSON File Storage

Configurations are stored in Azure Blob Storage as JSON:
- **Container**: `function-config`
- **File**: `interface-configurations.json`
- **Format**: JSON array of `InterfaceConfiguration` objects

### In-Memory Cache

Configurations are loaded into memory on Function App startup:
- Fast access for runtime operations
- Automatic refresh on configuration changes
- Thread-safe access with `SemaphoreSlim`

## Configuration Lifecycle

### 1. Creation

When a new interface is configured:
1. User defines source and destination adapters
2. System generates default instance names
3. System generates adapter instance GUIDs
4. Configuration saved to JSON file
5. Configuration loaded into memory cache

### 2. Updates

Configuration updates are atomic:
1. Update in-memory cache
2. Persist to JSON file
3. Update timestamp (`UpdatedAt`)

### 3. Deletion

When an interface is deleted:
1. Removed from in-memory cache
2. Removed from JSON file
3. Adapter processes stop (if running)

## Adapter Instance Management

### Instance GUIDs

Each adapter instance has a unique GUID:
- Generated on first configuration
- Stored in `InterfaceConfiguration`
- Used in MessageBox for message tracking
- Maintained in `AdapterInstances` table

### Instance Names

User-editable names for UI display:
- Default: "Source" or "Destination"
- Can be customized per interface
- Displayed in adapter cards
- Stored in configuration

### Enable/Disable

Independent control of source and destination:
- `SourceIsEnabled`: Controls source adapter process
- `DestinationIsEnabled`: Controls destination adapter process
- When disabled, process stops immediately
- When enabled, process starts on next timer trigger

## Process Execution

### Source Adapter Process

Runs as separate Azure Function (Timer Trigger):
1. Loads enabled source configurations
2. For each configuration:
   - Checks if `SourceIsEnabled == true`
   - Instantiates source adapter
   - Calls `ReadAsync()` with source configuration
   - Adapter debatches and writes to MessageBox

### Destination Adapter Process

Runs as separate Azure Function (Timer Trigger):
1. Loads enabled destination configurations
2. For each configuration:
   - Checks if `DestinationIsEnabled == true`
   - Instantiates destination adapter
   - Reads pending messages from MessageBox
   - Processes messages and writes to destination

## Configuration Examples

### CSV → SQL Server

```json
{
  "interfaceName": "FromCsvToSqlServerExample",
  "sourceAdapterName": "CSV",
  "sourceConfiguration": "{\"source\": \"csv-files/csv-incoming\"}",
  "destinationAdapterName": "SqlServer",
  "destinationConfiguration": "{\"destination\": \"TransportData\"}",
  "sourceInstanceName": "CSV Source",
  "destinationInstanceName": "SQL Server Destination",
  "sourceIsEnabled": true,
  "destinationIsEnabled": true,
  "sourceReceiveFolder": "csv-files/csv-incoming"
}
```

### SQL Server → CSV

```json
{
  "interfaceName": "FromSqlServerToCsv",
  "sourceAdapterName": "SqlServer",
  "sourceConfiguration": "{\"source\": \"SourceTable\"}",
  "destinationAdapterName": "CSV",
  "destinationConfiguration": "{\"destination\": \"csv-files/output\"}",
  "sourceInstanceName": "SQL Source",
  "destinationInstanceName": "CSV Export",
  "sourceIsEnabled": true,
  "destinationIsEnabled": true
}
```

## API Endpoints

### Get All Configurations

```
GET /api/GetInterfaceConfigurations
Response: List<InterfaceConfiguration>
```

### Get Configuration

```
GET /api/GetInterfaceConfiguration?interfaceName={name}
Response: InterfaceConfiguration
```

### Create Configuration

```
POST /api/CreateInterfaceConfiguration
Body: InterfaceConfiguration
Response: Success message
```

### Update Configuration

```
PUT /api/UpdateInterfaceConfiguration
Body: InterfaceConfiguration
Response: Success message
```

### Toggle Enable/Disable

```
POST /api/ToggleInterfaceConfiguration
Body: {
  "interfaceName": "...",
  "adapterType": "Source" | "Destination",
  "enabled": true | false
}
Response: Success message
```

### Update Instance Name

```
PUT /api/UpdateInstanceName
Body: {
  "interfaceName": "...",
  "instanceType": "Source" | "Destination",
  "instanceName": "..."
}
Response: Success message
```

### Update Receive Folder

```
PUT /api/UpdateReceiveFolder
Body: {
  "interfaceName": "...",
  "receiveFolder": "..."
}
Response: Success message
```

## Benefits

- ✅ **Zero Code Changes**: New interfaces = configuration only
- ✅ **Runtime Updates**: Change configurations without redeployment
- ✅ **Independent Control**: Enable/disable adapters independently
- ✅ **User-Friendly**: Editable instance names and settings
- ✅ **Audit Trail**: Configuration changes tracked with timestamps
- ✅ **Scalability**: Add unlimited interfaces with same codebase





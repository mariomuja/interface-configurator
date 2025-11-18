# Adapter Pattern Architecture

## Overview

The adapter pattern is the core architectural concept that enables **configuration-based integration**. Instead of writing custom code for each interface, adapters provide a unified interface for reading from and writing to different data sources.

## Universal Adapters

Each adapter can be used as **both source and destination**:

### CsvAdapter

**As Source:**
- Reads CSV files from Azure Blob Storage
- Supports folder monitoring via `ReceiveFolder` property
- Debatches data into individual records
- Writes each record to MessageBox as a separate message

**As Destination:**
- Reads messages from MessageBox
- Transforms message data back to CSV format
- Writes CSV files to Azure Blob Storage

**Key Features:**
- Configurable field separator (supports UTF-8 characters)
- Column count validation (throws exception if inconsistent)
- Automatic error handling (moves malformed files to error folder)
- File system monitoring for `ReceiveFolder`

### SqlServerAdapter

**As Source:**
- Reads data from SQL Server tables
- Debatches into individual records
- Writes each record to MessageBox

**As Destination:**
- Reads messages from MessageBox
- Ensures destination table structure matches schema
- Writes records to SQL Server tables
- Dynamic schema management (creates/modifies tables automatically)

**Key Features:**
- Dynamic table creation based on CSV schema
- Type inference and conversion
- Transaction support for data integrity
- Connection pooling for performance

## IAdapter Interface

All adapters implement the `IAdapter` interface:

```csharp
public interface IAdapter
{
    string AdapterName { get; }
    string AdapterAlias { get; }
    bool SupportsRead { get; }
    bool SupportsWrite { get; }
    
    Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(
        string source, CancellationToken cancellationToken = default);
    
    Task WriteAsync(
        string destination, 
        List<string> headers, 
        List<Dictionary<string, string>> records, 
        CancellationToken cancellationToken = default);
    
    Task<Dictionary<string, ColumnTypeInfo>> GetSchemaAsync(
        string source, CancellationToken cancellationToken = default);
    
    Task EnsureDestinationStructureAsync(
        string destination, 
        Dictionary<string, ColumnTypeInfo> columnTypes, 
        CancellationToken cancellationToken = default);
}
```

## Adapter Instance Management

Each adapter instance has:
- **AdapterInstanceGuid**: Unique identifier for tracking
- **InstanceName**: User-editable name for UI display
- **IsEnabled**: Enable/disable flag for process control
- **InterfaceName**: Links adapter to interface configuration

## Implementation Details

### Constructor Pattern

Adapters accept dependencies via constructor injection:
- Service dependencies (e.g., `ICsvProcessingService`, `IDynamicTableService`)
- Configuration (e.g., `IAdapterConfigurationService`)
- Infrastructure clients (e.g., `BlobServiceClient`, `ApplicationDbContext`)
- MessageBox services (`IMessageBoxService`, `IMessageSubscriptionService`)
- Logger (`ILogger<T>`)

### Error Handling

- Adapters validate input data before processing
- Exceptions are caught and logged with context
- Failed records are isolated (not affecting successful ones)
- Error details are written to MessageBox for audit trail

### Performance Considerations

- Adapters use async/await for non-blocking I/O
- Batch processing for large datasets
- Connection pooling for database adapters
- Streaming for large file operations

## Adding New Adapters

To add a new adapter (e.g., JSON, SAP, REST API):

1. Create a new class implementing `IAdapter`
2. Implement all required methods
3. Register the adapter in dependency injection
4. Add adapter configuration support
5. No changes needed to existing code!

The system automatically recognizes new adapters and allows them to be used in interface configurations.





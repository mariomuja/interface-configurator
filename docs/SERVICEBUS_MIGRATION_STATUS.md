# Service Bus Migration - Implementation Status

## ✅ Completed

1. **Service Bus Service Interface** (`IServiceBusService.cs`)
   - Created interface with methods for sending/receiving messages
   - Includes message completion, abandonment, and dead lettering

2. **Service Bus Service Implementation** (`ServiceBusService.cs`)
   - Implemented Service Bus client using Azure.Messaging.ServiceBus
   - Topic naming: `interface-{interfaceName}`
   - Subscription naming: `destination-{destinationAdapterInstanceGuid}`
   - Batch message sending support
   - Message peeking for UI display

3. **NuGet Package**
   - Added `Azure.Messaging.ServiceBus` version 7.18.1 to main.csproj

4. **AdapterBase Updates**
   - Added `IServiceBusService` support
   - Created `WriteRecordsToServiceBusAsync` method
   - Kept MessageBox fallback for backward compatibility
   - Legacy `WriteRecordsToMessageBoxAsync` redirects to Service Bus method

5. **Program.cs Registration**
   - Registered `IServiceBusService` with connection string from environment
   - Kept `IMessageBoxService` registration for backward compatibility

6. **Documentation**
   - Created migration guide (`SERVICEBUS_MIGRATION.md`)
   - Created status document (this file)

## ⏳ Remaining Work

### 1. AdapterFactory Updates
**File**: `azure-functions/main/Services/AdapterFactory.cs`

**Changes needed**:
- Get `IServiceBusService` from service provider in `CreateCsvAdapter`, `CreateFileAdapter`, `CreateSftpAdapter` methods
- Pass `serviceBusService` parameter to adapter constructors

**Example**:
```csharp
var serviceBusService = _serviceProvider.GetService<IServiceBusService>();
// Then pass to adapter constructors
```

### 2. Adapter Constructor Updates
**Files**: 
- `azure-functions/main/Adapters/CsvAdapter.cs`
- `azure-functions/main/Adapters/FileAdapter.cs`
- `azure-functions/main/Adapters/SftpAdapter.cs`
- `azure-functions/main/Adapters/SqlServerAdapter.cs`

**Changes needed**:
- Add `IServiceBusService? serviceBusService = null` parameter to constructors
- Pass to `base()` constructor call
- Update `WriteRecordsToMessageBoxAsync` calls to `WriteRecordsToServiceBusAsync`

### 3. File Forwarding to Blob Containers
**Files**: 
- `azure-functions/main/Adapters/CsvAdapter.cs`
- `azure-functions/main/Adapters/FileAdapter.cs`
- `azure-functions/main/Adapters/SftpAdapter.cs`

**Changes needed**:
- When source adapters read files, forward them to blob containers:
  - Container: `csv-files`
  - Path: `{interfaceName}/{adapterInstanceGuid}/{timestamp}/{filename}`
- Do this in parallel with Service Bus message sending
- Use existing `BlobServiceClient` for file operations

**Example**:
```csharp
// After reading file content
if (AdapterRole == "Source" && !string.IsNullOrWhiteSpace(_interfaceName) && _adapterInstanceGuid.HasValue)
{
    var blobPath = $"csv-files/{_interfaceName}/{_adapterInstanceGuid.Value}/{DateTime.UtcNow:yyyyMMddHHmmss}/{fileName}";
    await UploadFileToBlobAsync(content, blobPath, cancellationToken);
}
```

### 4. Destination Adapter Service Bus Subscription
**File**: `azure-functions/main/Adapters/AdapterBase.cs`

**Changes needed**:
- Update `ReadMessagesFromMessageBoxAsync` to also support Service Bus
- Create new method `ReadMessagesFromServiceBusAsync`
- Use Service Bus receiver to get messages from subscription
- Complete messages after successful processing
- Abandon messages on error

**Implementation**:
```csharp
protected async Task<(List<string> headers, List<Dictionary<string, string>> records, List<ServiceBusMessage> processedMessages)?> ReadMessagesFromServiceBusAsync(
    CancellationToken cancellationToken = default)
{
    if (!AdapterRole.Equals("Destination", StringComparison.OrdinalIgnoreCase))
        return null;

    if (_serviceBusService == null || string.IsNullOrWhiteSpace(_interfaceName) || !_adapterInstanceGuid.HasValue)
        return null;

    var messages = await _serviceBusService.ReceiveMessagesAsync(
        _interfaceName, 
        _adapterInstanceGuid.Value, 
        maxMessages: _batchSize, 
        cancellationToken);

    // Process messages similar to MessageBox version
    // Complete messages after successful processing
}
```

### 5. UI Updates
**Files**:
- `frontend/src/app/components/transport/transport.component.ts`
- `frontend/src/app/components/transport/transport.component.html`
- `frontend/src/app/services/transport.service.ts`

**Changes needed**:
- Replace MessageBox card with Service Bus messages display
- Add API endpoint to get Service Bus messages for interface
- Update `loadMessageBoxData()` to `loadServiceBusMessages()`
- Show compact display with message count and recent messages
- Display messages from Service Bus topic (all source messages for interface)

**New API Endpoint** (in Azure Functions):
```csharp
[Function("GetServiceBusMessages")]
public async Task<IActionResult> GetServiceBusMessages(
    [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetServiceBusMessages/{interfaceName}")] HttpRequest req,
    string interfaceName)
{
    var messages = await _serviceBusService.GetRecentMessagesAsync(interfaceName, maxMessages: 100);
    return new OkObjectResult(messages);
}
```

### 6. Infrastructure Updates
**Files**:
- `bicep/main.bicep` or `terraform/main.tf`

**Changes needed**:
- Add Azure Service Bus namespace resource
- Add Service Bus topic creation (or create dynamically)
- Add Service Bus subscription creation (or create dynamically)
- Configure connection string in Function App settings

**Bicep Example**:
```bicep
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: 'sb-${environment}-${uniqueSuffix}'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

resource serviceBusTopic 'Microsoft.ServiceBus/namespaces/topics@2021-11-01' = {
  parent: serviceBusNamespace
  name: 'interface-${interfaceName}'
  // Topics created dynamically per interface
}
```

### 7. Database Cleanup
**Files**:
- `azure-functions/main/Data/MessageBoxDbContext.cs`
- SQL migration scripts

**Changes needed**:
- Remove `Messages` table (replaced by Service Bus)
- Remove `MessageSubscriptions` table (replaced by Service Bus subscriptions)
- Remove `AdapterSubscriptions` table (replaced by Service Bus subscriptions)
- Remove `MessageProcessing` table (replaced by Service Bus message handling)
- Keep `ProcessLogs` and `ProcessingStatistics` (still needed for logging)

**SQL Script**:
```sql
-- Drop unused MessageBox tables
DROP TABLE IF EXISTS MessageProcessing;
DROP TABLE IF EXISTS AdapterSubscriptions;
DROP TABLE IF EXISTS MessageSubscriptions;
DROP TABLE IF EXISTS Messages;
```

### 8. Update Documentation
**Files**:
- `docs/ARCHITECTURE_ADAPTERS.md`
- `docs/ARCHITECTURE_MESSAGEBOX.md` (update or archive)
- `docs/ARCHITECTURE_INTERFACE_CONFIGURATION.md`

**Changes needed**:
- Update architecture docs to reflect Service Bus usage
- Document Service Bus topic/subscription structure
- Update message flow diagrams
- Document file forwarding to blob containers

## Testing Checklist

- [ ] Source adapters send messages to Service Bus
- [ ] Files are forwarded to blob containers
- [ ] Destination adapters receive messages from Service Bus
- [ ] Messages are completed after successful processing
- [ ] Messages are abandoned on error
- [ ] Dead letter queue works correctly
- [ ] UI displays Service Bus messages
- [ ] Multiple destination instances can subscribe to same source
- [ ] Backward compatibility with MessageBox (fallback)

## Rollback Plan

If issues occur:
1. Set `UseServiceBus=false` in app settings (if implemented)
2. Adapters will fall back to MessageBox automatically
3. Service Bus messages can be manually migrated if needed

## Notes

- Service Bus topics/subscriptions should be created dynamically when interfaces are created
- Consider using Service Bus Management client for dynamic topic/subscription creation
- File forwarding happens in parallel with message sending (non-blocking)
- Keep MessageBox code for backward compatibility during migration period





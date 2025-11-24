# Container App Settings Synchronization

## Overview

This document describes how adapter instance settings are synchronized with container apps when users save settings in the UI.

## Architecture

### Settings Flow

1. **User saves settings in UI** → Settings dialog closes with all properties
2. **UI calls update API** → `UpdateSourceAdapterInstance` or `UpdateDestinationAdapterInstance`
3. **Backend updates database** → Adapter instance configuration saved
4. **Backend syncs to container app** → Container app's `adapter-config.json` updated in blob storage
5. **UI receives feedback** → Container app status and errors displayed to user

### Container App Configuration Storage

Each container app has:
- **Dedicated blob storage account**: `st{guid}`
- **Blob container**: `adapter-{guid}`
- **Configuration file**: `adapter-config.json` containing ALL adapter instance settings

### Settings Included

When settings are saved, **ALL** adapter instance properties are synchronized:

#### Source Adapter Settings
- Instance name, enabled status
- CSV: receive folder, file mask, batch size, field separator, adapter type (RAW/FILE/SFTP), CSV data, polling interval
- SFTP: host, port, username, password, SSH key, folder, file mask, connection pool size, buffer size
- SQL Server: server name, database, credentials, polling statement, polling interval, transaction settings, batch size
- SAP: all SAP connection and IDOC properties
- Dynamics 365: all Dynamics 365 connection and entity properties
- CRM: all CRM connection and entity properties

#### Destination Adapter Settings
- Instance name, enabled status
- CSV: destination folder, file mask, field separator
- SQL Server: server name, database, credentials, table name, custom statements (INSERT/UPDATE/DELETE), transaction settings
- SAP: all SAP connection and IDOC properties
- Dynamics 365: all Dynamics 365 connection and entity properties
- CRM: all CRM connection and entity properties
- JQ transformation: script file URI
- Source adapter subscription: GUID of source adapter to subscribe to

## Implementation

### Backend

#### UpdateDestinationAdapterInstance.cs
```csharp
// After updating database configuration:
var config = await _configService.GetConfigurationAsync(request.InterfaceName, cancellationToken);
var updatedInstance = config?.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == request.AdapterInstanceGuid);

if (updatedInstance != null)
{
    // Update container app with COMPLETE adapter instance (all properties)
    await _containerAppService.UpdateContainerAppConfigurationAsync(
        request.AdapterInstanceGuid,
        updatedInstance, // Full instance with all properties
        cancellationToken);
}
```

#### UpdateSourceAdapterInstance.cs
```csharp
// After updating database configuration:
var config = await _configService.GetConfigurationAsync(request.InterfaceName, cancellationToken);
var updatedInstance = config?.Sources.Values.FirstOrDefault(s => s.AdapterInstanceGuid == request.AdapterInstanceGuid);

if (updatedInstance != null)
{
    // Update container app with COMPLETE adapter instance (all properties)
    await _containerAppService.UpdateContainerAppConfigurationAsync(
        request.AdapterInstanceGuid,
        updatedInstance, // Full instance with all properties
        cancellationToken);
}
```

#### ContainerAppService.UpdateContainerAppConfigurationAsync
```csharp
public async Task UpdateContainerAppConfigurationAsync(
    Guid adapterInstanceGuid,
    object adapterConfiguration, // Full adapter instance object
    CancellationToken cancellationToken = default)
{
    // Get blob storage for this container app
    var storageAccountName = $"st{adapterInstanceGuid.ToString("N").Substring(0, 20)}";
    var containerName = $"adapter-{adapterInstanceGuid.ToString("N").Substring(0, 8)}";
    
    // Serialize ENTIRE adapter configuration object to JSON
    var configJson = JsonSerializer.Serialize(adapterConfiguration, new JsonSerializerOptions
    {
        WriteIndented = true
    });
    
    // Store in blob storage as adapter-config.json
    await blobClient.UploadAsync(new BinaryData(configJson), overwrite: true, cancellationToken);
}
```

### Frontend

#### Destination Adapter Settings Save
```typescript
// In updateDestinationInstance method:
this.transportService.updateDestinationAdapterInstance(
  interfaceName,
  instanceGuid,
  properties.instanceName,
  properties.isEnabled,
  JSON.stringify(configuration) // ALL settings included
).subscribe({
  next: (response: any) => {
    // Show container app status feedback
    if (response.containerAppStatus === 'Updated') {
      this.snackBar.open(
        `✅ Container app "${response.containerAppName}" configuration synced successfully.`,
        'OK',
        { duration: 5000 }
      );
    } else if (response.containerAppStatus === 'Error') {
      this.snackBar.open(
        `⚠️ Container app sync failed: ${response.containerAppError}`,
        'OK',
        { duration: 10000 }
      );
    }
  }
});
```

#### Source Adapter Settings Save
Source adapter settings are currently updated via individual property methods. To ensure container app sync, all settings should be collected and sent via `updateSourceAdapterInstance` with the full configuration.

## Error Handling

### Container App Status Values

- **Updated**: Container app configuration successfully updated
- **Created**: Container app was created (didn't exist before)
- **Error**: Error updating container app configuration
- **CreateError**: Error creating container app
- **Skipped**: Container app update skipped (adapter instance not found)
- **NotCreated**: Container app not created (adapter instance not found)

### UI Feedback

The UI displays container app status to users:
- ✅ **Success**: Green snackbar with success message
- ⚠️ **Warning**: Yellow snackbar with error details
- ℹ️ **Info**: Blue snackbar with informational message

## Container App Creation

When an adapter instance is added:
1. Adapter instance created in database
2. Container app created automatically
3. Full adapter instance configuration stored in `adapter-config.json`
4. Container app configured with environment variables pointing to config file

When settings are updated:
1. If container app exists → Configuration updated
2. If container app doesn't exist → Container app created automatically

## Verification

To verify settings are synchronized:
1. Save settings in UI
2. Check container app blob storage: `st{guid}/adapter-{guid}/adapter-config.json`
3. Verify all settings are present in JSON file
4. Check UI feedback for container app status

## Troubleshooting

### Settings Not Syncing
- Check Function App logs for errors
- Verify container app exists: `GetContainerAppStatus` API
- Check blob storage access permissions
- Verify adapter instance GUID is correct

### Container App Not Created
- Check Function App logs
- Verify ACR credentials are configured
- Check Container App Environment exists
- Verify resource group permissions

### Partial Settings Missing
- Ensure UI sends complete configuration object
- Verify backend receives full adapter instance object
- Check JSON serialization includes all properties


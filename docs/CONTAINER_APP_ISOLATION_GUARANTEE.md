# Container App Isolation Guarantee

## Overview

This document describes how the system ensures that **each adapter instance gets its own isolated container app**, providing complete process isolation between adapter instances.

## Why the Azure Function App Still Matters

Even though adapters now run inside Azure Container Apps, the Azure Function App remains the **control plane** for the platform:

- **Configuration API surface** – All UI flows and automation call Function App endpoints (`CreateInterfaceConfiguration`, `UpdateSourceAdapterInstance`, `ToggleInterfaceConfiguration`, etc.) to create/update adapter metadata, toggle enablement, and validate settings.
- **Container-app orchestration** – The Function App provisions blob storage, secrets, and Service Bus subscriptions, and it creates/updates/deletes the per-adapter container apps (`CreateContainerAppAsync`, `DeleteContainerApp`). Without these triggers, container apps would never be instantiated or cleaned up.
- **Central diagnostics & governance** – Endpoints such as `GetContainerAppStatus`, `GetProcessLogs`, `Diagnose`, and `HealthCheck` expose state to the frontend, enforce auth/CORS, and provide auditing. Container apps focus purely on adapter workloads and do not host these APIs.

In short, container apps execute the adapters, but the Function App orchestrates their lifecycle and serves as the single entry point for configuration and monitoring.

## Isolation Mechanism

### Unique Container App per Instance

Each adapter instance (Source or Destination) receives a **unique container app** based on its `AdapterInstanceGuid`:

```
Container App Name: ca-{adapterInstanceGuid}
```

Example:
- Adapter Instance GUID: `a1b2c3d4-e5f6-7890-abcd-ef1234567890`
- Container App Name: `ca-a1b2c3d4e5f67890abcd`

### Container App Naming

The container app name is generated using the `GetContainerAppName` method:

```csharp
public string GetContainerAppName(Guid adapterInstanceGuid)
{
    // Container app names must be lowercase, alphanumeric, and hyphens only
    // Max 32 characters
    var guidStr = adapterInstanceGuid.ToString("N");
    return $"ca-{guidStr.Substring(0, Math.Min(24, guidStr.Length))}";
}
```

**Key Properties:**
- **Uniqueness**: Each `AdapterInstanceGuid` is unique, ensuring unique container app names
- **Deterministic**: Same GUID always produces the same container app name
- **Azure-compliant**: Names follow Azure Container App naming rules (lowercase, alphanumeric, hyphens)

## Isolation Guarantees

### 1. Separate Container Apps

- ✅ Each adapter instance has its own container app
- ✅ Container apps are completely isolated from each other
- ✅ No shared resources between adapter instances
- ✅ Each container app runs independently

### 2. Separate Blob Storage

Each container app has:
- **Dedicated storage account**: `st{guid}` (first 20 chars of GUID)
- **Dedicated blob container**: `adapter-{guid}` (first 8 chars of GUID)
- **Isolated configuration**: `adapter-config.json` with instance-specific settings

### 3. Separate Environment Variables

Each container app receives:
- `ADAPTER_INSTANCE_GUID`: Unique identifier for this instance
- `ADAPTER_NAME`: Type of adapter (CSV, SqlServer, etc.)
- `ADAPTER_TYPE`: Source or Destination
- `INTERFACE_NAME`: Interface this adapter belongs to
- `INSTANCE_NAME`: User-friendly instance name
- `BLOB_CONNECTION_STRING`: Connection to instance-specific blob storage
- `BLOB_CONTAINER_NAME`: Instance-specific blob container name
- `ADAPTER_CONFIG_PATH`: Path to instance-specific configuration file

### 4. Separate Process Execution

- Each container app runs as a separate process
- No shared memory or state between container apps
- Independent scaling (each can scale independently)
- Independent resource allocation (CPU, memory)

## Container App Creation Flow

### Destination Adapter Instance

1. **User adds destination adapter instance** → `AddDestinationAdapterInstance` API called
2. **Instance created in database** → `AdapterInstanceGuid` generated/assigned
3. **Container app created** → `CreateContainerAppAsync` called with instance GUID
4. **Unique container app name generated** → `ca-{guid}`
5. **Blob storage created** → `st{guid}` storage account and `adapter-{guid}` container
6. **Configuration stored** → `adapter-config.json` with all instance settings
7. **Container app deployed** → Isolated container app running

### Source Adapter Instance

1. **User updates source adapter settings** → `UpdateSourceAdapterInstance` API called
2. **Instance updated in database** → `AdapterInstanceGuid` used/created
3. **Container app checked** → `ContainerAppExistsAsync` called
4. **If not exists** → `CreateContainerAppAsync` called with instance GUID
5. **Unique container app name generated** → `ca-{guid}`
6. **Blob storage created** → `st{guid}` storage account and `adapter-{guid}` container
7. **Configuration stored** → `adapter-config.json` with all instance settings
8. **Container app deployed** → Isolated container app running

## Verification

### Check Container App Isolation

To verify that each instance has its own container app:

```bash
# List all container apps
az containerapp list --resource-group rg-interface-configurator --query "[].{Name:name, Guid:tags.adapterInstanceGuid}" -o table

# Check specific container app
az containerapp show --name ca-{guid} --resource-group rg-interface-configurator
```

### Check Blob Storage Isolation

```bash
# List storage accounts (each instance has its own)
az storage account list --resource-group rg-interface-configurator --query "[].{Name:name, Tags:tags.adapterInstanceGuid}" -o table

# Check blob container
az storage container list --account-name st{guid} --account-key {key}
```

## Error Handling

### Duplicate Container App Prevention

The system checks if a container app already exists before creating:

```csharp
var exists = await _containerAppService.ContainerAppExistsAsync(
    adapterInstanceGuid,
    cancellationToken);

if (!exists)
{
    // Create new container app
}
else
{
    // Log warning - container app already exists
}
```

### Container App Name Collision

Since container app names are derived from GUIDs:
- ✅ **No collisions possible**: GUIDs are unique
- ✅ **Deterministic**: Same GUID always produces same name
- ✅ **Azure-enforced**: Azure prevents duplicate container app names in same environment

## Benefits of Isolation

1. **Process Isolation**: Each adapter instance runs in its own process
2. **Configuration Isolation**: Each instance has its own settings
3. **Data Isolation**: Each instance has its own blob storage
4. **Failure Isolation**: Failure in one instance doesn't affect others
5. **Scaling Isolation**: Each instance can scale independently
6. **Resource Isolation**: Each instance has dedicated CPU/memory
7. **Security Isolation**: Each instance has its own secrets and credentials

## Implementation Details

### Container App Creation

```csharp
public async Task<ContainerAppInfo> CreateContainerAppAsync(
    Guid adapterInstanceGuid,  // Unique identifier
    string adapterName,
    string adapterType,
    string interfaceName,
    string instanceName,
    object adapterConfiguration,  // Complete instance configuration
    CancellationToken cancellationToken = default)
{
    // Generate unique container app name
    var containerAppName = GetContainerAppName(adapterInstanceGuid);
    
    // Create dedicated blob storage
    var blobStorageInfo = await CreateBlobStorageForInstanceAsync(
        adapterInstanceGuid, resourceGroup, cancellationToken);
    
    // Store instance-specific configuration
    await StoreAdapterConfigurationAsync(
        blobStorageInfo.ConnectionString,
        blobStorageInfo.ContainerName,
        adapterConfiguration,  // ALL settings for this instance
        cancellationToken);
    
    // Create container app with instance-specific environment variables
    var containerAppData = new ContainerAppData(_location)
    {
        // ... configuration with ADAPTER_INSTANCE_GUID, etc.
    };
    
    // Create/update container app
    await containerAppCollection.CreateOrUpdateAsync(
        WaitUntil.Started,
        containerAppName,  // Unique name per instance
        containerAppData,
        cancellationToken);
}
```

## Summary

✅ **Each adapter instance gets its own container app**
✅ **Container app names are unique** (derived from GUID)
✅ **Complete process isolation** between instances
✅ **Separate blob storage** for each instance
✅ **Separate configuration** for each instance
✅ **Independent scaling and resource allocation**

This ensures that adapter instances are completely isolated from each other, providing a clean separation between processes for receiving and sending data.


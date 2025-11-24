# Container App Isolation for Adapter Instances

## Overview

This document describes the implementation of process isolation for adapter instances using Azure Container Apps. Each adapter instance now runs in its own isolated container app with dedicated blob storage.

## Architecture

### Key Components

1. **Container App Service** (`ContainerAppService.cs`)
   - Manages the lifecycle of container apps for adapter instances
   - Creates/deletes container apps dynamically
   - Creates dedicated blob storage for each instance

2. **API Endpoints**
   - `CreateContainerApp` - Creates a container app for an adapter instance
   - `DeleteContainerApp` - Deletes a container app for an adapter instance
   - `GetContainerAppStatus` - Gets the status of a container app

3. **Infrastructure**
   - Bicep module: `bicep/container-app-module.bicep`
   - Terraform module: `terraform/container-app-module.tf`
   - Container App Environment: `cae-adapter-instances`

4. **UI Integration**
   - Adapter cards display container app status
   - Status indicators show: Creating, Running, Stopped, Error, Unknown
   - Users are informed when container apps are being created/deleted

## Implementation Details

### Container App Creation

When a user adds a new adapter instance (source or destination):
1. The adapter instance is created in the database
2. A container app is automatically created for that instance
3. A dedicated blob storage account and container are created
4. The adapter instance configuration (all settings) is serialized to JSON and stored in the container app's blob storage as `adapter-config.json`
5. The container app is configured with:
   - Adapter-specific Docker image
   - Environment variables (adapter GUID, name, type, interface name, config file path)
   - Blob storage connection string
   - Resource limits (0.25 CPU, 0.5Gi memory)

### Adapter Instance Settings Management

Each container app maintains its own copy of the adapter instance settings:
- **Configuration Storage**: Settings are stored in `adapter-config.json` in the container app's dedicated blob storage
- **Settings Include**: All adapter properties (CSV settings, SQL Server settings, SFTP settings, SAP settings, Dynamics365 settings, CRM settings, etc.)
- **Automatic Updates**: When settings are changed in the UI, the container app configuration is automatically updated
- **Isolation**: Each container app instance has its own settings file, ensuring complete isolation between instances

### Container App Deletion

When a user deletes an adapter instance:
1. The adapter instance is removed from the database
2. The container app is deleted
3. The blob storage account is deleted

### Docker Images

Each adapter type has its own Docker image:
- `csv-adapter:latest`
- `sqlserver-adapter:latest`
- `file-adapter:latest`
- `sftp-adapter:latest`
- `sap-adapter:latest`
- `dynamics365-adapter:latest`
- `crm-adapter:latest`
- `generic-adapter:latest` (fallback)

### Blob Storage Isolation

Each container app instance has:
- Its own storage account: `st{guid}`
- Its own blob container: `adapter-{guid}`
- Isolated data storage (no cross-contamination)

## Configuration

### Required Environment Variables

For the Azure Function App:
- `ResourceGroupName` - Resource group name (default: `rg-interface-configurator`)
- `Location` - Azure region (default: `Central US`)
- `ContainerAppEnvironmentName` - Container app environment name (default: `cae-adapter-instances`)
- `ContainerRegistryServer` - Container registry server URL
- `ContainerRegistryUsername` - Container registry username
- `ContainerRegistryPassword` - Container registry password

### Infrastructure Setup

1. Deploy Container App Environment:
   ```bash
   # Bicep
   az deployment group create --resource-group rg-interface-configurator --template-file bicep/main.bicep
   
   # Terraform
   terraform apply
   ```

2. Build and push Docker images for each adapter type (see Docker Images section below)

## Docker Images

### Creating Docker Images

Each adapter type needs a Docker image. Example structure:

```dockerfile
# Dockerfile for CSV Adapter
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY ./publish/ .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CsvAdapter.dll"]
```

### Building Images

```bash
# Build CSV adapter image
docker build -t csv-adapter:latest -f Dockerfile.csv .

# Tag for Azure Container Registry
docker tag csv-adapter:latest <registry>.azurecr.io/csv-adapter:latest

# Push to registry
docker push <registry>.azurecr.io/csv-adapter:latest
```

Repeat for each adapter type.

## UI Features

### Container App Status Display

- **Creating**: Container app is being created (yellow indicator)
- **Running**: Container app is running (green indicator)
- **Stopped**: Container app is stopped (red indicator)
- **Error**: Error creating/running container app (red indicator)
- **Unknown**: Status unknown (grey indicator)

### User Notifications

- Users are informed when container apps are being created
- Status is checked periodically and displayed on adapter cards
- Error messages are shown if container app creation fails

## API Usage

### Create Container App

```http
POST /api/CreateContainerApp
Content-Type: application/json

{
  "adapterInstanceGuid": "guid-here",
  "adapterName": "CSV",
  "adapterType": "Source",
  "interfaceName": "FromCsvToSqlServerExample",
  "instanceName": "Source"
}
```

### Delete Container App

```http
POST /api/DeleteContainerApp
Content-Type: application/json

{
  "adapterInstanceGuid": "guid-here"
}
```

### Get Container App Status

```http
GET /api/GetContainerAppStatus?adapterInstanceGuid=guid-here
```

### Update Container App Configuration

```http
POST /api/UpdateContainerAppConfiguration
Content-Type: application/json

{
  "adapterInstanceGuid": "guid-here",
  "interfaceName": "FromCsvToSqlServerExample",
  "adapterType": "Destination"
}
```

This endpoint automatically retrieves the latest adapter instance configuration and updates the container app's `adapter-config.json` file.

## Future Enhancements

1. **Container App Scaling**: Auto-scale based on workload
2. **Health Checks**: Implement health check endpoints in adapter containers
3. **Logging**: Centralized logging for all container apps
4. **Monitoring**: Application Insights integration for container apps
5. **Resource Optimization**: Dynamic resource allocation based on adapter type

## Troubleshooting

### Container App Not Created

- Check Azure Function App logs for errors
- Verify container registry credentials are correct
- Ensure Container App Environment exists
- Check resource group permissions

### Container App Status Unknown

- Verify network connectivity
- Check Azure Function App has proper permissions
- Review Application Insights logs

### Blob Storage Not Created

- Check storage account naming (must be unique)
- Verify resource group has storage account quota
- Review Azure Function App logs


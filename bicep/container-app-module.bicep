// Bicep module for creating a container app for an adapter instance
// This module is called dynamically when an adapter instance is created

@description('Resource group name')
param resourceGroupName string

@description('Location for resources')
param location string = 'Central US'

@description('Container App Environment name')
param containerAppEnvironmentName string = 'cae-adapter-instances'

@description('Adapter instance GUID')
param adapterInstanceGuid string

@description('Adapter name (CSV, SqlServer, etc.)')
param adapterName string

@description('Adapter type (Source or Destination)')
param adapterType string = 'Source'

@description('Interface name')
param interfaceName string

@description('Instance name')
param instanceName string

@description('Container registry server')
param containerRegistryServer string

@description('Container registry username')
@secure()
param containerRegistryUsername string

@description('Container registry password')
@secure()
param containerRegistryPassword string

@description('Blob storage connection string')
@secure()
param blobStorageConnectionString string

@description('Blob container name')
param blobContainerName string

var containerAppName = 'ca-${substring(adapterInstanceGuid, 0, 24)}'
var adapterImage = '${containerRegistryServer}/${toLower(adapterName)}-adapter:latest'

// Get existing container app environment
resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' existing = {
  name: containerAppEnvironmentName
  scope: resourceGroup(resourceGroupName)
}

// Create blob storage account for this instance (if not using existing connection string)
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'st${substring(adapterInstanceGuid, 0, 20)}'
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

// Create blob container
resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: blobContainerName
  parent: storageAccount::blobServices
  properties: {
    publicAccess: 'None'
  }
}

// Create container app
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
      registries: [
        {
          server: containerRegistryServer
          username: containerRegistryUsername
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: [
        {
          name: 'registry-password'
          value: containerRegistryPassword
        }
        {
          name: 'blob-connection-string'
          value: blobStorageConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: containerAppName
          image: adapterImage
          env: [
            {
              name: 'ADAPTER_INSTANCE_GUID'
              value: adapterInstanceGuid
            }
            {
              name: 'ADAPTER_NAME'
              value: adapterName
            }
            {
              name: 'ADAPTER_TYPE'
              value: adapterType
            }
            {
              name: 'INTERFACE_NAME'
              value: interfaceName
            }
            {
              name: 'INSTANCE_NAME'
              value: instanceName
            }
            {
              name: 'BLOB_CONNECTION_STRING'
              secretRef: 'blob-connection-string'
            }
            {
              name: 'BLOB_CONTAINER_NAME'
              value: blobContainerName
            }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

output containerAppName string = containerApp.name
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output blobStorageAccountName string = storageAccount.name
output blobContainerName string = blobContainer.name



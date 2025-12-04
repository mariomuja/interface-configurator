// Bicep template for provisioning Azure resources needed for integration tests
// This ensures a consistent, repeatable test environment

@description('Azure Storage Account name for blob storage')
param storageAccountName string

@description('Service Bus Namespace name')
param serviceBusNamespaceName string

@description('SQL Server name')
param sqlServerName string

@description('SQL Database name')
param sqlDatabaseName string

@description('Azure Container Registry name')
param acrName string

@description('Service Principal Object ID for role assignments')
param servicePrincipalObjectId string

@description('Location for all resources')
param location string = resourceGroup().location

// Reference existing Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

// Create Blob Containers for global use
resource functionConfigContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccountName}/default/function-config'
  properties: {
    publicAccess: 'None'
  }
}

resource terraformStateContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccountName}/default/terraform-state'
  properties: {
    publicAccess: 'None'
  }
}

resource backupContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccountName}/default/backup'
  properties: {
    publicAccess: 'None'
  }
}

// Create Blob Container for all adapter instances
// Individual adapter instance folders (guid/incoming, guid/error, guid/processed) 
// are created dynamically by the application
resource adapterDataContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccountName}/default/adapter-data'
  properties: {
    publicAccess: 'None'
    metadata: {
      description: 'Container for all file-based adapter instances (CSV, SFTP, File, SAP)'
      structure: 'Dynamic folders: {adapter-instance-guid}/{incoming|error|processed}'
    }
  }
}

// Reference existing Service Bus Namespace
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' existing = {
  name: serviceBusNamespaceName
}

// Create Service Bus Topics for integration tests
resource testTopic1 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'interface-test-interface'
  properties: {
    maxSizeInMegabytes: 1024
    defaultMessageTimeToLive: 'P14D'
    enablePartitioning: false
  }
}

resource testTopic1Subscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: testTopic1
  name: 'destination-test'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT5M'
    requiresSession: false
  }
}

resource testTopic2 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'interface-test-interface-csv'
  properties: {
    maxSizeInMegabytes: 1024
    defaultMessageTimeToLive: 'P14D'
    enablePartitioning: false
  }
}

resource testTopic2Subscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: testTopic2
  name: 'destination-test'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT5M'
    requiresSession: false
  }
}

resource testTopic3 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'interface-test-interface-sftp'
  properties: {
    maxSizeInMegabytes: 1024
    defaultMessageTimeToLive: 'P14D'
    enablePartitioning: false
  }
}

resource testTopic3Subscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: testTopic3
  name: 'destination-test'
  properties: {
    maxDeliveryCount: 10
    lockDuration: 'PT5M'
    requiresSession: false
  }
}

// Reference existing ACR
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: acrName
}

// Grant AcrPull role to Service Principal (for integration tests)
resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acr
  name: guid(acr.id, servicePrincipalObjectId, 'AcrPull')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d') // AcrPull
    principalId: servicePrincipalObjectId
    principalType: 'ServicePrincipal'
  }
}

// Grant AcrPush role to Service Principal (for CI/CD pipeline)
resource acrPushRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acr
  name: guid(acr.id, servicePrincipalObjectId, 'AcrPush')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8311e382-0749-4cb8-b61a-304f252e45ec') // AcrPush
    principalId: servicePrincipalObjectId
    principalType: 'ServicePrincipal'
  }
}

// Note: SQL Server tables are NOT provisioned via Bicep
// Reason: Database schema should be managed by:
//   1. Entity Framework Core Migrations (preferred)
//   2. SQL Scripts in version control
//   3. Database projects (SSDT)
// 
// Bicep is for infrastructure, not application schema

output storageAccountId string = storageAccount.id
output adapterDataContainerName string = 'adapter-data'
output serviceBusNamespaceId string = serviceBusNamespace.id
output testTopics array = [
  testTopic1.name
  testTopic2.name
  testTopic3.name
]


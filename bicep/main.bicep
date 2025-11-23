// Note: subscriptionId and tenantId are not used in the template
// They are kept for reference but Azure CLI handles authentication

@description('Name of the resource group')
@allowed([
  'rg-interface-configurator'
])
param resourceGroupName string = 'rg-interface-configurator'

@description('Azure region for resources')
param location string = 'West Europe'

@description('Azure region for SQL Server (if different from main location, leave empty to use main location)')
param sqlLocation string = ''

@description('Environment name (e.g., dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'prod'

@description('Name for storage account (descriptive, no suffix, no hyphens - Azure requirement)')
param storageAccountName string = 'stappgeneral'

@description('Name for SQL Server (descriptive, no suffix)')
param sqlServerName string = 'sql-main-database'

@description('Name of the SQL database')
param sqlDatabaseName string = 'app-database'

@description('SQL Server administrator login')
@secure()
param sqlAdminLogin string

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

@description('SQL Database license type (LicenseIncluded or BasePrice)')
@allowed([
  'LicenseIncluded'
  'BasePrice'
])
param sqlLicenseType string = 'LicenseIncluded'

@description('SQL Database SKU name (e.g., S0, S1, P1, GP_S_Gen5_2, BC_Gen5_2)')
param sqlSkuName string = 'S0'

@description('Maximum size in GB for SQL Database')
param sqlMaxSizeGb int = 2

@description('Enable zone redundancy for SQL Database')
param sqlZoneRedundant bool = false

@description('Allow current IP address to access SQL Server')
param allowCurrentIp bool = false

@description('Current IP address for SQL firewall rule')
param currentIpAddress string = ''

@description('Name for Functions storage account (descriptive, no suffix, no hyphens - Azure requirement)')
param functionsStorageName string = 'stfunccsvprocessor'

@description('Name for Functions app service plan (descriptive, no suffix)')
param functionsAppPlanName string = 'plan-func-csv-processor'

@description('Base name for Functions app (suffix will be added)')
param functionsAppName string = 'func-integration-main'

@description('SKU name for Functions app service plan. Use Y1 for Consumption (current), EP1 for Flex Consumption (migration required before Sep 2028)')
@allowed([
  'Y1'
  'EP1'
])
param functionsSkuName string = 'Y1'

@description('Enable Azure Function App deployment')
param enableFunctionApp bool = false

@description('JWT secret for authentication (reserved for future use)')
@secure()
param jwtSecret string = ''

@description('List of allowed CORS origins')
param corsAllowedOrigins array = []

@description('GitHub repository URL for source control deployment (reserved for future use)')
param githubRepoUrl string = ''

@description('GitHub branch for source control deployment (reserved for future use)')
param githubBranch string = 'main'

@description('Path to the Function App code in the repository (reserved for future use)')
param githubRepoPath string = 'azure-functions/main'

// Note: No random suffix - using descriptive names directly
// Azure resource names must be globally unique, so descriptive names are used

// Tags (used in resource tags below)
var commonTags = {
  Environment: environment
  Project: 'Infrastructure'
}

// Note: Resource Group is assumed to exist or will be created by the deployment command
// Storage Account for general use
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false // Disable public blob access for security
    // Enable Event Grid for blob events
    isHnsEnabled: false
  }
  tags: commonTags
}


// Blob Services (implicit child resource of storage account)
resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

// Blob Container: csv-files
// Single container with three virtual folders:
// - csv-incoming/ : New CSV files land here and trigger the Azure Function Blob Trigger
// - csv-processed/ : Successfully processed CSV files are moved here
// - csv-error/ : CSV files that failed processing are moved here
resource csvFilesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: 'csv-files'
  properties: {
    publicAccess: 'None' // Private access - uses connection string authentication
    metadata: {
      description: 'Container for CSV files with virtual folders: csv-incoming, csv-processed, csv-error'
      purpose: 'csv-storage'
    }
  }
}

// Azure SQL Server
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: sqlLocation != '' ? sqlLocation : location
  properties: {
    version: '12.0'
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
  identity: {
    type: 'SystemAssigned'
  }
  tags: commonTags
}

// SQL Server Firewall Rule - Allow Azure Services
resource sqlFirewallRuleAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// SQL Server Firewall Rule - Allow All IPs (for Vercel Serverless Functions)
resource sqlFirewallRuleAllowAll 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAllIPs'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '255.255.255.255'
  }
}

// SQL Server Firewall Rule - Allow current IP (if provided)
resource sqlFirewallRuleCurrentIp 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = if (allowCurrentIp && currentIpAddress != '') {
  parent: sqlServer
  name: 'AllowCurrentIP'
  properties: {
    startIpAddress: currentIpAddress
    endIpAddress: currentIpAddress
  }
}

// Azure SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: sqlLocation != '' ? sqlLocation : location
  sku: {
    name: sqlSkuName
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    licenseType: sqlLicenseType
    maxSizeBytes: sqlMaxSizeGb * 1024 * 1024 * 1024 // Convert GB to bytes
    zoneRedundant: sqlZoneRedundant
  }
  tags: commonTags
}

// Application Insights for Function App monitoring
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = if (enableFunctionApp) {
  name: '${functionsAppName}-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
    WorkspaceResourceId: null
  }
  tags: commonTags
}

// Storage Account for Functions
resource functionsStorageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: functionsStorageName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
  tags: commonTags
}

// App Service Plan for Azure Functions
resource functionsAppServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: functionsAppPlanName
  location: location
  kind: 'linux'
  sku: {
    name: functionsSkuName
  }
  properties: {
    reserved: true // Required for Linux
  }
  tags: commonTags
}

// Linux Function App
// CSV to SQL Server processor - processes CSV blobs and stores data in SQL Database
resource functionApp 'Microsoft.Web/sites@2023-01-01' = if (enableFunctionApp) {
  name: functionsAppName
  location: location
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: functionsAppServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'AZURE_FUNCTIONS_ENVIRONMENT'
          value: environment
        }
        {
          name: 'AZURE_SQL_SERVER'
          value: sqlServer.properties.fullyQualifiedDomainName
        }
        {
          name: 'AZURE_SQL_DATABASE'
          value: sqlDatabaseName
        }
        {
          name: 'AZURE_SQL_USER'
          value: sqlAdminLogin
        }
        {
          name: 'AZURE_SQL_PASSWORD'
          value: sqlAdminPassword
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${functionsStorageAccount.name};AccountKey=${functionsStorageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
        }
        {
          name: 'WEBSITE_USE_PLACEHOLDER'
          value: '0'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${functionsStorageAccount.name};AccountKey=${functionsStorageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(replace(functionsAppName, '-', ''))
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: enableFunctionApp && applicationInsights != null ? applicationInsights.properties.InstrumentationKey : ''
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: enableFunctionApp && applicationInsights != null ? applicationInsights.properties.ConnectionString : ''
        }
        {
          name: 'MainStorageConnection'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
        }
      ]
      cors: corsAllowedOrigins != [] ? {
        allowedOrigins: corsAllowedOrigins
      } : null
    }
    httpsOnly: true
  }
  tags: commonTags
}

// Event Grid System Topic for Storage Account
// Azure automatically creates a system topic when we create an event subscription
// Note: Event Grid Subscription removed - Azure Function uses native Blob Trigger
// The Function App's main function uses a Blob Trigger binding,
// which automatically triggers when blobs are created in the configured container.
// Event Grid subscriptions only support Event Grid triggers, not Blob triggers.

// Outputs
output resourceGroupName string = resourceGroupName
output resourceGroupLocation string = location
output storageAccountName string = storageAccount.name
output storageAccountConnectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${az.environment().suffixes.storage}'
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output sqlConnectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
output functionAppName string = enableFunctionApp && functionApp != null ? functionApp.name : ''
output functionAppUrl string = enableFunctionApp && functionApp != null ? 'https://${functionApp.properties.defaultHostName}' : ''
output functionsStorageAccountName string = functionsStorageAccount.name
output applicationInsightsName string = enableFunctionApp && applicationInsights != null ? applicationInsights.name : ''
output applicationInsightsInstrumentationKey string = enableFunctionApp && applicationInsights != null ? applicationInsights.properties.InstrumentationKey : ''


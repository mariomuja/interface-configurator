// Create only the SQL databases
@description('SQL Server resource')
param sqlServer resource 'Microsoft.Sql/servers@2023-05-01-preview'

param sqlDatabaseName string = 'app-database'
param sqlSkuName string = 'S0'
param sqlMaxSizeGb int = 2
param sqlLicenseType string = 'LicenseIncluded'
param sqlZoneRedundant bool = false

// Azure SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: resourceGroup().location
  sku: {
    name: sqlSkuName
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    licenseType: sqlLicenseType
    maxSizeBytes: sqlMaxSizeGb * 1024 * 1024 * 1024
    zoneRedundant: sqlZoneRedundant
  }
}

// MessageBox Database
resource messageBoxDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: 'MessageBox'
  location: resourceGroup().location
  sku: {
    name: sqlSkuName
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    licenseType: sqlLicenseType
    maxSizeBytes: sqlMaxSizeGb * 1024 * 1024 * 1024
    zoneRedundant: sqlZoneRedundant
  }
}


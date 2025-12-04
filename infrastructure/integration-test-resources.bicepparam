// Parameters file for integration test resources
// Override these in CI/CD pipeline or use environment-specific param files

using './integration-test-resources.bicep'

// These should be set via environment variables in Jenkins
param storageAccountName = readEnvironmentVariable('AZURE_STORAGE_ACCOUNT_NAME', 'stinterfaceconfig')
param serviceBusNamespaceName = readEnvironmentVariable('SERVICE_BUS_NAMESPACE', 'sb-interface-configurator')
param sqlServerName = readEnvironmentVariable('AZURE_SQL_SERVER_NAME', 'sql-interface-config')
param sqlDatabaseName = readEnvironmentVariable('AZURE_SQL_DATABASE', 'InterfaceConfigDb')
param acrName = readEnvironmentVariable('ACR_NAME', 'acrinterfaceconfig')
param servicePrincipalObjectId = readEnvironmentVariable('AZURE_SERVICE_PRINCIPAL_OBJECT_ID', '')


output "resource_group_name" {
  description = "Name of the resource group"
  value       = azurerm_resource_group.main.name
}

output "resource_group_location" {
  description = "Location of the resource group"
  value       = azurerm_resource_group.main.location
}

# Storage account outputs commented out - general storage account not currently used
# output "storage_account_name" {
#   description = "Name of the storage account"
#   value       = azurerm_storage_account.main.name
# }
#
# output "storage_account_connection_string" {
#   description = "Connection string for the storage account"
#   value       = azurerm_storage_account.main.primary_connection_string
#   sensitive   = true
# }

output "sql_server_name" {
  description = "Name of the SQL Server"
  value       = azurerm_mssql_server.main.name
}

output "sql_server_fqdn" {
  description = "Fully qualified domain name of the SQL Server"
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "sql_database_name" {
  description = "Name of the SQL database"
  value       = azurerm_mssql_database.main.name
}

output "sql_connection_string" {
  description = "SQL Server connection string"
  value       = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${var.sql_database_name};Persist Security Info=False;User ID=${var.sql_admin_login};Password=${var.sql_admin_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  sensitive   = true
}

# Backend API runs on Vercel Serverless Functions, not Azure App Service
# output "backend_app_service_name" {
#   description = "Name of the backend App Service"
#   value       = azurerm_linux_web_app.backend.name
# }
#
# output "backend_app_service_url" {
#   description = "URL of the backend App Service"
#   value       = "https://${azurerm_linux_web_app.backend.default_hostname}"
# }

output "function_app_name" {
  description = "Name of the Function App (if enabled)"
  value       = var.enable_function_app ? azurerm_linux_function_app.main[0].name : null
}

output "function_app_url" {
  description = "URL of the Function App (if enabled)"
  value       = var.enable_function_app ? "https://${azurerm_linux_function_app.main[0].default_hostname}" : null
}

output "functions_storage_account_name" {
  description = "Name of the Functions storage account"
  value       = azurerm_storage_account.functions.name
}

output "service_bus_namespace_name" {
  description = "Name of the Service Bus namespace"
  value       = azurerm_servicebus_namespace.main.name
}

output "service_bus_namespace_connection_string" {
  description = "Connection string for the Service Bus namespace"
  value       = azurerm_servicebus_namespace_authorization_rule.root_manage.primary_connection_string
  sensitive   = true
}

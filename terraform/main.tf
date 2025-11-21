terraform {
  required_version = ">= 1.0"
  
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
    archive = {
      source  = "hashicorp/archive"
      version = "~> 2.4"
    }
  }
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }
  
  # Service Principal Authentication
  # If variables are null, provider will automatically use ARM_* environment variables
  subscription_id = var.subscription_id != null ? var.subscription_id : null
  client_id       = var.client_id != null ? var.client_id : null
  client_secret   = var.client_secret != null ? var.client_secret : null
  tenant_id       = var.tenant_id != null ? var.tenant_id : null
}

# Note: No random suffix - using descriptive names directly
# Azure resource names must be globally unique, so descriptive names are used

# Resource Group
resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location

  tags = {
    Environment = var.environment
    Project     = "Infrastructure"
  }
}

# Storage Account for general use (Blob Storage for CSV files)
resource "azurerm_storage_account" "main" {
  name                     = var.storage_account_name
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  
  # Disable public blob access for security
  allow_nested_items_to_be_public = false

  tags = {
    Environment = var.environment
  }
}

# Blob Container: csv-files
# Single container with three virtual folders:
# - csv-incoming/ : New CSV files land here and trigger the Azure Function Blob Trigger
# - csv-processed/ : Successfully processed CSV files are moved here
# - csv-error/ : CSV files that failed processing are moved here
resource "azurerm_storage_container" "csv_files" {
  name                  = "csv-files"
  storage_account_name  = azurerm_storage_account.main.name
  container_access_type = "private" # Private access - uses connection string authentication
}

# Azure SQL Server
resource "azurerm_mssql_server" "main" {
  name                         = var.sql_server_name
  resource_group_name          = azurerm_resource_group.main.name
  location                     = var.sql_location != "" ? var.sql_location : azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"

  identity {
    type = "SystemAssigned"
  }

  tags = {
    Environment = var.environment
  }
}

# SQL Server Firewall Rule - Allow Azure Services
resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# SQL Server Firewall Rule - Allow All IPs (for Vercel Serverless Functions)
# Note: This allows connections from any IP address
# For production, consider restricting to specific IP ranges
resource "azurerm_mssql_firewall_rule" "allow_all_ips" {
  name             = "AllowAllIPs"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "255.255.255.255"
}

# SQL Server Firewall Rule - Allow current IP (if provided)
resource "azurerm_mssql_firewall_rule" "current_ip" {
  count            = var.allow_current_ip && var.current_ip_address != "" ? 1 : 0
  name             = "AllowCurrentIP"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = var.current_ip_address
  end_ip_address   = var.current_ip_address
}

# Azure SQL Database
resource "azurerm_mssql_database" "main" {
  name           = var.sql_database_name
  server_id      = azurerm_mssql_server.main.id
  collation      = "SQL_Latin1_General_CP1_CI_AS"
  license_type   = var.sql_license_type
  max_size_gb    = var.sql_max_size_gb
  sku_name       = var.sql_sku_name
  zone_redundant = var.sql_zone_redundant

  tags = {
    Environment = var.environment
  }
}

# MessageBox Database - Staging area for all data
resource "azurerm_mssql_database" "messagebox" {
  name           = "MessageBox"
  server_id      = azurerm_mssql_server.main.id
  collation      = "SQL_Latin1_General_CP1_CI_AS"
  license_type   = var.sql_license_type
  max_size_gb    = var.sql_max_size_gb
  sku_name       = var.sql_sku_name
  zone_redundant = var.sql_zone_redundant

  tags = {
    Environment = var.environment
    Purpose     = "MessageBox"
  }
}

# Note: Backend API runs on Vercel Serverless Functions, not Azure App Service
# The backend is deployed alongside the frontend on Vercel

# Application Insights for Function App monitoring
resource "azurerm_application_insights" "functions" {
  count               = var.enable_function_app ? 1 : 0
  name                = "${var.functions_app_name}-insights"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  application_type    = "web"

  tags = {
    Environment = var.environment
  }

  # Ignore workspace_id changes - Azure automatically creates/manages this
  lifecycle {
    ignore_changes = [workspace_id]
  }
}

# Storage Account for Functions (if needed)
resource "azurerm_storage_account" "functions" {
  name                     = var.functions_storage_name
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  tags = {
    Environment = var.environment
  }
}

# App Service Plan for Azure Functions
# Currently using Consumption Plan (Y1) - will migrate to Flex Consumption (EP1) before Sep 2028
resource "azurerm_service_plan" "functions" {
  name                = var.functions_app_plan_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.functions_sku_name  # Y1 for Consumption (current), EP1 for Flex Consumption (future)

  tags = {
    Environment = var.environment
  }
}

# Linux Function App (optional - for serverless functions)
# CSV to SQL Server processor - processes CSV blobs and stores data in SQL Database
resource "azurerm_linux_function_app" "main" {
  count               = var.enable_function_app ? 1 : 0
  name                = var.functions_app_name
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_service_plan.functions.location
  service_plan_id     = azurerm_service_plan.functions.id
  storage_account_name       = azurerm_storage_account.functions.name
  storage_account_access_key = azurerm_storage_account.functions.primary_access_key

  site_config {
    application_stack {
      dotnet_version              = "8.0"
      use_dotnet_isolated_runtime = true
    }
    
    dynamic "cors" {
      for_each = length(var.cors_allowed_origins) > 0 ? [1] : []
      content {
        allowed_origins = var.cors_allowed_origins
      }
    }
  }

  app_settings = {
    "FUNCTIONS_WORKER_RUNTIME" = "dotnet-isolated"
    "AZURE_FUNCTIONS_ENVIRONMENT" = var.environment
    "AZURE_SQL_SERVER" = azurerm_mssql_server.main.fully_qualified_domain_name
    "AZURE_SQL_DATABASE" = var.sql_database_name
    "AZURE_SQL_USER" = var.sql_admin_login
    "AZURE_SQL_PASSWORD" = var.sql_admin_password
    "AzureWebJobsStorage" = azurerm_storage_account.functions.primary_connection_string
    # MainStorageConnection for blob triggers (use same storage account as AzureWebJobsStorage)
    "MainStorageConnection" = azurerm_storage_account.functions.primary_connection_string
    # Disable placeholder mode to ensure functions are loaded
    "WEBSITE_USE_PLACEHOLDER" = "0"
    # Application Insights integration
    "APPINSIGHTS_INSTRUMENTATIONKEY" = azurerm_application_insights.functions[0].instrumentation_key
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.functions[0].connection_string
    # Note: WEBSITE_RUN_FROM_PACKAGE is set by deployment workflow and should NOT be removed
    # Terraform will ignore this setting if it's managed by the deployment workflow
  }
  
  # Prevent Terraform from removing WEBSITE_RUN_FROM_PACKAGE
  lifecycle {
    ignore_changes = [app_settings["WEBSITE_RUN_FROM_PACKAGE"]]
  }

  tags = {
    Environment = var.environment
  }
}


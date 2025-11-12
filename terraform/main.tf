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

# Random suffix for unique resource names
resource "random_string" "suffix" {
  length  = 6
  special = false
  upper   = false
}

# Resource Group
resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location

  tags = {
    Environment = var.environment
    Project     = "Infrastructure"
  }
}

# Storage Account for general use
resource "azurerm_storage_account" "main" {
  name                     = "${var.storage_account_name}${random_string.suffix.result}"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  tags = {
    Environment = var.environment
  }
}

# Azure SQL Server
resource "azurerm_mssql_server" "main" {
  name                         = "${var.sql_server_name}${random_string.suffix.result}"
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

# Note: Backend API runs on Vercel Serverless Functions, not Azure App Service
# The backend is deployed alongside the frontend on Vercel

# Storage Account for Functions (if needed)
resource "azurerm_storage_account" "functions" {
  name                     = "${var.functions_storage_name}${random_string.suffix.result}"
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
resource "azurerm_service_plan" "functions" {
  name                = "${var.functions_app_plan_name}${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = var.functions_sku_name

  tags = {
    Environment = var.environment
  }
}

# Linux Function App (optional - for serverless functions)
resource "azurerm_linux_function_app" "main" {
  count               = var.enable_function_app ? 1 : 0
  name                = "${var.functions_app_name}${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_service_plan.functions.location
  service_plan_id     = azurerm_service_plan.functions.id
  storage_account_name       = azurerm_storage_account.functions.name
  storage_account_access_key = azurerm_storage_account.functions.primary_access_key

  site_config {
    application_stack {
      node_version = "20"
    }
    
    dynamic "cors" {
      for_each = length(var.cors_allowed_origins) > 0 ? [1] : []
      content {
        allowed_origins = var.cors_allowed_origins
      }
    }
  }

  app_settings = {
    "FUNCTIONS_WORKER_RUNTIME" = "node"
    "NODE_ENV"                 = var.environment
    "DATABASE_URL"             = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${var.sql_database_name};Persist Security Info=False;User ID=${var.sql_admin_login};Password=${var.sql_admin_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }

  tags = {
    Environment = var.environment
  }
}

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
  
  # Backend configuration - using local state for simplicity
  # For production, configure Azure Storage backend
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
  }
  
  # Use Service Principal authentication via environment variables:
  # ARM_CLIENT_ID, ARM_CLIENT_SECRET, ARM_TENANT_ID, ARM_SUBSCRIPTION_ID
  # These are set in the environment, no need to specify here
  skip_provider_registration = false
}

# Resource Group
resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location
}

# Note: Azure SQL Database requires a logical SQL Server as a container.
# The SQL Server (sql-csvtransportud3e1cem) is just a management object, not a physical server.
# The actual database is csvtransportdb, which is what we use.

# Random suffix for unique naming
resource "random_string" "suffix" {
  length  = 8
  special = false
  upper   = false
}

# Storage Account for Blob Storage
resource "azurerm_storage_account" "main" {
  name                     = "${var.storage_account_name}${random_string.suffix.result}"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  
  # Allow public access to blobs for anonymous access
  allow_nested_items_to_be_public = true
  
  # Enable anonymous access
  shared_access_key_enabled = true
}

# Blob Container - Allow anonymous read access for public access
resource "azurerm_storage_container" "csv_uploads" {
  name                  = "csv-uploads"
  storage_account_name  = azurerm_storage_account.main.name
  container_access_type = "blob"  # Allows anonymous read access to blobs
}

# SQL Server
resource "azurerm_mssql_server" "main" {
  name                         = "${var.sql_server_name}${random_string.suffix.result}"
  resource_group_name          = azurerm_resource_group.main.name
  location                     = azurerm_resource_group.main.location
  version                      = "12.0"
  administrator_login          = var.sql_admin_login
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"
  
  # Allow public network access for Vercel and other cloud services
  public_network_access_enabled = true
}

# SQL Database
resource "azurerm_mssql_database" "main" {
  name           = var.sql_database_name
  server_id      = azurerm_mssql_server.main.id
  collation      = "SQL_Latin1_General_CP1_CI_AS"
  license_type   = "LicenseIncluded"
  max_size_gb    = 2
  sku_name       = "Basic"
  zone_redundant = false
}

# SQL Firewall Rule - Allow Azure Services (required for Azure Functions and other Azure services)
resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# SQL Firewall Rule - Allow all Azure IPs (for Vercel and other cloud services)
# Note: This allows access from any Azure datacenter IP
resource "azurerm_mssql_firewall_rule" "allow_all_azure" {
  name             = "AllowAllAzureIPs"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "255.255.255.255"
}

# Storage Account for Azure Functions
resource "azurerm_storage_account" "functions" {
  name                     = "${var.functions_storage_name}${random_string.suffix.result}"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  
  # Functions storage needs shared access keys
  shared_access_key_enabled = true
}

# App Service Plan for Azure Functions
# Using Consumption Plan (Y1) - no quota limits, pay-per-use
resource "azurerm_service_plan" "functions" {
  name                = "${var.functions_app_plan_name}${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  os_type             = "Linux"
  sku_name            = "Y1" # Consumption plan - no quota limits, pay-per-use
}

# Azure Function App
resource "azurerm_linux_function_app" "main" {
  name                = "${var.functions_app_name}${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  service_plan_id     = azurerm_service_plan.functions.id
  storage_account_name = azurerm_storage_account.functions.name

  site_config {
    application_stack {
      dotnet_version = "8.0"
    }
  }

  app_settings = {
    "FUNCTIONS_WORKER_RUNTIME"              = "dotnet-isolated"
    "AzureWebJobsStorage"                   = azurerm_storage_account.functions.primary_connection_string
    "WEBSITE_CONTENTAZUREFILECONNECTIONSTRING" = azurerm_storage_account.functions.primary_connection_string
    "AZURE_STORAGE_CONNECTION_STRING"       = azurerm_storage_account.main.primary_connection_string
    "AZURE_STORAGE_ACCOUNT_NAME"            = azurerm_storage_account.main.name
    "AZURE_STORAGE_ACCOUNT_KEY"             = azurerm_storage_account.main.primary_access_key
    "AZURE_SQL_SERVER"                      = "${azurerm_mssql_server.main.name}.database.windows.net"
    "AZURE_SQL_DATABASE"                    = azurerm_mssql_database.main.name
    "AZURE_SQL_USER"                        = var.sql_admin_login
    "AZURE_SQL_PASSWORD"                    = var.sql_admin_password
  }

  identity {
    type = "SystemAssigned"
  }
}

# Note: Event Grid is not needed - Azure Functions Blob Trigger works directly.
# The blob trigger in function.json automatically monitors the csv-uploads container
# and triggers the function when new CSV files are uploaded.

# Deploy Azure Functions code using Terraform (Infrastructure as Code)
# This null_resource ensures the Functions code is deployed as part of Terraform
resource "null_resource" "deploy_function_app" {
  # Trigger redeployment when Function App or code changes
        triggers = {
          function_app_id = azurerm_linux_function_app.main.id
          function_code_hash = filebase64sha256("${path.module}/../azure-functions/ProcessCsvBlobTrigger/ProcessCsvBlobTrigger.cs")
          host_json_hash = filebase64sha256("${path.module}/../azure-functions/host.json")
          csproj_hash = filebase64sha256("${path.module}/../azure-functions/ProcessCsvBlobTrigger/ProcessCsvBlobTrigger.csproj")
        }

        # Build and create deployment package
        provisioner "local-exec" {
          command = <<-EOT
            cd ${path.module}/../azure-functions/ProcessCsvBlobTrigger
            dotnet publish -c Release -o ./bin/publish
            cd ${path.module}/../azure-functions
            if (Test-Path function-app.zip) { Remove-Item function-app.zip -Force }
            Compress-Archive -Path host.json,ProcessCsvBlobTrigger/bin/publish/* -DestinationPath function-app.zip -Force
          EOT
          interpreter = ["PowerShell", "-Command"]
        }

  # Deploy to Azure Functions
  provisioner "local-exec" {
    command = <<-EOT
      az functionapp deployment source config-zip `
        --resource-group ${azurerm_resource_group.main.name} `
        --name ${azurerm_linux_function_app.main.name} `
        --src ${path.module}/../azure-functions/function-app.zip `
        --timeout 600
    EOT
    interpreter = ["PowerShell", "-Command"]
  }

  depends_on = [
    azurerm_linux_function_app.main,
    azurerm_mssql_database.main
  ]
}


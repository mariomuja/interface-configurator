# Terraform module for creating a container app for an adapter instance
# This module is called dynamically when an adapter instance is created

variable "adapter_instance_guid" {
  description = "Adapter instance GUID"
  type        = string
}

variable "adapter_name" {
  description = "Adapter name (CSV, SqlServer, etc.)"
  type        = string
}

variable "adapter_type" {
  description = "Adapter type (Source or Destination)"
  type        = string
  default     = "Source"
}

variable "interface_name" {
  description = "Interface name"
  type        = string
}

variable "instance_name" {
  description = "Instance name"
  type        = string
}

variable "container_registry_server" {
  description = "Container registry server"
  type        = string
}

variable "container_registry_username" {
  description = "Container registry username"
  type        = string
  sensitive   = true
}

variable "container_registry_password" {
  description = "Container registry password"
  type        = string
  sensitive   = true
}

variable "blob_storage_connection_string" {
  description = "Blob storage connection string"
  type        = string
  sensitive   = true
}

variable "blob_container_name" {
  description = "Blob container name"
  type        = string
}

locals {
  container_app_name = "ca-${substr(var.adapter_instance_guid, 0, 24)}"
  adapter_image      = "${var.container_registry_server}/${lower(var.adapter_name)}-adapter:latest"
  storage_account_name = "st${substr(var.adapter_instance_guid, 0, 20)}"
}

# Create blob storage account for this instance
resource "azurerm_storage_account" "adapter_instance" {
  name                     = local.storage_account_name
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  tags = {
    AdapterInstanceGuid = var.adapter_instance_guid
    AdapterName         = var.adapter_name
    AdapterType         = var.adapter_type
    InterfaceName      = var.interface_name
  }
}

# Create blob container
resource "azurerm_storage_container" "adapter_instance" {
  name                  = var.blob_container_name
  storage_account_name  = azurerm_storage_account.adapter_instance.name
  container_access_type = "private"
}

# Create container app
resource "azurerm_container_app" "adapter_instance" {
  name                         = local.container_app_name
  container_app_environment_id = var.container_app_environment_id
  resource_group_name           = var.resource_group_name
  revision_mode                 = "Single"

  template {
    min_replicas = 1
    max_replicas = 1

    container {
      name   = local.container_app_name
      image  = local.adapter_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "ADAPTER_INSTANCE_GUID"
        value = var.adapter_instance_guid
      }

      env {
        name  = "ADAPTER_NAME"
        value = var.adapter_name
      }

      env {
        name  = "ADAPTER_TYPE"
        value = var.adapter_type
      }

      env {
        name  = "INTERFACE_NAME"
        value = var.interface_name
      }

      env {
        name  = "INSTANCE_NAME"
        value = var.instance_name
      }

      env {
        name        = "BLOB_CONNECTION_STRING"
        secret_name = "blob-connection-string"
      }

      env {
        name  = "BLOB_CONTAINER_NAME"
        value = var.blob_container_name
      }
    }
  }

  ingress {
    external_enabled = false
    target_port     = 8080
    transport       = "http"
  }

  registry {
    server   = var.container_registry_server
    username = var.container_registry_username
    password_secret_name = "registry-password"
  }

  secret {
    name  = "registry-password"
    value = var.container_registry_password
  }

  secret {
    name  = "blob-connection-string"
    value = var.blob_storage_connection_string
  }

  tags = {
    AdapterInstanceGuid = var.adapter_instance_guid
    AdapterName         = var.adapter_name
    AdapterType         = var.adapter_type
    InterfaceName       = var.interface_name
  }
}

output "container_app_name" {
  value = azurerm_container_app.adapter_instance.name
}

output "container_app_url" {
  value = azurerm_container_app.adapter_instance.latest_revision_fqdn
}

output "blob_storage_account_name" {
  value = azurerm_storage_account.adapter_instance.name
}

output "blob_container_name" {
  value = azurerm_storage_container.adapter_instance.name
}



# Azure Authentication
# These can be provided via terraform.tfvars or environment variables (ARM_SUBSCRIPTION_ID, ARM_CLIENT_ID, ARM_CLIENT_SECRET, ARM_TENANT_ID)
variable "subscription_id" {
  description = "Azure subscription ID (or set ARM_SUBSCRIPTION_ID environment variable)"
  type        = string
  default     = null
}

variable "client_id" {
  description = "Service Principal Client ID (or set ARM_CLIENT_ID environment variable)"
  type        = string
  default     = null
}

variable "client_secret" {
  description = "Service Principal Client Secret (or set ARM_CLIENT_SECRET environment variable)"
  type        = string
  sensitive   = true
  default     = null
}

variable "tenant_id" {
  description = "Azure Tenant ID (or set ARM_TENANT_ID environment variable)"
  type        = string
  default     = null
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
  default     = "rg-interface-configuration"
}

variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "West Europe"
}

variable "sql_location" {
  description = "Azure region for SQL Server (if different from main location, leave empty to use main location)"
  type        = string
  default     = ""
}

variable "environment" {
  description = "Environment name (e.g., dev, staging, prod)"
  type        = string
  default     = "prod"
}

variable "storage_account_name" {
  description = "Name for storage account (descriptive, no suffix, no hyphens - Azure requirement)"
  type        = string
  default     = "stappgeneral"
}

variable "sql_server_name" {
  description = "Name for SQL Server (descriptive, no suffix)"
  type        = string
  default     = "sql-main-database"
}

variable "sql_database_name" {
  description = "Name of the SQL database"
  type        = string
  default     = "app-database"
}

variable "sql_admin_login" {
  description = "SQL Server administrator login"
  type        = string
  sensitive   = true
}

variable "sql_admin_password" {
  description = "SQL Server administrator password"
  type        = string
  sensitive   = true
}

variable "sql_license_type" {
  description = "SQL Database license type (LicenseIncluded or BasePrice)"
  type        = string
  default     = "LicenseIncluded"
}

variable "sql_sku_name" {
  description = "SQL Database SKU name (e.g., S0, S1, P1, GP_S_Gen5_2, BC_Gen5_2)"
  type        = string
  default     = "S0"
}

variable "sql_max_size_gb" {
  description = "Maximum size in GB for SQL Database"
  type        = number
  default     = 2
}

variable "sql_zone_redundant" {
  description = "Enable zone redundancy for SQL Database"
  type        = bool
  default     = false
}

variable "allow_current_ip" {
  description = "Allow current IP address to access SQL Server"
  type        = bool
  default     = false
}

variable "current_ip_address" {
  description = "Current IP address for SQL firewall rule"
  type        = string
  default     = ""
}

# Backend API runs on Vercel Serverless Functions, not Azure App Service
# These variables are kept for potential future use but not currently used
# variable "backend_app_plan_name" {
#   description = "Base name for backend app service plan (suffix will be added)"
#   type        = string
#   default     = "plan-backend-app"
# }
#
# variable "backend_app_name" {
#   description = "Base name for backend app service (suffix will be added)"
#   type        = string
#   default     = "app-backend"
# }
#
# variable "backend_sku_name" {
#   description = "SKU name for backend app service plan"
#   type        = string
#   default     = "B1"
# }

variable "functions_storage_name" {
  description = "Name for Functions storage account (descriptive, no suffix, no hyphens - Azure requirement)"
  type        = string
  default     = "stfunccsvprocessor"
}

variable "functions_app_plan_name" {
  description = "Name for Functions app service plan (descriptive, no suffix)"
  type        = string
  default     = "plan-func-csv-processor"
}

variable "functions_app_name" {
  description = "Name for Functions app (descriptive, no suffix) - Integration platform"
  type        = string
  default     = "func-integration-main"
}

variable "functions_sku_name" {
  description = "SKU name for Functions app service plan. Use 'Y1' for Consumption (current), 'EP1' for Flex Consumption (migration required before Sep 2028)"
  type        = string
  default     = "Y1"  # Consumption Plan (will be migrated to EP1 Flex Consumption before Sep 2028)
}

variable "enable_function_app" {
  description = "Enable Azure Function App deployment"
  type        = bool
  default     = false
}

variable "jwt_secret" {
  description = "JWT secret for authentication"
  type        = string
  sensitive   = true
  default     = ""
}

variable "cors_allowed_origins" {
  description = "List of allowed CORS origins"
  type        = list(string)
  default     = []
}

variable "github_repo_url" {
  description = "GitHub repository URL for source control deployment (e.g., https://github.com/owner/repo)"
  type        = string
  default     = ""
}

variable "github_branch" {
  description = "GitHub branch for source control deployment"
  type        = string
  default     = "main"
}

variable "github_repo_path" {
  description = "Path to the Function App code in the repository (e.g., azure-functions/main)"
  type        = string
  default     = "azure-functions/main"
}

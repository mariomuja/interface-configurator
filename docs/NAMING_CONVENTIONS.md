# Azure Resource Naming Conventions

## Overview

This document outlines naming conventions for Azure resources to ensure clarity, maintainability, and easy identification of resource purpose.

## Problem Statement

Azure resources with generic or cryptic names (e.g., `func-apprigklebtsay2o`) make it difficult to:
- Understand the resource's purpose
- Identify resources in large deployments
- Troubleshoot issues
- Manage resources across environments
- Onboard new team members

## Naming Convention Principles

### 1. Descriptive and Self-Documenting

Resource names should clearly indicate:
- **What** the resource is (type)
- **What** it does (purpose/function)
- **Where** it belongs (environment/project)

### 2. Consistent Pattern

Use a consistent naming pattern across all resources:
```
{project}-{environment}-{resource-type}-{purpose}-{instance}
```

### 3. Abbreviations

Use standard abbreviations for resource types:
- `func` or `fa` for Function App
- `st` for Storage Account
- `sql` for SQL Server
- `rg` for Resource Group
- `plan` for App Service Plan
- `kv` for Key Vault
- `vnet` for Virtual Network

## Current Naming Examples

### ❌ Bad Examples (Current)

```
func-apprigklebtsay2o          # Generic, random suffix
stapprigklebtsay2o             # Unclear purpose
sql-infrastructurerigklebtsay2o # Too generic
```

### ✅ Good Examples (Recommended)

```
infra-prod-func-csv-processor     # CSV processing function
infra-prod-st-csv-blobs           # Storage for CSV blobs
infra-prod-sql-main-db            # Main database server
infra-prod-plan-func-consumption  # Function app plan
infra-dev-func-csv-processor       # Development environment
```

## Recommended Naming Patterns

### Function Apps

**Pattern:** `{project}-{env}-func-{purpose}`

Examples:
- `infra-prod-func-csv-processor` - Processes CSV files
- `infra-prod-func-blob-trigger` - Triggered by blob uploads
- `infra-prod-func-api-gateway` - API gateway function
- `infra-dev-func-csv-processor` - Development version

### Storage Accounts

**Pattern:** `{project}{env}st{purpose}` (max 24 chars, lowercase, no hyphens)

Examples:
- `infraprodstcsvblobs` - CSV blob storage
- `infraprodstfuncs` - Function app storage
- `infradevstcsvblobs` - Development CSV storage

### SQL Servers

**Pattern:** `{project}-{env}-sql-{purpose}`

Examples:
- `infra-prod-sql-main` - Main production database
- `infra-prod-sql-reporting` - Reporting database
- `infra-dev-sql-main` - Development database

### App Service Plans

**Pattern:** `{project}-{env}-plan-{purpose}-{sku}`

Examples:
- `infra-prod-plan-func-consumption` - Consumption plan for functions
- `infra-prod-plan-func-flex` - Flex consumption plan
- `infra-dev-plan-func-consumption` - Development plan

### Resource Groups

**Pattern:** `rg-{project}-{environment}`

Examples:
- `rg-infrastructure-prod` - Production resource group
- `rg-infrastructure-dev` - Development resource group
- `rg-infrastructure-staging` - Staging resource group

## Implementation Guidelines

### 1. Update Bicep Parameters

When creating new resources, use descriptive parameter names:

```bicep
@description('Base name for CSV processing Function App')
param csvProcessorFunctionAppName string = 'infra-prod-func-csv-processor'

@description('Base name for blob storage (CSV files)')
param csvBlobStorageName string = 'infraprodstcsvblobs'
```

### 2. Update Terraform Variables

Similarly in Terraform:

```hcl
variable "csv_processor_function_app_name" {
  description = "Base name for CSV processing Function App"
  type        = string
  default     = "infra-prod-func-csv-processor"
}
```

### 3. Environment-Specific Naming

Always include environment in the name:
- `-prod-` for production
- `-dev-` for development
- `-staging-` for staging
- `-test-` for testing

### 4. Purpose-Specific Naming

Include the specific purpose:
- `csv-processor` - Processes CSV files
- `blob-trigger` - Triggered by blob uploads
- `api-gateway` - API gateway functionality
- `data-transformer` - Transforms data

## Azure Naming Constraints

### Storage Accounts
- 3-24 characters
- Lowercase letters and numbers only
- Must be globally unique
- No hyphens allowed

### Function Apps
- 1-60 characters
- Alphanumeric and hyphens
- Must be globally unique
- Cannot start or end with hyphen

### SQL Servers
- 1-63 characters
- Lowercase letters, numbers, and hyphens
- Must be globally unique
- Cannot start or end with hyphen

### Resource Groups
- 1-90 characters
- Alphanumeric, hyphens, underscores, periods, and parentheses
- Case-insensitive

## Migration Strategy

When renaming existing resources:

1. **Create new resources** with descriptive names
2. **Migrate data** from old to new resources
3. **Update references** in code and configuration
4. **Verify functionality** with new names
5. **Delete old resources** after successful migration

## Best Practices

1. **Document naming decisions** in this file
2. **Review names** before deployment
3. **Use consistent abbreviations** across the project
4. **Include environment** in every resource name
5. **Avoid random suffixes** unless absolutely necessary
6. **Keep names concise** but descriptive
7. **Use hyphens** for readability (where allowed)
8. **Avoid special characters** that might cause issues

## Examples for This Project

### Current Resources (to be renamed)

| Current Name | Recommended Name | Purpose |
|-------------|------------------|---------|
| `func-apprigklebtsay2o` | `infra-prod-func-csv-processor` | Processes CSV blob uploads |
| `stapprigklebtsay2o` | `infra-prod-st-general` | General purpose storage |
| `stfuncsapprigklebtsay2o` | `infra-prod-st-funcs` | Function app storage |
| `sql-infrastructurerigklebtsay2o` | `infra-prod-sql-main` | Main database server |
| `plan-funcs-apprigklebtsay2o` | `infra-prod-plan-func-consumption` | Function app hosting plan |

### Updated Parameter Files

**Bicep (`bicep/parameters.json`):**
```json
{
  "functionsAppName": {
    "value": "infra-prod-func-csv-processor"
  },
  "functionsStorageName": {
    "value": "infraprodstfuncs"
  },
  "storageAccountName": {
    "value": "infraprodstgeneral"
  },
  "sqlServerName": {
    "value": "infra-prod-sql-main"
  }
}
```

**Terraform (`terraform/terraform.tfvars`):**
```hcl
functions_app_name      = "infra-prod-func-csv-processor"
functions_storage_name   = "infraprodstfuncs"
storage_account_name     = "infraprodstgeneral"
sql_server_name          = "infra-prod-sql-main"
```

## References

- [Azure naming rules and restrictions](https://learn.microsoft.com/azure/azure-resource-manager/management/resource-name-rules)
- [Naming conventions best practices](https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/naming-and-tagging)










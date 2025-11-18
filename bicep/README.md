# Azure Bicep Infrastructure Deployment

This directory contains Azure Bicep templates for deploying infrastructure as code, parallel to the Terraform implementation.

## Prerequisites

1. **Azure CLI** - Install from [https://aka.ms/installazurecliwindows](https://aka.ms/installazurecliwindows)
2. **Azure Bicep CLI** - Install using:
   ```powershell
   winget install --id Microsoft.Bicep --accept-source-agreements --accept-package-agreements
   ```
   Or use Azure CLI's built-in Bicep support (no separate installation needed).

3. **Azure Authentication** - Login to Azure:
   ```powershell
   az login
   ```

## Project Structure

```
bicep/
├── main.bicep              # Main Bicep template with all resources
├── bicepconfig.json        # Bicep configuration file
├── parameters.json         # Default parameters file
├── parameters.dev.json      # Development environment parameters
├── parameters.prod.json    # Production environment parameters
├── deploy.ps1              # Deployment script
├── validate.ps1           # Validation script
└── README.md              # This file
```

## Naming Conventions

**IMPORTANT:** Always use descriptive, self-documenting names for Azure resources that explain what the resource does.

Use the naming pattern: `{project}-{env}-{resource-type}-{purpose}`

**Examples:**
- Function Apps: `infra-prod-func-csv-processor` (not `func-apprigklebtsay2o`)
- Storage Accounts: `infraprodstcsvblobs` (not `stapprigklebtsay2o`)
- SQL Servers: `infra-prod-sql-main` (not `sql-infrastructurerigklebtsay2o`)

See `NAMING_CONVENTIONS.md` in the project root for detailed naming guidelines.

## Resources Deployed

The Bicep template deploys the following Azure resources:

- **Resource Group** - Container for all resources
- **Storage Account** - General purpose storage
- **SQL Server** - Azure SQL Database server with:
  - System-assigned managed identity
  - Firewall rules (Azure Services, All IPs, Current IP if specified)
- **SQL Database** - Azure SQL Database
- **Functions Storage Account** - Storage account for Azure Functions
- **App Service Plan** - Linux-based plan for Azure Functions (Consumption or Flex Consumption)
- **Linux Function App** - Azure Functions app (optional, controlled by `enableFunctionApp` parameter)

## Quick Start

### 1. Configure Parameters

Edit `parameters.json` or create environment-specific files (`parameters.dev.json`, `parameters.prod.json`):

```json
{
  "parameters": {
    "resourceGroupName": {
      "value": "rg-interface-configuration"
    },
    "location": {
      "value": "Central US"
    },
    "sqlAdminLogin": {
      "value": "your-admin-login"
    },
    "sqlAdminPassword": {
      "value": "your-secure-password"
    },
    "enableFunctionApp": {
      "value": true
    }
  }
}
```

### 2. Validate Template

Validate the Bicep template before deploying:

```powershell
.\validate.ps1
```

Or validate with a specific parameters file:

```powershell
.\validate.ps1 -ParametersFile "parameters.prod.json"
```

### 3. Preview Changes (What-If)

Preview what will be deployed without making changes:

```powershell
.\deploy.ps1 -WhatIf
```

### 4. Deploy Infrastructure

Deploy using the default parameters file:

```powershell
.\deploy.ps1
```

Deploy with a specific parameters file:

```powershell
.\deploy.ps1 -ParametersFile "parameters.prod.json"
```

Deploy to a specific resource group:

```powershell
.\deploy.ps1 -ResourceGroupName "rg-my-infrastructure" -Location "West Europe"
```

## Manual Deployment

You can also deploy manually using Azure CLI:

```powershell
# Create resource group
az group create --name rg-interface-configuration --location "Central US"

# Deploy template
az deployment group create `
    --resource-group rg-interface-configuration `
    --template-file main.bicep `
    --parameters parameters.json `
    --name infrastructure-deployment-$(Get-Date -Format 'yyyyMMdd-HHmmss')
```

## Parameters Reference

### Required Parameters

- `sqlAdminLogin` - SQL Server administrator login
- `sqlAdminPassword` - SQL Server administrator password

### Optional Parameters

- `resourceGroupName` - Name of the resource group (default: `rg-interface-configuration`)
- `location` - Azure region (default: `West Europe`)
- `sqlLocation` - SQL Server location (default: uses main location)
- `environment` - Environment name: `dev`, `staging`, or `prod` (default: `prod`)
- `storageAccountName` - Base name for storage account (default: `stapp`)
- `sqlServerName` - Base name for SQL Server (default: `sql-infrastructure`)
- `sqlDatabaseName` - SQL database name (default: `app_database`)
- `sqlSkuName` - SQL Database SKU (default: `S0`)
- `sqlMaxSizeGb` - Maximum database size in GB (default: `2`)
- `enableFunctionApp` - Enable Function App deployment (default: `false`)
- `functionsSkuName` - Function App plan SKU: `Y1` (Consumption) or `EP1` (Flex Consumption) (default: `Y1`)
- `corsAllowedOrigins` - Array of allowed CORS origins (default: `[]`)

## Outputs

After deployment, the following outputs are available:

- `resourceGroupName` - Name of the resource group
- `resourceGroupLocation` - Location of the resource group
- `storageAccountName` - Name of the storage account
- `storageAccountConnectionString` - Storage account connection string
- `sqlServerName` - Name of the SQL Server
- `sqlServerFqdn` - Fully qualified domain name of the SQL Server
- `sqlDatabaseName` - Name of the SQL database
- `sqlConnectionString` - SQL Server connection string
- `functionAppName` - Name of the Function App (if enabled)
- `functionAppUrl` - URL of the Function App (if enabled)
- `functionsStorageAccountName` - Name of the Functions storage account

View outputs:

```powershell
az deployment group show `
    --resource-group rg-interface-configuration `
    --name <deployment-name> `
    --query properties.outputs
```

## Differences from Terraform

### Unique Suffix Generation

- **Terraform**: Uses `random_string` resource
- **Bicep**: Uses `uniqueString()` function with resource group ID and subscription ID

### Lifecycle Management

- **Terraform**: Uses `lifecycle` blocks to ignore changes (e.g., `WEBSITE_RUN_FROM_PACKAGE`)
- **Bicep**: No direct equivalent; app settings are managed directly. Consider using Azure Policy or separate deployments for settings managed by CI/CD.

### Conditional Resources

- **Terraform**: Uses `count` or `for_each`
- **Bicep**: Uses `condition` property on resources

### App Settings Management

- **Terraform**: Can ignore changes to specific app settings using `lifecycle { ignore_changes = [app_settings["WEBSITE_RUN_FROM_PACKAGE"]] }`
- **Bicep**: All app settings are defined in the template. Settings managed by CI/CD (like `WEBSITE_RUN_FROM_PACKAGE`) should be set separately or excluded from the template.

## Best Practices

1. **Use Parameter Files** - Store environment-specific configurations in separate parameter files
2. **Validate Before Deploy** - Always run `validate.ps1` before deploying
3. **Use What-If** - Preview changes with `-WhatIf` flag before deploying
4. **Version Control** - Commit parameter files but exclude sensitive values (use Azure Key Vault references)
5. **Separate Concerns** - Consider separating infrastructure deployment from app settings managed by CI/CD

## Troubleshooting

### Bicep CLI Not Found

If `bicep` command is not found:
1. Restart your PowerShell session after installation
2. Or use Azure CLI's built-in Bicep support (no separate installation needed)

### Deployment Fails

1. Check Azure CLI login: `az account show`
2. Verify resource group exists: `az group exists --name <resource-group-name>`
3. Validate template: `.\validate.ps1`
4. Check deployment status: `az deployment group list --resource-group <resource-group-name>`

### Parameter Validation Errors

Ensure all required parameters are provided and match the expected types:
- Strings for names and locations
- Secure strings for passwords
- Arrays for CORS origins
- Booleans for flags

## Additional Resources

- [Azure Bicep Documentation](https://learn.microsoft.com/azure/azure-resource-manager/bicep/)
- [Bicep Language Specification](https://github.com/Azure/bicep/blob/main/docs/spec/bicep.md)
- [Azure CLI Documentation](https://learn.microsoft.com/cli/azure/)
- [Azure Resource Manager Templates](https://learn.microsoft.com/azure/azure-resource-manager/templates/)

## Comparison with Terraform

Both Terraform and Bicep are available in this repository:

- **Terraform** (`terraform/`) - Uses HashiCorp Terraform with Azure provider
- **Bicep** (`bicep/`) - Uses Azure-native Bicep templates

Choose based on your preferences:
- **Terraform**: Multi-cloud support, extensive provider ecosystem
- **Bicep**: Azure-native, simpler syntax, better Azure integration


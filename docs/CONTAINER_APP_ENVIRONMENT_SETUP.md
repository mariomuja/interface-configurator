# Container App Environment Setup

## Overview

The Container App Environment (`cae-adapter-instances`) is required for running adapter container apps. This environment is now included in both Bicep and Terraform infrastructure definitions.

## Infrastructure Configuration

### Bicep (`bicep/main.bicep`)

The Container App Environment is defined at lines 307-317:

```bicep
resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: 'cae-adapter-instances'
  location: location
  properties: {
    // appLogsConfiguration omitted - using Azure's default logging
  }
  tags: commonTags
}
```

**Outputs:**
- `containerAppEnvironmentName`: Name of the environment (`cae-adapter-instances`)
- `containerAppEnvironmentId`: Resource ID of the environment

### Terraform (`terraform/main.tf`)

The Container App Environment is defined at lines 286-298:

```hcl
resource "azurerm_container_app_environment" "adapter_instances" {
  name                       = "cae-adapter-instances"
  resource_group_name        = azurerm_resource_group.main.name
  location                   = azurerm_resource_group.main.location
  # log_analytics_workspace_id omitted - using Azure's default logging

  tags = {
    Environment = var.environment
  }
}
```

**Outputs:**
- `container_app_environment_name`: Name of the environment
- `container_app_environment_id`: Resource ID of the environment

## Function App Configuration

The Function App requires the following app settings for the Container App Service:

### Required Settings:
- `ResourceGroupName`: Resource group name (automatically set)
- `Location`: Azure region (automatically set)
- `ContainerAppEnvironmentName`: Environment name (automatically set to `cae-adapter-instances`)

### Container Registry Settings (Manual Configuration Required):
These must be set manually after deployment or via deployment script:

- `ContainerRegistryServer`: Container Registry server (e.g., `acrinterfaceconfig.azurecr.io`)
- `ContainerRegistryUsername`: ACR admin username
- `ContainerRegistryPassword`: ACR admin password (sensitive)

**Note:** The Container Registry (ACR) is not managed by the infrastructure templates. It's an existing resource that must be configured separately.

## Deployment

### Deploy with Bicep:
```bash
az deployment group create \
  --resource-group rg-interface-configurator \
  --template-file bicep/main.bicep \
  --parameters @bicep/parameters.json
```

### Deploy with Terraform:
```bash
cd terraform
terraform init
terraform plan
terraform apply
```

## Post-Deployment Configuration

After deploying the infrastructure, you must configure the Container Registry credentials:

### Option 1: Azure Portal
1. Navigate to Function App → Configuration → Application Settings
2. Set the following:
   - `ContainerRegistryServer`: `acrinterfaceconfig.azurecr.io`
   - `ContainerRegistryUsername`: Get from ACR admin credentials
   - `ContainerRegistryPassword`: Get from ACR admin credentials

### Option 2: Azure CLI
```bash
# Get ACR credentials
ACR_NAME="acrinterfaceconfig"
ACR_USERNAME=$(az acr credential show --name $ACR_NAME --query "username" -o tsv)
ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query "passwords[0].value" -o tsv)

# Set Function App settings
az functionapp config appsettings set \
  --resource-group rg-interface-configurator \
  --name func-integration-main \
  --settings \
    ContainerRegistryServer="${ACR_NAME}.azurecr.io" \
    ContainerRegistryUsername="${ACR_USERNAME}" \
    ContainerRegistryPassword="${ACR_PASSWORD}"
```

## Verification

After deployment, verify the Container App Environment exists:

```bash
# Check environment
az containerapp env show \
  --name cae-adapter-instances \
  --resource-group rg-interface-configurator

# Check Function App settings
az functionapp config appsettings list \
  --name func-integration-main \
  --resource-group rg-interface-configurator \
  --query "[?contains(name, 'Container') || contains(name, 'ResourceGroup') || contains(name, 'Location')]"
```

## Usage

Once configured, the Container App Service can create container apps for adapter instances:

- Each adapter instance gets its own isolated container app
- Container apps are named: `ca-{first-24-chars-of-guid}`
- Container apps run in the `cae-adapter-instances` environment
- Images are pulled from the configured Container Registry

## Important Notes

1. **First Deployment**: The Container App Environment will be created on first deployment. This may take 2-5 minutes.

2. **Container Registry**: The ACR (`acrinterfaceconfig`) must exist and have admin credentials enabled. Container images must be pushed to ACR before container apps can be created.

3. **Logging**: The environment uses Azure's built-in logging (no Log Analytics workspace required).

4. **Cost**: Container App Environment has a base cost. Container apps scale to zero when not in use (min replicas = 0).

5. **Permissions**: The Function App needs appropriate permissions (via Managed Identity or Service Principal) to create resources in the resource group.













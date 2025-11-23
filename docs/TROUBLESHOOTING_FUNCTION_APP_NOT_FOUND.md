# Troubleshooting: Function App Not Found Error

## Error Message

```
ERROR: Not Found("error":"code":"ResourceNotFound","message":"The Resource 'Microsoft.Web/sites/***' under resource group '***' was not found. For more details please go to https://aka.ms/ARMResourceNotFoundFix")

ERROR: Function App not found!
```

## What This Means

The GitHub Actions workflow is trying to access an Azure Function App that doesn't exist in the specified resource group. This can happen for several reasons:

1. **Function App was never created** - The infrastructure deployment (Bicep/Terraform) didn't create the Function App
2. **Wrong Function App name** - The name in GitHub secrets doesn't match the actual Function App name
3. **Wrong resource group** - The resource group in GitHub secrets is incorrect
4. **Function App was deleted** - The Function App was manually deleted from Azure

## Solution Steps

### Step 1: Verify GitHub Secrets

Check that your GitHub repository secrets are correctly configured:

1. Go to your GitHub repository → Settings → Secrets and variables → Actions
2. Verify these secrets exist and have correct values:
   - `AZURE_RESOURCE_GROUP` - Should match your Azure resource group name
   - `AZURE_FUNCTIONAPP_NAME` - Should match your Function App name exactly
   - `AZURE_CREDENTIALS` - Should contain valid Azure service principal credentials

### Step 2: Check if Function App Exists

Run this command locally (or in Azure Cloud Shell):

```bash
# List all Function Apps in the resource group
az functionapp list \
  --resource-group <your-resource-group-name> \
  --query "[].{Name: name, State: state, Runtime: siteConfig.linuxFxVersion}" \
  --output table
```

If no Function Apps are listed, the Function App doesn't exist and needs to be created.

### Step 3: Verify Resource Group Exists

```bash
# List all resource groups
az group list --query "[].name" -o table
```

### Step 4: Check Infrastructure Deployment

If using Bicep:

1. Check `bicep/main.bicep` - ensure `enableFunctionApp` parameter is set to `true`
2. Check `bicep/parameters.json` or `bicep/parameters.dev.json` - verify Function App configuration
3. Redeploy infrastructure if needed:

```bash
az deployment group create \
  --resource-group <your-resource-group> \
  --template-file bicep/main.bicep \
  --parameters @bicep/parameters.dev.json
```

If using Terraform:

1. Check Terraform configuration files
2. Run `terraform plan` to see what will be created
3. Run `terraform apply` to create missing resources

### Step 5: Get Function App Name from Infrastructure

If the Function App exists but you don't know the exact name:

**Bicep:**
```bash
az deployment group show \
  --resource-group <your-resource-group> \
  --name <deployment-name> \
  --query "properties.outputs.functionsAppName.value" \
  -o tsv
```

**Terraform:**
```bash
cd terraform
terraform output function_app_name
```

### Step 6: Update GitHub Secrets

Once you have the correct Function App name:

1. Go to GitHub repository → Settings → Secrets and variables → Actions
2. Update `AZURE_FUNCTIONAPP_NAME` with the correct name
3. Update `AZURE_RESOURCE_GROUP` if needed

## Improved Error Handling

The workflow has been updated to provide better diagnostics:

- ✅ Checks if resource group exists before checking Function App
- ✅ Lists available Function Apps if the specified one doesn't exist
- ✅ Shows the actual Azure error message
- ✅ Provides troubleshooting steps in the workflow output

## Next Steps After Fixing

Once the Function App exists and secrets are configured:

1. Re-run the GitHub Actions workflow
2. The workflow will now provide clearer error messages if something is still wrong
3. Check the workflow logs for detailed diagnostics

## Common Issues

### Issue: Function App name has random suffix

Azure Function App names must be globally unique, so Bicep/Terraform may add a random suffix. Always get the actual name from infrastructure outputs, not from parameter files.

### Issue: Function App in different resource group

If your Function App is in a different resource group than expected, either:
- Update `AZURE_RESOURCE_GROUP` secret to match the correct resource group
- Or move the Function App to the expected resource group

### Issue: Function App was deleted

If the Function App was accidentally deleted, you need to recreate it by running your infrastructure deployment again (Bicep/Terraform).




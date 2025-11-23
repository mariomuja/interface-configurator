# Region Migration Guide: Central US → West Europe

## Overview

This guide documents the migration of Azure resources from **Central US** to **West Europe**.

## Important Note

**Azure does not support moving resources between regions directly.** Resources can only be moved between resource groups in the same region. To migrate to a different region, you must:

1. Export/backup all data
2. Create new resources in the target region (West Europe)
3. Restore/migrate the data
4. Update all configurations and connection strings
5. Delete old resources in Central US

## Configuration Files Updated

The following configuration files have been updated to use **West Europe** and **rg-interface-configurator**:

### Bicep Configuration
- ✅ `bicep/main.bicep` - Default location already set to "West Europe"
- ✅ `bicep/parameters.json` - Updated location to "West Europe"
- ✅ `bicep/parameters.prod.json` - Updated location to "West Europe"
- ✅ `bicep/parameters.dev.json` - Already set to "West Europe"
- ✅ `bicep/deploy.ps1` - Updated default location and resource group name

### Terraform Configuration
- ✅ `terraform/variables.tf` - Default location already set to "West Europe"
- ✅ `terraform/terraform.tfvars` - Updated location to "West Europe"
- ✅ `terraform/migrate-resource-group.ps1` - Updated default location
- ✅ `terraform/migrate-with-terraform.ps1` - Updated default location

## Migration Steps

### Step 1: Backup Current Resources

1. **Export SQL Database**
   ```powershell
   # Export app-database
   az sql db export \
     --resource-group rg-infrastructure-as-code \
     --server sql-main-database \
     --name app-database \
     --storage-key-type StorageAccessKey \
     --storage-key <storage-key> \
     --storage-uri "https://stappgeneral.blob.core.windows.net/backups/app-database.bacpac" \
     --administrator-login sqladmin \
     --administrator-login-password <password>

   # Export MessageBox database
   az sql db export \
     --resource-group rg-infrastructure-as-code \
     --server sql-main-database \
     --name MessageBox \
     --storage-key-type StorageAccessKey \
     --storage-key <storage-key> \
     --storage-uri "https://stappgeneral.blob.core.windows.net/backups/MessageBox.bacpac" \
     --administrator-login sqladmin \
     --administrator-login-password <password>
   ```

2. **Export Blob Storage Data**
   ```powershell
   # Download all blobs from function-config container
   az storage blob download-batch \
     --destination ./backups/blob-storage \
     --source function-config \
     --account-name stappgeneral \
     --account-key <storage-key>
   ```

3. **Export Function App Configuration**
   ```powershell
   # Get function app settings
   az functionapp config appsettings list \
     --name func-integration-main \
     --resource-group rg-infrastructure-as-code \
     --output json > function-app-settings.json
   ```

### Step 2: Create New Resources in West Europe

1. **Deploy Infrastructure**
   ```powershell
   # Using Bicep
   cd bicep
   .\deploy.ps1 -ResourceGroupName rg-interface-configurator -Location "West Europe" -ParametersFile parameters.json

   # OR using Terraform
   cd terraform
   terraform init
   terraform plan
   terraform apply
   ```

2. **Verify New Resources**
   ```powershell
   az resource list --resource-group rg-interface-configurator --output table
   ```

### Step 3: Restore Data

1. **Import SQL Databases**
   ```powershell
   # Import app-database
   az sql db import \
     --resource-group rg-interface-configurator \
     --server sql-main-database \
     --name app-database \
     --storage-key-type StorageAccessKey \
     --storage-key <storage-key> \
     --storage-uri "https://stappgeneral.blob.core.windows.net/backups/app-database.bacpac" \
     --administrator-login sqladmin \
     --administrator-login-password <password>

   # Import MessageBox database
   az sql db import \
     --resource-group rg-interface-configurator \
     --server sql-main-database \
     --name MessageBox \
     --storage-key-type StorageAccessKey \
     --storage-key <storage-key> \
     --storage-uri "https://stappgeneral.blob.core.windows.net/backups/MessageBox.bacpac" \
     --administrator-login sqladmin \
     --administrator-login-password <password>
   ```

2. **Restore Blob Storage Data**
   ```powershell
   # Upload all blobs to new storage account
   az storage blob upload-batch \
     --destination function-config \
     --source ./backups/blob-storage \
     --account-name stappgeneral \
     --account-key <storage-key>
   ```

3. **Update Function App Settings**
   ```powershell
   # Update connection strings and settings
   az functionapp config appsettings set \
     --name func-integration-main \
     --resource-group rg-interface-configurator \
     --settings @function-app-settings.json
   ```

### Step 4: Update Application Configuration

1. **Update Environment Variables**
   - Update `AZURE_FUNCTION_APP_URL` in Vercel
   - Update `AZURE_STORAGE_CONNECTION_STRING` if changed
   - Update SQL connection strings

2. **Redeploy Function App**
   ```powershell
   cd azure-functions/main
   func azure functionapp publish func-integration-main
   ```

3. **Test Application**
   - Verify all endpoints are working
   - Test CSV upload and processing
   - Verify SQL database connectivity
   - Check MessageBox functionality

### Step 5: Clean Up Old Resources

**⚠️ WARNING: Only delete old resources after confirming everything works in the new region!**

```powershell
# Delete old resource group (this will delete all resources)
az group delete --name rg-infrastructure-as-code --yes --no-wait
```

## Resource Group Name Change

The resource group has been renamed from `rg-infrastructure-as-code` to `rg-interface-configurator`. 

**Note:** Azure does not support renaming resource groups. Resources must be moved to a new resource group. The move operation can be done within the same region using:

```powershell
.\scripts\move-resource-group.ps1
```

## Rollback Plan

If migration fails:

1. Keep old resources in Central US until migration is fully verified
2. Update DNS/URLs to point back to old resources
3. Restore from backups if needed
4. Delete new resources in West Europe

## Verification Checklist

- [ ] All SQL databases imported successfully
- [ ] Blob storage data restored
- [ ] Function App deployed and running
- [ ] All API endpoints responding
- [ ] CSV processing working
- [ ] MessageBox functionality verified
- [ ] Frontend connecting to new backend
- [ ] No errors in Application Insights
- [ ] All connection strings updated
- [ ] Old resources deleted (after verification)

## Estimated Downtime

- **Backup**: 30-60 minutes (depending on database size)
- **Deployment**: 15-30 minutes
- **Data Migration**: 30-60 minutes
- **Testing**: 15-30 minutes
- **Total**: ~2-3 hours

## Support

If you encounter issues during migration:
1. Check Azure Activity Log for errors
2. Review Application Insights for application errors
3. Verify all connection strings and URLs
4. Check firewall rules for SQL Server
5. Verify storage account access keys


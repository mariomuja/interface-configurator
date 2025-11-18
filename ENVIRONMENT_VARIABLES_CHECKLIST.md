# Environment Variables Checklist

This document lists all required environment variables for the application to function correctly.

## Vercel Environment Variables

### Required for API Functions

1. **Azure SQL Database**
   - `AZURE_SQL_SERVER` - SQL Server FQDN (e.g., `sql-csvtransport.database.windows.net`)
   - `AZURE_SQL_DATABASE` - Database name (e.g., `csvtransportdb`)
   - `AZURE_SQL_USER` - SQL Server admin username (e.g., `sqladmin`)
   - `AZURE_SQL_PASSWORD` - SQL Server admin password

2. **Azure Storage Account**
   - `AZURE_STORAGE_CONNECTION_STRING` - Full connection string
   - **OR** (alternative):
     - `AZURE_STORAGE_ACCOUNT_NAME` - Storage account name
     - `AZURE_STORAGE_ACCOUNT_KEY` - Storage account key
   - `AZURE_STORAGE_CONTAINER` - Container name (default: `csv-files`)

3. **Azure Function App**
   - `AZURE_FUNCTION_APP_URL` - Function App URL (e.g., `https://your-function-app.azurewebsites.net`)
   - **Alternative**: `FUNCTION_APP_URL`

### Optional

- `CsvFieldSeparator` - CSV field separator (default: `║`)

## Azure Function App Environment Variables

### Required

1. **Azure SQL Database**
   - `AZURE_SQL_SERVER` - SQL Server FQDN
   - `AZURE_SQL_DATABASE` - Database name
   - `AZURE_SQL_USER` - SQL Server admin username
   - `AZURE_SQL_PASSWORD` - SQL Server admin password

2. **Azure Storage Account**
   - `MainStorageConnection` - Storage account connection string (for blob triggers)
   - **OR** `AzureWebJobsStorage` - Fallback storage connection string

### Optional

- `CsvFieldSeparator` - CSV field separator (default: `║`)

## How to Check Configuration

### Using the UI

1. Open the application
2. Click the **"Diagnose"** button in the Process Log section
3. Review the diagnostic results:
   - ✅ Green = Configuration OK
   - ❌ Red = Configuration missing or incorrect
   - ⚠️ Orange = Error occurred

### Using the API

```bash
curl https://your-app.vercel.app/api/diagnose
```

## Common Issues

### "SQL query failed. Check if tables exist."

**Cause**: SQL environment variables not set or incorrect.

**Solution**:
1. Check Vercel Environment Variables
2. Verify all 4 SQL variables are set:
   - `AZURE_SQL_SERVER`
   - `AZURE_SQL_DATABASE`
   - `AZURE_SQL_USER`
   - `AZURE_SQL_PASSWORD`
3. Run diagnostics to verify connection

### "Function App URL not configured"

**Cause**: `AZURE_FUNCTION_APP_URL` not set in Vercel.

**Solution**:
1. Get Function App URL from Azure Portal
2. Set `AZURE_FUNCTION_APP_URL` in Vercel Environment Variables
3. Format: `https://your-function-app.azurewebsites.net` (no trailing slash)

### "Azure Storage credentials not configured"

**Cause**: Storage connection string or account/key not set.

**Solution**:
1. Set `AZURE_STORAGE_CONNECTION_STRING` in Vercel
2. **OR** set both `AZURE_STORAGE_ACCOUNT_NAME` and `AZURE_STORAGE_ACCOUNT_KEY`

### "Cannot connect to Azure SQL Server"

**Cause**: Firewall rules blocking connection or incorrect credentials.

**Solution**:
1. Check SQL Server firewall rules in Azure Portal
2. Ensure "Allow Azure services and resources to access this server" is enabled
3. Verify credentials are correct
4. Check if SQL Server is accessible from Vercel's IP ranges

### Empty Destination Table / Process Logs

**Possible Causes**:
1. Azure Function not triggered (check Function App logs)
2. SQL connection not configured in Function App
3. Function App not deployed or not running
4. Blob Trigger not working

**Solution**:
1. Run diagnostics to check all configurations
2. Check Azure Function App logs in Azure Portal
3. Verify Function App is deployed and running
4. Check if CSV files are uploaded to Blob Storage
5. Verify `MainStorageConnection` is set in Function App

## Verification Steps

1. **Check Vercel Environment Variables**:
   - Go to Vercel Dashboard → Project Settings → Environment Variables
   - Verify all required variables are set

2. **Check Azure Function App Configuration**:
   - Go to Azure Portal → Function App → Configuration
   - Verify all required Application Settings are present

3. **Run Diagnostics**:
   - Use the Diagnose button in the UI
   - Review all check results

4. **Test SQL Connection**:
   - Use Azure Portal Query Editor
   - Or use `sqlcmd` from command line

5. **Test Function App**:
   - Check Function App logs in Azure Portal
   - Verify HTTP endpoints are accessible:
     - `https://your-function-app.azurewebsites.net/api/GetProcessLogs`
     - Should return JSON array (can be empty)

## Quick Setup Script

For Azure Function App, you can use Azure CLI:

```bash
az functionapp config appsettings set \
  --name <function-app-name> \
  --resource-group <resource-group-name> \
  --settings \
    AZURE_SQL_SERVER="your-server.database.windows.net" \
    AZURE_SQL_DATABASE="your-database" \
    AZURE_SQL_USER="your-user" \
    AZURE_SQL_PASSWORD="your-password" \
    MainStorageConnection="your-connection-string"
```

For Vercel, use Vercel CLI:

```bash
vercel env add AZURE_SQL_SERVER
vercel env add AZURE_SQL_DATABASE
vercel env add AZURE_SQL_USER
vercel env add AZURE_SQL_PASSWORD
vercel env add AZURE_STORAGE_CONNECTION_STRING
vercel env add AZURE_FUNCTION_APP_URL
```






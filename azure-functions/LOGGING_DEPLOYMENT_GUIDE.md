# Logging Deployment Guide - Ensuring Logging Works After Deployment

## Problem

The Azure Function App returns `ServiceUnavailable` error, potentially due to logging functionality trying to access the database during startup.

## Solution: Fail-Safe Logging Configuration

The logging implementation is already designed to work **without a database**. Here's how it works:

### 1. LoggingServiceAdapter Design

The `LoggingServiceAdapter` follows a **fail-safe pattern**:

1. **Always logs to Console/ILogger first** (fail-safe)
2. **Only attempts database logging if context is available**
3. **Never fails the function if database logging fails**

```csharp
// Always log to console/ILogger first (fail-safe)
try {
    if (_logger != null) {
        _logger.LogInformation(logMessage);
    } else {
        Console.WriteLine(logMessage);
    }
} catch {
    // Even console logging failures are handled
}

// Try database logging (only if context available)
if (_context == null) {
    return; // No database context, console logging already done
}
```

### 2. Database Connection Checks

Before attempting database logging, the service:

- Checks if `_context` is null
- Verifies database connection with timeout (5 seconds)
- Checks if table exists
- Handles all errors gracefully

### 3. JavaScript Functions Don't Use C# Logging

The JavaScript `SimpleTestFunction` uses:
- `context.log()` - Built-in Azure Functions logging
- No C# dependencies
- No database access

## Deployment Checklist

### ✅ Pre-Deployment

1. **SQL Firewall Rules**
   ```bash
   # Ensure Azure Services are allowed
   az sql server firewall-rule create \
     --resource-group rg-infrastructure-as-code \
     --server <sql-server-name> \
     --name "AllowAzureServices" \
     --start-ip-address "0.0.0.0" \
     --end-ip-address "0.0.0.0"
   ```

2. **App Settings**
   - `FUNCTIONS_WORKER_RUNTIME=node` (for JavaScript)
   - `WEBSITE_NODE_DEFAULT_VERSION=~20`
   - `AzureWebJobsStorage` (required)
   - SQL settings (optional - logging works without them)

3. **Package Structure**
   - `host.json` (root)
   - `package.json` (root)
   - `SimpleTestFunction/function.json`
   - `SimpleTestFunction/index.js`
   - **NO C# DLLs** (for JavaScript functions)

### ✅ Deployment

1. **Create Clean Package**
   ```powershell
   Compress-Archive -Path "SimpleTestFunction", "host.json", "package.json" `
     -DestinationPath "deploy-package.zip" -Force
   ```

2. **Upload to Blob Storage**
   ```powershell
   az storage blob upload \
     --account-name <storage-account> \
     --account-key <key> \
     --container-name function-releases \
     --name "function-app-<timestamp>.zip" \
     --file deploy-package.zip
   ```

3. **Set WEBSITE_RUN_FROM_PACKAGE**
   ```powershell
   az functionapp config appsettings set \
     --resource-group <rg> \
     --name <function-app> \
     --settings "WEBSITE_RUN_FROM_PACKAGE=<blob-url-with-sas>"
   ```

4. **Restart Function App**
   ```powershell
   az functionapp restart --resource-group <rg> --name <function-app>
   ```

### ✅ Post-Deployment Verification

1. **Test Function Directly**
   ```bash
   curl https://<function-app>.azurewebsites.net/api/SimpleTestFunction
   ```

2. **Check Logs**
   ```bash
   az functionapp log tail --resource-group <rg> --name <function-app>
   ```

3. **Verify Functions List**
   ```bash
   az rest --method GET \
     --uri "https://management.azure.com/subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.Web/sites/<function-app>/functions?api-version=2022-03-01"
   ```

## Troubleshooting

### Problem: ServiceUnavailable Error

**Possible Causes:**
1. Package structure incorrect
2. `WEBSITE_RUN_FROM_PACKAGE` not set or invalid
3. Function App still initializing (wait 30-60 seconds)
4. C# dependencies in JavaScript function package

**Solutions:**
1. Verify package contains only JavaScript files
2. Check `WEBSITE_RUN_FROM_PACKAGE` is valid blob URL
3. Wait longer after restart
4. Remove C# DLLs from package

### Problem: Logging to Database Fails

**This is OK!** The logging service is designed to:
- Always log to console/ILogger first
- Only attempt database logging if available
- Never fail the function if database logging fails

**To Fix Database Logging:**
1. Check SQL Firewall Rules
2. Verify SQL connection string in App Settings
3. Ensure database exists and is accessible
4. Check Function App can reach SQL Server (network)

### Problem: Functions Not Listed

**Possible Causes:**
1. Package not deployed correctly
2. `host.json` missing or invalid
3. Function structure incorrect
4. Function App still starting

**Solutions:**
1. Verify package structure
2. Check `host.json` is valid JSON
3. Ensure `function.json` exists for each function
4. Wait 1-2 minutes after restart

## Best Practices

1. **Always log to console/ILogger first** - This ensures logging works even if database is unavailable
2. **Use fail-safe patterns** - Never let logging failures crash the function
3. **Separate JavaScript and C# functions** - Don't mix dependencies
4. **Test without database** - Ensure functions work even if SQL is unavailable
5. **Monitor logs** - Use Application Insights or console logs to debug issues

## Scripts

Use the provided scripts:
- `ensure-logging-works-without-db.ps1` - Verifies logging configuration
- `ensure-function-app-ready.ps1` - Ensures all services are ready
- `verify-function-app.ps1` - Verifies function app configuration





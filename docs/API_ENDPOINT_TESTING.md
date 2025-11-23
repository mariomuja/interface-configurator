# API Endpoint Testing Guide

## Test Script

The PowerShell script `azure-functions/test-api-endpoints.ps1` tests all new API endpoints.

## Usage

```powershell
.\azure-functions\test-api-endpoints.ps1 `
  -FunctionAppName "func-integration-main" `
  -InterfaceName "FromCsvToSqlServerExample" `
  -CsvBlobPath "csv-files/csv-incoming/test.csv" `
  -TableName "TransportData"
```

## Parameters

- `FunctionAppName` (required): Azure Function App name (e.g., "func-integration-main")
- `FunctionAppUrl` (optional): Full URL to Function App (alternative to FunctionAppName)
- `InterfaceName` (optional): Interface name for testing (default: "FromCsvToSqlServerExample")
- `CsvBlobPath` (optional): Path to CSV blob for validation/comparison tests
- `TableName` (optional): SQL table name (default: "TransportData")

## Endpoints Tested

1. **GetProcessingStatistics** (all interfaces)
   - `GET /api/GetProcessingStatistics`
   - Returns recent statistics for all interfaces

2. **GetProcessingStatistics** (specific interface)
   - `GET /api/GetProcessingStatistics?interfaceName={name}`
   - Returns summary statistics for a specific interface

3. **GetSqlTableSchema**
   - `GET /api/GetSqlTableSchema?interfaceName={name}&tableName={table}`
   - Returns SQL table schema details

4. **ValidateCsvFile**
   - `GET /api/ValidateCsvFile?blobPath={path}`
   - Validates CSV file (encoding, delimiter, structure)

5. **CompareCsvSqlSchema**
   - `GET /api/CompareCsvSqlSchema?interfaceName={name}&csvBlobPath={path}&tableName={table}`
   - Compares CSV and SQL schemas

## Deployment Status

**Current Status:** The new endpoints need to be deployed.

The Function App is running, but the new endpoints (`GetProcessingStatistics`, `GetSqlTableSchema`, `ValidateCsvFile`, `CompareCsvSqlSchema`) are not yet deployed.

### To Deploy:

1. **Automatic (Recommended):**
   - Push changes to GitHub
   - GitHub Actions workflow will automatically deploy

2. **Manual:**
   ```powershell
   cd azure-functions/main
   func azure functionapp publish func-integration-main --dotnet-isolated
   ```

## Troubleshooting

### 404 Errors

If all endpoints return 404:
- ✅ Function App is running
- ❌ New endpoints not deployed yet
- **Solution:** Deploy the latest code

### URL Encoding Errors

The script uses PowerShell's native `[System.Uri]::EscapeDataString()` for URL encoding, which works without additional dependencies.

### Authentication Errors

All endpoints use `AuthorizationLevel.Anonymous`, so no authentication is required for testing.

## Expected Results

After successful deployment, you should see:

- ✅ Success messages for each endpoint
- Statistics data (rows processed, success rate, etc.)
- SQL schema details (columns, types, etc.)
- CSV validation results (encoding, delimiter, issues)
- Schema comparison results (compatible/incompatible, mismatches)


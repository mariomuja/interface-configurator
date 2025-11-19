# Local Development Setup for Azure Functions

This guide explains how to configure and test Azure Functions locally with access to Azure databases (MessageBox and App Database).

## Prerequisites

- .NET 8.0 SDK installed
- Azure Functions Core Tools v4 installed
- Access to Azure SQL Database (MessageBox and App Database)
- Azure Storage Account connection string

## Configuration

### 1. Copy Example Configuration

Copy the example configuration file:

```bash
cd azure-functions/main
cp local.settings.json.example local.settings.json
```

### 2. Configure Environment Variables

Edit `local.settings.json` and update the following values:

```json
{
  "IsEncrypted": false,
  "Values": {
    "FUNCTIONS_EXTENSION_VERSION": "~4",
    "AZURE_FUNCTIONS_ENVIRONMENT": "Development",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    
    "MainStorageConnection": "DefaultEndpointsProtocol=https;AccountName=YOUR_STORAGE_ACCOUNT_NAME;AccountKey=YOUR_STORAGE_ACCOUNT_KEY;EndpointSuffix=core.windows.net",
    
    "AZURE_SQL_SERVER": "YOUR_SQL_SERVER.database.windows.net",
    "AZURE_SQL_DATABASE": "AppDatabase",
    "AZURE_SQL_USER": "YOUR_SQL_USERNAME",
    "AZURE_SQL_PASSWORD": "YOUR_SQL_PASSWORD",
    
    "CsvFieldSeparator": "║"
  }
}
```

### Required Environment Variables

#### Azure SQL Database Configuration

These variables are used to connect to both the **App Database** and **MessageBox Database**:

- **`AZURE_SQL_SERVER`**: Your Azure SQL Server FQDN (e.g., `yourserver.database.windows.net`)
- **`AZURE_SQL_DATABASE`**: Name of your application database (default: `AppDatabase`)
  - The MessageBox database name is hardcoded as `MessageBox` and uses the same server credentials
- **`AZURE_SQL_USER`**: SQL Server username
- **`AZURE_SQL_PASSWORD`**: SQL Server password

#### Azure Storage Configuration

- **`MainStorageConnection`**: Connection string for Azure Blob Storage
  - Format: `DefaultEndpointsProtocol=https;AccountName=YOUR_ACCOUNT_NAME;AccountKey=YOUR_ACCOUNT_KEY;EndpointSuffix=core.windows.net`
  - Used for CSV file processing and configuration storage
- **`AzureWebJobsStorage`**: Fallback storage connection string
  - Can use `UseDevelopmentStorage=true` for local Azure Storage Emulator

#### Optional Configuration

- **`CsvFieldSeparator`**: CSV field separator character (default: `║`)

## Database Access

The Azure Functions connect to two databases on the same SQL Server:

1. **App Database** (`AZURE_SQL_DATABASE`):
   - Contains application tables (e.g., `TransportData`)
   - Used by `SqlServerAdapter` for reading/writing data

2. **MessageBox Database** (`MessageBox`):
   - Contains staging tables: `Messages`, `MessageSubscriptions`, `ProcessLogs`
   - Used for guaranteed delivery pattern
   - Automatically created on first run if it doesn't exist

### Firewall Configuration

Ensure your local IP address is allowed in Azure SQL Server firewall:

1. Go to Azure Portal → SQL Server → Firewalls and virtual networks
2. Add your current IP address
3. Or enable "Allow Azure services and resources to access this server" for testing

## Running Locally

### 1. Restore Dependencies

```bash
cd azure-functions/main
dotnet restore
```

### 2. Build

```bash
dotnet build
```

### 3. Run Functions Locally

```bash
func start
```

The functions will start on `http://localhost:7071`

### 4. Test Functions

```bash
# Health check
curl http://localhost:7071/api/HealthCheck

# Get interface configurations
curl http://localhost:7071/api/GetInterfaceConfigurations

# Diagnose (checks all environment variables)
curl http://localhost:7071/api/Diagnose
```

## Debugging

### VS Code / Cursor

1. Set breakpoints in your function code
2. Press `F5` to start debugging
3. Functions will be available at `http://localhost:7071`

### Visual Studio

1. Set `azure-functions/main` as startup project
2. Press `F5` to start debugging
3. Functions will be available at `http://localhost:7071`

## Environment Variable Usage

The code uses `Environment.GetEnvironmentVariable()` to read configuration:

- **Program.cs**: Reads SQL connection variables and storage connection strings
- **main.cs**: Reads `MainStorageConnection` for BlobServiceClient
- **InterfaceConfigurationService.cs**: Reads SQL variables for default configuration
- **AdapterConfigurationService.cs**: Reads `CsvFieldSeparator` for CSV separator

## Troubleshooting

### "SQL connection environment variables not set"

**Solution**: Ensure all SQL environment variables are set in `local.settings.json`:
- `AZURE_SQL_SERVER`
- `AZURE_SQL_DATABASE`
- `AZURE_SQL_USER`
- `AZURE_SQL_PASSWORD`

### "Storage connection string not found"

**Solution**: Set `MainStorageConnection` or `AzureWebJobsStorage` in `local.settings.json`

### "Cannot connect to SQL Server"

**Solutions**:
1. Check firewall rules in Azure Portal
2. Verify SQL Server name is correct (include `.database.windows.net`)
3. Verify username and password are correct
4. Check if SQL Server allows Azure services (for testing)

### "MessageBox database not found"

**Solution**: The MessageBox database is automatically created on first run. Ensure:
- SQL Server credentials are correct
- User has permission to create databases
- Firewall allows your IP address

## Security Notes

⚠️ **Important**: `local.settings.json` contains sensitive credentials and is excluded from Git (via `.gitignore`). Never commit this file to version control.

- Use `local.settings.json.example` as a template
- Keep actual credentials in `local.settings.json` (not committed)
- Use Azure Key Vault for production deployments

## Next Steps

- Test CSV file processing: Upload a CSV file to your blob storage container
- Test SQL Server adapter: Configure an interface with SQL Server as destination
- Monitor MessageBox: Check `Messages` and `MessageSubscriptions` tables
- View logs: Check `ProcessLogs` table in MessageBox database


# Restore Demo Interface Configuration - Direct Blob Storage Write
# This script directly writes the demo interface configuration to blob storage

param(
    [string]$StorageAccountName = "stappgeneral",
    [string]$ResourceGroup = "rg-interface-configurator"
)

Write-Host "`n=== Restore Demo Interface Configuration (Direct Blob Storage) ===" -ForegroundColor Cyan

# Get storage account key
Write-Host "`nGetting storage account key..." -ForegroundColor Yellow
$storageKey = az storage account keys list --resource-group $ResourceGroup --account-name $StorageAccountName --query "[0].value" -o tsv

if ([string]::IsNullOrWhiteSpace($storageKey)) {
    Write-Host "❌ Could not get storage account key" -ForegroundColor Red
    Write-Host "Trying alternative resource groups..." -ForegroundColor Yellow
    
    # Try to find storage account in any resource group
    $storageAccount = az storage account list --query "[?name=='$StorageAccountName']" -o json | ConvertFrom-Json | Select-Object -First 1
    if ($storageAccount) {
        $rgName = $storageAccount.resourceGroup
        $storageKey = az storage account keys list --resource-group $rgName --account-name $StorageAccountName --query "[0].value" -o tsv
        Write-Host "✅ Found storage account in resource group: $rgName" -ForegroundColor Green
    }
    
    if ([string]::IsNullOrWhiteSpace($storageKey)) {
        Write-Host "❌ Could not get storage account key" -ForegroundColor Red
        exit 1
    }
}

Write-Host "✅ Storage account key obtained" -ForegroundColor Green

# Get SQL Server connection details
$sqlServer = $env:AZURE_SQL_SERVER
$sqlDatabase = $env:AZURE_SQL_DATABASE
$sqlUser = $env:AZURE_SQL_USER
$sqlPassword = $env:AZURE_SQL_PASSWORD

if ([string]::IsNullOrWhiteSpace($sqlServer)) {
    Write-Host "`n⚠ Warning: AZURE_SQL_SERVER not set. Using default values." -ForegroundColor Yellow
    $sqlServer = "sql-main-database.database.windows.net"
    $sqlDatabase = "app-database"
    $sqlUser = "sqladmin"
    $sqlPassword = "InfrastructureAsCode2024!Secure"
}

# Create the demo interface configuration JSON
$now = Get-Date -Format "yyyy-MM-ddTHH:mm:ss.fffZ"
$sourceGuid = [guid]::NewGuid().ToString()
$destGuid = [guid]::NewGuid().ToString()

$demoConfig = @{
    InterfaceName = "FromCsvToSqlServerExample"
    Description = "Default CSV to SQL Server demo interface created automatically."
    CreatedAt = $now
    UpdatedAt = $now
    Sources = @{
        "CSV Source" = @{
            InstanceName = "CSV Source"
            AdapterName = "CSV"
            IsEnabled = $true
            AdapterInstanceGuid = $sourceGuid
            Configuration = '{"source":"csv-files/csv-incoming","enabled":true}'
            SourceReceiveFolder = "csv-files/csv-incoming"
            SourceFileMask = "*.txt"
            SourceBatchSize = 100
            SourceFieldSeparator = "║"
            CsvPollingInterval = 10
            CsvAdapterType = "FILE"
            CreatedAt = $now
            UpdatedAt = $now
        }
    }
    Destinations = @{
        "SQL Destination" = @{
            InstanceName = "SQL Destination"
            AdapterName = "SqlServer"
            IsEnabled = $true
            AdapterInstanceGuid = $destGuid
            Configuration = (@{
                destination = "TransportData"
                tableName = "TransportData"
                sqlServerName = $sqlServer
                sqlDatabaseName = $sqlDatabase
                sqlUserName = $sqlUser
                sqlPassword = $sqlPassword
                sqlIntegratedSecurity = $false
            } | ConvertTo-Json -Compress)
            SqlServerName = $sqlServer
            SqlDatabaseName = $sqlDatabase
            SqlUserName = $sqlUser
            SqlPassword = $sqlPassword
            SqlIntegratedSecurity = $false
            SqlTableName = "TransportData"
            SqlUseTransaction = $false
            SqlBatchSize = 1000
            SqlCommandTimeout = 30
            SqlFailOnBadStatement = $false
            CreatedAt = $now
            UpdatedAt = $now
        }
    }
}

# Convert to JSON array (as the service expects a list)
$configArray = @($demoConfig)
$jsonContent = $configArray | ConvertTo-Json -Depth 10

Write-Host "`nConfiguration JSON:" -ForegroundColor Gray
Write-Host $jsonContent -ForegroundColor Gray

# Create container if it doesn't exist
Write-Host "`nCreating container 'function-config' if it doesn't exist..." -ForegroundColor Yellow
az storage container create --account-name $StorageAccountName --account-key $storageKey --name "function-config" --public-access off --output none 2>&1 | Out-Null

# Upload the configuration file
Write-Host "Uploading interface configuration to blob storage..." -ForegroundColor Yellow
$tempFile = [System.IO.Path]::GetTempFileName()
$jsonContent | Out-File -FilePath $tempFile -Encoding UTF8

try {
    az storage blob upload --account-name $StorageAccountName --account-key $storageKey --container-name "function-config" --name "interface-configurations.json" --file $tempFile --overwrite --output none
    
    Write-Host "`n✅ Demo interface configuration restored successfully!" -ForegroundColor Green
    Write-Host "Interface Name: FromCsvToSqlServerExample" -ForegroundColor White
    Write-Host "Source Adapter: CSV (CSV Source)" -ForegroundColor White
    Write-Host "Destination Adapter: SqlServer (SQL Destination)" -ForegroundColor White
    Write-Host "`n✅ Configuration saved to blob storage: function-config/interface-configurations.json" -ForegroundColor Green
}
catch {
    Write-Host "`n❌ Error uploading configuration:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
finally {
    Remove-Item $tempFile -ErrorAction SilentlyContinue
}

Write-Host "`n=== Done ===" -ForegroundColor Cyan


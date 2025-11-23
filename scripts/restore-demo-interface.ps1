# Restore Demo Interface Configuration
# This script restores the default "FromCsvToSqlServerExample" interface configuration
# with CSV source adapter and SQL Server destination adapter

param(
    [string]$FunctionAppUrl = "",
    [string]$ResourceGroup = "rg-interface-configurator"
)

Write-Host "`n=== Restore Demo Interface Configuration ===" -ForegroundColor Cyan

# Get Function App URL if not provided
if ([string]::IsNullOrWhiteSpace($FunctionAppUrl)) {
    Write-Host "`nGetting Function App URL..." -ForegroundColor Yellow
    $functionAppName = "func-integration-main"
    
    # Try to get from any resource group
    $FunctionAppUrl = az functionapp list --query "[?name=='$functionAppName'].defaultHostName" -o tsv | Select-Object -First 1
    
    if ([string]::IsNullOrWhiteSpace($FunctionAppUrl)) {
        # Fallback to known URL
        $FunctionAppUrl = "https://func-integration-main.azurewebsites.net"
        Write-Host "⚠ Using default Function App URL: $FunctionAppUrl" -ForegroundColor Yellow
    } else {
        $FunctionAppUrl = "https://$FunctionAppUrl"
        Write-Host "✅ Function App URL: $FunctionAppUrl" -ForegroundColor Green
    }
}

# Get SQL Server connection details from environment or parameters
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

Write-Host "`nCreating demo interface configuration..." -ForegroundColor Yellow

# Prepare the request body
$requestBody = @{
    InterfaceName = "FromCsvToSqlServerExample"
    SourceAdapterName = "CSV"
    SourceConfiguration = '{"source":"csv-files/csv-incoming","enabled":true}'
    SourceInstanceName = "CSV Source"
    SourceIsEnabled = $true
    DestinationAdapterName = "SqlServer"
    DestinationConfiguration = (@{
        destination = "TransportData"
        tableName = "TransportData"
        sqlServerName = $sqlServer
        sqlDatabaseName = $sqlDatabase
        sqlUserName = $sqlUser
        sqlPassword = $sqlPassword
        sqlIntegratedSecurity = $false
    } | ConvertTo-Json -Compress)
    DestinationInstanceName = "SQL Destination"
    DestinationIsEnabled = $true
    Description = "Default CSV to SQL Server demo interface"
} | ConvertTo-Json -Depth 10

Write-Host "Request body:" -ForegroundColor Gray
Write-Host $requestBody -ForegroundColor Gray

# Call the API
$apiUrl = "$FunctionAppUrl/api/CreateInterfaceConfiguration"
Write-Host "`nCalling API: $apiUrl" -ForegroundColor Yellow

try {
    $response = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $requestBody -ContentType "application/json" -ErrorAction Stop
    
    Write-Host "`n✅ Demo interface configuration restored successfully!" -ForegroundColor Green
    Write-Host "Interface Name: $($response.InterfaceName)" -ForegroundColor White
    Write-Host "Source Adapter: $($response.Sources.'CSV Source'.AdapterName) ($($response.Sources.'CSV Source'.InstanceName))" -ForegroundColor White
    Write-Host "Destination Adapter: $($response.Destinations.'SQL Destination'.AdapterName) ($($response.Destinations.'SQL Destination'.InstanceName))" -ForegroundColor White
    
    Write-Host "`n✅ Configuration saved to blob storage" -ForegroundColor Green
}
catch {
    Write-Host "`n❌ Error restoring demo interface configuration:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    if ($_.ErrorDetails) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    
    # Try to get response body for more details
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response body: $responseBody" -ForegroundColor Red
    }
    
    Write-Host "`nTrying alternative approach: Checking if interface already exists..." -ForegroundColor Yellow
    
    # Check if interface already exists
    try {
        $checkUrl = "$FunctionAppUrl/api/GetInterfaceConfiguration?interfaceName=FromCsvToSqlServerExample"
        $existing = Invoke-RestMethod -Uri $checkUrl -Method Get -ErrorAction Stop
        if ($existing) {
            Write-Host "✅ Interface 'FromCsvToSqlServerExample' already exists!" -ForegroundColor Green
            Write-Host "Interface Name: $($existing.InterfaceName)" -ForegroundColor White
            exit 0
        }
    }
    catch {
        Write-Host "Interface does not exist. Error details:" -ForegroundColor Yellow
        Write-Host $_.Exception.Message -ForegroundColor Red
    }
    
    exit 1
}

Write-Host "`n=== Done ===" -ForegroundColor Cyan


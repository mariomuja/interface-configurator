# Check MessageBox Database
# This script checks if data exists in the MessageBox database

param(
    [string]$SqlServer = "",
    [string]$SqlDatabase = "MessageBox",
    [string]$SqlUser = "",
    [string]$SqlPassword = ""
)

Write-Host "=== Checking MessageBox Database ===" -ForegroundColor Cyan

# Get SQL Server connection details if not provided
if ([string]::IsNullOrEmpty($SqlServer)) {
    Write-Host "Getting SQL Server details from Azure..." -ForegroundColor Yellow
    $sqlServer = az sql server list --query "[0].fullyQualifiedDomainName" -o tsv
    $sqlUser = az sql server list --query "[0].administratorLogin" -o tsv
    
    if ([string]::IsNullOrEmpty($sqlServer)) {
        Write-Host "Error: Could not find SQL Server. Please provide SqlServer parameter." -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Found SQL Server: $sqlServer" -ForegroundColor Green
    Write-Host "SQL User: $sqlUser" -ForegroundColor Green
}

# Prompt for password if not provided
if ([string]::IsNullOrEmpty($SqlPassword)) {
    $securePassword = Read-Host "Enter SQL Server password" -AsSecureString
    $SqlPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword))
}

# Create connection string
$connectionString = "Server=tcp:$SqlServer,1433;Initial Catalog=$SqlDatabase;User ID=$SqlUser;Password=$SqlPassword;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host ""
Write-Host "=== Checking Messages Table Structure ===" -ForegroundColor Cyan

$checkStructureQuery = @"
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Messages'
ORDER BY ORDINAL_POSITION;
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    
    $command = $connection.CreateCommand()
    $command.CommandText = $checkStructureQuery
    $reader = $command.ExecuteReader()
    
    Write-Host "Columns in Messages table:" -ForegroundColor Yellow
    while ($reader.Read()) {
        $columnName = $reader["COLUMN_NAME"]
        $dataType = $reader["DATA_TYPE"]
        $isNullable = $reader["IS_NULLABLE"]
        Write-Host "  - $columnName ($dataType, Nullable: $isNullable)" -ForegroundColor White
    }
    $reader.Close()
    
    # Check if AdapterInstanceGuid exists
    Write-Host ""
    Write-Host "=== Checking for Required Columns ===" -ForegroundColor Cyan
    $checkColumnQuery = "SELECT COUNT(*) AS ColumnExists FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Messages' AND COLUMN_NAME = 'AdapterInstanceGuid'"
    $command.CommandText = $checkColumnQuery
    $columnExists = $command.ExecuteScalar()
    
    if ($columnExists -eq 0) {
        Write-Host "ERROR: AdapterInstanceGuid column DOES NOT EXIST!" -ForegroundColor Red
        Write-Host "This is why messages cannot be written to the database." -ForegroundColor Red
        Write-Host "Run update-messagebox-database.sql to add missing columns." -ForegroundColor Yellow
    } else {
        Write-Host "OK: AdapterInstanceGuid column exists" -ForegroundColor Green
    }
    
    # Check row count
    Write-Host ""
    Write-Host "=== Checking Data ===" -ForegroundColor Cyan
    $countQuery = "SELECT COUNT(*) AS TotalMessages FROM [dbo].[Messages]"
    $command.CommandText = $countQuery
    $totalMessages = $command.ExecuteScalar()
    Write-Host "Total Messages: $totalMessages" -ForegroundColor $(if ($totalMessages -gt 0) { "Green" } else { "Yellow" })
    
    if ($totalMessages -gt 0) {
        # Show messages by interface
        Write-Host ""
        Write-Host "Messages by Interface:" -ForegroundColor Yellow
        $interfaceQuery = @"
SELECT 
    InterfaceName,
    COUNT(*) AS MessageCount,
    MIN(datetime_created) AS FirstMessage,
    MAX(datetime_created) AS LastMessage
FROM [dbo].[Messages]
GROUP BY InterfaceName
ORDER BY MessageCount DESC;
"@
        $command.CommandText = $interfaceQuery
        $reader = $command.ExecuteReader()
        while ($reader.Read()) {
            $interfaceName = $reader["InterfaceName"]
            $messageCount = $reader["MessageCount"]
            $firstMessage = $reader["FirstMessage"]
            $lastMessage = $reader["LastMessage"]
            Write-Host "  - $interfaceName : $messageCount messages (First: $firstMessage, Last: $lastMessage)" -ForegroundColor White
        }
        $reader.Close()
        
        # Show messages by status
        Write-Host ""
        Write-Host "Messages by Status:" -ForegroundColor Yellow
        $statusQuery = "SELECT Status, COUNT(*) AS MessageCount FROM [dbo].[Messages] GROUP BY Status ORDER BY MessageCount DESC"
        $command.CommandText = $statusQuery
        $reader = $command.ExecuteReader()
        while ($reader.Read()) {
            $status = $reader["Status"]
            $messageCount = $reader["MessageCount"]
            Write-Host "  - $status : $messageCount messages" -ForegroundColor White
        }
        $reader.Close()
        
        # Show recent messages
        Write-Host ""
        Write-Host "Recent Messages (Last 5):" -ForegroundColor Yellow
        $recentQuery = @"
SELECT TOP 5
    MessageId,
    InterfaceName,
    AdapterName,
    AdapterType,
    Status,
    datetime_created,
    LEN(MessageData) AS MessageDataLength
FROM [dbo].[Messages]
ORDER BY datetime_created DESC;
"@
        $command.CommandText = $recentQuery
        $reader = $command.ExecuteReader()
        while ($reader.Read()) {
            $messageId = $reader["MessageId"]
            $interfaceName = $reader["InterfaceName"]
            $adapterName = $reader["AdapterName"]
            $status = $reader["Status"]
            $created = $reader["datetime_created"]
            Write-Host "  - $messageId : $interfaceName / $adapterName / $status / $created" -ForegroundColor White
        }
        $reader.Close()
    } else {
        Write-Host ""
        Write-Host "No messages found in database." -ForegroundColor Yellow
        Write-Host "Possible reasons:" -ForegroundColor Yellow
        Write-Host "  1. CsvAdapter is not writing to MessageBox (check logs)" -ForegroundColor White
        Write-Host "  2. Database connection issues" -ForegroundColor White
        Write-Host "  3. Missing columns in Messages table" -ForegroundColor White
    }
    
    # Check AdapterInstances
    Write-Host ""
    Write-Host "=== Checking AdapterInstances ===" -ForegroundColor Cyan
    $adapterInstancesQuery = "SELECT COUNT(*) AS TotalAdapterInstances FROM [dbo].[AdapterInstances]"
    $command.CommandText = $adapterInstancesQuery
    $totalInstances = $command.ExecuteScalar()
    Write-Host "Total Adapter Instances: $totalInstances" -ForegroundColor $(if ($totalInstances -gt 0) { "Green" } else { "Yellow" })
    
    if ($totalInstances -gt 0) {
        $instancesQuery = "SELECT AdapterInstanceGuid, InterfaceName, InstanceName, AdapterName, AdapterType, IsEnabled FROM [dbo].[AdapterInstances] ORDER BY InterfaceName, AdapterType"
        $command.CommandText = $instancesQuery
        $reader = $command.ExecuteReader()
        Write-Host "Adapter Instances:" -ForegroundColor Yellow
        while ($reader.Read()) {
            $guid = $reader["AdapterInstanceGuid"]
            $interfaceName = $reader["InterfaceName"]
            $instanceName = $reader["InstanceName"]
            $adapterName = $reader["AdapterName"]
            $adapterType = $reader["AdapterType"]
            $isEnabled = $reader["IsEnabled"]
            Write-Host "  - $guid : $interfaceName / $instanceName / $adapterName ($adapterType) - Enabled: $isEnabled" -ForegroundColor White
        }
        $reader.Close()
    }
    
    $connection.Close()
    Write-Host ""
    Write-Host "=== Check Complete ===" -ForegroundColor Green
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}


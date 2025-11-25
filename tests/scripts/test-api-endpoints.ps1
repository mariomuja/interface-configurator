# Test API Endpoints Script
# Tests the new API endpoints: GetProcessingStatistics, GetSqlTableSchema, ValidateCsvFile, CompareCsvSqlSchema

param(
    [string]$FunctionAppName = "",
    [string]$FunctionAppUrl = "",
    [string]$InterfaceName = "FromCsvToSqlServerExample",
    [string]$CsvBlobPath = "csv-files/csv-incoming/test.csv",
    [string]$TableName = "TransportData"
)

# Helper function for URL encoding (PowerShell native)
function Encode-UriComponent {
    param([string]$Value)
    [System.Uri]::EscapeDataString($Value)
}

# Get Function App URL
if ([string]::IsNullOrEmpty($FunctionAppUrl)) {
    if ([string]::IsNullOrEmpty($FunctionAppName)) {
        Write-Host "Error: Either FunctionAppUrl or FunctionAppName must be provided" -ForegroundColor Red
        exit 1
    }
    $FunctionAppUrl = "https://$FunctionAppName.azurewebsites.net"
}

Write-Host "Testing API endpoints at: $FunctionAppUrl" -ForegroundColor Cyan
Write-Host ""

# Test 1: GetProcessingStatistics (all interfaces)
Write-Host "=== Test 1: GetProcessingStatistics (all interfaces) ===" -ForegroundColor Yellow
try {
    $url = "$FunctionAppUrl/api/GetProcessingStatistics"
    $response = Invoke-RestMethod -Uri $url -Method Get -ContentType "application/json"
    Write-Host "✅ Success! Retrieved statistics for all interfaces" -ForegroundColor Green
    Write-Host "Response: $($response | ConvertTo-Json -Depth 3)" -ForegroundColor Gray
} catch {
    Write-Host "❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Red
    }
}
Write-Host ""

# Test 2: GetProcessingStatistics (specific interface)
Write-Host "=== Test 2: GetProcessingStatistics (interface: $InterfaceName) ===" -ForegroundColor Yellow
try {
    $url = "$FunctionAppUrl/api/GetProcessingStatistics?interfaceName=$(Encode-UriComponent $InterfaceName)"
    $response = Invoke-RestMethod -Uri $url -Method Get -ContentType "application/json"
    Write-Host "✅ Success! Retrieved statistics for interface: $InterfaceName" -ForegroundColor Green
    Write-Host "Total Rows Processed: $($response.totalRowsProcessed)" -ForegroundColor Cyan
    Write-Host "Success Rate: $($response.successRate)%" -ForegroundColor Cyan
    Write-Host "Rows/Hour: $($response.rowsPerHour)" -ForegroundColor Cyan
} catch {
    Write-Host "❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Red
    }
}
Write-Host ""

# Test 3: GetSqlTableSchema
Write-Host "=== Test 3: GetSqlTableSchema (interface: $InterfaceName, table: $TableName) ===" -ForegroundColor Yellow
try {
    $url = "$FunctionAppUrl/api/GetSqlTableSchema?interfaceName=$(Encode-UriComponent $InterfaceName)&tableName=$(Encode-UriComponent $TableName)"
    $response = Invoke-RestMethod -Uri $url -Method Get -ContentType "application/json"
    Write-Host "✅ Success! Retrieved SQL schema for table: $TableName" -ForegroundColor Green
    Write-Host "Table: $($response.tableName)" -ForegroundColor Cyan
    Write-Host "Column Count: $($response.columnCount)" -ForegroundColor Cyan
    Write-Host "Columns:" -ForegroundColor Cyan
    $response.columns | ForEach-Object {
        Write-Host "  - $($_.columnName): $($_.dataType) ($($_.sqlTypeDefinition))" -ForegroundColor Gray
    }
} catch {
    Write-Host "❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Red
    }
}
Write-Host ""

# Test 4: ValidateCsvFile
Write-Host "=== Test 4: ValidateCsvFile (blob: $CsvBlobPath) ===" -ForegroundColor Yellow
try {
    $url = "$FunctionAppUrl/api/ValidateCsvFile?blobPath=$(Encode-UriComponent $CsvBlobPath)"
    $response = Invoke-RestMethod -Uri $url -Method Get -ContentType "application/json"
    Write-Host "✅ Success! Validated CSV file" -ForegroundColor Green
    Write-Host "Is Valid: $($response.isValid)" -ForegroundColor $(if ($response.isValid) { "Green" } else { "Red" })
    Write-Host "Encoding: $($response.encoding)" -ForegroundColor Cyan
    Write-Host "Delimiter: $($response.detectedDelimiter)" -ForegroundColor Cyan
    Write-Host "Line Count: $($response.lineCount)" -ForegroundColor Cyan
    Write-Host "Column Count: $($response.columnCount)" -ForegroundColor Cyan
    if ($response.issues -and $response.issues.Count -gt 0) {
        Write-Host "Issues:" -ForegroundColor Yellow
        $response.issues | ForEach-Object {
            Write-Host "  - $_" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Red
    }
}
Write-Host ""

# Test 5: CompareCsvSqlSchema
Write-Host "=== Test 5: CompareCsvSqlSchema (interface: $InterfaceName, CSV: $CsvBlobPath, Table: $TableName) ===" -ForegroundColor Yellow
try {
    $url = "$FunctionAppUrl/api/CompareCsvSqlSchema?interfaceName=$(Encode-UriComponent $InterfaceName)&csvBlobPath=$(Encode-UriComponent $CsvBlobPath)&tableName=$(Encode-UriComponent $TableName)"
    $response = Invoke-RestMethod -Uri $url -Method Get -ContentType "application/json"
    Write-Host "✅ Success! Compared CSV and SQL schemas" -ForegroundColor Green
    Write-Host "Is Compatible: $($response.isCompatible)" -ForegroundColor $(if ($response.isCompatible) { "Green" } else { "Red" })
    Write-Host "CSV Columns: $($response.csvColumnCount)" -ForegroundColor Cyan
    Write-Host "SQL Columns: $($response.sqlColumnCount)" -ForegroundColor Cyan
    Write-Host "Common Columns: $($response.commonColumnCount)" -ForegroundColor Cyan
    Write-Host "Missing in SQL: $($response.missingInSql.Count)" -ForegroundColor $(if ($response.missingInSql.Count -gt 0) { "Red" } else { "Green" })
    Write-Host "Missing in CSV: $($response.missingInCsv.Count)" -ForegroundColor $(if ($response.missingInCsv.Count -gt 0) { "Red" } else { "Green" })
    Write-Host "Type Mismatches: $($response.typeMismatches.Count)" -ForegroundColor $(if ($response.typeMismatches.Count -gt 0) { "Red" } else { "Green" })
    
    if ($response.missingInSql -and $response.missingInSql.Count -gt 0) {
        Write-Host "Missing in SQL:" -ForegroundColor Yellow
        $response.missingInSql | ForEach-Object {
            Write-Host "  - $_" -ForegroundColor Red
        }
    }
    
    if ($response.typeMismatches -and $response.typeMismatches.Count -gt 0) {
        Write-Host "Type Mismatches:" -ForegroundColor Yellow
        $response.typeMismatches | ForEach-Object {
            Write-Host "  - $($_.columnName): CSV=$($_.csvType), SQL=$($_.sqlType)" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Red
    }
}
Write-Host ""

Write-Host "=== All Tests Completed ===" -ForegroundColor Cyan


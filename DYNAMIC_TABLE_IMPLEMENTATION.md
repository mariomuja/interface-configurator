# Dynamic Table Implementation Plan

## Overview
This document outlines the implementation plan for dynamic table creation and row-by-row processing with error handling.

## Key Components Created

### 1. CsvColumnAnalyzer (`ProcessCsvBlobTrigger.Core/Services/CsvColumnAnalyzer.cs`)
- Analyzes CSV column values to determine SQL data types
- Supports: NVARCHAR, INT, DECIMAL, DATETIME2, BIT, UNIQUEIDENTIFIER
- Calculates appropriate precision/scale for decimals
- Determines max length for strings

### 2. IDynamicTableService Interface (`ProcessCsvBlobTrigger.Core/Interfaces/IDynamicTableService.cs`)
- `EnsureTableStructureAsync`: Creates/updates table based on CSV columns
- `AddMissingColumnsAsync`: Adds new columns when CSV structure changes
- `GetCurrentTableStructureAsync`: Retrieves current table schema

### 3. DynamicTableService (`ProcessCsvBlobTrigger/Services/DynamicTableService.cs`)
- Implements IDynamicTableService
- Creates TransportData table dynamically
- Adds missing columns automatically
- Sanitizes column names for SQL compatibility

### 4. RowProcessingResult (`ProcessCsvBlobTrigger.Core/Models/RowProcessingResult.cs`)
- Represents result of processing a single CSV row
- Contains: Success flag, ErrorMessage, Exception, RowData, RowNumber

### 5. ProcessingResult Extended
- Added `RecordsFailed` property
- Added `FailedRows` list for tracking failed rows

## Implementation Steps Required

### Step 1: Update CsvProcessor for Row-by-Row Processing

```csharp
public async Task<ProcessingResult> ProcessCsvAsync(byte[] blobContent, string blobName, CancellationToken cancellationToken = default)
{
    // 1. Parse CSV to get headers and rows
    var csvRecords = _csvProcessingService.ParseCsv(csvContent);
    var headers = csvRecords.FirstOrDefault()?.Keys.ToList();
    
    // 2. Analyze column types from all rows
    var columnTypes = AnalyzeColumnTypes(headers, csvRecords);
    
    // 3. Ensure table structure matches CSV
    await _dynamicTableService.EnsureTableStructureAsync(columnTypes, cancellationToken);
    
    // 4. Process each row individually
    var successfulRows = new List<Dictionary<string, string>>();
    var failedRows = new List<RowProcessingResult>();
    
    for (int i = 0; i < csvRecords.Count; i++)
    {
        var row = csvRecords[i];
        var result = await ProcessRowAsync(row, columnTypes, i + 1, cancellationToken);
        
        if (result.Success)
        {
            successfulRows.Add(row);
        }
        else
        {
            failedRows.Add(result);
            // Save failed row to error folder
            await SaveFailedRowToErrorFolderAsync(blobName, row, result, i + 1);
        }
    }
    
    // 5. Insert successful rows in batches
    if (successfulRows.Count > 0)
    {
        await InsertRowsAsync(successfulRows, columnTypes, cancellationToken);
    }
    
    return ProcessingResult.SuccessResult(successfulRows.Count, 1, failedRows);
}
```

### Step 2: Implement Row Processing with Type Validation

```csharp
private async Task<RowProcessingResult> ProcessRowAsync(
    Dictionary<string, string> row, 
    Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes,
    int rowNumber,
    CancellationToken cancellationToken)
{
    try
    {
        // Validate each column value matches expected type
        foreach (var column in columnTypes)
        {
            var columnName = column.Key;
            var expectedType = column.Value.DataType;
            var value = row.GetValueOrDefault(columnName, string.Empty);
            
            if (!string.IsNullOrEmpty(value))
            {
                if (!ValidateValueType(value, expectedType))
                {
                    return RowProcessingResult.FailureResult(
                        $"Type mismatch in column '{columnName}': '{value}' cannot be converted to {expectedType}",
                        row,
                        rowNumber
                    );
                }
            }
        }
        
        return RowProcessingResult.SuccessResult(row, rowNumber);
    }
    catch (Exception ex)
    {
        return RowProcessingResult.FailureResult(
            $"Error processing row: {ex.Message}",
            row,
            rowNumber,
            ex
        );
    }
}
```

### Step 3: Save Failed Rows to Error Folder

```csharp
private async Task SaveFailedRowToErrorFolderAsync(
    string originalBlobName,
    Dictionary<string, string> row,
    RowProcessingResult result,
    int rowNumber)
{
    // Create CSV content for single row
    var headers = row.Keys.ToList();
    var csvLine = string.Join(",", headers.Select(h => $"\"{row[h]}\""));
    var csvContent = string.Join(",", headers) + "\n" + csvLine;
    
    // Generate error file name: {original}_row{number}_error_{timestamp}.csv
    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    var errorFileName = $"{Path.GetFileNameWithoutExtension(originalBlobName)}_row{rowNumber}_error_{timestamp}.csv";
    
    // Save to csv-error folder
    await _blobServiceClient.GetBlobContainerClient("csv-files")
        .GetBlobClient($"csv-error/{errorFileName}")
        .UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(csvContent)));
    
    // Also save error metadata as JSON
    var errorMetadata = new
    {
        originalFile = originalBlobName,
        rowNumber = rowNumber,
        error = result.ErrorMessage,
        errorTime = DateTime.UtcNow,
        rowData = row
    };
    
    var metadataFileName = errorFileName.Replace(".csv", ".error.json");
    await _blobServiceClient.GetBlobContainerClient("csv-files")
        .GetBlobClient($"csv-error/{metadataFileName}")
        .UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(errorMetadata))));
}
```

### Step 4: Update DataServiceAdapter for Dynamic Columns

```csharp
public async Task InsertRowsAsync(
    List<Dictionary<string, string>> rows,
    Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes,
    CancellationToken cancellationToken = default)
{
    // Build dynamic INSERT statement
    var columnNames = columnTypes.Keys.Select(SanitizeColumnName).ToList();
    var insertSql = $@"
        INSERT INTO TransportData ({string.Join(", ", columnNames)}, datetime_created)
        VALUES ({string.Join(", ", columnNames.Select(c => "@" + c))}, GETUTCDATE())";
    
    // Execute in batches
    foreach (var batch in rows.Chunk(100))
    {
        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var row in batch)
            {
                // Convert values to appropriate types
                var parameters = new Dictionary<string, object>();
                foreach (var column in columnTypes)
                {
                    var value = row.GetValueOrDefault(column.Key, null);
                    parameters[column.Key] = ConvertValue(value, column.Value.DataType);
                }
                
                // Execute INSERT with parameters
                await ExecuteDynamicInsertAsync(insertSql, parameters, cancellationToken);
            }
            
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
```

## Error Handling Flow

1. **CSV Parsing**: If CSV cannot be parsed, entire file moves to error folder
2. **Table Creation**: If table creation fails, log error and fail processing
3. **Row Processing**: Each row processed individually
   - Type validation failure → Row saved to error folder
   - Database insert failure → Row saved to error folder
   - Other errors → Row saved to error folder
4. **Successful Rows**: Inserted in batches for performance
5. **Original File**: Moved to `csv-processed` if any rows succeeded, or `csv-error` if all failed

## Column Management

### Adding Columns
- When CSV has new column → Automatically added to table
- Column type determined by analyzing all values
- Existing data unaffected (NULL for new column)

### Removing Columns
- When CSV no longer has column → Column remains in table
- Column not populated for new rows
- Existing data preserved

### Type Changes
- If value doesn't match column type → Row fails validation
- Row saved to error folder with error message
- Table structure unchanged (requires manual migration)

## Testing Checklist

- [ ] CSV with new columns → Columns added automatically
- [ ] CSV with removed columns → Processing continues
- [ ] Type mismatch → Row saved to error folder
- [ ] Multiple failed rows → Each saved separately
- [ ] Mixed success/failure → Successful rows inserted, failed rows in error folder
- [ ] Table creation → Works on first run
- [ ] Column addition → Works on subsequent runs

## Reprocessing

See `REPROCESSING_IDEAS.md` for ideas on how to reprocess failed rows after fixing issues.


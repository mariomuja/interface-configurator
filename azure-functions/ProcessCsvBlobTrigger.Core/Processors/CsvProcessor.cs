using System.Text;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Helpers;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Core.Services;

namespace ProcessCsvBlobTrigger.Core.Processors;

/// <summary>
/// CSV Processor with row-by-row processing, type validation, and dynamic table management
/// </summary>
public class CsvProcessor : ICsvProcessor
{
    private readonly ICsvProcessingService _csvProcessingService;
    private readonly IDataService _dataService;
    private readonly IDynamicTableService _dynamicTableService;
    private readonly IErrorRowService _errorRowService;
    private readonly ILoggingService _loggingService;
    private readonly ILogger<CsvProcessor> _logger;
    private readonly CsvColumnAnalyzer _columnAnalyzer;
    private readonly TypeValidator _typeValidator;

    public CsvProcessor(
        ICsvProcessingService csvProcessingService,
        IDataService dataService,
        IDynamicTableService dynamicTableService,
        IErrorRowService errorRowService,
        ILoggingService loggingService,
        ILogger<CsvProcessor> logger)
    {
        _csvProcessingService = csvProcessingService;
        _dataService = dataService;
        _dynamicTableService = dynamicTableService;
        _errorRowService = errorRowService;
        _loggingService = loggingService;
        _logger = logger;
        _columnAnalyzer = new CsvColumnAnalyzer();
        _typeValidator = new TypeValidator();
    }

    public async Task<ProcessingResult> ProcessCsvAsync(byte[] blobContent, string blobName, CancellationToken cancellationToken = default)
    {
        if (blobContent == null)
        {
            var error = "Blob content is null";
            _logger.LogError(error);
            await SafeLogAsync("error", "CSV processing failed", $"File: {blobName ?? "Unknown"}, Error: {error}", cancellationToken);
            return ProcessingResult.Failure(error, new ArgumentNullException(nameof(blobContent)));
        }

        var safeBlobName = blobName ?? "Unknown";
        var failedRows = new List<RowProcessingResult>();

        try
        {
            _logger.LogInformation("Processing CSV blob: {BlobName} ({BlobSize} bytes)", safeBlobName, blobContent.Length);

            // Ensure database exists
            await SafeLogAsync("info", "Initializing database schema", "Checking and creating tables if needed", cancellationToken);
            await _dataService.EnsureDatabaseCreatedAsync(cancellationToken);

            // Parse CSV
            string csvContent = Encoding.UTF8.GetString(blobContent);
            var (headers, records) = await _csvProcessingService.ParseCsvWithHeadersAsync(csvContent, cancellationToken);

            if (headers == null || headers.Count == 0)
            {
                await SafeLogAsync("warning", "CSV file has no headers", $"File: {safeBlobName}", cancellationToken);
                return ProcessingResult.Failure("CSV file has no headers", null);
            }

            if (records == null || records.Count == 0)
            {
                await SafeLogAsync("warning", "CSV file is empty or has no data rows", $"File: {safeBlobName}", cancellationToken);
                return ProcessingResult.SuccessResult(0, 0);
            }

            // Filter out reserved columns from CSV (case-insensitive):
            // - 'id' column: PrimaryKey is handled separately
            // - 'CsvDataJson': Old JSON column approach, not used anymore
            var reservedColumns = new[] { "id", "CsvDataJson", "PrimaryKey", "datetime_created" };
            var filteredHeaders = headers
                .Where(h => !reservedColumns.Any(rc => string.Equals(h, rc, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var filteredRecords = records.Select(r => r
                .Where(kvp => !reservedColumns.Any(rc => string.Equals(kvp.Key, rc, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
                .ToList();

            // Analyze column types from all rows (using filtered headers)
            await SafeLogAsync("info", "Analyzing CSV column types", $"File: {safeBlobName}, Columns: {filteredHeaders.Count}", cancellationToken);
            var columnTypes = AnalyzeColumnTypes(filteredHeaders, filteredRecords);

            // Ensure table structure matches CSV columns
            await SafeLogAsync("info", "Ensuring table structure", $"File: {safeBlobName}", cancellationToken);
            await _dynamicTableService.EnsureTableStructureAsync(columnTypes, cancellationToken);

            // Process each row individually (using filtered records)
            var successfulRows = new List<Dictionary<string, string>>();

            for (int i = 0; i < filteredRecords.Count; i++)
            {
                var row = filteredRecords[i];
                var rowNumber = i + 2; // +2 because row 1 is header, rows start at 2

                var result = await ProcessRowAsync(row, columnTypes, rowNumber, cancellationToken);

                if (result.Success)
                {
                    successfulRows.Add(row);
                }
                else
                {
                    failedRows.Add(result);
                    _logger.LogWarning("Row {RowNumber} failed: {Error}", rowNumber, result.ErrorMessage);

                    // Save failed row to error folder
                    try
                    {
                        await _errorRowService.SaveFailedRowAsync(safeBlobName, row, result, rowNumber, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save error row {RowNumber} to error folder", rowNumber);
                    }
                }
            }

            // Insert successful rows in batches
            if (successfulRows.Count > 0)
            {
                await SafeLogAsync("info", "Inserting successful rows", $"File: {safeBlobName}, Successful: {successfulRows.Count}", cancellationToken);
                await _dataService.InsertRowsAsync(successfulRows, columnTypes, cancellationToken);
            }

            var totalProcessed = successfulRows.Count;
            var totalFailed = failedRows.Count;

            await SafeLogAsync("info", "CSV processing completed",
                $"File: {safeBlobName}, Processed: {totalProcessed}, Failed: {totalFailed}", cancellationToken);

            _logger.LogInformation("Processed {Processed} rows successfully, {Failed} rows failed from {BlobName}",
                totalProcessed, totalFailed, safeBlobName);

            return ProcessingResult.SuccessResult(totalProcessed, 1, failedRows);
        }
        catch (Exception ex)
        {
            var errorMessage = ex?.Message ?? "Unknown error";
            var exceptionDetails = ExceptionHelper.FormatException(ex, includeStackTrace: true);

            _logger.LogError(ex, "Error processing CSV {BlobName}: {ErrorMessage}", safeBlobName, errorMessage);
            _logger.LogError("CSV processing error - Full exception details:\n{ExceptionDetails}", exceptionDetails);

            await SafeLogAsync("error", "CSV processing failed",
                $"File: {safeBlobName}, Error: {errorMessage}\n\nFull Details:\n{exceptionDetails}", cancellationToken);

            return ProcessingResult.Failure(errorMessage, ex);
        }
    }

    private Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> AnalyzeColumnTypes(List<string> headers, List<Dictionary<string, string>> records)
    {
        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        foreach (var header in headers)
        {
            // Get all values for this column
            var values = records
                .Select(r => r.GetValueOrDefault(header, string.Empty))
                .ToList();

            // Analyze column type
            var typeInfo = _columnAnalyzer.AnalyzeColumn(header, values);
            columnTypes[header] = typeInfo;

            _logger.LogDebug("Column '{Header}' analyzed as {DataType} ({SqlType})",
                header, typeInfo.DataType, typeInfo.SqlTypeDefinition);
        }

        return columnTypes;
    }

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

                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (!_typeValidator.ValidateValueType(value, expectedType))
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

    private async Task SafeLogAsync(string level, string message, string? details = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _loggingService.LogAsync(level, message, details, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log to database: {Message}", message);
        }
    }
}


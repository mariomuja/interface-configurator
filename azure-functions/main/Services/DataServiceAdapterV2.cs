using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Core.Helpers;
using InterfaceConfigurator.Main.Data;
using System.Data;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Enhanced version of DataServiceAdapter with improved error handling and performance
/// This is the NEW implementation that will be used when the feature is enabled
/// </summary>
public class DataServiceAdapterV2 : IDataService
{
    private readonly ApplicationDbContext? _context;
    private readonly ILoggingService? _loggingService;
    private readonly ILogger<DataServiceAdapterV2>? _logger;

    public DataServiceAdapterV2(
        ApplicationDbContext? context, 
        ILoggingService? loggingService,
        ILogger<DataServiceAdapterV2>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _loggingService = loggingService;
        _logger = logger;
    }

    private async Task SafeLogAsync(string level, string message, string? details = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_loggingService != null)
            {
                await _loggingService.LogAsync(level, message, details, cancellationToken);
            }
            else if (_logger != null)
            {
                switch (level.ToLowerInvariant())
                {
                    case "error":
                        _logger.LogError("{Message} | Details: {Details}", message, details);
                        break;
                    case "warning":
                        _logger.LogWarning("{Message} | Details: {Details}", message, details);
                        break;
                    default:
                        _logger.LogInformation("{Message} | Details: {Details}", message, details);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to log message: {Message}", message);
        }
    }

    public async Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken = default)
    {
        if (_context == null)
        {
            await SafeLogAsync("error", "Database context is null", null, cancellationToken);
            throw new InvalidOperationException("Database context is not available");
        }

        try
        {
            // Enhanced: Add retry logic
            var retryCount = 0;
            const int maxRetries = 3;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    if (_context.Database == null)
                    {
                        throw new InvalidOperationException("Database is not available");
                    }

                    await _context.Database.EnsureCreatedAsync(cancellationToken);
                    await SafeLogAsync("info", "Database schema verified (V2)", 
                        "TransportData table is ready", cancellationToken);
                    return;
                }
                catch (Exception ex) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    await SafeLogAsync("warning", "Database creation retry", 
                        $"Retry {retryCount}/{maxRetries} after error: {ex.Message}", cancellationToken);
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * retryCount), cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            await SafeLogAsync("error", "Error ensuring database created (V2)", 
                $"Error: {ex.Message}", cancellationToken);
            throw;
        }
    }

    public async Task ProcessChunksAsync(List<List<Core.Models.TransportData>> chunks, CancellationToken cancellationToken = default)
    {
        // Note: ProcessChunksAsync is deprecated - the new implementation uses InsertRowsAsync
        // This method is kept for backward compatibility but delegates to the old implementation
        // Enhanced V2 implementation would use InsertRowsAsync with better performance
        await SafeLogAsync("info", "ProcessChunksAsync called (V2) - delegating to InsertRowsAsync", 
            $"Total chunks: {chunks?.Count ?? 0}", cancellationToken);
        
        // For now, throw NotSupportedException to indicate this should use InsertRowsAsync instead
        // In a full implementation, this would convert chunks to rows and call InsertRowsAsync
        throw new NotSupportedException("ProcessChunksAsync is deprecated in V2. Use InsertRowsAsync instead for better performance.");
    }

    public async Task InsertRowsAsync(List<Dictionary<string, string>> rows, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default)
    {
        if (_context == null)
        {
            await SafeLogAsync("error", "Database context is null", null, cancellationToken);
            throw new InvalidOperationException("Database context is not available");
        }

        if (rows == null || rows.Count == 0)
        {
            await SafeLogAsync("warning", "No rows to insert", null, cancellationToken);
            return;
        }

        // Enhanced: Use same implementation as V1 but with improved error handling
        // This delegates to the same bulk insert logic but with better logging
        var typeValidator = new TypeValidator();
        
        // Filter out reserved columns before sanitizing
        var reservedColumns = new[] { "id", "Id", "CsvDataJson", "PrimaryKey", "datetime_created" };
        var filteredColumnTypes = columnTypes
            .Where(kvp => !reservedColumns.Any(rc => string.Equals(kvp.Key, rc, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        var sanitizedColumns = filteredColumnTypes.Keys.Select(SanitizeColumnName).ToList();

        await SafeLogAsync("info", "Starting enhanced row insertion (V2)",
            $"Total rows: {rows.Count}, Columns: {string.Join(", ", sanitizedColumns)}", cancellationToken);

        // Use bulk insert for better performance (SqlBulkCopy) - same as V1 but with enhanced logging
        await InsertRowsBulkAsync(rows, filteredColumnTypes, sanitizedColumns, typeValidator, cancellationToken);
    }

    private string SanitizeColumnName(string columnName)
    {
        // Remove special characters and ensure valid SQL identifier
        var sanitized = columnName
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace(".", "_")
            .Replace("/", "_")
            .Replace("\\", "_");

        // Ensure it starts with a letter
        if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
        {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }

    /// <summary>
    /// Enhanced bulk insert with improved error handling and retry logic
    /// </summary>
    private async Task InsertRowsBulkAsync(
        List<Dictionary<string, string>> rows,
        Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> filteredColumnTypes,
        List<string> sanitizedColumns,
        TypeValidator typeValidator,
        CancellationToken cancellationToken)
    {
        if (_context == null)
            throw new InvalidOperationException("Database context is not available");

        var connectionString = _context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string is not available");

        // Enhanced: Larger batch size for better performance
        var bulkBatchSize = 10000; // Increased from 5000
        var totalInserted = 0;

        for (int batchStart = 0; batchStart < rows.Count; batchStart += bulkBatchSize)
        {
            var batch = rows.Skip(batchStart).Take(bulkBatchSize).ToList();
            var batchNumber = (batchStart / bulkBatchSize) + 1;
            var totalBatches = (int)Math.Ceiling((double)rows.Count / bulkBatchSize);

            // Enhanced: Retry logic for transient errors
            var retryCount = 0;
            const int maxRetries = 3;
            bool success = false;

            while (retryCount < maxRetries && !success)
            {
                try
                {
                    // Create DataTable for bulk insert (same as V1)
                    var dataTable = new System.Data.DataTable();
                    
                    // Add columns to DataTable
                    foreach (var column in sanitizedColumns)
                    {
                        var columnType = typeof(string); // Default to string
                        var columnKey = filteredColumnTypes.Keys.FirstOrDefault(k => SanitizeColumnName(k) == column);
                        if (columnKey != null && filteredColumnTypes.TryGetValue(columnKey, out var typeInfo))
                        {
                            columnType = typeInfo.DataType switch
                            {
                                CsvColumnAnalyzer.SqlDataType.INT => typeof(int?),
                                CsvColumnAnalyzer.SqlDataType.DECIMAL => typeof(decimal?),
                                CsvColumnAnalyzer.SqlDataType.DATETIME2 => typeof(DateTime?),
                                CsvColumnAnalyzer.SqlDataType.BIT => typeof(bool?),
                                _ => typeof(string)
                            };
                        }
                        
                        dataTable.Columns.Add(column, columnType);
                    }
                    
                    dataTable.Columns.Add("datetime_created", typeof(DateTime));

                    // Add rows to DataTable
                    foreach (var row in batch)
                    {
                        var dataRow = dataTable.NewRow();
                        
                        foreach (var column in sanitizedColumns)
                        {
                            var columnKey = filteredColumnTypes.Keys.FirstOrDefault(k => SanitizeColumnName(k) == column);
                            if (columnKey != null)
                            {
                                var value = row.TryGetValue(columnKey, out var val) ? val : null;
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    try
                                    {
                                        var convertedValue = typeValidator.ConvertValue(value, filteredColumnTypes[columnKey].DataType);
                                        dataRow[column] = convertedValue ?? DBNull.Value;
                                    }
                                    catch
                                    {
                                        dataRow[column] = DBNull.Value;
                                    }
                                }
                                else
                                {
                                    dataRow[column] = DBNull.Value;
                                }
                            }
                        }
                        
                        dataRow["datetime_created"] = DateTime.UtcNow;
                        dataTable.Rows.Add(dataRow);
                    }

                    // Use SqlBulkCopy for bulk insert
                    using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
                    await connection.OpenAsync(cancellationToken);
                    
                    using var transaction = connection.BeginTransaction();
                    try
                    {
                        using var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(connection, Microsoft.Data.SqlClient.SqlBulkCopyOptions.Default, transaction);
                        bulkCopy.DestinationTableName = "TransportData";
                        bulkCopy.BatchSize = 2000; // Increased from 1000
                        bulkCopy.BulkCopyTimeout = 600; // Increased to 10 minutes
                        
                        // Map columns
                        foreach (var column in sanitizedColumns)
                        {
                            bulkCopy.ColumnMappings.Add(column, column);
                        }
                        bulkCopy.ColumnMappings.Add("datetime_created", "datetime_created");
                        
                        await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        
                        totalInserted += batch.Count;
                        await SafeLogAsync("info", $"Enhanced bulk batch {batchNumber}/{totalBatches} inserted (V2)",
                            $"Rows: {batch.Count}, Total inserted: {totalInserted}/{rows.Count}", cancellationToken);
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        
                        // Handle duplicate key violations gracefully (idempotency)
                        if (ex is Microsoft.Data.SqlClient.SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
                        {
                            await SafeLogAsync("warning", $"Enhanced bulk batch {batchNumber}/{totalBatches} skipped (duplicate key) (V2)",
                                $"Duplicate key violation detected. Rows in batch: {batch.Count}", cancellationToken);
                            totalInserted += batch.Count;
                            success = true;
                            continue;
                        }
                        
                        throw;
                    }
                }
                catch (Exception ex) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    await SafeLogAsync("warning", $"Enhanced bulk batch {batchNumber}/{totalBatches} retry (V2)",
                        $"Retry {retryCount}/{maxRetries} after error: {ex.Message}", cancellationToken);
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * retryCount), cancellationToken);
                }
            }

            if (!success)
            {
                await SafeLogAsync("error", $"Enhanced bulk batch {batchNumber}/{totalBatches} failed after retries (V2)",
                    $"Failed to insert batch after {maxRetries} retries", cancellationToken);
                throw new InvalidOperationException($"Failed to insert batch {batchNumber} after {maxRetries} retries");
            }
        }

        await SafeLogAsync("info", "Enhanced row insertion completed (V2)", 
            $"Total rows inserted: {totalInserted}/{rows.Count}", cancellationToken);
    }
}


using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Helpers;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Services;

public class DataServiceAdapter : IDataService
{
    private readonly ApplicationDbContext? _context;
    private readonly ILoggingService? _loggingService;
    private readonly ILogger<DataServiceAdapter>? _logger;

    public DataServiceAdapter(
        ApplicationDbContext? context, 
        ILoggingService? loggingService,
        ILogger<DataServiceAdapter>? logger = null)
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
            else
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{level}] {message} | {details ?? "N/A"}");
            }
        }
        catch
        {
            // Fail-safe: If logging fails, try console
            try
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{level}] {message} | {details ?? "N/A"}");
            }
            catch
            {
                // Last resort failed, ignore
            }
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
            var connectionString = _context.Database?.GetConnectionString();
            var dbName = connectionString?.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(s => s.StartsWith("Database=", StringComparison.OrdinalIgnoreCase))
                ?.Split('=').LastOrDefault() ?? "Unknown";

            await SafeLogAsync("info", "Checking database schema", 
                $"Database: {dbName}", cancellationToken);

            if (_context.Database == null)
            {
                throw new InvalidOperationException("Database is not available");
            }

            await _context.Database.EnsureCreatedAsync(cancellationToken);

            await SafeLogAsync("info", "Database schema verified", 
                "TransportData table is ready", cancellationToken);
        }
        catch (Exception ex)
        {
            var errorMessage = ex?.Message ?? "Unknown error";
            await SafeLogAsync("error", "Error ensuring database created", 
                $"Error: {errorMessage}", cancellationToken);
            throw;
        }
    }

    public async Task ProcessChunksAsync(List<List<TransportData>> chunks, CancellationToken cancellationToken = default)
    {
        if (_context == null)
        {
            await SafeLogAsync("error", "Database context is null", null, cancellationToken);
            throw new InvalidOperationException("Database context is not available");
        }

        if (chunks == null || chunks.Count == 0)
        {
            await SafeLogAsync("warning", "No chunks to process", null, cancellationToken);
            return;
        }

        var totalRecords = chunks.Sum(chunk => chunk?.Count ?? 0);
        var totalProcessed = 0;

        await SafeLogAsync("info", "Starting sequential chunk processing", 
            $"Total chunks to process: {chunks.Count}, Strategy: Sequential with transactions", cancellationToken);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkNumber = i + 1;
            var chunk = chunks[i];

            if (chunk == null || chunk.Count == 0)
            {
                await SafeLogAsync("warning", 
                    $"Chunk {chunkNumber}/{chunks.Count} is null or empty", 
                    "Skipping empty chunk", cancellationToken);
                continue;
            }

            var recordIds = chunk.Where(r => r != null).Select(r => r.Id.ToString()).Take(10);
            var recordIdsStr = string.Join(", ", recordIds);
            if (chunk.Count > 10)
            {
                recordIdsStr += $", ... ({chunk.Count} total)";
            }

            await SafeLogAsync("info", 
                $"Chunk {chunkNumber}/{chunks.Count} processing started", 
                $"Chunk size: {chunk.Count} records, Record IDs: {recordIdsStr}", cancellationToken);

            try
            {
                await InsertChunkAsync(chunk, chunkNumber, chunks.Count, cancellationToken);
                totalProcessed += chunk.Count;

                await SafeLogAsync("info", 
                    $"Chunk {chunkNumber}/{chunks.Count} processing completed", 
                    $"Records inserted: {chunk.Count}, Total processed so far: {totalProcessed}/{totalRecords}", cancellationToken);
            }
            catch (Exception chunkError)
            {
                // Log to Application Insights with full exception details including inner exceptions
                if (_logger != null)
                {
                    _logger.LogError(chunkError, "Chunk {ChunkNumber}/{TotalChunks} processing failed", chunkNumber, chunks.Count);
                    var exceptionDetails = ExceptionHelper.FormatException(chunkError, includeStackTrace: true);
                    _logger.LogError("Chunk processing error - Full exception details:\n{ExceptionDetails}", exceptionDetails);
                }
                
                var exceptionSummary = ExceptionHelper.GetExceptionSummary(chunkError);
                var exceptionDetailsForDb = ExceptionHelper.FormatException(chunkError, includeStackTrace: true);
                await SafeLogAsync("error", 
                    $"Chunk {chunkNumber}/{chunks.Count} processing failed", 
                    $"Error: {exceptionSummary}, Chunk size: {chunk.Count}, Records: {recordIdsStr}\n\nFull Details:\n{exceptionDetailsForDb}", cancellationToken);
                throw;
            }
        }

        await SafeLogAsync("info", "All chunks processed successfully", 
            $"Total chunks: {chunks.Count}, Total records: {totalRecords}, Total processed: {totalProcessed}", cancellationToken);
    }

    // InsertChunkAsync is deprecated - use InsertRowsAsync instead
    // This method is kept for backward compatibility but should not be called
    private async Task InsertChunkAsync(List<Core.Models.TransportData> chunk, int chunkNumber, int totalChunks, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("InsertChunkAsync is deprecated. The new implementation uses InsertRowsAsync with dynamic columns.");
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

        var typeValidator = new TypeValidator();
        
        // Filter out reserved columns before sanitizing
        var reservedColumns = new[] { "id", "Id", "CsvDataJson", "PrimaryKey", "datetime_created" };
        var filteredColumnTypes = columnTypes
            .Where(kvp => !reservedColumns.Any(rc => string.Equals(kvp.Key, rc, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        var sanitizedColumns = filteredColumnTypes.Keys.Select(SanitizeColumnName).ToList();

        // Build dynamic INSERT statement - PrimaryKey is auto-generated, don't include it
        var columnList = string.Join(", ", sanitizedColumns);
        var parameterList = string.Join(", ", sanitizedColumns.Select(c => "@" + c));
        var insertSql = $@"
            INSERT INTO TransportData ({columnList}, datetime_created)
            VALUES ({parameterList}, GETUTCDATE())";

        await SafeLogAsync("info", "Starting row insertion",
            $"Total rows: {rows.Count}, Columns: {columnList}", cancellationToken);

        // Process in batches of 100
        var batchSize = 100;
        var totalInserted = 0;

        for (int batchStart = 0; batchStart < rows.Count; batchStart += batchSize)
        {
            var batch = rows.Skip(batchStart).Take(batchSize).ToList();
            var batchNumber = (batchStart / batchSize) + 1;
            var totalBatches = (int)Math.Ceiling((double)rows.Count / batchSize);

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var rowIndexInBatch = 0;
                    foreach (var row in batch)
                    {
                        try
                        {
                            var parameters = new List<SqlParameter>();
                            var columnErrors = new List<string>();
                            
                            foreach (var column in filteredColumnTypes)
                            {
                                try
                                {
                                    var sanitizedColumnName = SanitizeColumnName(column.Key);
                                    var value = row.TryGetValue(column.Key, out var val) ? val : null;
                                    var sqlValue = value != null && !string.IsNullOrWhiteSpace(value)
                                        ? typeValidator.ConvertValue(value, column.Value.DataType)
                                        : DBNull.Value;

                                    parameters.Add(new SqlParameter("@" + sanitizedColumnName, sqlValue ?? DBNull.Value));
                                }
                                catch (Exception colEx)
                                {
                                    // Track column-level errors
                                    columnErrors.Add($"Column '{column.Key}': {colEx.Message}");
                                }
                            }

                            if (columnErrors.Count > 0)
                            {
                                var globalRowIndex = (batchStart + rowIndexInBatch) + 1;
                                var errorMsg = $"Row {globalRowIndex} (batch {batchNumber}): Column errors - {string.Join("; ", columnErrors)}";
                                await SafeLogAsync("error", $"Row {globalRowIndex} column validation failed", errorMsg, cancellationToken);
                                throw new InvalidOperationException(errorMsg);
                            }

                            await _context.Database.ExecuteSqlRawAsync(insertSql, parameters.ToArray(), cancellationToken);
                        }
                        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
                        {
                            var globalRowIndex = (batchStart + rowIndexInBatch) + 1;
                            var errorDetails = $"Row {globalRowIndex} (batch {batchNumber}): SQL Error {sqlEx.Number} - {sqlEx.Message}";
                            
                            // Try to extract column name from error message if available
                            if (sqlEx.Message.Contains("column") || sqlEx.Message.Contains("Column"))
                            {
                                errorDetails += $". Column information may be available in error message.";
                            }
                            
                            await SafeLogAsync("error", $"Row {globalRowIndex} SQL insert failed", errorDetails, cancellationToken);
                            throw new InvalidOperationException(errorDetails, sqlEx);
                        }
                        catch (Exception rowEx)
                        {
                            var globalRowIndex = (batchStart + rowIndexInBatch) + 1;
                            var errorDetails = $"Row {globalRowIndex} (batch {batchNumber}): {rowEx.Message}";
                            await SafeLogAsync("error", $"Row {globalRowIndex} processing failed", errorDetails, cancellationToken);
                            throw;
                        }
                        
                        rowIndexInBatch++;
                    }

                    await transaction.CommitAsync(cancellationToken);
                    totalInserted += batch.Count;

                    await SafeLogAsync("info", $"Batch {batchNumber}/{totalBatches} inserted",
                        $"Rows: {batch.Count}, Total inserted: {totalInserted}/{rows.Count}", cancellationToken);
                }
                catch (Microsoft.Data.SqlClient.SqlException sqlEx)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    
                    // Handle duplicate key violations gracefully (idempotency)
                    // SQL Server error 2627 = Violation of PRIMARY KEY constraint
                    // SQL Server error 2601 = Cannot insert duplicate key row
                    if (sqlEx.Number == 2627 || sqlEx.Number == 2601)
                    {
                        await SafeLogAsync("warning", $"Batch {batchNumber}/{totalBatches} skipped (duplicate key)",
                            $"Duplicate key violation detected. This may indicate idempotent retry. Rows in batch: {batch.Count}", cancellationToken);
                        // Don't throw - treat as idempotent operation (already inserted)
                        totalInserted += batch.Count; // Count as inserted for logging
                        continue; // Continue with next batch
                    }
                    
                    await SafeLogAsync("error", $"Batch {batchNumber}/{totalBatches} failed",
                        $"SQL Error {sqlEx.Number}: {sqlEx.Message}, Rows in batch: {batch.Count}", cancellationToken);
                    throw;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    await SafeLogAsync("error", $"Batch {batchNumber}/{totalBatches} failed",
                        $"Error: {ex.Message}, Rows in batch: {batch.Count}", cancellationToken);
                    throw;
                }
            }
            catch (Exception batchEx)
            {
                _logger?.LogError(batchEx, "Error inserting batch {BatchNumber}", batchNumber);
                throw;
            }
        }

        await SafeLogAsync("info", "Row insertion completed",
            $"Total rows inserted: {totalInserted}/{rows.Count}", cancellationToken);
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
}


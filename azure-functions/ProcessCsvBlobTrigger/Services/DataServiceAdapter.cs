using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Data;

namespace ProcessCsvBlobTrigger.Services;

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
                "TransportData and ProcessLogs tables are ready", cancellationToken);
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
                var errorMessage = chunkError?.Message ?? "Unknown error";
                await SafeLogAsync("error", 
                    $"Chunk {chunkNumber}/{chunks.Count} processing failed", 
                    $"Error: {errorMessage}, Chunk size: {chunk.Count}, Records: {recordIdsStr}", cancellationToken);
                throw;
            }
        }

        await SafeLogAsync("info", "All chunks processed successfully", 
            $"Total chunks: {chunks.Count}, Total records: {totalRecords}, Total processed: {totalProcessed}", cancellationToken);
    }

    private async Task InsertChunkAsync(List<TransportData> chunk, int chunkNumber, int totalChunks, CancellationToken cancellationToken)
    {
        if (_context == null)
        {
            await SafeLogAsync("error", "Database context is null", null, cancellationToken);
            throw new InvalidOperationException("Database context is not available");
        }

        if (chunk == null || chunk.Count == 0)
        {
            await SafeLogAsync("warning", 
                $"Chunk {chunkNumber}/{totalChunks} is null or empty", 
                "Skipping empty chunk", cancellationToken);
            return;
        }

        try
        {
            await SafeLogAsync("info", 
                $"Chunk {chunkNumber}/{totalChunks} - Starting database transaction", 
                $"Records: {chunk.Count}", cancellationToken);

            if (_context.Database == null)
            {
                throw new InvalidOperationException("Database is not available");
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Convert Core models to Function App models with null-safety
                var transportDataEntities = chunk
                    .Where(td => td != null)
                    .Select(td => new Models.TransportData
                    {
                        Id = td.Id,
                        Name = td.Name ?? string.Empty,
                        Email = td.Email ?? string.Empty,
                        Age = td.Age,
                        City = td.City ?? string.Empty,
                        Salary = td.Salary,
                        CreatedAt = td.CreatedAt
                    })
                    .ToList();

                if (transportDataEntities.Count == 0)
                {
                    await SafeLogAsync("warning", 
                        $"Chunk {chunkNumber}/{totalChunks} - No valid records to insert", 
                        "All records were null", cancellationToken);
                    await transaction.RollbackAsync(cancellationToken);
                    return;
                }

                _context.TransportData.AddRange(transportDataEntities);
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                await SafeLogAsync("info", 
                    $"Chunk {chunkNumber}/{totalChunks} - Transaction committed", 
                    $"Records inserted: {transportDataEntities.Count}, Transaction status: Committed", cancellationToken);
            }
            catch (Exception ex)
            {
                var errorMessage = ex?.Message ?? "Unknown error";
                await SafeLogAsync("error", 
                    $"Chunk {chunkNumber}/{totalChunks} - Transaction error occurred", 
                    $"Error: {errorMessage}, Attempting rollback", cancellationToken);

                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                    await SafeLogAsync("error", 
                        $"Chunk {chunkNumber}/{totalChunks} - Transaction rolled back", 
                        $"Error: {errorMessage}, Records in chunk: {chunk.Count}", cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    await SafeLogAsync("error", 
                        $"Chunk {chunkNumber}/{totalChunks} - Rollback failed", 
                        $"Rollback error: {rollbackEx?.Message ?? "Unknown"}", cancellationToken);
                }

                throw;
            }
        }
        catch (Exception ex)
        {
            var errorMessage = ex?.Message ?? "Unknown error";
            var stackTrace = ex?.StackTrace;
            var stackTraceStr = "N/A";
            
            if (!string.IsNullOrWhiteSpace(stackTrace))
            {
                stackTraceStr = stackTrace.Length > 500 
                    ? stackTrace.Substring(0, 500) + "... [truncated]" 
                    : stackTrace;
            }

            await SafeLogAsync("error", 
                $"Error processing chunk {chunkNumber}/{totalChunks}", 
                $"Error: {errorMessage}, Stack: {stackTraceStr}", cancellationToken);
            throw;
        }
    }
}


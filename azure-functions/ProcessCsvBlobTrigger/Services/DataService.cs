using Microsoft.EntityFrameworkCore;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Models;

namespace ProcessCsvBlobTrigger.Services;

public class DataService
{
    private readonly ApplicationDbContext _context;
    private readonly LoggingService _loggingService;

    public DataService(ApplicationDbContext context, LoggingService loggingService)
    {
        _context = context;
        _loggingService = loggingService;
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        try
        {
            await _loggingService.LogAsync("info", "Checking database schema", 
                $"Database: {_context.Database.GetConnectionString()?.Split(';').FirstOrDefault()}");

            await _context.Database.EnsureCreatedAsync();

            await _loggingService.LogAsync("info", "Database schema verified", 
                "TransportData and ProcessLogs tables are ready");
        }
        catch (Exception ex)
        {
            await _loggingService.LogAsync("error", "Error ensuring database created", 
                $"Error: {ex.Message}");
            throw;
        }
    }

    public async Task InsertChunkAsync(List<TransportData> chunk, int chunkNumber, int totalChunks)
    {
        try
        {
            await _loggingService.LogAsync("info", 
                $"Chunk {chunkNumber}/{totalChunks} - Starting database transaction", 
                $"Records: {chunk.Count}");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Bulk insert using AddRange (EF Core optimizes this)
                _context.TransportData.AddRange(chunk);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                await _loggingService.LogAsync("info", 
                    $"Chunk {chunkNumber}/{totalChunks} - Transaction committed", 
                    $"Records inserted: {chunk.Count}, Transaction status: Committed");
            }
            catch (Exception ex)
            {
                await _loggingService.LogAsync("error", 
                    $"Chunk {chunkNumber}/{totalChunks} - Transaction error occurred", 
                    $"Error: {ex.Message}, Attempting rollback");

                await transaction.RollbackAsync();

                await _loggingService.LogAsync("error", 
                    $"Chunk {chunkNumber}/{totalChunks} - Transaction rolled back", 
                    $"Error: {ex.Message}, Records in chunk: {chunk.Count}");

                throw;
            }
        }
        catch (Exception ex)
        {
            await _loggingService.LogAsync("error", 
                $"Error processing chunk {chunkNumber}/{totalChunks}", 
                $"Error: {ex.Message}, Stack: {ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace.Length ?? 0)) ?? "N/A"}");
            throw;
        }
    }

    public async Task ProcessChunksAsync(List<List<TransportData>> chunks)
    {
        var totalRecords = chunks.Sum(chunk => chunk.Count);
        var totalProcessed = 0;

        await _loggingService.LogAsync("info", "Starting sequential chunk processing", 
            $"Total chunks to process: {chunks.Count}, Strategy: Sequential with transactions");

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunkNumber = i + 1;
            var chunk = chunks[i];

            await _loggingService.LogAsync("info", 
                $"Chunk {chunkNumber}/{chunks.Count} processing started", 
                $"Chunk size: {chunk.Count} records, Record IDs: {string.Join(", ", chunk.Select(r => r.Id))}");

            try
            {
                await InsertChunkAsync(chunk, chunkNumber, chunks.Count);
                totalProcessed += chunk.Count;

                await _loggingService.LogAsync("info", 
                    $"Chunk {chunkNumber}/{chunks.Count} processing completed", 
                    $"Records inserted: {chunk.Count}, Total processed so far: {totalProcessed}/{totalRecords}");
            }
            catch (Exception chunkError)
            {
                await _loggingService.LogAsync("error", 
                    $"Chunk {chunkNumber}/{chunks.Count} processing failed", 
                    $"Error: {chunkError.Message}, Chunk size: {chunk.Count}, Records: {string.Join(", ", chunk.Select(r => r.Id))}");
                throw;
            }
        }

        await _loggingService.LogAsync("info", "All chunks processed successfully", 
            $"Total chunks: {chunks.Count}, Total records: {totalRecords}, Total processed: {totalProcessed}");
    }
}


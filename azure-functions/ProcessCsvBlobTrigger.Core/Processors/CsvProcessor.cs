using System.Text;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Core.Services;

namespace ProcessCsvBlobTrigger.Core.Processors;

public class CsvProcessor : ICsvProcessor
{
    private readonly ICsvProcessingService _csvProcessingService;
    private readonly IDataService _dataService;
    private readonly ILoggingService _loggingService;
    private readonly ILogger<CsvProcessor> _logger;

    public CsvProcessor(
        ICsvProcessingService csvProcessingService,
        IDataService dataService,
        ILoggingService loggingService,
        ILogger<CsvProcessor> logger)
    {
        _csvProcessingService = csvProcessingService;
        _dataService = dataService;
        _loggingService = loggingService;
        _logger = logger;
    }

    public async Task<ProcessingResult> ProcessCsvAsync(byte[] blobContent, string blobName, CancellationToken cancellationToken = default)
    {
        var blobSize = blobContent.Length;
        _logger.LogInformation("Processing CSV blob: {BlobName} ({BlobSize} bytes)", blobName, blobSize);

        try
        {
            // Initialize database tables on first run
            await _loggingService.LogAsync("info", "Initializing database schema",
                "Checking and creating tables if needed", cancellationToken);
            await _dataService.EnsureDatabaseCreatedAsync(cancellationToken);
            await _loggingService.LogAsync("info", "Database schema initialized",
                "TransportData and ProcessLogs tables ready", cancellationToken);

            // Read blob content
            await _loggingService.LogAsync("info", "Reading blob content from storage", 
                $"File: {blobName}", cancellationToken);
            var csvContent = Encoding.UTF8.GetString(blobContent);
            var contentLength = csvContent.Length;
            await _loggingService.LogAsync("info", "Blob content read successfully",
                $"File: {blobName}, Content length: {contentLength} characters", cancellationToken);

            // Parse CSV
            await _loggingService.LogAsync("info", "Starting CSV parsing", $"File: {blobName}", cancellationToken);
            var records = _csvProcessingService.ParseCsv(csvContent);

            if (records.Count == 0)
            {
                await _loggingService.LogAsync("warning", "CSV file is empty or invalid",
                    $"File: {blobName}, Content length: {contentLength} characters", cancellationToken);
                return new ProcessingResult
                {
                    Success = true,
                    RecordsProcessed = 0,
                    ChunksProcessed = 0
                };
            }

            // Create chunks
            await _loggingService.LogAsync("info", "CSV parsing completed",
                $"File: {blobName}, Records parsed: {records.Count}, Content length: {contentLength} characters", cancellationToken);
            await _loggingService.LogAsync("info", "Starting chunk creation",
                $"Total records: {records.Count}, Chunk size: 100", cancellationToken);

            var chunks = _csvProcessingService.CreateChunks(records);

            await _loggingService.LogAsync("info", "Chunk creation completed",
                $"Total chunks: {chunks.Count}, Chunk size: 100, Total records: {records.Count}", cancellationToken);

            // Process chunks
            await _dataService.ProcessChunksAsync(chunks, cancellationToken);

            await _loggingService.LogAsync("info", "CSV processing completed successfully",
                $"File: {blobName}, Total records: {records.Count}, Chunks processed: {chunks.Count}, Status: Success", cancellationToken);

            _logger.LogInformation("Successfully processed {RecordCount} records from {BlobName} in {ChunkCount} chunks",
                records.Count, blobName, chunks.Count);

            return new ProcessingResult
            {
                Success = true,
                RecordsProcessed = records.Count,
                ChunksProcessed = chunks.Count
            };
        }
        catch (Exception error)
        {
            var errorMessage = error.Message;
            var errorStack = error.StackTrace ?? string.Empty;

            await _loggingService.LogAsync("error", "CSV processing failed",
                $"File: {blobName}, Error: {errorMessage}, Stack: {errorStack.Substring(0, Math.Min(500, errorStack.Length))}", cancellationToken);

            _logger.LogError(error, "Error processing CSV {BlobName}: {ErrorMessage}", blobName, errorMessage);

            return new ProcessingResult
            {
                Success = false,
                RecordsProcessed = 0,
                ChunksProcessed = 0,
                ErrorMessage = errorMessage,
                Exception = error
            };
        }
    }
}


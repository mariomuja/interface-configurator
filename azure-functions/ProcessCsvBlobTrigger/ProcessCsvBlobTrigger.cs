using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Services;

namespace ProcessCsvBlobTrigger;

// GitHub Source Control Deployment Test - Function deployed directly from GitHub
public class ProcessCsvBlobTriggerFunction
{
    private readonly ApplicationDbContext _context;
    private readonly CsvProcessingService _csvProcessingService;
    private readonly DataService _dataService;
    private readonly LoggingService _loggingService;
    private readonly ILogger<ProcessCsvBlobTriggerFunction> _logger;

    public ProcessCsvBlobTriggerFunction(
        ApplicationDbContext context,
        CsvProcessingService csvProcessingService,
        DataService dataService,
        LoggingService loggingService,
        ILogger<ProcessCsvBlobTriggerFunction> logger)
    {
        _context = context;
        _csvProcessingService = csvProcessingService;
        _dataService = dataService;
        _loggingService = loggingService;
        _logger = logger;
    }

    [Function("ProcessCsvBlobTrigger")]
    public async Task Run(
        [BlobTrigger("csv-uploads/{name}")] byte[] blobContent,
        string name,
        FunctionContext context)
    {
        var blobSize = blobContent.Length;
        _logger.LogInformation("Blob trigger function processed blob: {BlobName} ({BlobSize} bytes)", name, blobSize);

        try
        {
            // Event: Function triggered
            await _loggingService.LogAsync("info", "Azure Function triggered by Blob Storage event",
                $"Blob: {name}, Size: {blobSize} bytes, Trigger: BlobCreated");

            // Initialize database tables on first run
            await _loggingService.LogAsync("info", "Initializing database schema",
                "Checking and creating tables if needed");
            await _dataService.EnsureDatabaseCreatedAsync();
            await _loggingService.LogAsync("info", "Database schema initialized",
                "TransportData and ProcessLogs tables ready");

            // Event: CSV file detected
            await _loggingService.LogAsync("info", "CSV file detected in blob storage",
                $"File: {name}, Container: csv-uploads, Size: {blobSize} bytes");

            // Event: Reading blob content
            await _loggingService.LogAsync("info", "Reading blob content from storage", $"File: {name}");
            var csvContent = Encoding.UTF8.GetString(blobContent);
            var contentLength = csvContent.Length;
            await _loggingService.LogAsync("info", "Blob content read successfully",
                $"File: {name}, Content length: {contentLength} characters");

            // Event: Parsing CSV
            await _loggingService.LogAsync("info", "Starting CSV parsing", $"File: {name}");
            var records = _csvProcessingService.ParseCsv(csvContent);

            if (records.Count == 0)
            {
                await _loggingService.LogAsync("warning", "CSV file is empty or invalid",
                    $"File: {name}, Content length: {contentLength} characters");
                return;
            }

            // Event: CSV parsed successfully
            await _loggingService.LogAsync("info", "CSV parsing completed",
                $"File: {name}, Records parsed: {records.Count}, Content length: {contentLength} characters");

            // Event: Starting chunk creation
            await _loggingService.LogAsync("info", "Starting chunk creation",
                $"Total records: {records.Count}, Chunk size: 100");

            // Create chunks using LINQ
            var chunks = _csvProcessingService.CreateChunks(records);

            // Event: Chunks created
            await _loggingService.LogAsync("info", "Chunk creation completed",
                $"Total chunks: {chunks.Count}, Chunk size: 100, Total records: {records.Count}");

            // Process chunks using LINQ and EF Core
            await _dataService.ProcessChunksAsync(chunks);

            // Event: Transport completed
            await _loggingService.LogAsync("info", "CSV processing completed successfully",
                $"File: {name}, Total records: {records.Count}, Chunks processed: {chunks.Count}, Status: Success");

            _logger.LogInformation("Successfully processed {RecordCount} records from {BlobName} in {ChunkCount} chunks",
                records.Count, name, chunks.Count);
        }
        catch (Exception error)
        {
            var errorMessage = error.Message;
            var errorStack = error.StackTrace ?? string.Empty;

            // Event: Transport failed
            await _loggingService.LogAsync("error", "CSV processing failed",
                $"File: {name}, Error: {errorMessage}, Stack: {errorStack.Substring(0, Math.Min(500, errorStack.Length))}");

            _logger.LogError(error, "Error processing CSV {BlobName}: {ErrorMessage}", name, errorMessage);
            throw;
        }
    }
}


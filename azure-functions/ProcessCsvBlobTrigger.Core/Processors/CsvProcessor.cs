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
        // Fail-safe: Validate inputs
        if (blobContent == null)
        {
            var nullError = "Blob content is null";
            _logger.LogError(nullError);
            try
            {
                await _loggingService.LogAsync("error", "CSV processing failed", 
                    $"File: {blobName ?? "Unknown"}, Error: {nullError}", cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log error to database: {ErrorMessage}", logEx.Message);
            }
            return ProcessingResult.Failure(nullError, new ArgumentNullException(nameof(blobContent)));
        }

        var blobSize = blobContent.Length;
        var safeBlobName = blobName ?? "Unknown";
        
        try
        {
            _logger.LogInformation("Processing CSV blob: {BlobName} ({BlobSize} bytes)", safeBlobName, blobSize);
        }
        catch (Exception logEx)
        {
            // Log to console as fallback if ILogger fails
            Console.Error.WriteLine($"Failed to log information: {logEx.Message}");
        }

        try
        {
            // Initialize database tables on first run
            try
            {
                await _loggingService.LogAsync("info", "Initializing database schema",
                    "Checking and creating tables if needed", cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log database initialization start: {ErrorMessage}", logEx.Message);
            }

            await _dataService.EnsureDatabaseCreatedAsync(cancellationToken);
            
            try
            {
                await _loggingService.LogAsync("info", "Database schema initialized",
                    "TransportData and ProcessLogs tables ready", cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log database initialization completion: {ErrorMessage}", logEx.Message);
            }

            // Read blob content
            try
            {
                await _loggingService.LogAsync("info", "Reading blob content from storage", 
                    $"File: {safeBlobName}", cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log blob read start: {ErrorMessage}", logEx.Message);
            }

            string csvContent;
            try
            {
                csvContent = Encoding.UTF8.GetString(blobContent);
            }
            catch (Exception ex)
            {
                var encodingError = $"Failed to decode blob content: {ex.Message}";
                _logger.LogError(ex, encodingError);
                try
                {
                    await _loggingService.LogAsync("error", "Blob content decoding failed",
                        $"File: {safeBlobName}, Error: {encodingError}", cancellationToken);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to log encoding error to database: {ErrorMessage}", logEx.Message);
                }
                return ProcessingResult.Failure(encodingError, ex);
            }

            var contentLength = csvContent?.Length ?? 0;
            
            try
            {
                await _loggingService.LogAsync("info", "Blob content read successfully",
                    $"File: {safeBlobName}, Content length: {contentLength} characters", cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log blob read success: {ErrorMessage}", logEx.Message);
            }

            // Parse CSV
            try
            {
                await _loggingService.LogAsync("info", "Starting CSV parsing", 
                    $"File: {safeBlobName}", cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log CSV parsing start: {ErrorMessage}", logEx.Message);
            }

            List<Dictionary<string, string>> records;
            try
            {
                records = _csvProcessingService.ParseCsv(csvContent ?? string.Empty);
            }
            catch (Exception ex)
            {
                var parseError = $"CSV parsing failed: {ex.Message}";
                _logger.LogError(ex, parseError);
                try
                {
                    await _loggingService.LogAsync("error", "CSV parsing failed",
                        $"File: {safeBlobName}, Error: {parseError}", cancellationToken);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to log parsing error to database: {ErrorMessage}", logEx.Message);
                }
                return ProcessingResult.Failure(parseError, ex);
            }

            if (records == null || records.Count == 0)
            {
                try
                {
                    await _loggingService.LogAsync("warning", "CSV file is empty or invalid",
                        $"File: {safeBlobName}, Content length: {contentLength} characters", cancellationToken);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to log empty CSV warning to database: {ErrorMessage}", logEx.Message);
                }
                return new ProcessingResult
                {
                    Success = true,
                    RecordsProcessed = 0,
                    ChunksProcessed = 0
                };
            }

            // Create chunks
            try
            {
                await _loggingService.LogAsync("info", "CSV parsing completed",
                    $"File: {safeBlobName}, Records parsed: {records.Count}, Content length: {contentLength} characters", cancellationToken);
                await _loggingService.LogAsync("info", "Starting chunk creation",
                    $"Total records: {records.Count}, Chunk size: 100", cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log chunk creation start: {ErrorMessage}", logEx.Message);
            }

            List<List<TransportData>> chunks;
            try
            {
                chunks = _csvProcessingService.CreateChunks(records);
            }
            catch (Exception ex)
            {
                var chunkError = $"Chunk creation failed: {ex.Message}";
                _logger.LogError(ex, chunkError);
                try
                {
                    await _loggingService.LogAsync("error", "Chunk creation failed",
                        $"File: {safeBlobName}, Error: {chunkError}", cancellationToken);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to log chunk creation error to database: {ErrorMessage}", logEx.Message);
                }
                return ProcessingResult.Failure(chunkError, ex);
            }

            if (chunks == null || chunks.Count == 0)
            {
                try
                {
                    await _loggingService.LogAsync("warning", "No chunks created",
                        $"File: {safeBlobName}, Records: {records.Count}", cancellationToken);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to log no chunks warning to database: {ErrorMessage}", logEx.Message);
                }
                return new ProcessingResult
                {
                    Success = true,
                    RecordsProcessed = records.Count,
                    ChunksProcessed = 0
                };
            }

            try
            {
                await _loggingService.LogAsync("info", "Chunk creation completed",
                    $"Total chunks: {chunks.Count}, Chunk size: 100, Total records: {records.Count}", cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log chunk creation completion: {ErrorMessage}", logEx.Message);
            }

            // Process chunks
            await _dataService.ProcessChunksAsync(chunks, cancellationToken);

            try
            {
                await _loggingService.LogAsync("info", "CSV processing completed successfully",
                    $"File: {safeBlobName}, Total records: {records.Count}, Chunks processed: {chunks.Count}, Status: Success", cancellationToken);
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log processing success to database: {ErrorMessage}", logEx.Message);
            }

            try
            {
                _logger.LogInformation("Successfully processed {RecordCount} records from {BlobName} in {ChunkCount} chunks",
                    records.Count, safeBlobName, chunks.Count);
            }
            catch (Exception logEx)
            {
                // Log to console as fallback if ILogger fails
                Console.Error.WriteLine($"Failed to log success information: {logEx.Message}");
            }

            return new ProcessingResult
            {
                Success = true,
                RecordsProcessed = records.Count,
                ChunksProcessed = chunks.Count
            };
        }
        catch (Exception error)
        {
            var errorMessage = error?.Message ?? "Unknown error";
            var errorStack = error?.StackTrace;
            var stackTraceStr = "N/A";

            if (!string.IsNullOrWhiteSpace(errorStack))
            {
                try
                {
                    stackTraceStr = errorStack.Length > 500 
                        ? errorStack.Substring(0, 500) + "... [truncated]" 
                        : errorStack;
                }
                catch
                {
                    stackTraceStr = "Error reading stack trace";
                }
            }

            try
            {
                await _loggingService.LogAsync("error", "CSV processing failed",
                    $"File: {safeBlobName}, Error: {errorMessage}, Stack: {stackTraceStr}", cancellationToken);
            }
            catch (Exception logEx)
            {
                // Log to ILogger as fallback if database logging fails
                _logger.LogWarning(logEx, "Failed to log processing error to database: {ErrorMessage}", logEx.Message);
            }

            try
            {
                _logger.LogError(error, "Error processing CSV {BlobName}: {ErrorMessage}", safeBlobName, errorMessage);
            }
            catch (Exception logEx)
            {
                // Log to console as final fallback if ILogger also fails
                Console.Error.WriteLine($"Failed to log error: {logEx.Message}");
                Console.Error.WriteLine($"Original error: {errorMessage}");
            }

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


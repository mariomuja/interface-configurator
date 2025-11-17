using ProcessCsvBlobTrigger.Core.Models;

namespace ProcessCsvBlobTrigger.Core.Interfaces;

public interface ICsvProcessor
{
    Task<ProcessingResult> ProcessCsvAsync(byte[] blobContent, string blobName, CancellationToken cancellationToken = default);
}

public class ProcessingResult
{
    public bool Success { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsFailed { get; set; }
    public int ChunksProcessed { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public List<RowProcessingResult> FailedRows { get; set; } = new();

    public static ProcessingResult Failure(string errorMessage, Exception? exception = null)
    {
        return new ProcessingResult
        {
            Success = false,
            RecordsProcessed = 0,
            RecordsFailed = 0,
            ChunksProcessed = 0,
            ErrorMessage = errorMessage,
            Exception = exception
        };
    }

    public static ProcessingResult SuccessResult(int recordsProcessed, int chunksProcessed, List<RowProcessingResult>? failedRows = null)
    {
        return new ProcessingResult
        {
            Success = true,
            RecordsProcessed = recordsProcessed,
            RecordsFailed = failedRows?.Count ?? 0,
            ChunksProcessed = chunksProcessed,
            FailedRows = failedRows ?? new List<RowProcessingResult>()
        };
    }
}


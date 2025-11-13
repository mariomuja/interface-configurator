namespace ProcessCsvBlobTrigger.Core.Interfaces;

public interface ICsvProcessor
{
    Task<ProcessingResult> ProcessCsvAsync(byte[] blobContent, string blobName, CancellationToken cancellationToken = default);
}

public class ProcessingResult
{
    public bool Success { get; set; }
    public int RecordsProcessed { get; set; }
    public int ChunksProcessed { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
}


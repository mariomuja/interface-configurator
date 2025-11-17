namespace ProcessCsvBlobTrigger.Core.Models;

/// <summary>
/// In-memory process log entry (no SQL dependency)
/// Logs have the same lifecycle as the Function App (lost on restart)
/// </summary>
public class ProcessLogEntry
{
    public int Id { get; set; }
    public DateTime datetime_created { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? Component { get; set; }
}


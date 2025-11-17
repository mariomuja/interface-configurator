using ProcessCsvBlobTrigger.Core.Models;

namespace ProcessCsvBlobTrigger.Core.Interfaces;

/// <summary>
/// Extended logging service interface for in-memory logging
/// </summary>
public interface IInMemoryLoggingService : ILoggingService
{
    /// <summary>
    /// Gets all logs from memory
    /// </summary>
    List<ProcessLogEntry> GetAllLogs();
    
    /// <summary>
    /// Clears all logs from memory
    /// </summary>
    void ClearLogs();
}


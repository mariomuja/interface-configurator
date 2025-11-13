using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Data;
using ProcessLog = ProcessCsvBlobTrigger.Models.ProcessLog;

namespace ProcessCsvBlobTrigger.Services;

public class LoggingServiceAdapter : ILoggingService
{
    private readonly ApplicationDbContext _context;

    public LoggingServiceAdapter(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string level, string message, string? details = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var log = new ProcessLog
            {
                Level = level,
                Message = message,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            _context.ProcessLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log to console if database logging fails
            Console.Error.WriteLine($"Error logging to database: {ex.Message}");
        }
    }
}


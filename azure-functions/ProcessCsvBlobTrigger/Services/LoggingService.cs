using Microsoft.EntityFrameworkCore;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Models;

namespace ProcessCsvBlobTrigger.Services;

public class LoggingService
{
    private readonly ApplicationDbContext _context;

    public LoggingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string level, string message, string? details = null)
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
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log to console if database logging fails
            Console.Error.WriteLine($"Error logging to database: {ex.Message}");
        }
    }

    public async Task LogBatchAsync(IEnumerable<(string Level, string Message, string? Details)> logs)
    {
        try
        {
            var logEntities = logs.Select(log => new ProcessLog
            {
                Level = log.Level,
                Message = log.Message,
                Details = log.Details,
                Timestamp = DateTime.UtcNow
            });

            _context.ProcessLogs.AddRange(logEntities);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error batch logging to database: {ex.Message}");
        }
    }
}


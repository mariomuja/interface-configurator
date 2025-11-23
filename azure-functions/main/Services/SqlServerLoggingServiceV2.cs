using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Models;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Enhanced version of SqlServerLoggingService with improved performance and batching
/// This is the NEW implementation that will be used when the feature is enabled
/// </summary>
public class SqlServerLoggingServiceV2 : ILoggingService
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<SqlServerLoggingServiceV2>? _logger;
    private readonly System.Collections.Concurrent.ConcurrentQueue<ProcessLog> _logQueue;
    private readonly System.Threading.Timer? _flushTimer;
    private const int BATCH_SIZE = 50;
    private const int FLUSH_INTERVAL_MS = 5000; // 5 seconds

    public SqlServerLoggingServiceV2(
        MessageBoxDbContext context,
        ILogger<SqlServerLoggingServiceV2>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
        _logQueue = new System.Collections.Concurrent.ConcurrentQueue<ProcessLog>();
        
        // Start background timer to flush logs periodically
        _flushTimer = new System.Threading.Timer(FlushLogs, null, FLUSH_INTERVAL_MS, FLUSH_INTERVAL_MS);
    }

    public async Task LogAsync(string level, string message, string? details = null, CancellationToken cancellationToken = default)
    {
        // Always log to console/ILogger first
        try
        {
            var logMessage = $"[{level}] {message}";
            if (!string.IsNullOrWhiteSpace(details))
            {
                logMessage += $" | Details: {details}";
            }

            if (_logger != null)
            {
                switch (level.ToLowerInvariant())
                {
                    case "error":
                        _logger.LogError(logMessage);
                        break;
                    case "warning":
                        _logger.LogWarning(logMessage);
                        break;
                    default:
                        _logger.LogInformation(logMessage);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            // If logging fails, at least try to write to console
            Console.WriteLine($"Logging error: {ex.Message}");
        }

        // Enhanced: Add to queue for batch processing instead of immediate write
        try
        {
            var logEntry = new ProcessLog
            {
                Level = level,
                Message = message,
                Details = details ?? string.Empty,
                datetime_created = DateTime.UtcNow
            };

            _logQueue.Enqueue(logEntry);

            // Flush immediately if queue is full
            if (_logQueue.Count >= BATCH_SIZE)
            {
                await FlushLogsAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error queuing log entry: {Message}", message);
        }
    }

    private void FlushLogs(object? state)
    {
        // Fire and forget - don't block timer thread
        _ = Task.Run(async () =>
        {
            try
            {
                await FlushLogsAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error flushing logs in background timer");
            }
        });
    }

    private async Task FlushLogsAsync(CancellationToken cancellationToken)
    {
        if (_logQueue.IsEmpty)
            return;

        var logsToSave = new List<ProcessLog>();
        
        // Dequeue up to BATCH_SIZE logs
        while (logsToSave.Count < BATCH_SIZE && _logQueue.TryDequeue(out var log))
        {
            logsToSave.Add(log);
        }

        if (logsToSave.Count == 0)
            return;

        try
        {
            // Enhanced: Use bulk insert for better performance
            await _context.ProcessLogs.AddRangeAsync(logsToSave, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger?.LogDebug("Flushed {Count} log entries to database", logsToSave.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving log entries to database. {Count} entries lost.", logsToSave.Count);
            
            // Re-queue failed logs (up to a limit to prevent memory issues)
            if (_logQueue.Count < 1000)
            {
                foreach (var log in logsToSave)
                {
                    _logQueue.Enqueue(log);
                }
            }
        }
    }

    /// <summary>
    /// Flushes all pending logs (call this on shutdown)
    /// </summary>
    public async Task FlushAllAsync(CancellationToken cancellationToken = default)
    {
        _flushTimer?.Dispose();
        await FlushLogsAsync(cancellationToken);
    }
}


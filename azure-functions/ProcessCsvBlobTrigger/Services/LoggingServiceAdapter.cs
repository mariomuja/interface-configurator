using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Data;
using ProcessLog = ProcessCsvBlobTrigger.Models.ProcessLog;

namespace ProcessCsvBlobTrigger.Services;

public class LoggingServiceAdapter : ILoggingService
{
    private readonly ApplicationDbContext? _context;
    private readonly ILogger<LoggingServiceAdapter>? _logger;
    private static readonly SemaphoreSlim _logSemaphore = new SemaphoreSlim(1, 1);
    private static readonly TimeSpan _logTimeout = TimeSpan.FromSeconds(5);

    public LoggingServiceAdapter(ApplicationDbContext? context, ILogger<LoggingServiceAdapter>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(string level, string message, string? details = null, CancellationToken cancellationToken = default)
    {
        // Fail-safe: Validate inputs
        if (string.IsNullOrWhiteSpace(level))
        {
            level = "unknown";
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "No message provided";
        }

        // Always log to console/ILogger first (fail-safe)
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
            else
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {logMessage}");
            }
        }
        catch
        {
            // Ignore console logging errors - we tried our best
        }

        // Try database logging (fail-safe with timeout and null checks)
        if (_context == null)
        {
            return; // No database context available, console logging already done
        }

        await _logSemaphore.WaitAsync(cancellationToken);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_logTimeout);

            // Check if context is disposed or database is unavailable
            if (_context.Database == null)
            {
                return;
            }

            // Verify database connection is available
            try
            {
                var canConnect = await _context.Database.CanConnectAsync(cts.Token);
                if (!canConnect)
                {
                    return; // Database not available, console logging already done
                }
            }
            catch
            {
                return; // Cannot verify connection, skip database logging
            }

            // Check if ProcessLogs table exists
            try
            {
                var tableExists = await _context.Database.ExecuteSqlRawAsync(
                    "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProcessLogs'",
                    cts.Token) >= 0;
                
                if (!tableExists)
                {
                    // Table doesn't exist yet, try to create it
                    try
                    {
                        await _context.Database.EnsureCreatedAsync(cts.Token);
                    }
                    catch
                    {
                        // Table creation failed, skip database logging
                        return;
                    }
                }
            }
            catch
            {
                // Cannot check table existence, try to log anyway (table might exist)
            }

            // Create log entity with null-safe operations
            var log = new ProcessLog
            {
                Level = level.Length > 50 ? level.Substring(0, 50) : level,
                Message = message ?? string.Empty,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            // Truncate message if too long (prevent database errors)
            if (log.Message.Length > 4000)
            {
                log.Message = log.Message.Substring(0, 4000) + "... [truncated]";
            }

            if (log.Details != null && log.Details.Length > 4000)
            {
                log.Details = log.Details.Substring(0, 4000) + "... [truncated]";
            }

            _context.ProcessLogs.Add(log);
            await _context.SaveChangesAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout occurred, console logging already done
        }
        catch (DbUpdateException dbEx)
        {
            // Database error - log to console but don't fail
            try
            {
                var errorMsg = $"Database logging failed: {dbEx.Message}";
                if (_logger != null)
                {
                    _logger.LogWarning(dbEx, errorMsg);
                }
                else
                {
                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] WARNING: {errorMsg}");
                }
            }
            catch
            {
                // Ignore error logging errors
            }
        }
        catch (Exception ex)
        {
            // Any other error - log to console but don't fail
            try
            {
                var errorMsg = $"Logging error: {ex.GetType().Name}: {ex.Message}";
                if (_logger != null)
                {
                    _logger.LogWarning(ex, errorMsg);
                }
                else
                {
                    Console.Error.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] WARNING: {errorMsg}");
                }
            }
            catch
            {
                // Ignore error logging errors
            }
        }
        finally
        {
            _logSemaphore.Release();
        }
    }
}


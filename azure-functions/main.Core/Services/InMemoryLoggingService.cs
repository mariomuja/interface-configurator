using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// In-memory logging service - logs are stored in memory and lost when Function App restarts
/// Provides fast logging without SQL dependency
/// </summary>
public class InMemoryLoggingService : IInMemoryLoggingService
{
    private readonly ConcurrentQueue<ProcessLogEntry> _logs = new();
    private readonly ILogger<InMemoryLoggingService>? _logger;
    private int _nextId = 1;
    private readonly object _idLock = new();
    private const int MaxLogEntries = 10000; // Prevent memory issues

    public InMemoryLoggingService(ILogger<InMemoryLoggingService>? logger = null)
    {
        _logger = logger;
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
            else
            {
                Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {logMessage}");
            }
        }
        catch (Exception ex)
        {
            // If even console logging fails, try stderr
            try
            {
                Console.Error.WriteLine($"Critical: Failed to log to console: {ex.Message}");
            }
            catch
            {
                // Absolute last resort
            }
        }

        // Store in memory
        try
        {
            var component = GetAzureComponentInfo();
            
            var logEntry = new ProcessLogEntry
            {
                Id = GetNextId(),
                datetime_created = DateTime.UtcNow,
                Level = level.Length > 50 ? level.Substring(0, 50) : level,
                Message = message ?? string.Empty,
                Details = details,
                Component = component
            };

            // Truncate if too long
            if (logEntry.Message.Length > 4000)
            {
                logEntry.Message = logEntry.Message.Substring(0, 4000) + "... [truncated]";
            }

            if (logEntry.Details != null && logEntry.Details.Length > 4000)
            {
                logEntry.Details = logEntry.Details.Substring(0, 4000) + "... [truncated]";
            }

            _logs.Enqueue(logEntry);

            // Limit queue size to prevent memory issues
            while (_logs.Count > MaxLogEntries)
            {
                _logs.TryDequeue(out _);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail
            try
            {
                if (_logger != null)
                {
                    _logger.LogWarning(ex, "Failed to store log in memory");
                }
                else
                {
                    Console.Error.WriteLine($"Warning: Failed to store log in memory: {ex.Message}");
                }
            }
            catch
            {
                // Ignore
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets all logs (for API access)
    /// </summary>
    public List<ProcessLogEntry> GetAllLogs()
    {
        return _logs.ToList();
    }

    /// <summary>
    /// Clears all logs
    /// </summary>
    public void ClearLogs()
    {
        while (_logs.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Gets Azure component information from environment variables
    /// Format: ResourceGroup/FunctionApp/FunctionName
    /// </summary>
    private static string? GetAzureComponentInfo()
    {
        try
        {
            var resourceGroup = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP") 
                ?? Environment.GetEnvironmentVariable("ResourceGroup");
            var functionAppName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") 
                ?? Environment.GetEnvironmentVariable("FUNCTIONAPP_NAME")
                ?? Environment.GetEnvironmentVariable("FunctionAppName");
            var functionName = Environment.GetEnvironmentVariable("FUNCTION_NAME") 
                ?? Environment.GetEnvironmentVariable("FunctionName")
                ?? "InterfaceConfigurator.Main";

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(resourceGroup))
                parts.Add(resourceGroup);
            if (!string.IsNullOrWhiteSpace(functionAppName))
                parts.Add(functionAppName);
            if (!string.IsNullOrWhiteSpace(functionName))
                parts.Add(functionName);

            return parts.Count > 0 ? string.Join("/", parts) : null;
        }
        catch
        {
            return null;
        }
    }

    private int GetNextId()
    {
        lock (_idLock)
        {
            return _nextId++;
        }
    }
}


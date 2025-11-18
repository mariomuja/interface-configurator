using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Models;

namespace ProcessCsvBlobTrigger.Services;

/// <summary>
/// SQL Server-based logging service - logs are stored in MessageBox database
/// </summary>
public class SqlServerLoggingService : ILoggingService
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<SqlServerLoggingService>? _logger;

    public SqlServerLoggingService(
        MessageBoxDbContext context,
        ILogger<SqlServerLoggingService>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
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
            try
            {
                Console.Error.WriteLine($"Critical: Failed to log to console: {ex.Message}");
            }
            catch
            {
                // Ignore
            }
        }

        // Store in SQL Server MessageBox database
        try
        {
            var component = GetAzureComponentInfo();
            
            var logEntry = new ProcessLog
            {
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

            _context.ProcessLogs.Add(logEntry);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log error but don't fail
            try
            {
                if (_logger != null)
                {
                    _logger.LogWarning(ex, "Failed to store log in SQL Server MessageBox");
                }
                else
                {
                    Console.Error.WriteLine($"Warning: Failed to store log in SQL Server MessageBox: {ex.Message}");
                }
            }
            catch
            {
                // Ignore
            }
        }
    }

    /// <summary>
    /// Gets Azure component information from environment variables
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
                ?? "ProcessCsvBlobTrigger";

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
}






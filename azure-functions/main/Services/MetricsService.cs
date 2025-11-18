using System.Reflection;
using Microsoft.Extensions.Logging;

namespace ProcessCsvBlobTrigger.Services;

/// <summary>
/// Service for tracking custom metrics and events in Application Insights
/// Note: Application Insights TelemetryClient is optional - if not available, metrics are skipped
/// Uses reflection to avoid hard dependency on Application Insights package
/// </summary>
public class MetricsService
{
    private readonly object? _telemetryClient; // Using object to avoid dependency on Application Insights package
    private readonly ILogger<MetricsService>? _logger;

    public MetricsService(object? telemetryClient, ILogger<MetricsService>? logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    /// <summary>
    /// Track a message processing event
    /// </summary>
    public void TrackMessageProcessed(string adapterName, int recordCount, TimeSpan duration)
    {
        try
        {
            if (_telemetryClient == null) return;

            InvokeTelemetryMethod("TrackMetric", "MessagesProcessed", recordCount, new Dictionary<string, string>
            {
                { "Adapter", adapterName },
                { "DurationSeconds", duration.TotalSeconds.ToString("F2") }
            });

            InvokeTelemetryMethod("TrackEvent", "MessageProcessed", new Dictionary<string, string>
            {
                { "Adapter", adapterName },
                { "RecordCount", recordCount.ToString() },
                { "DurationMs", duration.TotalMilliseconds.ToString("F2") }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to track message processed metric");
        }
    }

    /// <summary>
    /// Track an error event
    /// </summary>
    public void TrackError(string adapterName, string errorType, Exception ex)
    {
        try
        {
            if (_telemetryClient == null) return;

            InvokeTelemetryMethod("TrackException", ex, new Dictionary<string, string>
            {
                { "Adapter", adapterName },
                { "ErrorType", errorType }
            });

            InvokeTelemetryMethod("TrackEvent", "ErrorOccurred", new Dictionary<string, string>
            {
                { "Adapter", adapterName },
                { "ErrorType", errorType },
                { "ErrorMessage", ex.Message }
            });
        }
        catch (Exception logEx)
        {
            _logger?.LogWarning(logEx, "Failed to track error metric");
        }
    }

    /// <summary>
    /// Track a retry event
    /// </summary>
    public void TrackRetry(string adapterName, int retryCount, int maxRetries, string reason)
    {
        try
        {
            if (_telemetryClient == null) return;

            InvokeTelemetryMethod("TrackEvent", "RetryAttempt", new Dictionary<string, string>
            {
                { "Adapter", adapterName },
                { "RetryCount", retryCount.ToString() },
                { "MaxRetries", maxRetries.ToString() },
                { "Reason", reason }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to track retry metric");
        }
    }

    /// <summary>
    /// Track a dead letter event
    /// </summary>
    public void TrackDeadLetter(string adapterName, Guid messageId, string reason)
    {
        try
        {
            if (_telemetryClient == null) return;

            InvokeTelemetryMethod("TrackEvent", "DeadLetter", new Dictionary<string, string>
            {
                { "Adapter", adapterName },
                { "MessageId", messageId.ToString() },
                { "Reason", reason }
            });

            InvokeTelemetryMethod("TrackMetric", "DeadLetterCount", 1, new Dictionary<string, string>
            {
                { "Adapter", adapterName }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to track dead letter metric");
        }
    }

    /// <summary>
    /// Track database connection retry
    /// </summary>
    public void TrackDatabaseRetry(string databaseName, int retryCount, string errorMessage)
    {
        try
        {
            if (_telemetryClient == null) return;

            InvokeTelemetryMethod("TrackEvent", "DatabaseRetry", new Dictionary<string, string>
            {
                { "Database", databaseName },
                { "RetryCount", retryCount.ToString() },
                { "Error", errorMessage }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to track database retry metric");
        }
    }

    /// <summary>
    /// Track health check result
    /// </summary>
    public void TrackHealthCheck(string checkName, bool isHealthy, string? message = null)
    {
        try
        {
            if (_telemetryClient == null) return;

            InvokeTelemetryMethod("TrackEvent", "HealthCheck", new Dictionary<string, string>
            {
                { "CheckName", checkName },
                { "IsHealthy", isHealthy.ToString() },
                { "Message", message ?? string.Empty }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to track health check metric");
        }
    }

    /// <summary>
    /// Invoke TelemetryClient method using reflection (avoids hard dependency)
    /// </summary>
    private void InvokeTelemetryMethod(string methodName, params object[] parameters)
    {
        if (_telemetryClient == null) return;

        try
        {
            var method = _telemetryClient.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(_telemetryClient, parameters);
            }
        }
        catch
        {
            // Silently fail if method doesn't exist or invocation fails
            // This allows the code to work without Application Insights package
        }
    }
}

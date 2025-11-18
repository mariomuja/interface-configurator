using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;

namespace ProcessCsvBlobTrigger.Services;

/// <summary>
/// Service for tracking custom metrics and events in Application Insights
/// </summary>
public class MetricsService
{
    private readonly TelemetryClient? _telemetryClient;
    private readonly ILogger<MetricsService>? _logger;

    public MetricsService(TelemetryClient? telemetryClient, ILogger<MetricsService>? logger)
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
            _telemetryClient?.TrackMetric("MessagesProcessed", recordCount, new Dictionary<string, string>
            {
                { "Adapter", adapterName },
                { "DurationSeconds", duration.TotalSeconds.ToString("F2") }
            });

            _telemetryClient?.TrackEvent("MessageProcessed", new Dictionary<string, string>
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
            _telemetryClient?.TrackException(ex, new Dictionary<string, string>
            {
                { "Adapter", adapterName },
                { "ErrorType", errorType }
            });

            _telemetryClient?.TrackEvent("ErrorOccurred", new Dictionary<string, string>
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
            _telemetryClient?.TrackEvent("RetryAttempt", new Dictionary<string, string>
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
            _telemetryClient?.TrackEvent("DeadLetter", new Dictionary<string, string>
            {
                { "Adapter", adapterName },
                { "MessageId", messageId.ToString() },
                { "Reason", reason }
            });

            _telemetryClient?.TrackMetric("DeadLetterCount", 1, new Dictionary<string, string>
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
            _telemetryClient?.TrackEvent("DatabaseRetry", new Dictionary<string, string>
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
            _telemetryClient?.TrackEvent("HealthCheck", new Dictionary<string, string>
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
}


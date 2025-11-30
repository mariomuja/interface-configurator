using System.Diagnostics;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Service for tracking message flow across all components
/// Provides distributed tracing and performance metrics
/// </summary>
public class MessageTrackingService
{
    private readonly ILogger<MessageTrackingService>? _logger;
    private readonly Dictionary<string, ActivitySource> _activitySources = new();

    public MessageTrackingService(ILogger<MessageTrackingService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Track message flow through the system
    /// </summary>
    public async Task TrackMessageFlowAsync(
        string messageId,
        string stage,
        string adapterName,
        TimeSpan processingTime,
        bool success,
        Dictionary<string, object>? additionalProperties = null,
        CancellationToken cancellationToken = default)
    {
        var activitySource = GetOrCreateActivitySource(adapterName);
        
        using var activity = activitySource.StartActivity($"MessageFlow.{stage}");
        activity?.SetTag("messageId", messageId);
        activity?.SetTag("adapterName", adapterName);
        activity?.SetTag("stage", stage);
        activity?.SetTag("processingTimeMs", processingTime.TotalMilliseconds);
        activity?.SetTag("success", success);

        if (additionalProperties != null)
        {
            foreach (var prop in additionalProperties)
            {
                activity?.SetTag(prop.Key, prop.Value?.ToString() ?? string.Empty);
            }
        }

        _logger?.LogInformation(
            "[MessageTracking] MessageId={MessageId}, Stage={Stage}, Adapter={Adapter}, ProcessingTime={ProcessingTime}ms, Success={Success}",
            messageId, stage, adapterName, processingTime.TotalMilliseconds, success);
    }

    /// <summary>
    /// Track business metrics
    /// </summary>
    public void TrackBusinessMetric(
        string metricName,
        double value,
        string? interfaceName = null,
        string? adapterName = null,
        Dictionary<string, object>? tags = null)
    {
        var activitySource = GetOrCreateActivitySource(adapterName ?? "System");
        
        using var activity = activitySource.StartActivity($"BusinessMetric.{metricName}");
        activity?.SetTag("metricName", metricName);
        activity?.SetTag("value", value);
        
        if (!string.IsNullOrWhiteSpace(interfaceName))
            activity?.SetTag("interfaceName", interfaceName);
        if (!string.IsNullOrWhiteSpace(adapterName))
            activity?.SetTag("adapterName", adapterName);

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                activity?.SetTag(tag.Key, tag.Value?.ToString() ?? string.Empty);
            }
        }

        _logger?.LogInformation(
            "[BusinessMetric] {MetricName}={Value}, Interface={Interface}, Adapter={Adapter}",
            metricName, value, interfaceName ?? "N/A", adapterName ?? "N/A");
    }

    /// <summary>
    /// Track performance metrics
    /// </summary>
    public void TrackPerformanceMetric(
        string operationName,
        TimeSpan duration,
        string? interfaceName = null,
        string? adapterName = null,
        bool success = true)
    {
        var activitySource = GetOrCreateActivitySource(adapterName ?? "System");
        
        using var activity = activitySource.StartActivity($"Performance.{operationName}");
        activity?.SetTag("operation", operationName);
        activity?.SetTag("durationMs", duration.TotalMilliseconds);
        activity?.SetTag("success", success);
        
        if (!string.IsNullOrWhiteSpace(interfaceName))
            activity?.SetTag("interfaceName", interfaceName);
        if (!string.IsNullOrWhiteSpace(adapterName))
            activity?.SetTag("adapterName", adapterName);

        _logger?.LogInformation(
            "[Performance] {Operation}={Duration}ms, Interface={Interface}, Adapter={Adapter}, Success={Success}",
            operationName, duration.TotalMilliseconds, interfaceName ?? "N/A", adapterName ?? "N/A", success);
    }

    private ActivitySource GetOrCreateActivitySource(string adapterName)
    {
        var key = adapterName ?? "Default";
        if (!_activitySources.TryGetValue(key, out var source))
        {
            source = new ActivitySource($"InterfaceConfigurator.{key}");
            _activitySources[key] = source;
        }
        return source;
    }
}






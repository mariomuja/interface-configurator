using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Observability Service for distributed tracing, metrics, and monitoring
/// </summary>
public interface IObservabilityService
{
    /// <summary>
    /// Start a new activity (span) for distributed tracing
    /// </summary>
    Activity? StartActivity(string name, string? parentActivityId = null);

    /// <summary>
    /// Record a metric
    /// </summary>
    void RecordMetric(string name, double value, Dictionary<string, string>? tags = null);

    /// <summary>
    /// Increment a counter
    /// </summary>
    void IncrementCounter(string name, Dictionary<string, string>? tags = null);

    /// <summary>
    /// Record an event
    /// </summary>
    void RecordEvent(string name, Dictionary<string, object>? properties = null);

    /// <summary>
    /// Get current trace context
    /// </summary>
    string? GetTraceContext();
}

/// <summary>
/// Observability Service implementation using ActivitySource for distributed tracing
/// </summary>
public class ObservabilityService : IObservabilityService
{
    private readonly ILogger<ObservabilityService>? _logger;
    private static readonly ActivitySource ActivitySource = new ActivitySource("InterfaceConfigurator");
    private readonly Dictionary<string, Counter> _counters = new();
    private readonly Dictionary<string, List<Metric>> _metrics = new();
    private readonly object _lock = new object();

    public ObservabilityService(ILogger<ObservabilityService>? logger = null)
    {
        _logger = logger;
    }

    public Activity? StartActivity(string name, string? parentActivityId = null)
    {
        Activity? activity = null;

        if (!string.IsNullOrWhiteSpace(parentActivityId))
        {
            // Create activity with parent context
            var parentContext = new ActivityContext(
                ActivityTraceId.CreateFromString(parentActivityId),
                ActivitySpanId.CreateFromString(parentActivityId),
                ActivityTraceFlags.Recorded);

            activity = ActivitySource.StartActivity(name, ActivityKind.Internal, parentContext);
        }
        else
        {
            activity = ActivitySource.StartActivity(name);
        }

        if (activity != null)
        {
            activity.SetTag("service.name", "InterfaceConfigurator");
            activity.SetTag("service.version", "1.0");
            
            _logger?.LogDebug(
                "Started activity '{ActivityName}' with TraceId={TraceId}, SpanId={SpanId}",
                name, activity.TraceId, activity.SpanId);
        }

        return activity;
    }

    public void RecordMetric(string name, double value, Dictionary<string, string>? tags = null)
    {
        lock (_lock)
        {
            if (!_metrics.TryGetValue(name, out var metricList))
            {
                metricList = new List<Metric>();
                _metrics[name] = metricList;
            }

            metricList.Add(new Metric
            {
                Name = name,
                Value = value,
                Timestamp = DateTime.UtcNow,
                Tags = tags ?? new Dictionary<string, string>()
            });

            // Keep only last 1000 metrics per name
            if (metricList.Count > 1000)
            {
                metricList.RemoveAt(0);
            }
        }

        // Also log structured metric
        _logger?.LogInformation(
            "Metric recorded: {MetricName}={MetricValue}, Tags={Tags}",
            name, value, tags != null ? string.Join(", ", tags.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "none");
    }

    public void IncrementCounter(string name, Dictionary<string, string>? tags = null)
    {
        var key = $"{name}:{string.Join(",", tags?.Select(kvp => $"{kvp.Key}={kvp.Value}") ?? Array.Empty<string>())}";

        lock (_lock)
        {
            if (!_counters.TryGetValue(key, out var counter))
            {
                counter = new Counter { Name = name, Tags = tags ?? new Dictionary<string, string>() };
                _counters[key] = counter;
            }

            counter.Value++;
        }

        _logger?.LogDebug(
            "Counter incremented: {CounterName}={CounterValue}, Tags={Tags}",
            name, _counters[key].Value, tags != null ? string.Join(", ", tags.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "none");
    }

    public void RecordEvent(string name, Dictionary<string, object>? properties = null)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.AddEvent(new ActivityEvent(name));
            
            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    activity.SetTag(prop.Key, prop.Value?.ToString() ?? string.Empty);
                }
            }
        }

        _logger?.LogInformation(
            "Event recorded: {EventName}, Properties={Properties}",
            name, properties != null ? string.Join(", ", properties.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "none");
    }

    public string? GetTraceContext()
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            return activity.TraceId.ToString();
        }

        return null;
    }

    private class Counter
    {
        public string Name { get; set; } = string.Empty;
        public long Value { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    private class Metric
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }
}


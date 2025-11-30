using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Service for adaptive batch processing
/// Dynamically adjusts batch size based on message size, processing speed, and system load
/// </summary>
public class AdaptiveBatchingService
{
    private readonly ILogger<AdaptiveBatchingService>? _logger;
    
    // Track performance metrics per interface
    private readonly ConcurrentDictionary<string, BatchPerformanceMetrics> _metrics = new();

    public AdaptiveBatchingService(ILogger<AdaptiveBatchingService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculate optimal batch size for an interface
    /// </summary>
    public Task<int> GetOptimalBatchSizeAsync(
        string interfaceName,
        int defaultBatchSize = 100,
        int? averageRecordSize = null,
        CancellationToken cancellationToken = default)
    {
        var metrics = _metrics.GetOrAdd(interfaceName, _ => new BatchPerformanceMetrics
        {
            InterfaceName = interfaceName,
            OptimalBatchSize = defaultBatchSize
        });

        // If we have performance data, use it to adjust batch size
        if (metrics.ProcessedBatches > 10 && metrics.AverageProcessingTime > TimeSpan.Zero)
        {
            // Calculate optimal batch size based on processing time
            // Target: process batches in 1-5 seconds
            var targetProcessingTime = TimeSpan.FromSeconds(3);
            var currentTimePerRecord = metrics.AverageProcessingTime / metrics.AverageRecordsPerBatch;
            
            if (currentTimePerRecord > TimeSpan.Zero)
            {
                var optimalRecords = (int)(targetProcessingTime.TotalMilliseconds / currentTimePerRecord.TotalMilliseconds);
                
                // Clamp between 10 and 1000
                optimalRecords = Math.Max(10, Math.Min(1000, optimalRecords));
                
                // Smooth adjustment (don't change too drastically)
                metrics.OptimalBatchSize = (metrics.OptimalBatchSize + optimalRecords) / 2;
                
                _logger?.LogDebug(
                    "Adaptive batching: Interface={Interface}, OptimalBatchSize={OptimalBatchSize} (was {OldSize})",
                    interfaceName, metrics.OptimalBatchSize, defaultBatchSize);
            }
        }

        // Adjust based on average record size if provided
        if (averageRecordSize.HasValue)
        {
            // Service Bus message size limit is ~256KB
            // Leave some headroom for metadata
            var maxMessageSize = 200 * 1024; // 200KB
            var maxRecordsBySize = maxMessageSize / averageRecordSize.Value;
            
            if (maxRecordsBySize < metrics.OptimalBatchSize)
            {
                metrics.OptimalBatchSize = Math.Max(10, maxRecordsBySize);
                _logger?.LogDebug(
                    "Adaptive batching: Reduced batch size due to record size. Interface={Interface}, BatchSize={BatchSize}",
                    interfaceName, metrics.OptimalBatchSize);
            }
        }

        return Task.FromResult(metrics.OptimalBatchSize);
    }

    /// <summary>
    /// Record batch processing performance
    /// </summary>
    public void RecordBatchPerformance(
        string interfaceName,
        int recordCount,
        TimeSpan processingTime,
        bool success)
    {
        var metrics = _metrics.GetOrAdd(interfaceName, _ => new BatchPerformanceMetrics
        {
            InterfaceName = interfaceName
        });

        if (success)
        {
            metrics.ProcessedBatches++;
            metrics.TotalRecordsProcessed += recordCount;
            metrics.TotalProcessingTime += processingTime;
            
            // Update averages
            metrics.AverageRecordsPerBatch = metrics.TotalRecordsProcessed / (double)metrics.ProcessedBatches;
            metrics.AverageProcessingTime = TimeSpan.FromMilliseconds(
                metrics.TotalProcessingTime.TotalMilliseconds / metrics.ProcessedBatches);
        }
        else
        {
            metrics.FailedBatches++;
        }

        _logger?.LogDebug(
            "Batch performance recorded: Interface={Interface}, Records={Records}, Time={Time}ms, Success={Success}",
            interfaceName, recordCount, processingTime.TotalMilliseconds, success);
    }

    /// <summary>
    /// Create optimal batches with time-based and size-based limits
    /// </summary>
    public async Task<List<List<Dictionary<string, string>>>> CreateOptimalBatchesAsync(
        List<Dictionary<string, string>> records,
        string interfaceName,
        int? maxBatchSize = null,
        TimeSpan? maxWaitTime = null,
        int? maxBatchSizeBytes = null,
        CancellationToken cancellationToken = default)
    {
        maxWaitTime ??= TimeSpan.FromSeconds(5);
        maxBatchSize ??= await GetOptimalBatchSizeAsync(interfaceName, cancellationToken: cancellationToken);
        maxBatchSizeBytes ??= 200 * 1024; // 200KB default

        var batches = new List<List<Dictionary<string, string>>>();
        var currentBatch = new List<Dictionary<string, string>>();
        var currentBatchSizeBytes = 0;
        var batchStartTime = DateTime.UtcNow;

        foreach (var record in records)
        {
            // Estimate record size (rough approximation)
            var recordSize = EstimateRecordSize(record);

            // Check if adding this record would exceed limits
            var wouldExceedSize = currentBatchSizeBytes + recordSize > maxBatchSizeBytes;
            var wouldExceedCount = currentBatch.Count >= maxBatchSize;
            var wouldExceedTime = DateTime.UtcNow - batchStartTime >= maxWaitTime;

            if ((wouldExceedSize || wouldExceedCount || wouldExceedTime) && currentBatch.Count > 0)
            {
                // Finalize current batch
                batches.Add(new List<Dictionary<string, string>>(currentBatch));
                currentBatch.Clear();
                currentBatchSizeBytes = 0;
                batchStartTime = DateTime.UtcNow;
            }

            currentBatch.Add(record);
            currentBatchSizeBytes += recordSize;
        }

        // Add remaining records as final batch
        if (currentBatch.Count > 0)
        {
            batches.Add(currentBatch);
        }

        _logger?.LogDebug(
            "Created {BatchCount} optimal batches from {RecordCount} records: Interface={Interface}",
            batches.Count, records.Count, interfaceName);

        return batches;
    }

    private int EstimateRecordSize(Dictionary<string, string> record)
    {
        // Rough estimation: sum of key and value lengths
        return record.Sum(kvp => kvp.Key.Length + (kvp.Value?.Length ?? 0)) + 100; // +100 for JSON overhead
    }
}

public class BatchPerformanceMetrics
{
    public string InterfaceName { get; set; } = string.Empty;
    public int OptimalBatchSize { get; set; } = 100;
    public int ProcessedBatches { get; set; }
    public int FailedBatches { get; set; }
    public long TotalRecordsProcessed { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public double AverageRecordsPerBatch { get; set; }
    public TimeSpan AverageProcessingTime { get; set; }
}


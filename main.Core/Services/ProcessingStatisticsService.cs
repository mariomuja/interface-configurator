using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;
using Microsoft.EntityFrameworkCore;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Service for tracking processing statistics for adapter runs.
/// </summary>
public class ProcessingStatisticsService
{
    private readonly InterfaceConfigDbContext _context;
    private readonly ILogger? _logger;

    public ProcessingStatisticsService(InterfaceConfigDbContext context, ILogger? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    public async Task RecordProcessingStatsAsync(
        string interfaceName,
        int rowsProcessed,
        int rowsSucceeded,
        int rowsFailed,
        TimeSpan processingDuration,
        string? sourceFile = null,
        string? adapterType = null,
        string? adapterName = null,
        Guid? adapterInstanceGuid = null,
        string? sourceName = null,
        string? destinationName = null,
        int? batchSize = null,
        bool? useTransaction = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow.Add(-processingDuration);
            var rowsPerSecond = processingDuration.TotalSeconds > 0 
                ? rowsProcessed / processingDuration.TotalSeconds 
                : 0;

            var stats = new ProcessingStatistics
            {
                InterfaceName = interfaceName,
                RowsProcessed = rowsProcessed,
                RowsSucceeded = rowsSucceeded,
                RowsFailed = rowsFailed,
                ProcessingDurationMs = (long)processingDuration.TotalMilliseconds,
                ProcessingStartTime = startTime,
                ProcessingEndTime = DateTime.UtcNow,
                SourceFile = sourceFile,
                AdapterType = adapterType,
                AdapterName = adapterName,
                AdapterInstanceGuid = adapterInstanceGuid,
                SourceName = sourceName,
                DestinationName = destinationName,
                BatchSize = batchSize,
                UseTransaction = useTransaction,
                RowsPerSecond = rowsPerSecond > 0 ? (double?)rowsPerSecond : null
            };

            _context.ProcessingStatistics.Add(stats);
            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation(
                "Recorded processing stats for {InterfaceName}: {RowsProcessed} rows, Duration: {Duration}ms, Adapter: {AdapterType}/{AdapterName}",
                interfaceName, rowsProcessed, processingDuration.TotalMilliseconds, adapterType, adapterName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error recording processing statistics for {InterfaceName}", interfaceName);
        }
    }

    public async Task<ProcessingStatisticsSummary> GetStatisticsAsync(
        string interfaceName,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ProcessingStatistics.Where(s => s.InterfaceName == interfaceName);

        if (startDate.HasValue)
        {
            query = query.Where(s => s.ProcessingStartTime >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.ProcessingEndTime <= endDate.Value);
        }

        var stats = await query.ToListAsync(cancellationToken);

        if (stats.Count == 0)
        {
            return new ProcessingStatisticsSummary
            {
                InterfaceName = interfaceName
            };
        }

        var totalRowsProcessed = stats.Sum(s => s.RowsProcessed);
        var totalRowsSucceeded = stats.Sum(s => s.RowsSucceeded);
        var totalRowsFailed = stats.Sum(s => s.RowsFailed);
        var totalProcessingTimeMs = stats.Sum(s => s.ProcessingDurationMs);
        var averageProcessingTimeMs = stats.Average(s => s.ProcessingDurationMs);
        var totalDuration = stats.Max(s => s.ProcessingEndTime) - stats.Min(s => s.ProcessingStartTime);
        var rowsPerHour = totalDuration.TotalHours > 0 ? totalRowsProcessed / totalDuration.TotalHours : 0;
        var successRate = totalRowsProcessed > 0 ? (double)totalRowsSucceeded / totalRowsProcessed * 100 : 0;

        return new ProcessingStatisticsSummary
        {
            InterfaceName = interfaceName,
            TotalRowsProcessed = totalRowsProcessed,
            TotalRowsSucceeded = totalRowsSucceeded,
            TotalRowsFailed = totalRowsFailed,
            SuccessRate = successRate,
            AverageProcessingTimeMs = (long)averageProcessingTimeMs,
            TotalProcessingTimeMs = totalProcessingTimeMs,
            RowsPerHour = rowsPerHour,
            ProcessingCount = stats.Count,
            FirstProcessingTime = stats.Min(s => s.ProcessingStartTime),
            LastProcessingTime = stats.Max(s => s.ProcessingEndTime)
        };
    }

    public async Task<List<ProcessingStatistics>> GetRecentStatisticsAsync(
        string? interfaceName = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ProcessingStatistics.AsQueryable();

        if (!string.IsNullOrWhiteSpace(interfaceName))
        {
            query = query.Where(s => s.InterfaceName == interfaceName);
        }

        return await query
            .OrderByDescending(s => s.ProcessingEndTime)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}

public class ProcessingStatistics
{
    public int Id { get; set; }
    public string InterfaceName { get; set; } = string.Empty;
    public int RowsProcessed { get; set; }
    public int RowsSucceeded { get; set; }
    public int RowsFailed { get; set; }
    public long ProcessingDurationMs { get; set; }
    public DateTime ProcessingStartTime { get; set; }
    public DateTime ProcessingEndTime { get; set; }
    public string? SourceFile { get; set; }
    
    // Additional statistics for better tracking and analysis
    public string? AdapterType { get; set; } // "Source" or "Destination"
    public string? AdapterName { get; set; } // "CSV", "SqlServer", "SAP", etc.
    public Guid? AdapterInstanceGuid { get; set; } // Which specific adapter instance processed this
    public string? SourceName { get; set; } // Source location (table name, file path, etc.)
    public string? DestinationName { get; set; } // Destination location (table name, file path, etc.)
    public int? BatchSize { get; set; } // Batch size used for processing
    public bool? UseTransaction { get; set; } // Whether transaction was used
    public double? RowsPerSecond { get; set; } // Calculated throughput for quick queries
}

public class ProcessingStatisticsSummary
{
    public string InterfaceName { get; set; } = string.Empty;
    public int TotalRowsProcessed { get; set; }
    public int TotalRowsSucceeded { get; set; }
    public int TotalRowsFailed { get; set; }
    public double SuccessRate { get; set; }
    public long AverageProcessingTimeMs { get; set; }
    public long TotalProcessingTimeMs { get; set; }
    public double RowsPerHour { get; set; }
    public int ProcessingCount { get; set; }
    public DateTime? FirstProcessingTime { get; set; }
    public DateTime? LastProcessingTime { get; set; }
}


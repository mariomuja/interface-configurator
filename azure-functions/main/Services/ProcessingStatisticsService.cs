using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;
using Microsoft.EntityFrameworkCore;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for tracking processing statistics (processing time, rows/hour, success rates)
/// </summary>
public class ProcessingStatisticsService
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger? _logger;

    public ProcessingStatisticsService(MessageBoxDbContext context, ILogger? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    /// <summary>
    /// Record processing statistics for an interface
    /// </summary>
    public async Task RecordProcessingStatsAsync(
        string interfaceName,
        int rowsProcessed,
        int rowsSucceeded,
        int rowsFailed,
        TimeSpan processingDuration,
        string? sourceFile = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = new ProcessingStatistics
            {
                InterfaceName = interfaceName,
                RowsProcessed = rowsProcessed,
                RowsSucceeded = rowsSucceeded,
                RowsFailed = rowsFailed,
                ProcessingDurationMs = (long)processingDuration.TotalMilliseconds,
                ProcessingStartTime = DateTime.UtcNow.Add(-processingDuration),
                ProcessingEndTime = DateTime.UtcNow,
                SourceFile = sourceFile
            };

            _context.ProcessingStatistics.Add(stats);
            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation(
                "Recorded processing stats for {InterfaceName}: {RowsProcessed} rows, {RowsSucceeded} succeeded, {RowsFailed} failed, Duration: {Duration}ms",
                interfaceName, rowsProcessed, rowsSucceeded, rowsFailed, processingDuration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error recording processing statistics for {InterfaceName}", interfaceName);
            // Don't throw - statistics are not critical
        }
    }

    /// <summary>
    /// Get processing statistics for an interface
    /// </summary>
    public async Task<ProcessingStatisticsSummary> GetStatisticsAsync(
        string interfaceName,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.ProcessingStatistics
                .Where(s => s.InterfaceName == interfaceName);

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
                    InterfaceName = interfaceName,
                    TotalRowsProcessed = 0,
                    TotalRowsSucceeded = 0,
                    TotalRowsFailed = 0,
                    SuccessRate = 0,
                    AverageProcessingTimeMs = 0,
                    TotalProcessingTimeMs = 0,
                    RowsPerHour = 0
                };
            }

            var totalRowsProcessed = stats.Sum(s => s.RowsProcessed);
            var totalRowsSucceeded = stats.Sum(s => s.RowsSucceeded);
            var totalRowsFailed = stats.Sum(s => s.RowsFailed);
            var totalProcessingTimeMs = stats.Sum(s => s.ProcessingDurationMs);
            var averageProcessingTimeMs = stats.Average(s => s.ProcessingDurationMs);

            // Calculate rows per hour
            var totalDuration = stats.Max(s => s.ProcessingEndTime) - stats.Min(s => s.ProcessingStartTime);
            var rowsPerHour = totalDuration.TotalHours > 0 
                ? totalRowsProcessed / totalDuration.TotalHours 
                : 0;

            var successRate = totalRowsProcessed > 0 
                ? (double)totalRowsSucceeded / totalRowsProcessed * 100 
                : 0;

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
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting processing statistics for {InterfaceName}", interfaceName);
            throw;
        }
    }

    /// <summary>
    /// Get recent processing statistics
    /// </summary>
    public async Task<List<ProcessingStatistics>> GetRecentStatisticsAsync(
        string? interfaceName = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
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
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting recent processing statistics");
            throw;
        }
    }
}

/// <summary>
/// Processing statistics record
/// </summary>
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
}

/// <summary>
/// Summary of processing statistics
/// </summary>
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


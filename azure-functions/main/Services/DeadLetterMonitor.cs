using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for monitoring dead letter queue and providing statistics
/// </summary>
public class DeadLetterMonitor
{
    private readonly IMessageBoxService _messageBoxService;
    private readonly ILogger<DeadLetterMonitor>? _logger;

    public DeadLetterMonitor(
        IMessageBoxService messageBoxService,
        ILogger<DeadLetterMonitor>? logger)
    {
        _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        _logger = logger;
    }

    /// <summary>
    /// Get the count of dead letter messages
    /// </summary>
    public async Task<int> GetDeadLetterCountAsync(string? interfaceName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var deadLetters = await _messageBoxService.ReadDeadLetterMessagesAsync(interfaceName, cancellationToken);
            return deadLetters.Count;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting dead letter count for interface {InterfaceName}", interfaceName ?? "All");
            return 0;
        }
    }

    /// <summary>
    /// Get recent dead letter messages
    /// </summary>
    public async Task<List<MessageBoxMessage>> GetRecentDeadLettersAsync(
        int count = 10, 
        string? interfaceName = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deadLetters = await _messageBoxService.ReadDeadLetterMessagesAsync(interfaceName, cancellationToken);
            return deadLetters
                .OrderByDescending(m => m.datetime_processed ?? m.datetime_created)
                .Take(count)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting recent dead letters for interface {InterfaceName}", interfaceName ?? "All");
            return new List<MessageBoxMessage>();
        }
    }

    /// <summary>
    /// Get dead letter statistics grouped by interface
    /// </summary>
    public async Task<Dictionary<string, DeadLetterStats>> GetDeadLetterStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var deadLetters = await _messageBoxService.ReadDeadLetterMessagesAsync(null, cancellationToken);
            
            var stats = deadLetters
                .GroupBy(m => m.InterfaceName ?? "Unknown")
                .ToDictionary(
                    g => g.Key,
                    g => new DeadLetterStats
                    {
                        Count = g.Count(),
                        OldestMessage = g.Min(m => m.datetime_processed ?? m.datetime_created),
                        NewestMessage = g.Max(m => m.datetime_processed ?? m.datetime_created),
                        CommonErrors = g
                            .Where(m => !string.IsNullOrEmpty(m.ErrorMessage))
                            .GroupBy(m => m.ErrorMessage)
                            .OrderByDescending(eg => eg.Count())
                            .Take(5)
                            .ToDictionary(eg => eg.Key!, eg => eg.Count())
                    });

            return stats;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting dead letter statistics");
            return new Dictionary<string, DeadLetterStats>();
        }
    }

    /// <summary>
    /// Check if dead letter count exceeds threshold
    /// </summary>
    public async Task<bool> IsDeadLetterThresholdExceededAsync(
        int threshold = 100, 
        string? interfaceName = null, 
        CancellationToken cancellationToken = default)
    {
        var count = await GetDeadLetterCountAsync(interfaceName, cancellationToken);
        return count > threshold;
    }
}

/// <summary>
/// Statistics for dead letter messages
/// </summary>
public class DeadLetterStats
{
    public int Count { get; set; }
    public DateTime OldestMessage { get; set; }
    public DateTime NewestMessage { get; set; }
    public Dictionary<string, int> CommonErrors { get; set; } = new();
}


namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Rate limiter interface for throttling operations
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Waits until rate limit allows execution
    /// </summary>
    Task WaitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if rate limit allows execution without waiting
    /// </summary>
    bool CanExecute();

    /// <summary>
    /// Gets the current rate limit configuration
    /// </summary>
    RateLimitConfig GetConfig();
}

/// <summary>
/// Rate limit configuration
/// </summary>
public class RateLimitConfig
{
    public int MaxRequests { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public string? Identifier { get; set; }
}


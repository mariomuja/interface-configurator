namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Retry policy configuration for message processing
/// Supports exponential backoff with jitter
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Initial delay before first retry
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Multiplier for exponential backoff (e.g., 2.0 means delay doubles each retry)
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Maximum delay between retries
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Enable jitter to prevent thundering herd problem
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Jitter percentage (0.0 to 1.0) - random variation in delay
    /// </summary>
    public double JitterPercentage { get; set; } = 0.1; // 10% jitter

    /// <summary>
    /// Calculate delay for a specific retry attempt
    /// </summary>
    public TimeSpan CalculateDelay(int retryAttempt)
    {
        if (retryAttempt <= 0)
            return TimeSpan.Zero;

        // Exponential backoff: delay = initialDelay * (multiplier ^ (retryAttempt - 1))
        var delaySeconds = InitialDelay.TotalSeconds * Math.Pow(BackoffMultiplier, retryAttempt - 1);
        var delay = TimeSpan.FromSeconds(delaySeconds);

        // Apply jitter if enabled
        if (UseJitter)
        {
            var random = new Random();
            var jitterRange = delay.TotalSeconds * JitterPercentage;
            var jitter = (random.NextDouble() * 2 - 1) * jitterRange; // -jitterRange to +jitterRange
            delay = TimeSpan.FromSeconds(delay.TotalSeconds + jitter);
        }

        // Cap at max delay
        if (delay > MaxDelay)
            delay = MaxDelay;

        return delay;
    }

    /// <summary>
    /// Check if retry should be attempted based on exception type
    /// </summary>
    public bool ShouldRetry(Exception exception, int currentRetryCount)
    {
        if (currentRetryCount >= MaxRetries)
            return false;

        // Retry on transient errors
        if (exception is TimeoutException ||
            exception is System.Net.Http.HttpRequestException ||
            exception is TaskCanceledException)
        {
            return true;
        }

        // Retry on Service Bus specific transient errors
        if (exception is Azure.Messaging.ServiceBus.ServiceBusException sbEx)
        {
            return sbEx.Reason == Azure.Messaging.ServiceBus.ServiceBusFailureReason.ServiceBusy ||
                   sbEx.Reason == Azure.Messaging.ServiceBus.ServiceBusFailureReason.ServiceTimeout ||
                   sbEx.Reason == Azure.Messaging.ServiceBus.ServiceBusFailureReason.MessagingEntityNotFound;
        }

        // Don't retry on permanent errors
        return false;
    }
}







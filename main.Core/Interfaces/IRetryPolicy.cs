namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Retry policy interface for implementing exponential backoff retry logic
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Executes an async operation with retry logic
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an async operation with retry logic (no return value)
    /// </summary>
    Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an async operation with retry logic and custom retry condition
    /// </summary>
    Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the maximum number of retry attempts
    /// </summary>
    int MaxRetryAttempts { get; }

    /// <summary>
    /// Gets the base delay for exponential backoff
    /// </summary>
    TimeSpan BaseDelay { get; }
}


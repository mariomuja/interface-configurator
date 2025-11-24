using System.Net;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Helpers;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Retry policy with exponential backoff
/// Implements exponential backoff with jitter to prevent thundering herd
/// </summary>
public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private readonly ILogger<ExponentialBackoffRetryPolicy>? _logger;
    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly Random _random = new();

    public ExponentialBackoffRetryPolicy(
        int maxRetryAttempts = 3,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null,
        ILogger<ExponentialBackoffRetryPolicy>? logger = null)
    {
        _maxRetryAttempts = maxRetryAttempts;
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(1);
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public int MaxRetryAttempts => _maxRetryAttempts;
    public TimeSpan BaseDelay => _baseDelay;

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(operation, IsRetryableException, cancellationToken);
    }

    public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(async () =>
        {
            await operation();
            return true;
        }, IsRetryableException, cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken = default)
    {
        var correlationId = CorrelationIdHelper.Current ?? CorrelationIdHelper.Generate();
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= _maxRetryAttempts)
        {
            try
            {
                var result = await operation();
                
                if (attempt > 0)
                {
                    _logger?.LogInformation(
                        "[CorrelationId: {CorrelationId}] Operation succeeded after {Attempt} retry attempts",
                        correlationId, attempt);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (!shouldRetry(ex) || attempt >= _maxRetryAttempts)
                {
                    _logger?.LogError(ex,
                        "[CorrelationId: {CorrelationId}] Operation failed after {Attempt} attempts. Will not retry.",
                        correlationId, attempt);
                    throw;
                }

                attempt++;
                var delay = CalculateDelay(attempt);
                
                _logger?.LogWarning(ex,
                    "[CorrelationId: {CorrelationId}] Operation failed (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}ms. Error: {Error}",
                    correlationId, attempt, _maxRetryAttempts, delay.TotalMilliseconds, ex.Message);

                await Task.Delay(delay, cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException("Operation failed but no exception was captured");
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        // Exponential backoff: baseDelay * 2^(attempt-1)
        var exponentialDelay = TimeSpan.FromMilliseconds(
            _baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

        // Add jitter (random 0-25% of delay) to prevent thundering herd
        var jitter = TimeSpan.FromMilliseconds(
            exponentialDelay.TotalMilliseconds * _random.NextDouble() * 0.25);

        var totalDelay = exponentialDelay.Add(jitter);

        // Cap at max delay
        return totalDelay > _maxDelay ? _maxDelay : totalDelay;
    }

    private bool IsRetryableException(Exception ex)
    {
        // Retry on transient errors
        if (ex is HttpRequestException httpEx)
        {
            // Retry on 5xx errors, timeouts, and network errors
            return true;
        }

        if (ex is TaskCanceledException)
        {
            // Retry on timeouts
            return true;
        }

        // Retry on Azure service exceptions (transient)
        var exceptionType = ex.GetType().Name;
        if (exceptionType.Contains("ServiceBus") || 
            exceptionType.Contains("Storage") ||
            exceptionType.Contains("Sql"))
        {
            // Check for transient error codes
            if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("temporarily", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("retry", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}


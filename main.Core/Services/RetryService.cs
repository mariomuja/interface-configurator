using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Models;
using System.Collections.Concurrent;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Service for managing retry logic with exponential backoff and circuit breaker pattern
/// </summary>
public class RetryService
{
    private readonly ILogger<RetryService>? _logger;
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers = new();
    private readonly ConcurrentDictionary<string, RetryPolicy> _retryPolicies = new();

    public RetryService(ILogger<RetryService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Execute operation with retry logic and circuit breaker
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationKey,
        RetryPolicy? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        retryPolicy ??= GetDefaultRetryPolicy(operationKey);

        // Check circuit breaker
        var circuitBreaker = GetOrCreateCircuitBreaker(operationKey);
        if (!circuitBreaker.ShouldAllowRequest())
        {
            _logger?.LogWarning(
                "Circuit breaker is OPEN for {OperationKey}. Request blocked.",
                operationKey);
            throw new InvalidOperationException($"Circuit breaker is open for {operationKey}. Service is unavailable.");
        }

        int attempt = 0;
        Exception? lastException = null;

        while (attempt <= retryPolicy.MaxRetries)
        {
            try
            {
                var result = await operation();
                
                // Success - record in circuit breaker
                circuitBreaker.RecordSuccess();
                
                if (attempt > 0)
                {
                    _logger?.LogInformation(
                        "Operation {OperationKey} succeeded after {Attempt} retry attempts",
                        operationKey, attempt);
                }

                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;

                // Check if we should retry
                if (!retryPolicy.ShouldRetry(ex, attempt) || attempt > retryPolicy.MaxRetries)
                {
                    // Don't retry - record failure in circuit breaker
                    circuitBreaker.RecordFailure();
                    _logger?.LogError(ex,
                        "Operation {OperationKey} failed after {Attempt} attempts. Not retrying.",
                        operationKey, attempt);
                    throw;
                }

                // Calculate delay for next retry
                var delay = retryPolicy.CalculateDelay(attempt);
                
                _logger?.LogWarning(ex,
                    "Operation {OperationKey} failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms. Error: {ErrorMessage}",
                    operationKey, attempt, retryPolicy.MaxRetries, delay.TotalMilliseconds, ex.Message);

                // Wait before retry
                await Task.Delay(delay, cancellationToken);
            }
        }

        // All retries exhausted
        circuitBreaker.RecordFailure();
        _logger?.LogError(lastException,
            "Operation {OperationKey} failed after {MaxRetries} retry attempts",
            operationKey, retryPolicy.MaxRetries);
        
        throw new InvalidOperationException(
            $"Operation {operationKey} failed after {retryPolicy.MaxRetries} retry attempts",
            lastException);
    }

    /// <summary>
    /// Execute operation with retry logic (void return)
    /// </summary>
    public async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        string operationKey,
        RetryPolicy? retryPolicy = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        }, operationKey, retryPolicy, cancellationToken);
    }

    private CircuitBreakerState GetOrCreateCircuitBreaker(string operationKey)
    {
        return _circuitBreakers.GetOrAdd(operationKey, _ => new CircuitBreakerState
        {
            AdapterInstanceGuid = operationKey,
            FailureThreshold = 5,
            OpenDuration = TimeSpan.FromMinutes(1),
            SuccessThreshold = 2
        });
    }

    private RetryPolicy GetDefaultRetryPolicy(string operationKey)
    {
        return _retryPolicies.GetOrAdd(operationKey, _ => new RetryPolicy
        {
            MaxRetries = 5,
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromMinutes(5),
            UseJitter = true,
            JitterPercentage = 0.1
        });
    }

    /// <summary>
    /// Get circuit breaker status for monitoring
    /// </summary>
    public CircuitBreakerState? GetCircuitBreakerStatus(string operationKey)
    {
        _circuitBreakers.TryGetValue(operationKey, out var state);
        return state;
    }

    /// <summary>
    /// Reset circuit breaker (for manual intervention)
    /// </summary>
    public void ResetCircuitBreaker(string operationKey)
    {
        if (_circuitBreakers.TryGetValue(operationKey, out var state))
        {
            state.Status = CircuitBreakerStatus.Closed;
            state.FailureCount = 0;
            state.SuccessCount = 0;
            _logger?.LogInformation("Circuit breaker reset for {OperationKey}", operationKey);
        }
    }
}







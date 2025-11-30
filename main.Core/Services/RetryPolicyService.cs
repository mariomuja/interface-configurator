using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Enhanced retry policy service with exponential backoff, jitter, and circuit breaker pattern
/// Implements IRetryPolicy interface
/// </summary>
public class RetryPolicyService : IRetryPolicy
{
    private readonly ILogger<RetryPolicyService>? _logger;
    private readonly RetryPolicyConfig _config;
    
    // Circuit breaker state per operation key
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, CircuitBreakerState> _circuitBreakers = new();

    public RetryPolicyService(
        RetryPolicyConfig? config = null,
        ILogger<RetryPolicyService>? logger = null)
    {
        _config = config ?? new RetryPolicyConfig();
        _logger = logger;
    }

    public int MaxRetryAttempts => _config.MaxRetries;
    public TimeSpan BaseDelay => _config.InitialDelay;

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
        }, cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(async ct => await operation(), "Operation", _config, cancellationToken);
    }

    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithRetryAsync(operation, operationName, _config, cancellationToken);
    }

    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string operationName,
        RetryPolicyConfig config,
        CancellationToken cancellationToken = default)
    {
        var circuitBreaker = _circuitBreakers.GetOrAdd(operationName, _ => new CircuitBreakerState(config));
        
        // Check circuit breaker
        if (circuitBreaker.IsOpen)
        {
            if (DateTime.UtcNow < circuitBreaker.OpenUntil)
            {
                _logger?.LogWarning(
                    "Circuit breaker is OPEN for {OperationName}. Skipping execution until {OpenUntil}",
                    operationName, circuitBreaker.OpenUntil);
                throw new InvalidOperationException($"Circuit breaker is open for {operationName}. Too many failures.");
            }
            else
            {
                // Try half-open state
                circuitBreaker.MoveToHalfOpen();
                _logger?.LogInformation("Circuit breaker moving to HALF-OPEN for {OperationName}", operationName);
            }
        }

        Exception? lastException = null;
        var attempt = 0;

        while (attempt < config.MaxRetries)
        {
            try
            {
                var result = await operation(cancellationToken);
                
                // Success - reset circuit breaker
                circuitBreaker.RecordSuccess();
                _logger?.LogDebug("Operation {OperationName} succeeded on attempt {Attempt}", operationName, attempt + 1);
                
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;

                // Check if error is permanent (should not retry)
                if (IsPermanentError(ex))
                {
                    circuitBreaker.RecordFailure();
                    _logger?.LogWarning(
                        "Permanent error in {OperationName} on attempt {Attempt}: {ErrorType} - {ErrorMessage}",
                        operationName, attempt, ex.GetType().Name, ex.Message);
                    throw;
                }

                // Check if should retry based on exception type
                if (!IsRetryableException(ex))
                {
                    circuitBreaker.RecordFailure();
                    _logger?.LogWarning(
                        "Non-retryable error in {OperationName} on attempt {Attempt}: {ErrorType} - {ErrorMessage}",
                        operationName, attempt, ex.GetType().Name, ex.Message);
                    throw;
                }

                // Check if we should retry
                if (attempt >= config.MaxRetries)
                {
                    circuitBreaker.RecordFailure();
                    _logger?.LogError(
                        "Operation {OperationName} failed after {Attempt} attempts: {ErrorType} - {ErrorMessage}",
                        operationName, attempt, ex.GetType().Name, ex.Message);
                    throw;
                }

                // Calculate delay with exponential backoff and jitter
                var delay = CalculateDelay(attempt, config);
                
                _logger?.LogWarning(
                    "Operation {OperationName} failed on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms. Error: {ErrorType} - {ErrorMessage}",
                    operationName, attempt, config.MaxRetries, delay.TotalMilliseconds, ex.GetType().Name, ex.Message);

                await Task.Delay(delay, cancellationToken);
            }
        }

        // Should not reach here, but handle it
        circuitBreaker.RecordFailure();
        throw lastException ?? new InvalidOperationException($"Operation {operationName} failed after {attempt} attempts");
    }

    public async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        await ExecuteWithRetryAsync(async ct =>
        {
            await operation(ct);
            return true;
        }, operationName, cancellationToken);
    }

    private TimeSpan CalculateDelay(int attempt, RetryPolicyConfig config)
    {
        // Exponential backoff: delay = initialDelay * (multiplier ^ attempt)
        var exponentialDelay = config.InitialDelay.TotalMilliseconds * Math.Pow(config.BackoffMultiplier, attempt - 1);
        
        // Apply jitter if enabled (random variation to prevent thundering herd)
        double jitteredDelay = exponentialDelay;
        if (config.UseJitter)
        {
            var random = new Random();
            var jitterRange = exponentialDelay * 0.1; // 10% jitter
            var jitter = (random.NextDouble() * 2 - 1) * jitterRange; // -10% to +10%
            jitteredDelay = exponentialDelay + jitter;
        }

        // Cap at max delay
        var finalDelay = Math.Min(jitteredDelay, config.MaxDelay.TotalMilliseconds);
        
        return TimeSpan.FromMilliseconds(finalDelay);
    }

    private bool IsPermanentError(Exception ex)
    {
        // Permanent errors that should not be retried
        return ex is ArgumentException
            || ex is ArgumentNullException
            || ex is InvalidOperationException
            || ex is UnauthorizedAccessException
            || (ex is System.Net.Http.HttpRequestException httpEx && 
                (httpEx.Message.Contains("401") || httpEx.Message.Contains("403") || httpEx.Message.Contains("404")));
    }

    private bool IsRetryableException(Exception ex)
    {
        // Retry on transient errors
        if (ex is System.Net.Http.HttpRequestException)
        {
            // Retry on 5xx errors, timeouts, and network errors
            return true;
        }

        if (ex is TaskCanceledException || ex is TimeoutException)
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

    private class CircuitBreakerState
    {
        private readonly RetryPolicyConfig _config;
        private int _failureCount;
        private DateTime _lastFailureTime;
        private CircuitBreakerStatus _status;
        private DateTime _openUntil;

        public bool IsOpen => _status == CircuitBreakerStatus.Open;
        public DateTime OpenUntil => _openUntil;

        public CircuitBreakerState(RetryPolicyConfig config)
        {
            _config = config;
            _status = CircuitBreakerStatus.Closed;
            _failureCount = 0;
        }

        public void RecordSuccess()
        {
            if (_status == CircuitBreakerStatus.HalfOpen)
            {
                // Success in half-open state - close the circuit
                _status = CircuitBreakerStatus.Closed;
                _failureCount = 0;
            }
            else if (_status == CircuitBreakerStatus.Closed)
            {
                // Reset failure count on success
                _failureCount = Math.Max(0, _failureCount - 1);
            }
        }

        public void RecordFailure()
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_status == CircuitBreakerStatus.HalfOpen)
            {
                // Failure in half-open state - open the circuit again
                _status = CircuitBreakerStatus.Open;
                _openUntil = DateTime.UtcNow.Add(_config.CircuitBreakerTimeout);
            }
            else if (_status == CircuitBreakerStatus.Closed && _failureCount >= _config.CircuitBreakerFailureThreshold)
            {
                // Too many failures - open the circuit
                _status = CircuitBreakerStatus.Open;
                _openUntil = DateTime.UtcNow.Add(_config.CircuitBreakerTimeout);
            }
        }

        public void MoveToHalfOpen()
        {
            if (_status == CircuitBreakerStatus.Open && DateTime.UtcNow >= _openUntil)
            {
                _status = CircuitBreakerStatus.HalfOpen;
            }
        }

        private enum CircuitBreakerStatus
        {
            Closed,
            Open,
            HalfOpen
        }
    }
}

/// <summary>
/// Retry policy configuration
/// </summary>
public class RetryPolicyConfig
{
    public int MaxRetries { get; set; } = 5;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public double BackoffMultiplier { get; set; } = 2.0;
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    public bool UseJitter { get; set; } = true;
    
    // Circuit breaker settings
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(1);
}


namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Circuit breaker state for adapter instances
/// Prevents cascading failures by stopping requests when a service is down
/// </summary>
public class CircuitBreakerState
{
    public string AdapterInstanceGuid { get; set; } = string.Empty;
    public CircuitBreakerStatus Status { get; set; } = CircuitBreakerStatus.Closed;
    public int FailureCount { get; set; }
    public int SuccessCount { get; set; }
    public DateTime? LastFailureTime { get; set; }
    public DateTime? LastSuccessTime { get; set; }
    public DateTime? OpenedAt { get; set; }

    /// <summary>
    /// Threshold for opening circuit (number of consecutive failures)
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time to wait before attempting to close circuit (half-open state)
    /// </summary>
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Number of successful requests needed to close circuit from half-open
    /// </summary>
    public int SuccessThreshold { get; set; } = 2;

    /// <summary>
    /// Check if circuit breaker should allow request
    /// </summary>
    public bool ShouldAllowRequest()
    {
        switch (Status)
        {
            case CircuitBreakerStatus.Closed:
                return true;

            case CircuitBreakerStatus.Open:
                // Check if enough time has passed to try half-open
                if (OpenedAt.HasValue && DateTime.UtcNow - OpenedAt.Value >= OpenDuration)
                {
                    Status = CircuitBreakerStatus.HalfOpen;
                    SuccessCount = 0;
                    return true;
                }
                return false;

            case CircuitBreakerStatus.HalfOpen:
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Record a failure
    /// </summary>
    public void RecordFailure()
    {
        FailureCount++;
        LastFailureTime = DateTime.UtcNow;

        if (Status == CircuitBreakerStatus.HalfOpen)
        {
            // Failed in half-open state, open circuit again
            Status = CircuitBreakerStatus.Open;
            OpenedAt = DateTime.UtcNow;
        }
        else if (FailureCount >= FailureThreshold)
        {
            // Too many failures, open circuit
            Status = CircuitBreakerStatus.Open;
            OpenedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Record a success
    /// </summary>
    public void RecordSuccess()
    {
        SuccessCount++;
        LastSuccessTime = DateTime.UtcNow;
        FailureCount = 0; // Reset failure count on success

        if (Status == CircuitBreakerStatus.HalfOpen)
        {
            if (SuccessCount >= SuccessThreshold)
            {
                // Enough successes, close circuit
                Status = CircuitBreakerStatus.Closed;
                SuccessCount = 0;
            }
        }
    }
}

public enum CircuitBreakerStatus
{
    Closed,    // Normal operation, requests allowed
    Open,      // Circuit is open, requests blocked
    HalfOpen   // Testing if service recovered, limited requests allowed
}











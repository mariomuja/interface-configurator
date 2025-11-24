using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Service for tracking Service Bus message locks to prevent message loss during Container App restarts
/// </summary>
public interface IServiceBusLockTrackingService
{
    /// <summary>
    /// Records a message lock when a message is received
    /// </summary>
    Task RecordMessageLockAsync(
        string messageId,
        string lockToken,
        string topicName,
        string subscriptionName,
        string interfaceName,
        Guid adapterInstanceGuid,
        DateTime lockExpiresAt,
        int deliveryCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates lock status (Completed, Abandoned, DeadLettered)
    /// </summary>
    Task UpdateLockStatusAsync(
        string messageId,
        string status,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews a message lock
    /// </summary>
    Task<bool> RenewLockAsync(
        string messageId,
        DateTime newLockExpiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active locks that need renewal
    /// </summary>
    Task<List<ServiceBusMessageLock>> GetLocksNeedingRenewalAsync(
        TimeSpan renewalThreshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all expired locks
    /// </summary>
    Task<List<ServiceBusMessageLock>> GetExpiredLocksAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old completed/abandoned/dead lettered locks
    /// </summary>
    Task<int> CleanupOldLocksAsync(
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default);
}


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for tracking Service Bus message locks to prevent message loss during Container App restarts
/// </summary>
public class ServiceBusLockTrackingService : IServiceBusLockTrackingService
{
    private readonly InterfaceConfigDbContext _context;
    private readonly ILogger<ServiceBusLockTrackingService>? _logger;

    public ServiceBusLockTrackingService(
        InterfaceConfigDbContext context,
        ILogger<ServiceBusLockTrackingService>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    public async Task RecordMessageLockAsync(
        string messageId,
        string lockToken,
        string topicName,
        string subscriptionName,
        string interfaceName,
        Guid adapterInstanceGuid,
        DateTime lockExpiresAt,
        int deliveryCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if lock already exists (might be recovery scenario)
            var existingLock = await _context.ServiceBusMessageLocks
                .FirstOrDefaultAsync(l => l.MessageId == messageId, cancellationToken);

            if (existingLock != null)
            {
                // Update existing lock
                existingLock.LockToken = lockToken;
                existingLock.LockExpiresAt = lockExpiresAt;
                existingLock.LastRenewedAt = DateTime.UtcNow;
                existingLock.DeliveryCount = deliveryCount;
                existingLock.Status = "Active";
                existingLock.UpdatedAt = DateTime.UtcNow;
                
                _logger?.LogInformation("Updated existing lock record: MessageId={MessageId}, LockToken={LockToken}", messageId, lockToken);
            }
            else
            {
                // Create new lock record
                var messageLock = new ServiceBusMessageLock
                {
                    MessageId = messageId,
                    LockToken = lockToken,
                    TopicName = topicName,
                    SubscriptionName = subscriptionName,
                    InterfaceName = interfaceName,
                    AdapterInstanceGuid = adapterInstanceGuid,
                    LockAcquiredAt = DateTime.UtcNow,
                    LockExpiresAt = lockExpiresAt,
                    Status = "Active",
                    DeliveryCount = deliveryCount,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.ServiceBusMessageLocks.Add(messageLock);
                _logger?.LogInformation("Recorded new lock: MessageId={MessageId}, LockToken={LockToken}, ExpiresAt={ExpiresAt}", 
                    messageId, lockToken, lockExpiresAt);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error recording message lock: MessageId={MessageId}", messageId);
            throw;
        }
    }

    public async Task UpdateLockStatusAsync(
        string messageId,
        string status,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messageLock = await _context.ServiceBusMessageLocks
                .FirstOrDefaultAsync(l => l.MessageId == messageId, cancellationToken);

            if (messageLock != null)
            {
                messageLock.Status = status;
                messageLock.CompletionReason = reason;
                messageLock.UpdatedAt = DateTime.UtcNow;

                if (status == "Completed" || status == "Abandoned" || status == "DeadLettered")
                {
                    messageLock.CompletedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync(cancellationToken);
                
                _logger?.LogInformation("Updated lock status: MessageId={MessageId}, Status={Status}, Reason={Reason}", 
                    messageId, status, reason);
            }
            else
            {
                _logger?.LogWarning("Lock not found for status update: MessageId={MessageId}", messageId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating lock status: MessageId={MessageId}, Status={Status}", messageId, status);
            throw;
        }
    }

    public async Task<bool> RenewLockAsync(
        string messageId,
        DateTime newLockExpiresAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messageLock = await _context.ServiceBusMessageLocks
                .FirstOrDefaultAsync(l => l.MessageId == messageId && l.Status == "Active", cancellationToken);

            if (messageLock != null)
            {
                messageLock.LockExpiresAt = newLockExpiresAt;
                messageLock.LastRenewedAt = DateTime.UtcNow;
                messageLock.RenewalCount++;
                messageLock.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);
                
                _logger?.LogDebug("Renewed lock: MessageId={MessageId}, NewExpiresAt={NewExpiresAt}, RenewalCount={RenewalCount}", 
                    messageId, newLockExpiresAt, messageLock.RenewalCount);
                
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error renewing lock: MessageId={MessageId}", messageId);
            throw;
        }
    }

    public async Task<List<ServiceBusMessageLock>> GetLocksNeedingRenewalAsync(
        TimeSpan renewalThreshold,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var thresholdTime = DateTime.UtcNow.Add(renewalThreshold);
            
            var locks = await _context.ServiceBusMessageLocks
                .Where(l => l.Status == "Active" && l.LockExpiresAt <= thresholdTime)
                .OrderBy(l => l.LockExpiresAt)
                .ToListAsync(cancellationToken);

            return locks;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting locks needing renewal");
            throw;
        }
    }

    public async Task<List<ServiceBusMessageLock>> GetExpiredLocksAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            
            var expiredLocks = await _context.ServiceBusMessageLocks
                .Where(l => l.Status == "Active" && l.LockExpiresAt < now)
                .ToListAsync(cancellationToken);

            // Mark expired locks
            foreach (var lockRecord in expiredLocks)
            {
                lockRecord.Status = "Expired";
                lockRecord.CompletedAt = DateTime.UtcNow;
                lockRecord.CompletionReason = "Lock expired";
                lockRecord.UpdatedAt = DateTime.UtcNow;
            }

            if (expiredLocks.Any())
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger?.LogWarning("Marked {Count} expired locks", expiredLocks.Count);
            }

            return expiredLocks;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting expired locks");
            throw;
        }
    }

    public async Task<int> CleanupOldLocksAsync(
        TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.Subtract(retentionPeriod);
            
            var oldLocks = await _context.ServiceBusMessageLocks
                .Where(l => 
                    (l.Status == "Completed" || l.Status == "Abandoned" || l.Status == "DeadLettered" || l.Status == "Expired") &&
                    l.CompletedAt.HasValue &&
                    l.CompletedAt.Value < cutoffDate)
                .ToListAsync(cancellationToken);

            if (oldLocks.Any())
            {
                _context.ServiceBusMessageLocks.RemoveRange(oldLocks);
                await _context.SaveChangesAsync(cancellationToken);
                
                _logger?.LogInformation("Cleaned up {Count} old lock records older than {RetentionPeriod}", 
                    oldLocks.Count, retentionPeriod);
            }

            return oldLocks.Count;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error cleaning up old locks");
            throw;
        }
    }
}


using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Interfaces;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Background service that periodically renews Service Bus message locks to prevent expiration
/// Runs every 30 seconds to renew locks that are about to expire
/// Uses cached receiver instances for efficient lock renewal
/// </summary>
public class ServiceBusLockRenewalService : BackgroundService
{
    private readonly IServiceBusLockTrackingService _lockTrackingService;
    private readonly IServiceBusReceiverCache _receiverCache;
    private readonly ILogger<ServiceBusLockRenewalService> _logger;
    private readonly TimeSpan _renewalThreshold = TimeSpan.FromSeconds(30); // Renew locks 30 seconds before expiration
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30); // Check every 30 seconds

    public ServiceBusLockRenewalService(
        IServiceBusLockTrackingService lockTrackingService,
        IServiceBusReceiverCache receiverCache,
        ILogger<ServiceBusLockRenewalService> logger)
    {
        _lockTrackingService = lockTrackingService ?? throw new ArgumentNullException(nameof(lockTrackingService));
        _receiverCache = receiverCache ?? throw new ArgumentNullException(nameof(receiverCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Service Bus Lock Renewal Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RenewLocksAsync(stoppingToken);
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Service Bus Lock Renewal Service");
                // Continue running even if there's an error
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("Service Bus Lock Renewal Service stopped");
    }

    private async Task RenewLocksAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get locks that need renewal
            var locksToRenew = await _lockTrackingService.GetLocksNeedingRenewalAsync(_renewalThreshold, cancellationToken);

            if (!locksToRenew.Any())
            {
                return;
            }

            _logger.LogDebug("Found {Count} locks needing renewal", locksToRenew.Count);

            // Group locks by topic/subscription for efficient renewal
            var locksBySubscription = locksToRenew
                .GroupBy(l => new { l.TopicName, l.SubscriptionName })
                .ToList();

            foreach (var subscriptionGroup in locksBySubscription)
            {
                var topicName = subscriptionGroup.Key.TopicName;
                var subscriptionName = subscriptionGroup.Key.SubscriptionName;
                var locks = subscriptionGroup.ToList();

                try
                {
                    // Renew locks using cached receiver instances
                    foreach (var lockRecord in locks)
                    {
                        try
                        {
                            // Renew the actual Service Bus lock using cached receiver
                            var newExpiration = await _receiverCache.RenewMessageLockAsync(
                                topicName,
                                subscriptionName,
                                lockRecord.LockToken,
                                cancellationToken);

                            if (newExpiration.HasValue)
                            {
                                // Update database record with new expiration time
                                var renewed = await _lockTrackingService.RenewLockAsync(
                                    lockRecord.MessageId,
                                    newExpiration.Value.UtcDateTime,
                                    cancellationToken);

                                if (renewed)
                                {
                                    _logger.LogDebug(
                                        "Successfully renewed lock: MessageId={MessageId}, Topic={Topic}, Subscription={Subscription}, NewExpiration={NewExpiration}",
                                        lockRecord.MessageId, topicName, subscriptionName, newExpiration.Value);
                                }
                                else
                                {
                                    _logger.LogWarning(
                                        "Lock renewed in Service Bus but database update failed: MessageId={MessageId}",
                                        lockRecord.MessageId);
                                }
                            }
                            else
                            {
                                // Lock was lost or expired
                                _logger.LogWarning(
                                    "Failed to renew lock (lock lost or expired): MessageId={MessageId}, Topic={Topic}, Subscription={Subscription}",
                                    lockRecord.MessageId, topicName, subscriptionName);

                                // Update database to mark lock as expired
                                await _lockTrackingService.UpdateLockStatusAsync(
                                    lockRecord.MessageId,
                                    "Expired",
                                    "Lock renewal failed - lock lost or expired",
                                    cancellationToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Error renewing lock for MessageId={MessageId}, Topic={Topic}, Subscription={Subscription}",
                                lockRecord.MessageId, topicName, subscriptionName);

                            // Continue with next lock instead of failing entire batch
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error renewing locks for Topic={Topic}, Subscription={Subscription}",
                        topicName, subscriptionName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in lock renewal process");
        }
    }
}


using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Interfaces;
using InterfaceConfigurator.Main.Core.Helpers;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for caching and managing Service Bus receiver instances
/// Allows efficient lock renewal by reusing receiver instances
/// </summary>
public class ServiceBusReceiverCache : IServiceBusReceiverCache, IDisposable
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<ServiceBusReceiverCache>? _logger;
    private readonly ConcurrentDictionary<string, ServiceBusReceiver> _receivers = new();
    private readonly object _lockObject = new();
    private bool _disposed = false;

    public ServiceBusReceiverCache(
        ServiceBusClient serviceBusClient,
        ILogger<ServiceBusReceiverCache>? logger = null)
    {
        _serviceBusClient = serviceBusClient ?? throw new ArgumentNullException(nameof(serviceBusClient));
        _logger = logger;
    }

    public int Count => _receivers.Count;

    public async Task<ServiceBusReceiver> GetOrCreateReceiverAsync(
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            throw new ArgumentException("Topic name cannot be empty", nameof(topicName));
        if (string.IsNullOrWhiteSpace(subscriptionName))
            throw new ArgumentException("Subscription name cannot be empty", nameof(subscriptionName));

        var key = GetCacheKey(topicName, subscriptionName);
        var correlationId = CorrelationIdHelper.Ensure();

        // Try to get existing receiver
        if (_receivers.TryGetValue(key, out var existingReceiver))
        {
            // Check if receiver is still valid (not closed/disposed)
            try
            {
                // Try to peek a message to verify receiver is still valid
                // This is a lightweight check
                _logger?.LogDebug(
                    "[CorrelationId: {CorrelationId}] Using cached receiver for Topic={Topic}, Subscription={Subscription}",
                    correlationId, topicName, subscriptionName);
                return existingReceiver;
            }
            catch (ObjectDisposedException)
            {
                // Receiver was disposed, remove from cache and create new one
                _logger?.LogWarning(
                    "[CorrelationId: {CorrelationId}] Cached receiver was disposed, creating new one: Topic={Topic}, Subscription={Subscription}",
                    correlationId, topicName, subscriptionName);
                _receivers.TryRemove(key, out _);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                // Topic or subscription doesn't exist anymore
                _logger?.LogWarning(
                    "[CorrelationId: {CorrelationId}] Topic or subscription not found, removing receiver: Topic={Topic}, Subscription={Subscription}",
                    correlationId, topicName, subscriptionName);
                _receivers.TryRemove(key, out _);
            }
        }

        // Create new receiver
        _logger?.LogDebug(
            "[CorrelationId: {CorrelationId}] Creating new receiver for Topic={Topic}, Subscription={Subscription}",
            correlationId, topicName, subscriptionName);

        var receiver = _serviceBusClient.CreateReceiver(
            topicName,
            subscriptionName,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                PrefetchCount = 0 // Don't prefetch to avoid unnecessary message locks
            });

        // Add to cache
        _receivers.TryAdd(key, receiver);

        return receiver;
    }

    public async Task<DateTimeOffset?> RenewMessageLockAsync(
        string topicName,
        string subscriptionName,
        string lockToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            throw new ArgumentException("Topic name cannot be empty", nameof(topicName));
        if (string.IsNullOrWhiteSpace(subscriptionName))
            throw new ArgumentException("Subscription name cannot be empty", nameof(subscriptionName));
        if (string.IsNullOrWhiteSpace(lockToken))
            throw new ArgumentException("Lock token cannot be empty", nameof(lockToken));

        var correlationId = CorrelationIdHelper.Ensure();

        try
        {
            // Get or create receiver
            var receiver = await GetOrCreateReceiverAsync(topicName, subscriptionName, cancellationToken);

            // Renew the lock
            var newExpiration = await receiver.RenewMessageLockAsync(lockToken, cancellationToken);

            _logger?.LogDebug(
                "[CorrelationId: {CorrelationId}] Successfully renewed lock: Topic={Topic}, Subscription={Subscription}, LockToken={LockToken}, NewExpiration={NewExpiration}",
                correlationId, topicName, subscriptionName, lockToken, newExpiration);

            return newExpiration;
        }
        catch (ServiceBusException ex)
        {
            if (ex.Reason == ServiceBusFailureReason.MessageLockLost)
            {
                _logger?.LogWarning(
                    "[CorrelationId: {CorrelationId}] Lock already expired or lost: Topic={Topic}, Subscription={Subscription}, LockToken={LockToken}",
                    correlationId, topicName, subscriptionName, lockToken);
                return null;
            }
            else if (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                _logger?.LogWarning(
                    "[CorrelationId: {CorrelationId}] Topic or subscription not found: Topic={Topic}, Subscription={Subscription}",
                    correlationId, topicName, subscriptionName);
                RemoveReceiver(topicName, subscriptionName);
                return null;
            }

            _logger?.LogError(ex,
                "[CorrelationId: {CorrelationId}] Error renewing lock: Topic={Topic}, Subscription={Subscription}, LockToken={LockToken}",
                correlationId, topicName, subscriptionName, lockToken);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "[CorrelationId: {CorrelationId}] Unexpected error renewing lock: Topic={Topic}, Subscription={Subscription}, LockToken={LockToken}",
                correlationId, topicName, subscriptionName, lockToken);
            throw;
        }
    }

    public void RemoveReceiver(string topicName, string subscriptionName)
    {
        var key = GetCacheKey(topicName, subscriptionName);
        if (_receivers.TryRemove(key, out var receiver))
        {
            try
            {
                receiver.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex,
                    "Error disposing receiver: Topic={Topic}, Subscription={Subscription}",
                    topicName, subscriptionName);
            }
        }
    }

    public void Clear()
    {
        var receivers = _receivers.Values.ToList();
        _receivers.Clear();

        foreach (var receiver in receivers)
        {
            try
            {
                receiver.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disposing receiver during clear");
            }
        }
    }

    private static string GetCacheKey(string topicName, string subscriptionName)
    {
        return $"{topicName}|{subscriptionName}";
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Clear();
            _disposed = true;
        }
    }
}



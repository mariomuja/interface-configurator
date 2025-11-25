namespace InterfaceConfigurator.Main.Interfaces;

/// <summary>
/// Service for caching and managing Service Bus receiver instances
/// Allows efficient lock renewal by reusing receiver instances
/// </summary>
public interface IServiceBusReceiverCache
{
    /// <summary>
    /// Gets or creates a receiver for a topic/subscription combination
    /// </summary>
    /// <param name="topicName">Topic name</param>
    /// <param name="subscriptionName">Subscription name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ServiceBusReceiver instance</returns>
    Task<Azure.Messaging.ServiceBus.ServiceBusReceiver> GetOrCreateReceiverAsync(
        string topicName,
        string subscriptionName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews a message lock using the cached receiver
    /// </summary>
    /// <param name="topicName">Topic name</param>
    /// <param name="subscriptionName">Subscription name</param>
    /// <param name="lockToken">Lock token from the message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New expiration time if successful, null otherwise</returns>
    Task<DateTimeOffset?> RenewMessageLockAsync(
        string topicName,
        string subscriptionName,
        string lockToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a receiver from cache (e.g., when it's disposed or closed)
    /// </summary>
    /// <param name="topicName">Topic name</param>
    /// <param name="subscriptionName">Subscription name</param>
    void RemoveReceiver(string topicName, string subscriptionName);

    /// <summary>
    /// Clears all cached receivers
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets the number of cached receivers
    /// </summary>
    int Count { get; }
}


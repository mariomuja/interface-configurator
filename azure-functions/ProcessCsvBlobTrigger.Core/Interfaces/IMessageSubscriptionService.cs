namespace ProcessCsvBlobTrigger.Core.Interfaces;

/// <summary>
/// Service for managing message subscriptions and tracking which adapters have processed messages
/// </summary>
public interface IMessageSubscriptionService
{
    /// <summary>
    /// Creates a subscription for a destination adapter to process messages for an interface
    /// </summary>
    /// <param name="messageId">MessageId</param>
    /// <param name="interfaceName">Interface name</param>
    /// <param name="subscriberAdapterName">Name of the subscribing adapter (e.g., "SqlServer")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateSubscriptionAsync(Guid messageId, string interfaceName, string subscriberAdapterName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a subscription as processed (successful)
    /// </summary>
    /// <param name="messageId">MessageId</param>
    /// <param name="subscriberAdapterName">Name of the adapter that processed the message</param>
    /// <param name="processingDetails">Optional processing details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkSubscriptionAsProcessedAsync(Guid messageId, string subscriberAdapterName, string? processingDetails = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a subscription as error
    /// </summary>
    /// <param name="messageId">MessageId</param>
    /// <param name="subscriberAdapterName">Name of the adapter that encountered the error</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkSubscriptionAsErrorAsync(Guid messageId, string subscriberAdapterName, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if all subscriptions for a message have been processed successfully
    /// </summary>
    /// <param name="messageId">MessageId</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if all subscriptions are processed, false otherwise</returns>
    Task<bool> AreAllSubscriptionsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending subscriptions for a message
    /// </summary>
    /// <param name="messageId">MessageId</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending subscriber adapter names</returns>
    Task<List<string>> GetPendingSubscribersAsync(Guid messageId, CancellationToken cancellationToken = default);
}





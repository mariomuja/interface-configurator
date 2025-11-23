namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Service for tracking message processing status
/// This tracks which messages have been processed by which adapters (separate from subscriptions)
/// </summary>
public interface IMessageProcessingService
{
    /// <summary>
    /// Creates a processing record when an adapter starts processing a message
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="adapterInstanceGuid">Adapter instance GUID</param>
    /// <param name="interfaceName">Interface name</param>
    /// <param name="adapterName">Adapter name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateProcessingRecordAsync(
        Guid messageId,
        Guid adapterInstanceGuid,
        string interfaceName,
        string adapterName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as successfully processed by an adapter
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="adapterInstanceGuid">Adapter instance GUID</param>
    /// <param name="processingDetails">Optional processing details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsProcessedAsync(
        Guid messageId,
        Guid adapterInstanceGuid,
        string? processingDetails = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as error during processing
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="adapterInstanceGuid">Adapter instance GUID</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsErrorAsync(
        Guid messageId,
        Guid adapterInstanceGuid,
        string errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if all adapters that have subscriptions for a message have processed it
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if all subscribed adapters have processed the message, false otherwise</returns>
    Task<bool> AreAllSubscribedAdaptersProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all adapters that have not yet processed a message
    /// </summary>
    /// <param name="messageId">Message ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of adapter instance GUIDs that still need to process the message</returns>
    Task<List<Guid>> GetPendingAdapterInstancesAsync(Guid messageId, CancellationToken cancellationToken = default);
}



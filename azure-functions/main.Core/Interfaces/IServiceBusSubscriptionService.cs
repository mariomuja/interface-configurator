namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Service for managing Azure Service Bus subscriptions for destination adapter instances
/// Creates subscriptions when instances are enabled, deletes them when disabled
/// </summary>
public interface IServiceBusSubscriptionService
{
    /// <summary>
    /// Creates a Service Bus subscription for a destination adapter instance
    /// Subscription name: destination-{adapterInstanceGuid}
    /// Topic name: interface-{interfaceName}
    /// </summary>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="adapterInstanceGuid">GUID of the destination adapter instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CreateSubscriptionAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a Service Bus subscription for a destination adapter instance
    /// </summary>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="adapterInstanceGuid">GUID of the destination adapter instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteSubscriptionAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures a Service Bus topic exists for an interface
    /// Creates the topic if it doesn't exist
    /// </summary>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnsureTopicExistsAsync(
        string interfaceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a subscription exists for a destination adapter instance
    /// </summary>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="adapterInstanceGuid">GUID of the destination adapter instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if subscription exists, false otherwise</returns>
    Task<bool> SubscriptionExistsAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default);
}


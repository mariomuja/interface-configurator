using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Service for managing adapter subscriptions (BizTalk-style)
/// Subscriptions define filter criteria for which messages an adapter receives from the MessageBox
/// This is a configuration, not a tracking record
/// </summary>
public interface IAdapterSubscriptionService
{
    /// <summary>
    /// Creates or updates a subscription for a destination adapter
    /// A subscription defines which messages the adapter is interested in receiving
    /// </summary>
    /// <param name="adapterInstanceGuid">Adapter instance GUID</param>
    /// <param name="interfaceName">Interface name to filter by</param>
    /// <param name="adapterName">Adapter name (e.g., "SqlServer")</param>
    /// <param name="filterCriteria">Optional JSON filter criteria for advanced filtering</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<AdapterSubscription> CreateOrUpdateSubscriptionAsync(
        Guid adapterInstanceGuid,
        string interfaceName,
        string adapterName,
        string? filterCriteria = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active subscriptions for a given interface name
    /// Used to determine which adapters should receive messages for an interface
    /// </summary>
    /// <param name="interfaceName">Interface name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active subscriptions</returns>
    Task<List<AdapterSubscription>> GetSubscriptionsForInterfaceAsync(
        string interfaceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all subscriptions for a specific adapter instance
    /// </summary>
    /// <param name="adapterInstanceGuid">Adapter instance GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of subscriptions for this adapter</returns>
    Task<List<AdapterSubscription>> GetSubscriptionsForAdapterAsync(
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables a subscription
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="isEnabled">Whether to enable or disable</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnableSubscriptionAsync(int subscriptionId, bool isEnabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a subscription
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken = default);
}



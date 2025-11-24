using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for managing Azure Service Bus subscriptions for destination adapter instances
/// Creates subscriptions when instances are enabled, deletes them when disabled
/// </summary>
public class ServiceBusSubscriptionService : IServiceBusSubscriptionService
{
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ILogger<ServiceBusSubscriptionService>? _logger;
    private const string TopicNamePrefix = "interface-";
    private const string SubscriptionNamePrefix = "destination-";

    public ServiceBusSubscriptionService(
        string connectionString,
        ILogger<ServiceBusSubscriptionService>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Service Bus connection string cannot be empty", nameof(connectionString));
        }

        _logger = logger;
        _adminClient = new ServiceBusAdministrationClient(connectionString);
    }

    public async Task EnsureTopicExistsAsync(
        string interfaceName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));

        try
        {
            var topicName = GetTopicName(interfaceName);
            
            // Check if topic exists
            if (!await _adminClient.TopicExistsAsync(topicName, cancellationToken))
            {
                _logger?.LogInformation("Creating Service Bus topic: {TopicName} for interface '{InterfaceName}'", topicName, interfaceName);
                
                var topicOptions = new CreateTopicOptions(topicName)
                {
                    DefaultMessageTimeToLive = TimeSpan.FromDays(7), // Messages expire after 7 days
                    MaxSizeInMegabytes = 1024, // 1 GB max size
                    EnablePartitioning = false
                };

                await _adminClient.CreateTopicAsync(topicOptions, cancellationToken);
                
                _logger?.LogInformation("Successfully created Service Bus topic: {TopicName}", topicName);
            }
            else
            {
                _logger?.LogDebug("Service Bus topic already exists: {TopicName}", topicName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error ensuring Service Bus topic exists: Interface={InterfaceName}", interfaceName);
            throw;
        }
    }

    public async Task CreateSubscriptionAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));
        if (adapterInstanceGuid == Guid.Empty)
            throw new ArgumentException("Adapter instance GUID cannot be empty", nameof(adapterInstanceGuid));

        try
        {
            // Ensure topic exists first
            await EnsureTopicExistsAsync(interfaceName, cancellationToken);

            var topicName = GetTopicName(interfaceName);
            var subscriptionName = GetSubscriptionName(adapterInstanceGuid);

            // Check if subscription already exists
            if (await SubscriptionExistsAsync(interfaceName, adapterInstanceGuid, cancellationToken))
            {
                _logger?.LogInformation("Service Bus subscription already exists: Topic={TopicName}, Subscription={SubscriptionName}", 
                    topicName, subscriptionName);
                return;
            }

            _logger?.LogInformation("Creating Service Bus subscription: Topic={TopicName}, Subscription={SubscriptionName} for interface '{InterfaceName}', adapter instance {AdapterInstanceGuid}", 
                topicName, subscriptionName, interfaceName, adapterInstanceGuid);

            var subscriptionOptions = new CreateSubscriptionOptions(topicName, subscriptionName)
            {
                DefaultMessageTimeToLive = TimeSpan.FromDays(7),
                MaxDeliveryCount = 10, // Retry up to 10 times before dead lettering
                DeadLetteringOnMessageExpiration = true,
                LockDuration = TimeSpan.FromMinutes(5) // Lock duration for message processing
            };

            // Create subscription with default rule (accept all messages)
            await _adminClient.CreateSubscriptionAsync(subscriptionOptions, cancellationToken);

            _logger?.LogInformation("Successfully created Service Bus subscription: Topic={TopicName}, Subscription={SubscriptionName}", 
                topicName, subscriptionName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating Service Bus subscription: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}", 
                interfaceName, adapterInstanceGuid);
            throw;
        }
    }

    public async Task DeleteSubscriptionAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));
        if (adapterInstanceGuid == Guid.Empty)
            throw new ArgumentException("Adapter instance GUID cannot be empty", nameof(adapterInstanceGuid));

        try
        {
            var topicName = GetTopicName(interfaceName);
            var subscriptionName = GetSubscriptionName(adapterInstanceGuid);

            // Check if subscription exists
            if (!await SubscriptionExistsAsync(interfaceName, adapterInstanceGuid, cancellationToken))
            {
                _logger?.LogInformation("Service Bus subscription does not exist, skipping deletion: Topic={TopicName}, Subscription={SubscriptionName}", 
                    topicName, subscriptionName);
                return;
            }

            _logger?.LogInformation("Deleting Service Bus subscription: Topic={TopicName}, Subscription={SubscriptionName} for interface '{InterfaceName}', adapter instance {AdapterInstanceGuid}", 
                topicName, subscriptionName, interfaceName, adapterInstanceGuid);

            await _adminClient.DeleteSubscriptionAsync(topicName, subscriptionName, cancellationToken);

            _logger?.LogInformation("Successfully deleted Service Bus subscription: Topic={TopicName}, Subscription={SubscriptionName}", 
                topicName, subscriptionName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting Service Bus subscription: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}", 
                interfaceName, adapterInstanceGuid);
            throw;
        }
    }

    public async Task<bool> SubscriptionExistsAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));
        if (adapterInstanceGuid == Guid.Empty)
            throw new ArgumentException("Adapter instance GUID cannot be empty", nameof(adapterInstanceGuid));

        try
        {
            var topicName = GetTopicName(interfaceName);
            var subscriptionName = GetSubscriptionName(adapterInstanceGuid);

            return await _adminClient.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking if subscription exists: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}", 
                interfaceName, adapterInstanceGuid);
            throw;
        }
    }

    private static string GetTopicName(string interfaceName)
    {
        return $"{TopicNamePrefix}{interfaceName.ToLowerInvariant()}";
    }

    private static string GetSubscriptionName(Guid adapterInstanceGuid)
    {
        return $"{SubscriptionNamePrefix}{adapterInstanceGuid.ToString().ToLowerInvariant()}";
    }
}


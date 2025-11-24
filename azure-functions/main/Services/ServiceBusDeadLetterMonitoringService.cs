using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using InterfaceConfigurator.Main.Core.Interfaces;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Background service that monitors Service Bus Dead Letter Queues
/// Checks for dead-lettered messages and logs alerts
/// Runs every 5 minutes
/// </summary>
public class ServiceBusDeadLetterMonitoringService : BackgroundService
{
    private readonly ILogger<ServiceBusDeadLetterMonitoringService> _logger;
    private readonly string _connectionString;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(5);

    public ServiceBusDeadLetterMonitoringService(
        ILogger<ServiceBusDeadLetterMonitoringService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _connectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString")
            ?? Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING") ?? string.Empty;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _logger.LogWarning("Service Bus connection string not configured. Dead Letter monitoring disabled.");
            return;
        }

        _logger.LogInformation("Service Bus Dead Letter Monitoring Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MonitorDeadLetterQueuesAsync(stoppingToken);
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Service Bus Dead Letter Monitoring Service");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Service Bus Dead Letter Monitoring Service stopped");
    }

    private async Task MonitorDeadLetterQueuesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var adminClient = new ServiceBusAdministrationClient(_connectionString);
            
            // Get all topics
            await foreach (var topicProperties in adminClient.GetTopicsAsync(cancellationToken))
            {
                try
                {
                    // Get all subscriptions for this topic
                    await foreach (var subscriptionProperties in adminClient.GetSubscriptionsAsync(topicProperties.Name, cancellationToken))
                    {
                        try
                        {
                            // Get runtime properties to check dead letter count
                            var runtimeProperties = await adminClient.GetSubscriptionRuntimePropertiesAsync(
                                topicProperties.Name, 
                                subscriptionProperties.Name, 
                                cancellationToken);

                            var deadLetterCount = runtimeProperties.Value.DeadLetterMessageCount;
                            
                            if (deadLetterCount > 0)
                            {
                                _logger.LogWarning(
                                    "Dead Letter Queue Alert: Topic={Topic}, Subscription={Subscription}, DeadLetterCount={DeadLetterCount}",
                                    topicProperties.Name, subscriptionProperties.Name, deadLetterCount);

                                // Try to peek at dead-lettered messages to get details
                                try
                                {
                                    await using var client = new ServiceBusClient(_connectionString);
                                    await using var receiver = client.CreateReceiver(
                                        topicProperties.Name,
                                        subscriptionProperties.Name,
                                        new ServiceBusReceiverOptions
                                        {
                                            SubQueue = SubQueue.DeadLetter,
                                            ReceiveMode = ServiceBusReceiveMode.PeekLock
                                        });

                                    var deadLetterMessages = await receiver.PeekMessagesAsync(5, cancellationToken: cancellationToken);
                                    
                                    foreach (var message in deadLetterMessages)
                                    {
                                        var reason = message.DeadLetterReason ?? "Unknown";
                                        var errorDescription = message.DeadLetterErrorDescription ?? "No description";
                                        
                                        _logger.LogWarning(
                                            "Dead Letter Message Details: MessageId={MessageId}, Reason={Reason}, Error={Error}, DeliveryCount={DeliveryCount}, EnqueuedTime={EnqueuedTime}",
                                            message.MessageId, reason, errorDescription, message.DeliveryCount, message.EnqueuedTime);
                                    }
                                }
                                catch (Exception peekEx)
                                {
                                    _logger.LogWarning(peekEx, "Failed to peek dead-lettered messages for Topic={Topic}, Subscription={Subscription}",
                                        topicProperties.Name, subscriptionProperties.Name);
                                }
                            }
                        }
                        catch (Exception subEx)
                        {
                            _logger.LogWarning(subEx, "Error checking subscription: Topic={Topic}, Subscription={Subscription}",
                                topicProperties.Name, subscriptionProperties.Name);
                        }
                    }
                }
                catch (Exception topicEx)
                {
                    _logger.LogWarning(topicEx, "Error checking topic: Topic={Topic}", topicProperties.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring dead letter queues");
        }
    }
}


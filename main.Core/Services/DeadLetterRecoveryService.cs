using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Dead Letter Recovery Service for reprocessing failed messages
/// </summary>
public interface IDeadLetterRecoveryService
{
    /// <summary>
    /// Get dead letter messages for an interface
    /// </summary>
    Task<List<DeadLetterMessage>> GetDeadLetterMessagesAsync(
        string interfaceName,
        int maxMessages = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reprocess a dead letter message
    /// </summary>
    Task<bool> ReprocessDeadLetterMessageAsync(
        string messageId,
        string interfaceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reprocess multiple dead letter messages
    /// </summary>
    Task<DeadLetterRecoveryResult> ReprocessDeadLetterMessagesAsync(
        string interfaceName,
        List<string> messageIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get dead letter statistics
    /// </summary>
    Task<DeadLetterStatistics> GetDeadLetterStatisticsAsync(
        string interfaceName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Dead letter message
/// </summary>
public class DeadLetterMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string InterfaceName { get; set; } = string.Empty;
    public string AdapterName { get; set; } = string.Empty;
    public string DeadLetterReason { get; set; } = string.Empty;
    public string DeadLetterErrorDescription { get; set; } = string.Empty;
    public DateTime EnqueuedTime { get; set; }
    public DateTime DeadLetteredTime { get; set; }
    public int DeliveryCount { get; set; }
    public Dictionary<string, string> Record { get; set; } = new();
    public List<string> Headers { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Dead letter recovery result
/// </summary>
public class DeadLetterRecoveryResult
{
    public int TotalMessages { get; set; }
    public int SuccessfullyReprocessed { get; set; }
    public int Failed { get; set; }
    public List<string> FailedMessageIds { get; set; } = new();
    public List<string> ErrorMessages { get; set; } = new();
}

/// <summary>
/// Dead letter statistics
/// </summary>
public class DeadLetterStatistics
{
    public string InterfaceName { get; set; } = string.Empty;
    public int TotalDeadLetterMessages { get; set; }
    public Dictionary<string, int> DeadLetterReasons { get; set; } = new();
    public Dictionary<string, int> DeadLetterByAdapter { get; set; } = new();
    public DateTime? OldestDeadLetterTime { get; set; }
    public DateTime? NewestDeadLetterTime { get; set; }
}

/// <summary>
/// Dead Letter Recovery Service implementation
/// </summary>
public class DeadLetterRecoveryService : IDeadLetterRecoveryService
{
    private readonly IServiceBusService _serviceBusService;
    private readonly ILogger<DeadLetterRecoveryService>? _logger;
    private readonly string _serviceBusConnectionString;

    public DeadLetterRecoveryService(
        IServiceBusService serviceBusService,
        string serviceBusConnectionString,
        ILogger<DeadLetterRecoveryService>? logger = null)
    {
        _serviceBusService = serviceBusService;
        _serviceBusConnectionString = serviceBusConnectionString;
        _logger = logger;
    }

    public async Task<List<DeadLetterMessage>> GetDeadLetterMessagesAsync(
        string interfaceName,
        int maxMessages = 100,
        CancellationToken cancellationToken = default)
    {
        var deadLetterMessages = new List<DeadLetterMessage>();

        try
        {
            // Use Service Bus Administration Client to access dead letter queue
            var adminClient = new ServiceBusAdministrationClient(_serviceBusConnectionString);
            var topicName = $"interface-{interfaceName.ToLowerInvariant()}";

            // Get all subscriptions for this topic
            await foreach (var subscription in adminClient.GetSubscriptionsAsync(topicName, cancellationToken))
            {
                try
                {
                    // Create receiver for dead letter queue
                    var serviceBusClient = new ServiceBusClient(_serviceBusConnectionString);
                    var deadLetterReceiver = serviceBusClient.CreateReceiver(
                        topicName,
                        subscription.SubscriptionName,
                        new ServiceBusReceiverOptions
                        {
                            SubQueue = SubQueue.DeadLetter,
                            ReceiveMode = ServiceBusReceiveMode.PeekLock
                        });

                    // Receive dead letter messages
                    var receivedMessages = await deadLetterReceiver.ReceiveMessagesAsync(
                        maxMessages,
                        TimeSpan.FromSeconds(30),
                        cancellationToken);

                    foreach (var message in receivedMessages)
                    {
                        try
                        {
                            var deadLetterMessage = await ConvertToDeadLetterMessageAsync(message, interfaceName);
                            deadLetterMessages.Add(deadLetterMessage);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error converting dead letter message {MessageId}", message.MessageId);
                        }
                    }

                    await deadLetterReceiver.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error accessing dead letter queue for subscription {SubscriptionName}", subscription.SubscriptionName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting dead letter messages for interface {InterfaceName}", interfaceName);
            throw;
        }

        _logger?.LogInformation(
            "Retrieved {Count} dead letter messages for interface {InterfaceName}",
            deadLetterMessages.Count, interfaceName);

        return deadLetterMessages;
    }

    public async Task<bool> ReprocessDeadLetterMessageAsync(
        string messageId,
        string interfaceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get dead letter message
            var deadLetterMessages = await GetDeadLetterMessagesAsync(interfaceName, 1000, cancellationToken);
            var deadLetterMessage = deadLetterMessages.FirstOrDefault(m => m.MessageId == messageId);

            if (deadLetterMessage == null)
            {
                _logger?.LogWarning("Dead letter message {MessageId} not found", messageId);
                return false;
            }

            // Resend message to Service Bus
            await _serviceBusService.SendMessageAsync(
                deadLetterMessage.InterfaceName,
                deadLetterMessage.AdapterName,
                "Source", // Assume source adapter
                Guid.Empty, // Will need to extract from properties
                deadLetterMessage.Headers,
                deadLetterMessage.Record,
                cancellationToken);

            _logger?.LogInformation(
                "Successfully reprocessed dead letter message {MessageId} for interface {InterfaceName}",
                messageId, interfaceName);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error reprocessing dead letter message {MessageId} for interface {InterfaceName}",
                messageId, interfaceName);
            return false;
        }
    }

    public async Task<DeadLetterRecoveryResult> ReprocessDeadLetterMessagesAsync(
        string interfaceName,
        List<string> messageIds,
        CancellationToken cancellationToken = default)
    {
        var result = new DeadLetterRecoveryResult
        {
            TotalMessages = messageIds.Count
        };

        foreach (var messageId in messageIds)
        {
            try
            {
                var success = await ReprocessDeadLetterMessageAsync(messageId, interfaceName, cancellationToken);
                if (success)
                {
                    result.SuccessfullyReprocessed++;
                }
                else
                {
                    result.Failed++;
                    result.FailedMessageIds.Add(messageId);
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.FailedMessageIds.Add(messageId);
                result.ErrorMessages.Add($"Message {messageId}: {ex.Message}");
                _logger?.LogError(ex, "Error reprocessing dead letter message {MessageId}", messageId);
            }
        }

        _logger?.LogInformation(
            "Dead letter recovery completed for interface {InterfaceName}: {SuccessCount}/{TotalCount} messages reprocessed",
            interfaceName, result.SuccessfullyReprocessed, result.TotalMessages);

        return result;
    }

    public async Task<DeadLetterStatistics> GetDeadLetterStatisticsAsync(
        string interfaceName,
        CancellationToken cancellationToken = default)
    {
        var statistics = new DeadLetterStatistics
        {
            InterfaceName = interfaceName
        };

        try
        {
            var deadLetterMessages = await GetDeadLetterMessagesAsync(interfaceName, 10000, cancellationToken);

            statistics.TotalDeadLetterMessages = deadLetterMessages.Count;

            foreach (var message in deadLetterMessages)
            {
                // Count by reason
                var reason = message.DeadLetterReason ?? "Unknown";
                statistics.DeadLetterReasons.TryGetValue(reason, out var reasonCount);
                statistics.DeadLetterReasons[reason] = reasonCount + 1;

                // Count by adapter
                var adapter = message.AdapterName ?? "Unknown";
                statistics.DeadLetterByAdapter.TryGetValue(adapter, out var adapterCount);
                statistics.DeadLetterByAdapter[adapter] = adapterCount + 1;

                // Track oldest/newest
                if (!statistics.OldestDeadLetterTime.HasValue || 
                    message.DeadLetteredTime < statistics.OldestDeadLetterTime.Value)
                {
                    statistics.OldestDeadLetterTime = message.DeadLetteredTime;
                }

                if (!statistics.NewestDeadLetterTime.HasValue || 
                    message.DeadLetteredTime > statistics.NewestDeadLetterTime.Value)
                {
                    statistics.NewestDeadLetterTime = message.DeadLetteredTime;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting dead letter statistics for interface {InterfaceName}", interfaceName);
        }

        return statistics;
    }

    private async Task<DeadLetterMessage> ConvertToDeadLetterMessageAsync(
        ServiceBusReceivedMessage message,
        string interfaceName)
    {
        // Deserialize message body
        var messageBody = message.Body.ToString();
        var messageData = System.Text.Json.JsonSerializer.Deserialize<MessageData>(messageBody);

        return new DeadLetterMessage
        {
            MessageId = message.MessageId,
            InterfaceName = message.ApplicationProperties.TryGetValue("InterfaceName", out var ifName)
                ? ifName.ToString() ?? interfaceName
                : interfaceName,
            AdapterName = message.ApplicationProperties.TryGetValue("AdapterName", out var adName)
                ? adName.ToString() ?? string.Empty
                : string.Empty,
            DeadLetterReason = message.DeadLetterReason ?? "Unknown",
            DeadLetterErrorDescription = message.DeadLetterErrorDescription ?? string.Empty,
            EnqueuedTime = message.EnqueuedTime.DateTime,
            DeadLetteredTime = DateTime.UtcNow, // Approximate
            DeliveryCount = message.DeliveryCount,
            Record = messageData?.record ?? new Dictionary<string, string>(),
            Headers = messageData?.headers ?? new List<string>(),
            Properties = message.ApplicationProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    private class MessageData
    {
        public List<string>? headers { get; set; }
        public Dictionary<string, string>? record { get; set; }
    }
}


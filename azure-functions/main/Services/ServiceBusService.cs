using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using CustomServiceBusMessage = InterfaceConfigurator.Main.Core.Interfaces.ServiceBusMessage;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for reading and writing messages to Azure Service Bus
/// Replaces MessageBox database with Service Bus queues/topics
/// </summary>
public class ServiceBusService : IServiceBusService
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<ServiceBusService>? _logger;
    private readonly string _connectionString;
    private const string TopicNamePrefix = "interface-";
    private const string SubscriptionNamePrefix = "destination-";

    public ServiceBusService(
        string connectionString,
        ILogger<ServiceBusService>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Service Bus connection string cannot be empty", nameof(connectionString));
        }

        _connectionString = connectionString;
        _logger = logger;
        _serviceBusClient = new ServiceBusClient(connectionString);
    }

    public async Task<string> SendMessageAsync(
        string interfaceName,
        string adapterName,
        string adapterType,
        Guid adapterInstanceGuid,
        List<string> headers,
        Dictionary<string, string> record,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));
        if (string.IsNullOrWhiteSpace(adapterName))
            throw new ArgumentException("Adapter name cannot be empty", nameof(adapterName));
        if (string.IsNullOrWhiteSpace(adapterType))
            throw new ArgumentException("Adapter type cannot be empty", nameof(adapterType));
        if (record == null)
            throw new ArgumentNullException(nameof(record));

        try
        {
            _logger?.LogInformation(
                "Sending message to Service Bus: Interface={InterfaceName}, Adapter={AdapterName}, Type={AdapterType}, AdapterInstanceGuid={AdapterInstanceGuid}",
                interfaceName, adapterName, adapterType, adapterInstanceGuid);

            // Serialize message data
            var messageData = new
            {
                headers = headers ?? new List<string>(),
                record = record
            };
            var messageDataJson = JsonSerializer.Serialize(messageData);

            // Create topic name: interface-{interfaceName}
            var topicName = $"{TopicNamePrefix}{interfaceName.ToLowerInvariant()}";

            // Create sender for the topic
            await using var sender = _serviceBusClient.CreateSender(topicName);

            // Create Service Bus message
            var messageId = Guid.NewGuid().ToString();
            var serviceBusMessage = new Azure.Messaging.ServiceBus.ServiceBusMessage(messageDataJson)
            {
                MessageId = messageId,
                Subject = adapterName,
                ContentType = "application/json"
            };

            // Add custom properties
            serviceBusMessage.ApplicationProperties.Add("InterfaceName", interfaceName);
            serviceBusMessage.ApplicationProperties.Add("AdapterName", adapterName);
            serviceBusMessage.ApplicationProperties.Add("AdapterType", adapterType);
            serviceBusMessage.ApplicationProperties.Add("AdapterInstanceGuid", adapterInstanceGuid.ToString());
            serviceBusMessage.ApplicationProperties.Add("MessageHash", CalculateMessageHash(messageDataJson));

            // Send message
            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            _logger?.LogInformation(
                "Successfully sent message to Service Bus: MessageId={MessageId}, Interface={InterfaceName}, Topic={TopicName}",
                messageId, interfaceName, topicName);

            return messageId;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error sending message to Service Bus: Interface={InterfaceName}, Adapter={AdapterName}",
                interfaceName, adapterName);
            throw;
        }
    }

    public async Task<List<string>> SendMessagesAsync(
        string interfaceName,
        string adapterName,
        string adapterType,
        Guid adapterInstanceGuid,
        List<string> headers,
        List<Dictionary<string, string>> records,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));
        if (string.IsNullOrWhiteSpace(adapterName))
            throw new ArgumentException("Adapter name cannot be empty", nameof(adapterName));
        if (string.IsNullOrWhiteSpace(adapterType))
            throw new ArgumentException("Adapter type cannot be empty", nameof(adapterType));
        if (records == null)
            throw new ArgumentNullException(nameof(records));

        var messageIds = new List<string>();

        _logger?.LogInformation(
            "Debatching {RecordCount} records into individual Service Bus messages: Interface={InterfaceName}, Adapter={AdapterName}, AdapterInstanceGuid={AdapterInstanceGuid}",
            records.Count, interfaceName, adapterName, adapterInstanceGuid);

        // Create topic name
        var topicName = $"{TopicNamePrefix}{interfaceName.ToLowerInvariant()}";
        await using var sender = _serviceBusClient.CreateSender(topicName);

        // Batch send messages for better performance
        var batch = await sender.CreateMessageBatchAsync(cancellationToken);
        var successCount = 0;
        var errorCount = 0;

        foreach (var record in records)
        {
            try
            {
                // Serialize message data
                var messageData = new
                {
                    headers = headers ?? new List<string>(),
                    record = record
                };
                var messageDataJson = JsonSerializer.Serialize(messageData);

                var messageId = Guid.NewGuid().ToString();
                var serviceBusMessage = new Azure.Messaging.ServiceBus.ServiceBusMessage(messageDataJson)
                {
                    MessageId = messageId,
                    Subject = adapterName,
                    ContentType = "application/json"
                };

                // Add custom properties
                serviceBusMessage.ApplicationProperties.Add("InterfaceName", interfaceName);
                serviceBusMessage.ApplicationProperties.Add("AdapterName", adapterName);
                serviceBusMessage.ApplicationProperties.Add("AdapterType", adapterType);
                serviceBusMessage.ApplicationProperties.Add("AdapterInstanceGuid", adapterInstanceGuid.ToString());
                serviceBusMessage.ApplicationProperties.Add("MessageHash", CalculateMessageHash(messageDataJson));

                // Try to add to batch
                if (!batch.TryAddMessage(serviceBusMessage))
                {
                    // Batch is full, send it and create a new one
                    await sender.SendMessagesAsync(batch, cancellationToken);
                    batch.Dispose();
                    batch = await sender.CreateMessageBatchAsync(cancellationToken);
                    
                    // Add message to new batch
                    if (!batch.TryAddMessage(serviceBusMessage))
                    {
                        throw new InvalidOperationException("Message is too large for Service Bus batch");
                    }
                }

                messageIds.Add(messageId);
                successCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger?.LogError(ex,
                    "Failed to add message to batch: Interface={InterfaceName}, Adapter={AdapterName}, RecordIndex={RecordIndex}",
                    interfaceName, adapterName, successCount + errorCount);
            }
        }

        // Send remaining messages in batch
        if (batch.Count > 0)
        {
            await sender.SendMessagesAsync(batch, cancellationToken);
        }
        batch.Dispose();

        _logger?.LogInformation(
            "Completed debatching: {TotalRecords} records, {SuccessCount} succeeded, {ErrorCount} failed",
            records.Count, successCount, errorCount);

        return messageIds;
    }

    public async Task<List<CustomServiceBusMessage>> ReceiveMessagesAsync(
        string interfaceName,
        Guid destinationAdapterInstanceGuid,
        int maxMessages = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));

        try
        {
            _logger?.LogInformation(
                "Receiving messages from Service Bus: Interface={InterfaceName}, DestinationAdapterInstanceGuid={DestinationAdapterInstanceGuid}, MaxMessages={MaxMessages}",
                interfaceName, destinationAdapterInstanceGuid, maxMessages);

            // Create topic and subscription names
            var topicName = $"{TopicNamePrefix}{interfaceName.ToLowerInvariant()}";
            var subscriptionName = $"{SubscriptionNamePrefix}{destinationAdapterInstanceGuid.ToString().ToLowerInvariant()}";

            // Create receiver for the subscription
            await using var receiver = _serviceBusClient.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                PrefetchCount = maxMessages
            });

            // Receive messages
            var receivedMessages = await receiver.ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(30), cancellationToken);
            var result = new List<CustomServiceBusMessage>();

            foreach (var sbMessage in receivedMessages)
            {
                try
                {
                    // Deserialize message body
                    var messageDataJson = sbMessage.Body.ToString();
                    var messageData = JsonSerializer.Deserialize<MessageData>(messageDataJson);

                    if (messageData == null)
                    {
                        _logger?.LogWarning("Failed to deserialize message data: MessageId={MessageId}", sbMessage.MessageId);
                        continue;
                    }

                    // Extract properties
                    var serviceBusMessage = new CustomServiceBusMessage
                    {
                        MessageId = sbMessage.MessageId,
                        InterfaceName = sbMessage.ApplicationProperties.TryGetValue("InterfaceName", out var ifName) ? ifName.ToString() ?? interfaceName : interfaceName,
                        AdapterName = sbMessage.ApplicationProperties.TryGetValue("AdapterName", out var adName) ? adName.ToString() ?? string.Empty : string.Empty,
                        AdapterType = sbMessage.ApplicationProperties.TryGetValue("AdapterType", out var adType) ? adType.ToString() ?? string.Empty : string.Empty,
                        AdapterInstanceGuid = sbMessage.ApplicationProperties.TryGetValue("AdapterInstanceGuid", out var guidStr) && Guid.TryParse(guidStr.ToString(), out var guid) ? guid : Guid.Empty,
                        Headers = messageData.headers ?? new List<string>(),
                        Record = messageData.record ?? new Dictionary<string, string>(),
                        EnqueuedTime = sbMessage.EnqueuedTime.DateTime,
                        LockToken = sbMessage.LockToken,
                        DeliveryCount = sbMessage.DeliveryCount,
                        Properties = sbMessage.ApplicationProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };

                    result.Add(serviceBusMessage);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing received message: MessageId={MessageId}", sbMessage.MessageId);
                    // Abandon the message so it can be retried
                    await receiver.AbandonMessageAsync(sbMessage, cancellationToken: cancellationToken);
                }
            }

            _logger?.LogInformation(
                "Successfully received {Count} messages from Service Bus: Interface={InterfaceName}",
                result.Count, interfaceName);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error receiving messages from Service Bus: Interface={InterfaceName}",
                interfaceName);
            throw;
        }
    }

    public async Task CompleteMessageAsync(
        string messageId,
        string lockToken,
        CancellationToken cancellationToken = default)
    {
        // Note: Service Bus handles completion via the receiver, not by message ID
        // This method signature is kept for compatibility but implementation would need receiver reference
        _logger?.LogInformation("CompleteMessageAsync called: MessageId={MessageId}, LockToken={LockToken}", messageId, lockToken);
        await Task.CompletedTask;
    }

    public async Task AbandonMessageAsync(
        string messageId,
        string lockToken,
        Dictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        // Note: Service Bus handles abandonment via the receiver
        _logger?.LogInformation("AbandonMessageAsync called: MessageId={MessageId}, LockToken={LockToken}", messageId, lockToken);
        await Task.CompletedTask;
    }

    public async Task DeadLetterMessageAsync(
        string messageId,
        string lockToken,
        string reason,
        CancellationToken cancellationToken = default)
    {
        // Note: Service Bus handles dead lettering via the receiver
        _logger?.LogWarning("DeadLetterMessageAsync called: MessageId={MessageId}, LockToken={LockToken}, Reason={Reason}", messageId, lockToken, reason);
        await Task.CompletedTask;
    }

    public async Task<int> GetMessageCountAsync(
        string interfaceName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));

        try
        {
            // Note: Getting message count requires Service Bus Management client
            // For now, return 0 as placeholder - this would need ServiceBusAdministrationClient
            _logger?.LogInformation("GetMessageCountAsync called: Interface={InterfaceName}", interfaceName);
            return 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting message count: Interface={InterfaceName}", interfaceName);
            throw;
        }
    }

    public async Task<List<CustomServiceBusMessage>> GetRecentMessagesAsync(
        string interfaceName,
        int maxMessages = 100,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));

        try
        {
            // Peek messages without removing them from the queue
            var topicName = $"{TopicNamePrefix}{interfaceName.ToLowerInvariant()}";
            
            // Use a temporary receiver to peek messages
            // Note: This is a simplified implementation - in production, you'd want to peek from all subscriptions
            await using var receiver = _serviceBusClient.CreateReceiver(topicName, new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
            });

            var peekedMessages = await receiver.PeekMessagesAsync(maxMessages, cancellationToken: cancellationToken);
            var result = new List<CustomServiceBusMessage>();

            foreach (var sbMessage in peekedMessages)
            {
                try
                {
                    var messageDataJson = sbMessage.Body.ToString();
                    var messageData = JsonSerializer.Deserialize<MessageData>(messageDataJson);

                    if (messageData == null) continue;

                    var serviceBusMessage = new CustomServiceBusMessage
                    {
                        MessageId = sbMessage.MessageId,
                        InterfaceName = sbMessage.ApplicationProperties.TryGetValue("InterfaceName", out var ifName) ? ifName.ToString() ?? interfaceName : interfaceName,
                        AdapterName = sbMessage.ApplicationProperties.TryGetValue("AdapterName", out var adName) ? adName.ToString() ?? string.Empty : string.Empty,
                        AdapterType = sbMessage.ApplicationProperties.TryGetValue("AdapterType", out var adType) ? adType.ToString() ?? string.Empty : string.Empty,
                        AdapterInstanceGuid = sbMessage.ApplicationProperties.TryGetValue("AdapterInstanceGuid", out var guidStr) && Guid.TryParse(guidStr.ToString(), out var guid) ? guid : Guid.Empty,
                        Headers = messageData.headers ?? new List<string>(),
                        Record = messageData.record ?? new Dictionary<string, string>(),
                        EnqueuedTime = sbMessage.EnqueuedTime.DateTime,
                        LockToken = string.Empty, // Not available when peeking
                        DeliveryCount = sbMessage.DeliveryCount,
                        Properties = sbMessage.ApplicationProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };

                    result.Add(serviceBusMessage);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing peeked message: MessageId={MessageId}", sbMessage.MessageId);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting recent messages: Interface={InterfaceName}", interfaceName);
            throw;
        }
    }

    private string CalculateMessageHash(string messageData)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(messageData));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private class MessageData
    {
        public List<string>? headers { get; set; }
        public Dictionary<string, string>? record { get; set; }
    }

    public void Dispose()
    {
        _serviceBusClient?.DisposeAsync().AsTask().Wait();
    }
}


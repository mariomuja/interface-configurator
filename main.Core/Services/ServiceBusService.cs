using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Helpers;
using CustomServiceBusMessage = InterfaceConfigurator.Main.Core.Interfaces.ServiceBusMessage;
using System.Collections.Concurrent;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Service for reading and writing messages to Azure Service Bus
/// Replaces MessageBox database with Service Bus queues/topics
/// </summary>
public class ServiceBusService : IServiceBusService
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<ServiceBusService>? _logger;
    private readonly string _connectionString;
    private readonly IServiceProvider? _serviceProvider;
    private const string TopicNamePrefix = "interface-";
    private const string SubscriptionNamePrefix = "destination-";
    
    // Store received messages with their receivers for completion/abandonment
    // Key: messageId, Value: (receivedMessage, receiver, topicName, subscriptionName)
    private readonly ConcurrentDictionary<string, (ServiceBusReceivedMessage Message, ServiceBusReceiver Receiver, string TopicName, string SubscriptionName)> _activeMessages = new();
    
    // Store receivers per subscription to reuse them
    private readonly ConcurrentDictionary<string, ServiceBusReceiver> _receivers = new();

    public ServiceBusService(
        string connectionString,
        ILogger<ServiceBusService>? logger = null,
        IServiceProvider? serviceProvider = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Service Bus connection string cannot be empty", nameof(connectionString));
        }

        _connectionString = connectionString;
        _logger = logger;
        _serviceProvider = serviceProvider;
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

        // Use correlation ID for tracking
        var correlationId = CorrelationIdHelper.Ensure();
        CorrelationIdHelper.Set(correlationId);

        var messageIds = new List<string>();

        _logger?.LogInformation(
            "[CorrelationId: {CorrelationId}] Debatching {RecordCount} records into individual Service Bus messages: Interface={InterfaceName}, Adapter={AdapterName}, AdapterInstanceGuid={AdapterInstanceGuid}",
            correlationId, records.Count, interfaceName, adapterName, adapterInstanceGuid);

        // Create topic name
        var topicName = $"{TopicNamePrefix}{interfaceName.ToLowerInvariant()}";
        await using var sender = _serviceBusClient.CreateSender(topicName);

        // Default batch logic: reuse a ServiceBusMessageBatch and flush when needed
        var batch = await sender.CreateMessageBatchAsync(cancellationToken);
        var successCount = 0;
        var errorCount = 0;

        foreach (var record in records)
        {
            try
            {
                var messageId = await CreateAndAddMessageToBatchAsync(batch, sender, record, headers, interfaceName, adapterName, adapterType, adapterInstanceGuid, cancellationToken);
                messageIds.Add(messageId);
                successCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger?.LogError(ex,
                    "[CorrelationId: {CorrelationId}] Failed to add message to batch: Interface={InterfaceName}, Adapter={AdapterName}, RecordIndex={RecordIndex}",
                    correlationId, interfaceName, adapterName, successCount + errorCount);
            }
        }

        // Send remaining messages in batch
        if (batch.Count > 0)
        {
            await sender.SendMessagesAsync(batch, cancellationToken);
        }
        batch.Dispose();

        _logger?.LogInformation(
            "[CorrelationId: {CorrelationId}] Completed debatching: {TotalRecords} records, {SuccessCount} succeeded",
            correlationId, records.Count, messageIds.Count);

        return messageIds;
    }

    private async Task<List<string>> SendBatchAsync(
        ServiceBusSender sender,
        List<Dictionary<string, string>> batch,
        List<string> headers,
        string interfaceName,
        string adapterName,
        string adapterType,
        Guid adapterInstanceGuid,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var messageIds = new List<string>();
        var serviceBusMessages = new List<Azure.Messaging.ServiceBus.ServiceBusMessage>();

        foreach (var record in batch)
        {
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

            // Add custom properties including correlation ID
            serviceBusMessage.ApplicationProperties.Add("InterfaceName", interfaceName);
            serviceBusMessage.ApplicationProperties.Add("AdapterName", adapterName);
            serviceBusMessage.ApplicationProperties.Add("AdapterType", adapterType);
            serviceBusMessage.ApplicationProperties.Add("AdapterInstanceGuid", adapterInstanceGuid.ToString());
            serviceBusMessage.ApplicationProperties.Add("MessageHash", CalculateMessageHash(messageDataJson));
            serviceBusMessage.ApplicationProperties.Add("CorrelationId", correlationId);

            serviceBusMessages.Add(serviceBusMessage);
            messageIds.Add(messageId);
        }

        // Send batch
        var batchToSend = await sender.CreateMessageBatchAsync(cancellationToken);
        foreach (var message in serviceBusMessages)
        {
            if (!batchToSend.TryAddMessage(message))
            {
                // Batch is full, send it and create a new one
                await sender.SendMessagesAsync(batchToSend, cancellationToken);
                batchToSend.Dispose();
                batchToSend = await sender.CreateMessageBatchAsync(cancellationToken);
                
                if (!batchToSend.TryAddMessage(message))
                {
                    throw new InvalidOperationException("Message is too large for Service Bus batch");
                }
            }
        }

        if (batchToSend.Count > 0)
        {
            await sender.SendMessagesAsync(batchToSend, cancellationToken);
        }
        batchToSend.Dispose();

        return messageIds;
    }

    private async Task<string> CreateAndAddMessageToBatchAsync(
        ServiceBusMessageBatch batch,
        ServiceBusSender sender,
        Dictionary<string, string> record,
        List<string> headers,
        string interfaceName,
        string adapterName,
        string adapterType,
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken)
    {
        var messageData = new
        {
            headers = headers ?? new List<string>(),
            record = record
        };
        var messageDataJson = JsonSerializer.Serialize(messageData);

        var messageId = Guid.NewGuid().ToString();
        var correlationId = CorrelationIdHelper.Current ?? CorrelationIdHelper.Generate();
        
        var serviceBusMessage = new Azure.Messaging.ServiceBus.ServiceBusMessage(messageDataJson)
        {
            MessageId = messageId,
            Subject = adapterName,
            ContentType = "application/json"
        };

        // Add custom properties including correlation ID
        serviceBusMessage.ApplicationProperties.Add("InterfaceName", interfaceName);
        serviceBusMessage.ApplicationProperties.Add("AdapterName", adapterName);
        serviceBusMessage.ApplicationProperties.Add("AdapterType", adapterType);
        serviceBusMessage.ApplicationProperties.Add("AdapterInstanceGuid", adapterInstanceGuid.ToString());
        serviceBusMessage.ApplicationProperties.Add("MessageHash", CalculateMessageHash(messageDataJson));
        serviceBusMessage.ApplicationProperties.Add("CorrelationId", correlationId);

        // Try to add to batch
        if (!batch.TryAddMessage(serviceBusMessage))
        {
            // Batch is full, send it and create a new one
            await sender.SendMessagesAsync(batch, cancellationToken);
            batch.Dispose();
            var newBatch = await sender.CreateMessageBatchAsync(cancellationToken);
            
            // Add message to new batch
            if (!newBatch.TryAddMessage(serviceBusMessage))
            {
                throw new InvalidOperationException("Message is too large for Service Bus batch");
            }
        }

        return messageId;
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
            var receiverKey = $"{topicName}/{subscriptionName}";

            // Get or create receiver for this subscription (reuse receivers)
            var receiver = _receivers.GetOrAdd(receiverKey, _ =>
            {
                return _serviceBusClient.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock,
                    PrefetchCount = maxMessages
                });
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
                        // Abandon invalid message
                        await receiver.AbandonMessageAsync(sbMessage, cancellationToken: cancellationToken);
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

                    // Store message with receiver for later completion/abandonment
                    _activeMessages.TryAdd(sbMessage.MessageId, (sbMessage, receiver, topicName, subscriptionName));

                    // Record lock in database for recovery (if lock tracking service is available)
                    // This will be injected via dependency injection
                    try
                    {
                        var lockTrackingService = _serviceProvider?.GetService<IServiceBusLockTrackingService>();
                        if (lockTrackingService != null)
                        {
                            await lockTrackingService.RecordMessageLockAsync(
                                sbMessage.MessageId,
                                sbMessage.LockToken,
                                topicName,
                                subscriptionName,
                                interfaceName,
                                destinationAdapterInstanceGuid,
                                sbMessage.LockedUntil.UtcDateTime,
                                sbMessage.DeliveryCount,
                                cancellationToken);
                        }
                    }
                    catch (Exception lockEx)
                    {
                        _logger?.LogWarning(lockEx, "Failed to record lock for MessageId={MessageId}", sbMessage.MessageId);
                        // Don't fail message processing if lock tracking fails
                    }

                    result.Add(serviceBusMessage);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error processing received message: MessageId={MessageId}", sbMessage.MessageId);
                    // Abandon the message so it can be retried
                    try
                    {
                        await receiver.AbandonMessageAsync(sbMessage, cancellationToken: cancellationToken);
                    }
                    catch (Exception abandonEx)
                    {
                        _logger?.LogError(abandonEx, "Failed to abandon message {MessageId}", sbMessage.MessageId);
                    }
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
        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("Message ID cannot be empty", nameof(messageId));

        try
        {
            if (_activeMessages.TryRemove(messageId, out var messageInfo))
            {
                var (sbMessage, receiver, _, _) = messageInfo;
                
                // Verify lock token matches
                if (sbMessage.LockToken != lockToken)
                {
                    _logger?.LogWarning("Lock token mismatch for message {MessageId}. Expected: {Expected}, Got: {Actual}", 
                        messageId, sbMessage.LockToken, lockToken);
                }

                await receiver.CompleteMessageAsync(sbMessage, cancellationToken);
                
                // Update lock status in database
                try
                {
                    var lockTrackingService = _serviceProvider?.GetService<IServiceBusLockTrackingService>();
                    if (lockTrackingService != null)
                    {
                        await lockTrackingService.UpdateLockStatusAsync(messageId, "Completed", "Message processed successfully", cancellationToken);
                    }
                }
                catch (Exception lockEx)
                {
                    _logger?.LogWarning(lockEx, "Failed to update lock status for MessageId={MessageId}", messageId);
                }
                
                _logger?.LogInformation("Successfully completed message {MessageId} in Service Bus", messageId);
            }
            else
            {
                _logger?.LogWarning("Message {MessageId} not found in active messages cache. It may have already been processed or expired.", messageId);
                // Try to create a receiver and complete by lock token (fallback)
                // Note: This requires knowing the topic and subscription, which we don't have here
                // For now, log a warning - messages will be auto-completed when lock expires
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error completing message {MessageId} in Service Bus", messageId);
            throw;
        }
    }

    public async Task AbandonMessageAsync(
        string messageId,
        string lockToken,
        Dictionary<string, object>? propertiesToModify = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("Message ID cannot be empty", nameof(messageId));

        try
        {
            if (_activeMessages.TryGetValue(messageId, out var messageInfo))
            {
                var (sbMessage, receiver, _, _) = messageInfo;
                
                // Verify lock token matches
                if (sbMessage.LockToken != lockToken)
                {
                    _logger?.LogWarning("Lock token mismatch for message {MessageId}. Expected: {Expected}, Got: {Actual}", 
                        messageId, sbMessage.LockToken, lockToken);
                }

                await receiver.AbandonMessageAsync(sbMessage, propertiesToModify, cancellationToken);
                
                // Update lock status in database
                try
                {
                    var lockTrackingService = _serviceProvider?.GetService<IServiceBusLockTrackingService>();
                    if (lockTrackingService != null)
                    {
                        var reason = propertiesToModify?.ContainsKey("Reason") == true 
                            ? propertiesToModify["Reason"].ToString() 
                            : "Message abandoned for retry";
                        await lockTrackingService.UpdateLockStatusAsync(messageId, "Abandoned", reason, cancellationToken);
                    }
                }
                catch (Exception lockEx)
                {
                    _logger?.LogWarning(lockEx, "Failed to update lock status for MessageId={MessageId}", messageId);
                }
                
                _logger?.LogInformation("Successfully abandoned message {MessageId} in Service Bus for retry", messageId);
                
                // Remove from cache after abandoning
                _activeMessages.TryRemove(messageId, out _);
            }
            else
            {
                _logger?.LogWarning("Message {MessageId} not found in active messages cache. It may have already been processed.", messageId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error abandoning message {MessageId} in Service Bus", messageId);
            throw;
        }
    }

    public async Task DeadLetterMessageAsync(
        string messageId,
        string lockToken,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("Message ID cannot be empty", nameof(messageId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be empty", nameof(reason));

        try
        {
            if (_activeMessages.TryRemove(messageId, out var messageInfo))
            {
                var (sbMessage, receiver, _, _) = messageInfo;
                
                // Verify lock token matches
                if (sbMessage.LockToken != lockToken)
                {
                    _logger?.LogWarning("Lock token mismatch for message {MessageId}. Expected: {Expected}, Got: {Actual}", 
                        messageId, sbMessage.LockToken, lockToken);
                }

                await receiver.DeadLetterMessageAsync(sbMessage, deadLetterReason: reason, cancellationToken: cancellationToken);
                
                // Update lock status in database
                try
                {
                    var lockTrackingService = _serviceProvider?.GetService<IServiceBusLockTrackingService>();
                    if (lockTrackingService != null)
                    {
                        await lockTrackingService.UpdateLockStatusAsync(messageId, "DeadLettered", reason, cancellationToken);
                    }
                }
                catch (Exception lockEx)
                {
                    _logger?.LogWarning(lockEx, "Failed to update lock status for MessageId={MessageId}", messageId);
                }
                
                _logger?.LogWarning("Successfully dead lettered message {MessageId} in Service Bus. Reason: {Reason}", messageId, reason);
            }
            else
            {
                _logger?.LogWarning("Message {MessageId} not found in active messages cache. It may have already been processed.", messageId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error dead lettering message {MessageId} in Service Bus", messageId);
            throw;
        }
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


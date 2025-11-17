using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Data;

namespace ProcessCsvBlobTrigger.Services;

/// <summary>
/// Service for reading and writing messages to the MessageBox staging area
/// Implements debatching: each record is stored as a separate message
/// Triggers events when messages are added for event-driven processing
/// </summary>
public class MessageBoxService : IMessageBoxService
{
    private readonly MessageBoxDbContext _context;
    private readonly IEventQueue? _eventQueue;
    private readonly IMessageSubscriptionService? _subscriptionService;
    private readonly ILogger<MessageBoxService>? _logger;

    public MessageBoxService(
        MessageBoxDbContext context,
        IEventQueue? eventQueue = null,
        IMessageSubscriptionService? subscriptionService = null,
        ILogger<MessageBoxService>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _eventQueue = eventQueue;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    public async Task<Guid> WriteSingleRecordMessageAsync(
        string interfaceName,
        string adapterName,
        string adapterType,
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
                "Writing single record message to MessageBox: Interface={InterfaceName}, Adapter={AdapterName}, Type={AdapterType}",
                interfaceName, adapterName, adapterType);

            // Serialize single record to JSON
            var messageData = new
            {
                headers = headers ?? new List<string>(),
                record = record
            };
            var messageDataJson = JsonSerializer.Serialize(messageData);

            var message = new MessageBoxMessage
            {
                MessageId = Guid.NewGuid(),
                InterfaceName = interfaceName,
                AdapterName = adapterName,
                AdapterType = adapterType,
                MessageData = messageDataJson,
                Status = "Pending",
                datetime_created = DateTime.UtcNow
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation(
                "Successfully wrote single record message to MessageBox: MessageId={MessageId}, Interface={InterfaceName}",
                message.MessageId, interfaceName);

            // Trigger event for event-driven processing
            if (_eventQueue != null)
            {
                await _eventQueue.EnqueueMessageEventAsync(message.MessageId, interfaceName, cancellationToken);
                _logger?.LogInformation("Triggered event for message: MessageId={MessageId}", message.MessageId);
            }

            return message.MessageId;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error writing single record message to MessageBox: Interface={InterfaceName}, Adapter={AdapterName}",
                interfaceName, adapterName);
            throw;
        }
    }

    public async Task<List<Guid>> WriteMessagesAsync(
        string interfaceName,
        string adapterName,
        string adapterType,
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

        var messageIds = new List<Guid>();

        _logger?.LogInformation(
            "Debatching {RecordCount} records into individual messages: Interface={InterfaceName}, Adapter={AdapterName}",
            records.Count, interfaceName, adapterName);

        // Debatch: create one message per record
        foreach (var record in records)
        {
            var messageId = await WriteSingleRecordMessageAsync(
                interfaceName, adapterName, adapterType, headers, record, cancellationToken);
            messageIds.Add(messageId);
        }

        _logger?.LogInformation(
            "Successfully debatched {RecordCount} records into {MessageCount} messages: Interface={InterfaceName}",
            records.Count, messageIds.Count, interfaceName);

        return messageIds;
    }

    public async Task<List<MessageBoxMessage>> ReadMessagesAsync(
        string interfaceName,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));

        try
        {
            _logger?.LogInformation(
                "Reading messages from MessageBox: Interface={InterfaceName}, Status={Status}",
                interfaceName, status ?? "All");

            var query = _context.Messages
                .Where(m => m.InterfaceName == interfaceName);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(m => m.Status == status);
            }

            var messages = await query
                .OrderBy(m => m.datetime_created) // Process oldest first
                .ToListAsync(cancellationToken);

            _logger?.LogInformation(
                "Successfully read {Count} messages from MessageBox: Interface={InterfaceName}",
                messages.Count, interfaceName);

            return messages;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error reading messages from MessageBox: Interface={InterfaceName}",
                interfaceName);
            throw;
        }
    }

    public async Task<MessageBoxMessage?> ReadMessageAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Reading message from MessageBox: MessageId={MessageId}", messageId);

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

            if (message != null)
            {
                _logger?.LogInformation(
                    "Successfully read message from MessageBox: MessageId={MessageId}, Status={Status}",
                    messageId, message.Status);
            }
            else
            {
                _logger?.LogWarning("Message not found in MessageBox: MessageId={MessageId}", messageId);
            }

            return message;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading message from MessageBox: MessageId={MessageId}", messageId);
            throw;
        }
    }

    public async Task MarkMessageAsProcessedAsync(
        Guid messageId,
        string? processingDetails = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("Marking message as processed: MessageId={MessageId}", messageId);

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

            if (message == null)
            {
                throw new InvalidOperationException($"Message not found: {messageId}");
            }

            message.Status = "Processed";
            message.datetime_processed = DateTime.UtcNow;
            message.ProcessingDetails = processingDetails;

            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Successfully marked message as processed: MessageId={MessageId}", messageId);

            // Check if all subscriptions are processed, then remove message
            if (_subscriptionService != null)
            {
                var allProcessed = await _subscriptionService.AreAllSubscriptionsProcessedAsync(messageId, cancellationToken);
                if (allProcessed)
                {
                    await RemoveMessageAsync(messageId, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error marking message as processed: MessageId={MessageId}", messageId);
            throw;
        }
    }

    public async Task MarkMessageAsErrorAsync(
        Guid messageId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty", nameof(errorMessage));

        try
        {
            _logger?.LogError("Marking message as error: MessageId={MessageId}, Error={ErrorMessage}", messageId, errorMessage);

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

            if (message == null)
            {
                throw new InvalidOperationException($"Message not found: {messageId}");
            }

            message.Status = "Error";
            message.datetime_processed = DateTime.UtcNow;
            message.ErrorMessage = errorMessage;

            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Successfully marked message as error: MessageId={MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error marking message as error: MessageId={MessageId}", messageId);
            throw;
        }
    }

    public (List<string> headers, Dictionary<string, string> record) ExtractDataFromMessage(MessageBoxMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        try
        {
            var data = JsonSerializer.Deserialize<SingleRecordMessageData>(message.MessageData);
            if (data == null)
            {
                throw new InvalidOperationException($"Failed to deserialize message data: {message.MessageId}");
            }

            return (data.headers ?? new List<string>(), data.record ?? new Dictionary<string, string>());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error extracting data from message: MessageId={MessageId}", message.MessageId);
            throw;
        }
    }

    public async Task RemoveMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Only remove if all subscriptions are processed
            if (_subscriptionService != null)
            {
                var allProcessed = await _subscriptionService.AreAllSubscriptionsProcessedAsync(messageId, cancellationToken);
                if (!allProcessed)
                {
                    _logger?.LogWarning(
                        "Cannot remove message {MessageId}: Not all subscriptions are processed yet",
                        messageId);
                    return;
                }
            }

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

            if (message != null)
            {
                _context.Messages.Remove(message);
                await _context.SaveChangesAsync(cancellationToken);
                _logger?.LogInformation("Successfully removed message from MessageBox: MessageId={MessageId}", messageId);
            }
            else
            {
                _logger?.LogWarning("Message not found for removal: MessageId={MessageId}", messageId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing message from MessageBox: MessageId={MessageId}", messageId);
            throw;
        }
    }

    private class SingleRecordMessageData
    {
        public List<string>? headers { get; set; }
        public Dictionary<string, string>? record { get; set; }
    }
}

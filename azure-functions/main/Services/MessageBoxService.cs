using System.Security.Cryptography;
using System.Text;
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
                "Writing single record message to MessageBox: Interface={InterfaceName}, Adapter={AdapterName}, Type={AdapterType}, AdapterInstanceGuid={AdapterInstanceGuid}",
                interfaceName, adapterName, adapterType, adapterInstanceGuid);

            // Serialize single record to JSON
            var messageData = new
            {
                headers = headers ?? new List<string>(),
                record = record
            };
            var messageDataJson = JsonSerializer.Serialize(messageData);

            // Calculate message hash for idempotency checking
            var messageHash = CalculateMessageHash(messageDataJson);

            // Check for duplicate message (idempotency check)
            // Look for existing message with same hash, interface, and adapter within last 24 hours
            var existingMessage = await _context.Messages
                .Where(m => m.MessageHash == messageHash 
                    && m.InterfaceName == interfaceName
                    && m.AdapterName == adapterName
                    && m.AdapterInstanceGuid == adapterInstanceGuid
                    && m.datetime_created > DateTime.UtcNow.AddHours(-24))
                .FirstOrDefaultAsync(cancellationToken);

            if (existingMessage != null)
            {
                _logger?.LogInformation(
                    "Duplicate message detected (same hash). Returning existing MessageId={MessageId}, Interface={InterfaceName}",
                    existingMessage.MessageId, interfaceName);
                return existingMessage.MessageId; // Idempotent: return existing message ID
            }

            var message = new MessageBoxMessage
            {
                MessageId = Guid.NewGuid(),
                InterfaceName = interfaceName,
                AdapterName = adapterName,
                AdapterType = adapterType,
                AdapterInstanceGuid = adapterInstanceGuid,
                MessageData = messageDataJson,
                Status = "Pending",
                datetime_created = DateTime.UtcNow,
                RetryCount = 0,
                MaxRetries = 3, // Default max retries
                MessageHash = messageHash
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

        var messageIds = new List<Guid>();

        _logger?.LogInformation(
            "Debatching {RecordCount} records into individual messages: Interface={InterfaceName}, Adapter={AdapterName}, AdapterInstanceGuid={AdapterInstanceGuid}",
            records.Count, interfaceName, adapterName, adapterInstanceGuid);

        // Debatch: create one message per record
        foreach (var record in records)
        {
            var messageId = await WriteSingleRecordMessageAsync(
                interfaceName, adapterName, adapterType, adapterInstanceGuid, headers, record, cancellationToken);
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
                .Where(m => m.InterfaceName == interfaceName)
                .Where(m => !m.DeadLetter); // Exclude dead letter messages

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "Pending")
                {
                    // For Pending status, also include retryable Error messages
                    var now = DateTime.UtcNow;
                    query = query.Where(m => 
                        m.Status == "Pending" || 
                        (m.Status == "Error" && m.RetryCount < m.MaxRetries && 
                         (m.Status != "InProgress" || !m.InProgressUntil.HasValue || m.InProgressUntil.Value <= now)));
                }
                else
                {
                    query = query.Where(m => m.Status == status);
                }
            }
            else
            {
                // Exclude InProgress messages unless they're stale
                var now = DateTime.UtcNow;
                query = query.Where(m => 
                    m.Status != "InProgress" || 
                    !m.InProgressUntil.HasValue || 
                    m.InProgressUntil.Value <= now);
            }

            var messages = await query
                .OrderBy(m => m.RetryCount) // Process messages with fewer retries first
                .ThenBy(m => m.datetime_created) // Then oldest first
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

            // Use atomic update to prevent race conditions and ensure idempotency
            // Only update if message is not already processed
            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE Messages 
                  SET Status = 'Processed', 
                      datetime_processed = GETUTCDATE(),
                      ProcessingDetails = {0},
                      InProgressUntil = NULL
                  WHERE MessageId = {1}
                    AND Status != 'Processed'
                    AND Status != 'DeadLetter'",
                processingDetails ?? (object)DBNull.Value, messageId, cancellationToken);

            if (rowsAffected == 0)
            {
                // Check if message exists and current status
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

                if (message == null)
                {
                    throw new InvalidOperationException($"Message not found: {messageId}");
                }

                if (message.Status == "Processed")
                {
                    _logger?.LogInformation(
                        "Message {MessageId} is already Processed (idempotent operation)", messageId);
                    return; // Idempotent: already processed
                }

                if (message.Status == "DeadLetter")
                {
                    _logger?.LogWarning(
                        "Message {MessageId} is in DeadLetter status, cannot mark as processed", messageId);
                    return; // Cannot process dead letter messages
                }
            }
            else
            {
                _logger?.LogInformation("Successfully marked message as processed: MessageId={MessageId}", messageId);
            }

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

            // Increment retry count
            message.RetryCount++;
            message.LastRetryTime = DateTime.UtcNow;
            message.ErrorMessage = errorMessage;
            message.InProgressUntil = null; // Release lock

            // Check if max retries exceeded
            if (message.RetryCount >= message.MaxRetries)
            {
                await MoveMessageToDeadLetterAsync(messageId, 
                    $"Max retries ({message.MaxRetries}) exceeded. Last error: {errorMessage}", 
                    cancellationToken);
            }
            else
            {
                message.Status = "Error";
                message.datetime_processed = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                _logger?.LogWarning(
                    "Message marked as error (retry {RetryCount}/{MaxRetries}): MessageId={MessageId}", 
                    message.RetryCount, message.MaxRetries, messageId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error marking message as error: MessageId={MessageId}", messageId);
            throw;
        }
    }

    public async Task<bool> MarkMessageAsInProgressAsync(
        Guid messageId,
        int lockTimeoutMinutes = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use optimistic concurrency with database-level check to prevent race conditions
            // This ensures only one instance can acquire the lock
            var now = DateTime.UtcNow;
            var lockUntil = now.AddMinutes(lockTimeoutMinutes);

            // Try to acquire lock atomically using raw SQL with WHERE clause
            // This prevents race conditions by checking and updating in a single operation
            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE Messages 
                  SET Status = 'InProgress', 
                      InProgressUntil = {0}
                  WHERE MessageId = {1}
                    AND (Status != 'InProgress' 
                         OR InProgressUntil IS NULL 
                         OR InProgressUntil <= {2})
                    AND Status != 'Processed'
                    AND Status != 'DeadLetter'",
                lockUntil, messageId, now, cancellationToken);

            if (rowsAffected == 0)
            {
                // Check if message exists and why lock wasn't acquired
                var message = await _context.Messages
                    .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

                if (message == null)
                {
                    throw new InvalidOperationException($"Message not found: {messageId}");
                }

                if (message.Status == "InProgress" && message.InProgressUntil.HasValue && message.InProgressUntil.Value > now)
                {
                    _logger?.LogWarning(
                        "Message {MessageId} is already locked until {LockUntil}", 
                        messageId, message.InProgressUntil.Value);
                    return false; // Lock already held
                }

                if (message.Status == "Processed" || message.Status == "DeadLetter")
                {
                    _logger?.LogWarning(
                        "Message {MessageId} is already {Status}, cannot acquire lock", 
                        messageId, message.Status);
                    return false; // Message already processed
                }

                return false; // Lock acquisition failed for unknown reason
            }

            _logger?.LogInformation(
                "Message {MessageId} locked for processing until {LockUntil}", 
                messageId, lockUntil);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error marking message as InProgress: MessageId={MessageId}", messageId);
            throw;
        }
    }

    public async Task ReleaseMessageLockAsync(
        Guid messageId,
        string revertToStatus = "Pending",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

            if (message == null)
            {
                throw new InvalidOperationException($"Message not found: {messageId}");
            }

            if (message.Status == "InProgress")
            {
                message.Status = revertToStatus;
                message.InProgressUntil = null;
                await _context.SaveChangesAsync(cancellationToken);
                _logger?.LogInformation(
                    "Released lock on message {MessageId}, reverted to status: {Status}", 
                    messageId, revertToStatus);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error releasing message lock: MessageId={MessageId}", messageId);
            throw;
        }
    }

    public async Task MoveMessageToDeadLetterAsync(
        Guid messageId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogWarning("Moving message to dead letter: MessageId={MessageId}, Reason={Reason}", messageId, reason);

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

            if (message == null)
            {
                throw new InvalidOperationException($"Message not found: {messageId}");
            }

            message.Status = "DeadLetter";
            message.DeadLetter = true;
            message.ErrorMessage = $"Dead Letter: {reason}";
            message.InProgressUntil = null;
            message.datetime_processed = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogWarning("Successfully moved message to dead letter: MessageId={MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error moving message to dead letter: MessageId={MessageId}", messageId);
            throw;
        }
    }

    public async Task<List<MessageBoxMessage>> ReadRetryableMessagesAsync(
        string interfaceName,
        int minRetryDelayMinutes = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));

        try
        {
            var now = DateTime.UtcNow;
            var minRetryTime = now.AddMinutes(-minRetryDelayMinutes);

            // Calculate exponential backoff delay: 2^retryCount minutes
            // RetryCount 0: 1 min, RetryCount 1: 2 min, RetryCount 2: 4 min, etc.
            var messages = await _context.Messages
                .Where(m => m.InterfaceName == interfaceName)
                .Where(m => m.Status == "Error" || m.Status == "Pending")
                .Where(m => !m.DeadLetter)
                .Where(m => m.RetryCount < m.MaxRetries)
                .Where(m => m.Status != "InProgress" || !m.InProgressUntil.HasValue || m.InProgressUntil.Value <= now)
                .Where(m => !m.LastRetryTime.HasValue || m.LastRetryTime.Value <= minRetryTime || 
                    (m.LastRetryTime.Value.AddMinutes(Math.Pow(2, m.RetryCount)) <= now))
                .OrderBy(m => m.RetryCount) // Retry messages with fewer retries first
                .ThenBy(m => m.LastRetryTime ?? m.datetime_created) // Then by last retry time
                .ToListAsync(cancellationToken);

            _logger?.LogInformation(
                "Found {Count} retryable messages for interface {InterfaceName}", 
                messages.Count, interfaceName);

            return messages;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading retryable messages: Interface={InterfaceName}", interfaceName);
            throw;
        }
    }

    public async Task<List<MessageBoxMessage>> ReadDeadLetterMessagesAsync(
        string? interfaceName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.Messages
                .Where(m => m.DeadLetter || m.Status == "DeadLetter");

            if (!string.IsNullOrWhiteSpace(interfaceName))
            {
                query = query.Where(m => m.InterfaceName == interfaceName);
            }

            var messages = await query
                .OrderByDescending(m => m.datetime_processed ?? m.datetime_created)
                .ToListAsync(cancellationToken);

            _logger?.LogInformation(
                "Found {Count} dead letter messages for interface {InterfaceName}", 
                messages.Count, interfaceName ?? "All");

            return messages;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading dead letter messages: Interface={InterfaceName}", interfaceName ?? "All");
            throw;
        }
    }

    public async Task<int> ReleaseStaleLocksAsync(
        int lockTimeoutMinutes = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var timeoutThreshold = now.AddMinutes(-lockTimeoutMinutes);

            var staleMessages = await _context.Messages
                .Where(m => m.Status == "InProgress")
                .Where(m => m.InProgressUntil.HasValue && m.InProgressUntil.Value < timeoutThreshold)
                .ToListAsync(cancellationToken);

            foreach (var message in staleMessages)
            {
                var lockUntil = message.InProgressUntil; // Store before clearing
                message.Status = "Pending"; // Revert to Pending for retry
                message.InProgressUntil = null;
                _logger?.LogWarning(
                    "Released stale lock on message {MessageId} (was locked until {LockUntil})", 
                    message.MessageId, lockUntil);
            }

            if (staleMessages.Count > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger?.LogInformation("Released {Count} stale locks", staleMessages.Count);
            }

            return staleMessages.Count;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error releasing stale locks");
            throw;
        }
    }

    private string CalculateMessageHash(string messageData)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(messageData));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
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

    public async Task EnsureAdapterInstanceAsync(
        Guid adapterInstanceGuid,
        string interfaceName,
        string instanceName,
        string adapterName,
        string adapterType,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));
        if (string.IsNullOrWhiteSpace(instanceName))
            throw new ArgumentException("Instance name cannot be empty", nameof(instanceName));
        if (string.IsNullOrWhiteSpace(adapterName))
            throw new ArgumentException("Adapter name cannot be empty", nameof(adapterName));
        if (string.IsNullOrWhiteSpace(adapterType))
            throw new ArgumentException("Adapter type cannot be empty", nameof(adapterType));

        try
        {
            _logger?.LogInformation(
                "Ensuring adapter instance exists: AdapterInstanceGuid={AdapterInstanceGuid}, Interface={InterfaceName}, InstanceName={InstanceName}",
                adapterInstanceGuid, interfaceName, instanceName);

            var existingInstance = await _context.AdapterInstances
                .FirstOrDefaultAsync(a => a.AdapterInstanceGuid == adapterInstanceGuid, cancellationToken);

            if (existingInstance != null)
            {
                // Update existing instance
                existingInstance.InterfaceName = interfaceName;
                existingInstance.InstanceName = instanceName;
                existingInstance.AdapterName = adapterName;
                existingInstance.AdapterType = adapterType;
                existingInstance.IsEnabled = isEnabled;
                existingInstance.datetime_updated = DateTime.UtcNow;
                _logger?.LogInformation("Updated existing adapter instance: AdapterInstanceGuid={AdapterInstanceGuid}", adapterInstanceGuid);
            }
            else
            {
                // Create new instance
                var newInstance = new AdapterInstance
                {
                    AdapterInstanceGuid = adapterInstanceGuid,
                    InterfaceName = interfaceName,
                    InstanceName = instanceName,
                    AdapterName = adapterName,
                    AdapterType = adapterType,
                    IsEnabled = isEnabled,
                    datetime_created = DateTime.UtcNow
                };
                _context.AdapterInstances.Add(newInstance);
                _logger?.LogInformation("Created new adapter instance: AdapterInstanceGuid={AdapterInstanceGuid}", adapterInstanceGuid);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error ensuring adapter instance: AdapterInstanceGuid={AdapterInstanceGuid}, Interface={InterfaceName}",
                adapterInstanceGuid, interfaceName);
            throw;
        }
    }

    private class SingleRecordMessageData
    {
        public List<string>? headers { get; set; }
        public Dictionary<string, string>? record { get; set; }
    }
}

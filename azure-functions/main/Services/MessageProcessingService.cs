using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for tracking message processing status
/// This tracks which messages have been processed by which adapters (separate from subscriptions)
/// </summary>
public class MessageProcessingService : IMessageProcessingService
{
    private readonly MessageBoxDbContext _context;
    private readonly IAdapterSubscriptionService _subscriptionService;
    private readonly ILogger<MessageProcessingService>? _logger;

    public MessageProcessingService(
        MessageBoxDbContext context,
        IAdapterSubscriptionService subscriptionService,
        ILogger<MessageProcessingService>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
        _logger = logger;
    }

    public async Task CreateProcessingRecordAsync(
        Guid messageId,
        Guid adapterInstanceGuid,
        string interfaceName,
        string adapterName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));
        if (string.IsNullOrWhiteSpace(adapterName))
            throw new ArgumentException("Adapter name cannot be empty", nameof(adapterName));

        try
        {
            // Check if processing record already exists
            var existing = await _context.Set<MessageProcessing>()
                .FirstOrDefaultAsync(p => 
                    p.MessageId == messageId && 
                    p.AdapterInstanceGuid == adapterInstanceGuid, 
                    cancellationToken);

            if (existing != null)
            {
                _logger?.LogDebug("Processing record already exists: MessageId={MessageId}, AdapterInstanceGuid={AdapterInstanceGuid}",
                    messageId, adapterInstanceGuid);
                return;
            }

            var processing = new MessageProcessing
            {
                MessageId = messageId,
                AdapterInstanceGuid = adapterInstanceGuid,
                InterfaceName = interfaceName,
                AdapterName = adapterName,
                Status = "Pending",
                datetime_created = DateTime.UtcNow
            };

            _context.Set<MessageProcessing>().Add(processing);
            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Created processing record: MessageId={MessageId}, AdapterInstanceGuid={AdapterInstanceGuid}, Adapter={AdapterName}",
                messageId, adapterInstanceGuid, adapterName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating processing record: MessageId={MessageId}, AdapterInstanceGuid={AdapterInstanceGuid}",
                messageId, adapterInstanceGuid);
            throw;
        }
    }

    public async Task MarkAsProcessedAsync(
        Guid messageId,
        Guid adapterInstanceGuid,
        string? processingDetails = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var processing = await _context.Set<MessageProcessing>()
                .FirstOrDefaultAsync(p => 
                    p.MessageId == messageId && 
                    p.AdapterInstanceGuid == adapterInstanceGuid, 
                    cancellationToken);

            if (processing == null)
            {
                _logger?.LogWarning("Processing record not found: MessageId={MessageId}, AdapterInstanceGuid={AdapterInstanceGuid}",
                    messageId, adapterInstanceGuid);
                return;
            }

            processing.Status = "Processed";
            processing.datetime_processed = DateTime.UtcNow;
            processing.ProcessingDetails = processingDetails;

            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Marked as processed: MessageId={MessageId}, AdapterInstanceGuid={AdapterInstanceGuid}",
                messageId, adapterInstanceGuid);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error marking as processed: MessageId={MessageId}, AdapterInstanceGuid={AdapterInstanceGuid}",
                messageId, adapterInstanceGuid);
            throw;
        }
    }

    public async Task MarkAsErrorAsync(
        Guid messageId,
        Guid adapterInstanceGuid,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty", nameof(errorMessage));

        try
        {
            var processing = await _context.Set<MessageProcessing>()
                .FirstOrDefaultAsync(p => 
                    p.MessageId == messageId && 
                    p.AdapterInstanceGuid == adapterInstanceGuid, 
                    cancellationToken);

            if (processing == null)
            {
                _logger?.LogWarning("Processing record not found: MessageId={MessageId}, AdapterInstanceGuid={AdapterInstanceGuid}",
                    messageId, adapterInstanceGuid);
                return;
            }

            processing.Status = "Error";
            processing.datetime_processed = DateTime.UtcNow;
            processing.ErrorMessage = errorMessage;

            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogError("Marked as error: MessageId={MessageId}, AdapterInstanceGuid={AdapterInstanceGuid}, Error={Error}",
                messageId, adapterInstanceGuid, errorMessage);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error marking as error: MessageId={MessageId}, AdapterInstanceGuid={AdapterInstanceGuid}",
                messageId, adapterInstanceGuid);
            throw;
        }
    }

    public async Task<bool> AreAllSubscribedAdaptersProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the message to find its interface name
            var message = await _context.Set<MessageBoxMessage>()
                .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

            if (message == null)
            {
                _logger?.LogWarning("Message not found: MessageId={MessageId}", messageId);
                return false;
            }

            // Get all active subscriptions for this interface
            var subscriptions = await _subscriptionService.GetSubscriptionsForInterfaceAsync(
                message.InterfaceName, cancellationToken);

            if (subscriptions.Count == 0)
            {
                // No subscriptions means message can be removed (no destination adapters subscribed)
                return true;
            }

            // Get all processing records for this message
            var processingRecords = await _context.Set<MessageProcessing>()
                .Where(p => p.MessageId == messageId)
                .ToListAsync(cancellationToken);

            // Check if all subscribed adapters have processed the message
            foreach (var subscription in subscriptions)
            {
                var processing = processingRecords.FirstOrDefault(p => 
                    p.AdapterInstanceGuid == subscription.AdapterInstanceGuid);

                if (processing == null || processing.Status != "Processed")
                {
                    // At least one subscribed adapter hasn't processed the message
                    return false;
                }
            }

            // All subscribed adapters have processed the message
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking if all subscribed adapters processed: MessageId={MessageId}", messageId);
            throw;
        }
    }

    public async Task<List<Guid>> GetPendingAdapterInstancesAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the message to find its interface name
            var message = await _context.Set<MessageBoxMessage>()
                .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

            if (message == null)
            {
                _logger?.LogWarning("Message not found: MessageId={MessageId}", messageId);
                return new List<Guid>();
            }

            // Get all active subscriptions for this interface
            var subscriptions = await _subscriptionService.GetSubscriptionsForInterfaceAsync(
                message.InterfaceName, cancellationToken);

            // Get all processing records for this message
            var processingRecords = await _context.Set<MessageProcessing>()
                .Where(p => p.MessageId == messageId)
                .ToListAsync(cancellationToken);

            // Find subscribed adapters that haven't processed the message yet
            var pendingAdapters = new List<Guid>();

            foreach (var subscription in subscriptions)
            {
                var processing = processingRecords.FirstOrDefault(p => 
                    p.AdapterInstanceGuid == subscription.AdapterInstanceGuid);

                if (processing == null || processing.Status != "Processed")
                {
                    pendingAdapters.Add(subscription.AdapterInstanceGuid);
                }
            }

            return pendingAdapters;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting pending adapter instances: MessageId={MessageId}", messageId);
            throw;
        }
    }
}



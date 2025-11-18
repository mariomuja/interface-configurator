using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for managing message subscriptions and tracking adapter processing status
/// </summary>
public class MessageSubscriptionService : IMessageSubscriptionService
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<MessageSubscriptionService>? _logger;

    public MessageSubscriptionService(
        MessageBoxDbContext context,
        ILogger<MessageSubscriptionService>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    public async Task CreateSubscriptionAsync(
        Guid messageId,
        string interfaceName,
        string subscriberAdapterName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));
        if (string.IsNullOrWhiteSpace(subscriberAdapterName))
            throw new ArgumentException("Subscriber adapter name cannot be empty", nameof(subscriberAdapterName));

        try
        {
            // Check if subscription already exists
            var existing = await _context.Set<MessageSubscription>()
                .FirstOrDefaultAsync(s => s.MessageId == messageId && s.SubscriberAdapterName == subscriberAdapterName, cancellationToken);

            if (existing != null)
            {
                _logger?.LogDebug("Subscription already exists: MessageId={MessageId}, Subscriber={Subscriber}",
                    messageId, subscriberAdapterName);
                return;
            }

            var subscription = new MessageSubscription
            {
                MessageId = messageId,
                InterfaceName = interfaceName,
                SubscriberAdapterName = subscriberAdapterName,
                Status = "Pending",
                datetime_created = DateTime.UtcNow
            };

            _context.Set<MessageSubscription>().Add(subscription);
            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Created subscription: MessageId={MessageId}, Interface={InterfaceName}, Subscriber={Subscriber}",
                messageId, interfaceName, subscriberAdapterName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating subscription: MessageId={MessageId}, Subscriber={Subscriber}",
                messageId, subscriberAdapterName);
            throw;
        }
    }

    public async Task MarkSubscriptionAsProcessedAsync(
        Guid messageId,
        string subscriberAdapterName,
        string? processingDetails = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _context.Set<MessageSubscription>()
                .FirstOrDefaultAsync(s => s.MessageId == messageId && s.SubscriberAdapterName == subscriberAdapterName, cancellationToken);

            if (subscription == null)
            {
                _logger?.LogWarning("Subscription not found: MessageId={MessageId}, Subscriber={Subscriber}",
                    messageId, subscriberAdapterName);
                return;
            }

            subscription.Status = "Processed";
            subscription.datetime_processed = DateTime.UtcNow;
            subscription.ProcessingDetails = processingDetails;

            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Marked subscription as processed: MessageId={MessageId}, Subscriber={Subscriber}",
                messageId, subscriberAdapterName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error marking subscription as processed: MessageId={MessageId}, Subscriber={Subscriber}",
                messageId, subscriberAdapterName);
            throw;
        }
    }

    public async Task MarkSubscriptionAsErrorAsync(
        Guid messageId,
        string subscriberAdapterName,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty", nameof(errorMessage));

        try
        {
            var subscription = await _context.Set<MessageSubscription>()
                .FirstOrDefaultAsync(s => s.MessageId == messageId && s.SubscriberAdapterName == subscriberAdapterName, cancellationToken);

            if (subscription == null)
            {
                _logger?.LogWarning("Subscription not found: MessageId={MessageId}, Subscriber={Subscriber}",
                    messageId, subscriberAdapterName);
                return;
            }

            subscription.Status = "Error";
            subscription.datetime_processed = DateTime.UtcNow;
            subscription.ErrorMessage = errorMessage;

            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogError("Marked subscription as error: MessageId={MessageId}, Subscriber={Subscriber}, Error={Error}",
                messageId, subscriberAdapterName, errorMessage);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error marking subscription as error: MessageId={MessageId}, Subscriber={Subscriber}",
                messageId, subscriberAdapterName);
            throw;
        }
    }

    public async Task<bool> AreAllSubscriptionsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptions = await _context.Set<MessageSubscription>()
                .Where(s => s.MessageId == messageId)
                .ToListAsync(cancellationToken);

            if (subscriptions.Count == 0)
            {
                // No subscriptions means message can be removed (no destination adapters subscribed)
                return true;
            }

            // All subscriptions must be "Processed" (not "Pending" or "Error")
            return subscriptions.All(s => s.Status == "Processed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking subscription status: MessageId={MessageId}", messageId);
            throw;
        }
    }

    public async Task<List<string>> GetPendingSubscribersAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptions = await _context.Set<MessageSubscription>()
                .Where(s => s.MessageId == messageId && s.Status == "Pending")
                .Select(s => s.SubscriberAdapterName)
                .ToListAsync(cancellationToken);

            return subscriptions;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting pending subscribers: MessageId={MessageId}", messageId);
            throw;
        }
    }
}





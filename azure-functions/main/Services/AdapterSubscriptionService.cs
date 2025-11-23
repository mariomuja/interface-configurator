using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for managing adapter subscriptions (BizTalk-style)
/// Subscriptions define filter criteria for which messages an adapter receives from the MessageBox
/// </summary>
public class AdapterSubscriptionService : IAdapterSubscriptionService
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<AdapterSubscriptionService>? _logger;

    public AdapterSubscriptionService(
        MessageBoxDbContext context,
        ILogger<AdapterSubscriptionService>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    public async Task<AdapterSubscription> CreateOrUpdateSubscriptionAsync(
        Guid adapterInstanceGuid,
        string interfaceName,
        string adapterName,
        string? filterCriteria = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));
        if (string.IsNullOrWhiteSpace(adapterName))
            throw new ArgumentException("Adapter name cannot be empty", nameof(adapterName));

        try
        {
            // Check if subscription already exists for this adapter instance and interface
            var existing = await _context.Set<AdapterSubscription>()
                .FirstOrDefaultAsync(s => 
                    s.AdapterInstanceGuid == adapterInstanceGuid && 
                    s.InterfaceName == interfaceName, 
                    cancellationToken);

            if (existing != null)
            {
                // Update existing subscription
                existing.FilterCriteria = filterCriteria;
                existing.IsEnabled = true;
                existing.datetime_updated = DateTime.UtcNow;

                await _context.SaveChangesAsync(cancellationToken);

                _logger?.LogInformation("Updated subscription: AdapterInstanceGuid={AdapterInstanceGuid}, Interface={InterfaceName}",
                    adapterInstanceGuid, interfaceName);

                return existing;
            }

            // Create new subscription
            var subscription = new AdapterSubscription
            {
                AdapterInstanceGuid = adapterInstanceGuid,
                InterfaceName = interfaceName,
                AdapterName = adapterName,
                FilterCriteria = filterCriteria,
                IsEnabled = true,
                datetime_created = DateTime.UtcNow
            };

            _context.Set<AdapterSubscription>().Add(subscription);
            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Created subscription: AdapterInstanceGuid={AdapterInstanceGuid}, Interface={InterfaceName}, Adapter={AdapterName}",
                adapterInstanceGuid, interfaceName, adapterName);

            return subscription;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating/updating subscription: AdapterInstanceGuid={AdapterInstanceGuid}, Interface={InterfaceName}",
                adapterInstanceGuid, interfaceName);
            throw;
        }
    }

    public async Task<List<AdapterSubscription>> GetSubscriptionsForInterfaceAsync(
        string interfaceName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new ArgumentException("Interface name cannot be empty", nameof(interfaceName));

        try
        {
            var subscriptions = await _context.Set<AdapterSubscription>()
                .Where(s => s.InterfaceName == interfaceName && s.IsEnabled)
                .ToListAsync(cancellationToken);

            return subscriptions;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting subscriptions for interface: Interface={InterfaceName}", interfaceName);
            throw;
        }
    }

    public async Task<List<AdapterSubscription>> GetSubscriptionsForAdapterAsync(
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriptions = await _context.Set<AdapterSubscription>()
                .Where(s => s.AdapterInstanceGuid == adapterInstanceGuid && s.IsEnabled)
                .ToListAsync(cancellationToken);

            return subscriptions;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting subscriptions for adapter: AdapterInstanceGuid={AdapterInstanceGuid}", adapterInstanceGuid);
            throw;
        }
    }

    public async Task EnableSubscriptionAsync(int subscriptionId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _context.Set<AdapterSubscription>()
                .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);

            if (subscription == null)
            {
                _logger?.LogWarning("Subscription not found: SubscriptionId={SubscriptionId}", subscriptionId);
                return;
            }

            subscription.IsEnabled = isEnabled;
            subscription.datetime_updated = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Subscription {Action}: SubscriptionId={SubscriptionId}",
                isEnabled ? "enabled" : "disabled", subscriptionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error enabling/disabling subscription: SubscriptionId={SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public async Task DeleteSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _context.Set<AdapterSubscription>()
                .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);

            if (subscription == null)
            {
                _logger?.LogWarning("Subscription not found: SubscriptionId={SubscriptionId}", subscriptionId);
                return;
            }

            _context.Set<AdapterSubscription>().Remove(subscription);
            await _context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation("Deleted subscription: SubscriptionId={SubscriptionId}", subscriptionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting subscription: SubscriptionId={SubscriptionId}", subscriptionId);
            throw;
        }
    }
}



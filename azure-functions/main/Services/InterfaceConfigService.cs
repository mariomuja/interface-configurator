using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for managing interface configuration and adapter instances in the database
/// Replaces MessageBoxService - only contains methods needed for interface configuration
/// The database (formerly MessageBox) now stores:
/// - Interface configurations
/// - Adapter instances
/// - Process logs
/// </summary>
public class InterfaceConfigService : IInterfaceConfigService
{
    private readonly InterfaceConfigDbContext _context;
    private readonly ILogger<InterfaceConfigService>? _logger;

    public InterfaceConfigService(
        InterfaceConfigDbContext context,
        ILogger<InterfaceConfigService>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
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
}


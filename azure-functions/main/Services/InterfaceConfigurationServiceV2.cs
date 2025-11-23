using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable CS0618 // Type or member is obsolete - Deprecated properties are used for backward compatibility migration

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Interface Configuration Service using MessageBoxDbContext (moved from Blob Storage)
/// </summary>
public class InterfaceConfigurationServiceV2 : IInterfaceConfigurationService
{
    private const string DefaultInterfaceName = "FromCsvToSqlServerExample";
    
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<InterfaceConfigurationServiceV2>? _logger;
    private readonly IServiceProvider? _serviceProvider;

    public InterfaceConfigurationServiceV2(
        MessageBoxDbContext context,
        ILogger<InterfaceConfigurationServiceV2>? logger = null,
        IServiceProvider? serviceProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure database and tables exist
            await _context.Database.EnsureCreatedAsync(cancellationToken);

            // Create default configuration if none exists
            var existingConfig = await _context.InterfaceConfigurations
                .FirstOrDefaultAsync(c => c.InterfaceName == DefaultInterfaceName, cancellationToken);

            if (existingConfig == null)
            {
                var defaultConfiguration = CreateDefaultInterfaceConfiguration();
                await SaveConfigurationAsync(defaultConfiguration, cancellationToken);
                _logger?.LogInformation("Created default interface configuration '{InterfaceName}'", DefaultInterfaceName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing interface configuration service");
            throw;
        }
    }

    public async Task<List<InterfaceConfiguration>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var configs = await _context.InterfaceConfigurations
                .Include(c => c.Sources)
                .Include(c => c.Destinations)
                .ToListAsync(cancellationToken);

            var result = new List<InterfaceConfiguration>();
            foreach (var config in configs)
            {
                var interfaceConfig = await LoadFullConfigurationAsync(config.InterfaceName, cancellationToken);
                if (interfaceConfig != null)
                {
                    result.Add(interfaceConfig);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting all interface configurations");
            throw;
        }
    }

    public async Task<InterfaceConfiguration?> GetConfigurationAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoadFullConfigurationAsync(interfaceName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting interface configuration for {InterfaceName}", interfaceName);
            throw;
        }
    }

    public async Task<List<InterfaceConfiguration>> GetEnabledSourceConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allConfigs = await GetAllConfigurationsAsync(cancellationToken);
            return allConfigs
                .Where(c => c.Sources.Values.Any(s => s.IsEnabled))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting enabled source configurations");
            throw;
        }
    }

    public async Task<List<InterfaceConfiguration>> GetEnabledDestinationConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allConfigs = await GetAllConfigurationsAsync(cancellationToken);
            return allConfigs
                .Where(c => c.Destinations.Values.Any(d => d.IsEnabled))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting enabled destination configurations");
            throw;
        }
    }

    public async Task SaveConfigurationAsync(InterfaceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _context.InterfaceConfigurations
                .FirstOrDefaultAsync(c => c.InterfaceName == configuration.InterfaceName, cancellationToken);

            if (existing == null)
            {
                configuration.CreatedAt = DateTime.UtcNow;
                _context.InterfaceConfigurations.Add(configuration);
            }
            else
            {
                existing.Description = configuration.Description;
                existing.UpdatedAt = DateTime.UtcNow;
                _context.InterfaceConfigurations.Update(existing);
            }

            // Save SourceAdapterInstances
            // Note: In a full implementation, we would store InterfaceName as a foreign key
            // For now, we save them and they will be linked via LoadFullConfigurationAsync
            foreach (var source in configuration.Sources.Values)
            {
                var existingSource = await _context.SourceAdapterInstances
                    .FirstOrDefaultAsync(s => s.AdapterInstanceGuid == source.AdapterInstanceGuid, cancellationToken);

                if (existingSource == null)
                {
                    source.CreatedAt = DateTime.UtcNow;
                    _context.SourceAdapterInstances.Add(source);
                }
                else
                {
                    UpdateSourceAdapterInstance(existingSource, source);
                    existingSource.UpdatedAt = DateTime.UtcNow;
                    _context.SourceAdapterInstances.Update(existingSource);
                }
            }

            // Save DestinationAdapterInstances
            // Note: In a full implementation, we would store InterfaceName as a foreign key
            foreach (var destination in configuration.Destinations.Values)
            {
                var existingDest = await _context.DestinationAdapterInstances
                    .FirstOrDefaultAsync(d => d.AdapterInstanceGuid == destination.AdapterInstanceGuid, cancellationToken);

                if (existingDest == null)
                {
                    destination.CreatedAt = DateTime.UtcNow;
                    _context.DestinationAdapterInstances.Add(destination);
                }
                else
                {
                    UpdateDestinationAdapterInstance(existingDest, destination);
                    existingDest.UpdatedAt = DateTime.UtcNow;
                    _context.DestinationAdapterInstances.Update(existingDest);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger?.LogInformation("Saved interface configuration '{InterfaceName}'", configuration.InterfaceName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving interface configuration '{InterfaceName}'", configuration.InterfaceName);
            throw;
        }
    }

    public async Task DeleteConfigurationAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _context.InterfaceConfigurations
                .FirstOrDefaultAsync(c => c.InterfaceName == interfaceName, cancellationToken);

            if (config != null)
            {
                // Delete related adapter instances
                var sources = await _context.SourceAdapterInstances
                    .Where(s => s.AdapterInstanceGuid == config.Sources.Values.Select(s => s.AdapterInstanceGuid).FirstOrDefault())
                    .ToListAsync(cancellationToken);
                _context.SourceAdapterInstances.RemoveRange(sources);

                var destinations = await _context.DestinationAdapterInstances
                    .Where(d => config.Destinations.Values.Select(d => d.AdapterInstanceGuid).Contains(d.AdapterInstanceGuid))
                    .ToListAsync(cancellationToken);
                _context.DestinationAdapterInstances.RemoveRange(destinations);

                _context.InterfaceConfigurations.Remove(config);
                await _context.SaveChangesAsync(cancellationToken);
                _logger?.LogInformation("Deleted interface configuration '{InterfaceName}'", interfaceName);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting interface configuration '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task SetSourceEnabledAsync(string interfaceName, bool enabled, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var source in config.Sources.Values)
                {
                    source.IsEnabled = enabled;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting source enabled for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task SetDestinationEnabledAsync(string interfaceName, bool enabled, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var destination in config.Destinations.Values)
                {
                    var wasEnabled = destination.IsEnabled;
                    destination.IsEnabled = enabled;
                    
                    // Automatically create subscription when destination adapter is enabled
                    // and has a SourceAdapterSubscription configured
                    if (enabled && !wasEnabled && destination.SourceAdapterSubscription.HasValue)
                    {
                        // Create subscription asynchronously (fire and forget)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                if (_serviceProvider != null)
                                {
                                    var subscriptionService = _serviceProvider.GetService<IAdapterSubscriptionService>();
                                    if (subscriptionService != null)
                                    {
                                        await subscriptionService.CreateOrUpdateSubscriptionAsync(
                                            destination.AdapterInstanceGuid,
                                            interfaceName,
                                            destination.AdapterName,
                                            filterCriteria: null, // No filter criteria - subscribe to all messages from the interface
                                            cancellationToken);
                                        
                                        _logger?.LogInformation("Automatically created subscription for destination adapter '{InstanceName}' ({AdapterInstanceGuid}) in interface '{InterfaceName}' when enabled",
                                            destination.InstanceName, destination.AdapterInstanceGuid, interfaceName);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Error automatically creating subscription for destination adapter '{InstanceName}' ({AdapterInstanceGuid}) in interface '{InterfaceName}'",
                                    destination.InstanceName, destination.AdapterInstanceGuid, interfaceName);
                            }
                        }, cancellationToken);
                    }
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error setting destination enabled for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    private async Task<InterfaceConfiguration?> LoadFullConfigurationAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var config = await _context.InterfaceConfigurations
            .FirstOrDefaultAsync(c => c.InterfaceName == interfaceName, cancellationToken);

        if (config == null)
            return null;

        // Note: In a full implementation, we would need InterfaceName as a foreign key in SourceAdapterInstances and DestinationAdapterInstances
        // For now, we load all and filter by matching GUIDs from the configuration's Sources/Destinations dictionaries
        // This is a simplified approach - in production, add InterfaceName property to adapter instance models
        
        // Load all SourceAdapterInstances and DestinationAdapterInstances
        // In a real implementation, these would be filtered by InterfaceName via foreign key
        var allSources = await _context.SourceAdapterInstances.ToListAsync(cancellationToken);
        var allDestinations = await _context.DestinationAdapterInstances.ToListAsync(cancellationToken);

        // Reconstruct Sources and Destinations dictionaries
        // For now, we'll use the first source/destination found (simplified)
        // In production, this should use proper foreign key relationships
        config.Sources = allSources
            .Where(s => config.Sources.Values.Any(cs => cs.AdapterInstanceGuid == s.AdapterInstanceGuid))
            .ToDictionary(s => s.InstanceName, s => s);
        
        // If no sources found, create default
        if (config.Sources.Count == 0 && allSources.Count > 0)
        {
            config.Sources = allSources.Take(1).ToDictionary(s => s.InstanceName, s => s);
        }

        config.Destinations = allDestinations
            .Where(d => config.Destinations.Values.Any(cd => cd.AdapterInstanceGuid == d.AdapterInstanceGuid))
            .ToDictionary(d => d.InstanceName, d => d);
        
        // If no destinations found, create default
        if (config.Destinations.Count == 0 && allDestinations.Count > 0)
        {
            config.Destinations = allDestinations.Take(1).ToDictionary(d => d.InstanceName, d => d);
        }

        return config;
    }

    private void UpdateSourceAdapterInstance(SourceAdapterInstance existing, SourceAdapterInstance updated)
    {
        existing.InstanceName = updated.InstanceName;
        existing.AdapterName = updated.AdapterName;
        existing.IsEnabled = updated.IsEnabled;
        existing.Configuration = updated.Configuration;
        existing.SourceReceiveFolder = updated.SourceReceiveFolder;
        existing.SourceFileMask = updated.SourceFileMask;
        existing.SourceBatchSize = updated.SourceBatchSize;
        existing.SourceFieldSeparator = updated.SourceFieldSeparator;
        existing.CsvData = updated.CsvData;
        existing.CsvAdapterType = updated.CsvAdapterType;
        existing.CsvPollingInterval = updated.CsvPollingInterval;
        // ... update all other properties
    }

    private void UpdateDestinationAdapterInstance(DestinationAdapterInstance existing, DestinationAdapterInstance updated)
    {
        existing.InstanceName = updated.InstanceName;
        existing.AdapterName = updated.AdapterName;
        existing.IsEnabled = updated.IsEnabled;
        existing.Configuration = updated.Configuration;
        existing.DestinationReceiveFolder = updated.DestinationReceiveFolder;
        existing.DestinationFileMask = updated.DestinationFileMask;
        // ... update all other properties
    }

    private InterfaceConfiguration CreateDefaultInterfaceConfiguration()
    {
        // Implementation similar to original, but simplified
        var config = new InterfaceConfiguration
        {
            InterfaceName = DefaultInterfaceName,
            Description = "Default CSV to SQL Server interface",
            CreatedAt = DateTime.UtcNow
        };

        var defaultSource = CreateDefaultSourceInstance(config);
        config.Sources[defaultSource.InstanceName] = defaultSource;

        var defaultDest = CreateDefaultDestinationInstance(config);
        config.Destinations[defaultDest.InstanceName] = defaultDest;

        return config;
    }

    private SourceAdapterInstance CreateDefaultSourceInstance(InterfaceConfiguration config)
    {
        return new SourceAdapterInstance
        {
            InstanceName = "Source",
            AdapterName = "CSV",
            IsEnabled = true,
            AdapterInstanceGuid = Guid.NewGuid(),
            SourceReceiveFolder = "csv-files/csv-incoming",
            SourceFileMask = "*.csv",
            SourceBatchSize = 100,
            SourceFieldSeparator = "║",
            CsvAdapterType = "FILE",
            CsvPollingInterval = 10,
            CreatedAt = DateTime.UtcNow
        };
    }

    private DestinationAdapterInstance CreateDefaultDestinationInstance(InterfaceConfiguration config)
    {
        return new DestinationAdapterInstance
        {
            InstanceName = "Destination",
            AdapterName = "SqlServer",
            IsEnabled = true,
            AdapterInstanceGuid = Guid.NewGuid(),
            Configuration = "{\"destination\": \"TransportData\"}",
            SqlTableName = "TransportData",
            SqlUseTransaction = true,
            SqlBatchSize = 1000,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Additional methods required by IInterfaceConfigurationService interface
    public async Task UpdateInterfaceNameAsync(string oldInterfaceName, string newInterfaceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(oldInterfaceName, cancellationToken);
            if (config != null)
            {
                await DeleteConfigurationAsync(oldInterfaceName, cancellationToken);
                config.InterfaceName = newInterfaceName;
                await SaveConfigurationAsync(config, cancellationToken);
                _logger?.LogInformation("Interface name updated from '{OldName}' to '{NewName}'", oldInterfaceName, newInterfaceName);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{oldInterfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating interface name from '{OldName}' to '{NewName}'", oldInterfaceName, newInterfaceName);
            throw;
        }
    }

    public async Task UpdateInstanceNameAsync(string interfaceName, string instanceType, string instanceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                if (instanceType.Equals("Source", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var source in config.Sources.Values)
                    {
                        source.InstanceName = instanceName;
                    }
                }
                else if (instanceType.Equals("Destination", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var dest in config.Destinations.Values)
                    {
                        dest.InstanceName = instanceName;
                    }
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating instance name for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateReceiveFolderAsync(string interfaceName, string receiveFolder, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var source in config.Sources.Values)
                {
                    source.SourceReceiveFolder = receiveFolder;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating receive folder for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateFileMaskAsync(string interfaceName, string fileMask, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var source in config.Sources.Values)
                {
                    source.SourceFileMask = fileMask;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating file mask for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateBatchSizeAsync(string interfaceName, int batchSize, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var source in config.Sources.Values)
                {
                    source.SourceBatchSize = batchSize;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating batch size for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateSqlConnectionPropertiesAsync(
        string interfaceName,
        string? serverName,
        string? databaseName,
        string? userName,
        string? password,
        bool? integratedSecurity,
        string? resourceGroup,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var source in config.Sources.Values)
                {
                    if (serverName != null) source.SqlServerName = serverName;
                    if (databaseName != null) source.SqlDatabaseName = databaseName;
                    if (userName != null) source.SqlUserName = userName;
                    if (password != null) source.SqlPassword = password;
                    if (integratedSecurity.HasValue) source.SqlIntegratedSecurity = integratedSecurity.Value;
                    if (resourceGroup != null) source.SqlResourceGroup = resourceGroup;
                }
                foreach (var dest in config.Destinations.Values)
                {
                    if (serverName != null) dest.SqlServerName = serverName;
                    if (databaseName != null) dest.SqlDatabaseName = databaseName;
                    if (userName != null) dest.SqlUserName = userName;
                    if (password != null) dest.SqlPassword = password;
                    if (integratedSecurity.HasValue) dest.SqlIntegratedSecurity = integratedSecurity.Value;
                    if (resourceGroup != null) dest.SqlResourceGroup = resourceGroup;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating SQL connection properties for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateSqlPollingPropertiesAsync(
        string interfaceName,
        string? pollingStatement,
        int? pollingInterval,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var source in config.Sources.Values)
                {
                    if (pollingStatement != null) source.SqlPollingStatement = pollingStatement;
                    if (pollingInterval.HasValue) source.SqlPollingInterval = pollingInterval.Value;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating SQL polling properties for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateCsvPollingIntervalAsync(string interfaceName, int pollingInterval, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var source in config.Sources.Values)
                {
                    source.CsvPollingInterval = pollingInterval;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating CSV polling interval for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateFieldSeparatorAsync(string interfaceName, string fieldSeparator, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var source in config.Sources.Values)
                {
                    source.SourceFieldSeparator = fieldSeparator;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating field separator for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateCsvDataAsync(string interfaceName, string? csvData, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var source in config.Sources.Values)
                {
                    source.CsvData = csvData;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating CSV data for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateDestinationReceiveFolderAsync(string interfaceName, string destinationReceiveFolder, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var dest in config.Destinations.Values)
                {
                    dest.DestinationReceiveFolder = destinationReceiveFolder;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating destination receive folder for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateDestinationFileMaskAsync(string interfaceName, string destinationFileMask, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var dest in config.Destinations.Values)
                {
                    dest.DestinationFileMask = destinationFileMask;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating destination file mask for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task<List<DestinationAdapterInstance>> GetDestinationAdapterInstancesAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                return config.Destinations.Values.ToList();
            }
            return new List<DestinationAdapterInstance>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting destination adapter instances for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task<DestinationAdapterInstance> AddDestinationAdapterInstanceAsync(
        string interfaceName,
        string adapterName,
        string instanceName,
        string configuration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                var newInstance = new DestinationAdapterInstance
                {
                    AdapterInstanceGuid = Guid.NewGuid(),
                    InstanceName = instanceName,
                    AdapterName = adapterName,
                    IsEnabled = true,
                    Configuration = configuration,
                    CreatedAt = DateTime.UtcNow
                };
                config.Destinations[newInstance.InstanceName] = newInstance;
                await SaveConfigurationAsync(config, cancellationToken);
                return newInstance;
            }
            throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding destination adapter instance for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task RemoveDestinationAdapterInstanceAsync(string interfaceName, Guid adapterInstanceGuid, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                var instanceToRemove = config.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == adapterInstanceGuid);
                if (instanceToRemove != null)
                {
                    config.Destinations.Remove(instanceToRemove.InstanceName);
                    await SaveConfigurationAsync(config, cancellationToken);
                }
                else
                {
                    throw new KeyNotFoundException($"Destination adapter instance '{adapterInstanceGuid}' not found.");
                }
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing destination adapter instance for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateDestinationAdapterInstanceAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        string? instanceName = null,
        bool? isEnabled = null,
        string? configuration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                var instance = config.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == adapterInstanceGuid);
                if (instance != null)
                {
                    if (instanceName != null) instance.InstanceName = instanceName;
                    if (isEnabled.HasValue) instance.IsEnabled = isEnabled.Value;
                    if (configuration != null) instance.Configuration = configuration;
                    instance.UpdatedAt = DateTime.UtcNow;
                    await SaveConfigurationAsync(config, cancellationToken);
                }
                else
                {
                    throw new KeyNotFoundException($"Destination adapter instance '{adapterInstanceGuid}' not found.");
                }
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating destination adapter instance for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateSqlTransactionPropertiesAsync(
        string interfaceName,
        bool? useTransaction = null,
        int? batchSize = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                foreach (var dest in config.Destinations.Values)
                {
                    if (useTransaction.HasValue) dest.SqlUseTransaction = useTransaction.Value;
                    if (batchSize.HasValue) dest.SqlBatchSize = batchSize.Value;
                }
                await SaveConfigurationAsync(config, cancellationToken);
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating SQL transaction properties for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateDestinationJQScriptFileAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        string jqScriptFile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                var instance = config.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == adapterInstanceGuid);
                if (instance != null)
                {
                    instance.JQScriptFile = string.IsNullOrWhiteSpace(jqScriptFile) ? null : jqScriptFile.Trim();
                    instance.UpdatedAt = DateTime.UtcNow;
                    await SaveConfigurationAsync(config, cancellationToken);
                    _logger?.LogInformation("JQ Script File for destination adapter instance '{InstanceName}' ({AdapterInstanceGuid}) in interface '{InterfaceName}' updated to '{JQScriptFile}'",
                        instance.InstanceName, adapterInstanceGuid, interfaceName, instance.JQScriptFile ?? "null");
                }
                else
                {
                    throw new KeyNotFoundException($"Destination adapter instance '{adapterInstanceGuid}' not found in interface '{interfaceName}'.");
                }
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating JQ Script File for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateDestinationSourceAdapterSubscriptionAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        Guid? sourceAdapterSubscription,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                var instance = config.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == adapterInstanceGuid);
                if (instance != null)
                {
                    instance.SourceAdapterSubscription = sourceAdapterSubscription;
                    instance.UpdatedAt = DateTime.UtcNow;
                    await SaveConfigurationAsync(config, cancellationToken);
                    _logger?.LogInformation("Source Adapter Subscription for destination adapter instance '{InstanceName}' ({AdapterInstanceGuid}) in interface '{InterfaceName}' updated to '{SourceAdapterSubscription}'",
                        instance.InstanceName, adapterInstanceGuid, interfaceName, sourceAdapterSubscription?.ToString() ?? "null");
                }
                else
                {
                    throw new KeyNotFoundException($"Destination adapter instance '{adapterInstanceGuid}' not found in interface '{interfaceName}'.");
                }
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating Source Adapter Subscription for '{InterfaceName}'", interfaceName);
            throw;
        }
    }

    public async Task UpdateDestinationSqlStatementsAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        string insertStatement,
        string updateStatement,
        string deleteStatement,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await LoadFullConfigurationAsync(interfaceName, cancellationToken);
            if (config != null)
            {
                var instance = config.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == adapterInstanceGuid);
                if (instance != null)
                {
                    instance.InsertStatement = string.IsNullOrWhiteSpace(insertStatement) ? null : insertStatement.Trim();
                    instance.UpdateStatement = string.IsNullOrWhiteSpace(updateStatement) ? null : updateStatement.Trim();
                    instance.DeleteStatement = string.IsNullOrWhiteSpace(deleteStatement) ? null : deleteStatement.Trim();
                    instance.UpdatedAt = DateTime.UtcNow;
                    await SaveConfigurationAsync(config, cancellationToken);
                    _logger?.LogInformation("SQL Statements for destination adapter instance '{InstanceName}' ({AdapterInstanceGuid}) in interface '{InterfaceName}' updated",
                        instance.InstanceName, adapterInstanceGuid, interfaceName);
                }
                else
                {
                    throw new KeyNotFoundException($"Destination adapter instance '{adapterInstanceGuid}' not found in interface '{interfaceName}'.");
                }
            }
            else
            {
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error updating SQL Statements for '{InterfaceName}'", interfaceName);
            throw;
        }
    }
}


using ProcessCsvBlobTrigger.Core.Models;

namespace ProcessCsvBlobTrigger.Core.Interfaces;

/// <summary>
/// Service for managing interface configurations (Source -> Destination mappings)
/// </summary>
public interface IInterfaceConfigurationService
{
    /// <summary>
    /// Initialize the service (load configurations from storage)
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all interface configurations
    /// </summary>
    Task<List<InterfaceConfiguration>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific interface configuration by name
    /// </summary>
    Task<InterfaceConfiguration?> GetConfigurationAsync(string interfaceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all enabled source configurations
    /// </summary>
    Task<List<InterfaceConfiguration>> GetEnabledSourceConfigurationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all enabled destination configurations
    /// </summary>
    Task<List<InterfaceConfiguration>> GetEnabledDestinationConfigurationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save or update an interface configuration
    /// </summary>
    Task SaveConfigurationAsync(InterfaceConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an interface configuration
    /// </summary>
    Task DeleteConfigurationAsync(string interfaceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enable or disable an interface configuration
    /// </summary>
    Task SetEnabledAsync(string interfaceName, bool enabled, CancellationToken cancellationToken = default);
}


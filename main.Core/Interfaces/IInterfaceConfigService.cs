using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Service for managing interface configuration and adapter instances in the database
/// Replaces IMessageBoxService - only contains methods needed for interface configuration
/// </summary>
public interface IInterfaceConfigService
{
    /// <summary>
    /// Ensures an adapter instance exists in the AdapterInstances table
    /// Creates or updates the adapter instance record
    /// </summary>
    /// <param name="adapterInstanceGuid">GUID identifying the adapter instance</param>
    /// <param name="interfaceName">Name of the interface</param>
    /// <param name="instanceName">User-editable instance name</param>
    /// <param name="adapterName">Name of the adapter</param>
    /// <param name="adapterType">Type of adapter: "Source" or "Destination"</param>
    /// <param name="isEnabled">Whether the adapter instance is enabled</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnsureAdapterInstanceAsync(
        Guid adapterInstanceGuid,
        string interfaceName,
        string instanceName,
        string adapterName,
        string adapterType,
        bool isEnabled,
        CancellationToken cancellationToken = default);
}


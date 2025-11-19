using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Core.Interfaces;

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
    /// Enable or disable the Source adapter for an interface configuration
    /// </summary>
    Task SetSourceEnabledAsync(string interfaceName, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enable or disable the Destination adapter for an interface configuration
    /// </summary>
    Task SetDestinationEnabledAsync(string interfaceName, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the name of an interface configuration.
    /// </summary>
    Task UpdateInterfaceNameAsync(string oldInterfaceName, string newInterfaceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the instance name (Source or Destination) for an interface configuration.
    /// </summary>
    Task UpdateInstanceNameAsync(string interfaceName, string instanceType, string instanceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the receive folder for the Source adapter of an interface configuration.
    /// </summary>
    Task UpdateReceiveFolderAsync(string interfaceName, string receiveFolder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the file mask for the Source adapter of an interface configuration.
    /// </summary>
    Task UpdateFileMaskAsync(string interfaceName, string fileMask, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the batch size for the Source adapter of an interface configuration.
    /// </summary>
    Task UpdateBatchSizeAsync(string interfaceName, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update SQL Server connection properties for an interface configuration.
    /// </summary>
    Task UpdateSqlConnectionPropertiesAsync(
        string interfaceName,
        string? serverName,
        string? databaseName,
        string? userName,
        string? password,
        bool? integratedSecurity,
        string? resourceGroup,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update SQL Server polling properties for Source adapter.
    /// </summary>
    Task UpdateSqlPollingPropertiesAsync(
        string interfaceName,
        string? pollingStatement,
        int? pollingInterval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the polling interval for the CSV adapter.
    /// </summary>
    Task UpdateCsvPollingIntervalAsync(string interfaceName, int pollingInterval, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the field separator for the CSV adapter.
    /// </summary>
    Task UpdateFieldSeparatorAsync(string interfaceName, string fieldSeparator, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the CSV data property for the CSV adapter. When set, the adapter will debatch and send this data to the MessageBox.
    /// </summary>
    Task UpdateCsvDataAsync(string interfaceName, string? csvData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the destination receive folder for the CSV adapter when used as destination.
    /// </summary>
    Task UpdateDestinationReceiveFolderAsync(string interfaceName, string destinationReceiveFolder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update the destination file mask for the CSV adapter when used as destination.
    /// </summary>
    Task UpdateDestinationFileMaskAsync(string interfaceName, string destinationFileMask, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all destination adapter instances for an interface.
    /// </summary>
    Task<List<DestinationAdapterInstance>> GetDestinationAdapterInstancesAsync(string interfaceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new destination adapter instance to an interface.
    /// </summary>
    Task<DestinationAdapterInstance> AddDestinationAdapterInstanceAsync(
        string interfaceName,
        string adapterName,
        string instanceName,
        string configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a destination adapter instance from an interface.
    /// </summary>
    Task RemoveDestinationAdapterInstanceAsync(string interfaceName, Guid adapterInstanceGuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a destination adapter instance.
    /// </summary>
    Task UpdateDestinationAdapterInstanceAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        string? instanceName = null,
        bool? isEnabled = null,
        string? configuration = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update SQL Server transaction and batch size properties.
    /// </summary>
    Task UpdateSqlTransactionPropertiesAsync(
        string interfaceName,
        bool? useTransaction = null,
        int? batchSize = null,
        CancellationToken cancellationToken = default);
}


using ProcessCsvBlobTrigger.Core.Models;

namespace ProcessCsvBlobTrigger.Core.Interfaces;

/// <summary>
/// Factory for creating adapter instances based on configuration
/// </summary>
public interface IAdapterFactory
{
    /// <summary>
    /// Create a source adapter instance for the given interface configuration
    /// </summary>
    Task<IAdapter> CreateSourceAdapterAsync(InterfaceConfiguration config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a destination adapter instance for the given interface configuration
    /// </summary>
    Task<IAdapter> CreateDestinationAdapterAsync(InterfaceConfiguration config, CancellationToken cancellationToken = default);
}


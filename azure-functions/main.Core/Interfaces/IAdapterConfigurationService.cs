namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Service for managing adapter configurations
/// Uses JSON file storage in Blob Storage with in-memory cache for fast access
/// Allows dynamic configuration of source and destination adapters (CSV, JSON, SAP, SQL Server, etc.)
/// </summary>
public interface IAdapterConfigurationService
{
    /// <summary>
    /// Initializes the service by loading settings from storage
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a setting value for a specific adapter
    /// </summary>
    Task<string?> GetSettingAsync(string adapterName, string adapterType, string settingKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets a setting value for a specific adapter
    /// </summary>
    Task SetSettingAsync(string adapterName, string adapterType, string settingKey, string? settingValue, string? description = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all settings for a specific adapter
    /// </summary>
    Task<Dictionary<string, string?>> GetAllSettingsAsync(string adapterName, string adapterType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the field separator for CSV adapter (with fallback to environment variable or default)
    /// </summary>
    Task<string> GetCsvFieldSeparatorAsync(CancellationToken cancellationToken = default);
}

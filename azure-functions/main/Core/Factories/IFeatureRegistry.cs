namespace InterfaceConfigurator.Main.Core.Factories;

/// <summary>
/// Central registry for checking feature enablement status
/// This is the single source of truth for feature toggles
/// </summary>
public interface IFeatureRegistry
{
    /// <summary>
    /// Checks if a feature is enabled by feature number
    /// </summary>
    Task<bool> IsFeatureEnabledAsync(int featureNumber);
    
    /// <summary>
    /// Checks if a feature is enabled by feature name
    /// </summary>
    Task<bool> IsFeatureEnabledByNameAsync(string featureName);
    
    /// <summary>
    /// Gets all enabled features
    /// </summary>
    Task<List<int>> GetEnabledFeatureNumbersAsync();
    
    /// <summary>
    /// Refreshes the feature cache (call after feature toggle changes)
    /// </summary>
    Task RefreshCacheAsync();
}



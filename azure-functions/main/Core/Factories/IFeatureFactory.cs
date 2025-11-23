namespace InterfaceConfigurator.Main.Core.Factories;

/// <summary>
/// Factory interface for creating service instances based on feature flags
/// This is the single point where feature toggles are checked
/// </summary>
/// <typeparam name="T">The service interface type</typeparam>
public interface IFeatureFactory<T> where T : class
{
    /// <summary>
    /// Creates an instance of the service based on feature flags
    /// If the feature is enabled, returns the new implementation
    /// If the feature is disabled, returns the old/legacy implementation
    /// </summary>
    Task<T> CreateAsync();
    
    /// <summary>
    /// Creates an instance synchronously (uses cached feature status)
    /// </summary>
    T Create();
}



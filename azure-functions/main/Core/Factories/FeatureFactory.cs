using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InterfaceConfigurator.Main.Core.Factories;

/// <summary>
/// Generic factory for creating service instances based on feature flags
/// This is the ONLY place where feature toggles are checked
/// </summary>
/// <typeparam name="T">The service interface type</typeparam>
public class FeatureFactory<T> : IFeatureFactory<T> where T : class
{
    private readonly IFeatureRegistry _featureRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _featureNumber;
    private readonly Func<IServiceProvider, T> _oldImplementationFactory;
    private readonly Func<IServiceProvider, T> _newImplementationFactory;
    private readonly ILogger<FeatureFactory<T>> _logger;

    public FeatureFactory(
        IFeatureRegistry featureRegistry,
        IServiceProvider serviceProvider,
        int featureNumber,
        Func<IServiceProvider, T> oldImplementationFactory,
        Func<IServiceProvider, T> newImplementationFactory,
        ILogger<FeatureFactory<T>> logger)
    {
        _featureRegistry = featureRegistry ?? throw new ArgumentNullException(nameof(featureRegistry));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _featureNumber = featureNumber;
        _oldImplementationFactory = oldImplementationFactory ?? throw new ArgumentNullException(nameof(oldImplementationFactory));
        _newImplementationFactory = newImplementationFactory ?? throw new ArgumentNullException(nameof(newImplementationFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<T> CreateAsync()
    {
        var isEnabled = await _featureRegistry.IsFeatureEnabledAsync(_featureNumber);
        
        if (isEnabled)
        {
            _logger.LogDebug("Feature #{FeatureNumber} is enabled, using new implementation for {ServiceType}", 
                _featureNumber, typeof(T).Name);
            return _newImplementationFactory(_serviceProvider);
        }
        else
        {
            _logger.LogDebug("Feature #{FeatureNumber} is disabled, using old implementation for {ServiceType}", 
                _featureNumber, typeof(T).Name);
            return _oldImplementationFactory(_serviceProvider);
        }
    }

    public T Create()
    {
        // Use synchronous check with cached feature status
        // This is faster but may use slightly stale data (cache is refreshed every 5 minutes)
        var isEnabled = _featureRegistry.IsFeatureEnabledAsync(_featureNumber).GetAwaiter().GetResult();
        
        if (isEnabled)
        {
            _logger.LogDebug("Feature #{FeatureNumber} is enabled (cached), using new implementation for {ServiceType}", 
                _featureNumber, typeof(T).Name);
            return _newImplementationFactory(_serviceProvider);
        }
        else
        {
            _logger.LogDebug("Feature #{FeatureNumber} is disabled (cached), using old implementation for {ServiceType}", 
                _featureNumber, typeof(T).Name);
            return _oldImplementationFactory(_serviceProvider);
        }
    }
}



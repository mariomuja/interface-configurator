using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InterfaceConfigurator.Main.Core.Factories;

/// <summary>
/// Extension methods for registering feature factories in dependency injection
/// </summary>
public static class FeatureFactoryExtensions
{
    /// <summary>
    /// Registers a feature factory for a service interface
    /// </summary>
    /// <typeparam name="TInterface">The service interface</typeparam>
    /// <typeparam name="TOld">The old/legacy implementation</typeparam>
    /// <typeparam name="TNew">The new implementation (used when feature is enabled)</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="featureNumber">The feature number that controls which implementation to use</param>
    public static void AddFeatureFactory<TInterface, TOld, TNew>(
        this IServiceCollection services,
        int featureNumber)
        where TInterface : class
        where TOld : class, TInterface
        where TNew : class, TInterface
    {
        // Register the old and new implementations
        services.AddScoped<TOld>();
        services.AddScoped<TNew>();

        // Register the factory
        services.AddScoped<IFeatureFactory<TInterface>>(sp =>
        {
            var featureRegistry = sp.GetRequiredService<IFeatureRegistry>();
            var logger = sp.GetRequiredService<ILogger<FeatureFactory<TInterface>>>();
            
            return new FeatureFactory<TInterface>(
                featureRegistry,
                sp,
                featureNumber,
                oldImplementationFactory: sp => sp.GetRequiredService<TOld>(),
                newImplementationFactory: sp => sp.GetRequiredService<TNew>(),
                logger);
        });

        // Register the service to use the factory
        services.AddScoped<TInterface>(sp =>
        {
            var factory = sp.GetRequiredService<IFeatureFactory<TInterface>>();
            return factory.Create(); // Use synchronous version for DI
        });
    }
}


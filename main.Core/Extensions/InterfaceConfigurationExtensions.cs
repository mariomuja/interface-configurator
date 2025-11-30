using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Core.Extensions;

public static class InterfaceConfigurationExtensions
{
    public static SourceAdapterInstance GetPrimarySource(this InterfaceConfiguration config)
    {
        if (config.Sources.Count == 0)
            throw new InvalidOperationException($"Interface '{config.InterfaceName}' does not define a source adapter.");

        return config.Sources.Values.First();
    }

    public static DestinationAdapterInstance GetPrimaryDestination(this InterfaceConfiguration config)
    {
        if (config.Destinations.Count == 0)
            throw new InvalidOperationException($"Interface '{config.InterfaceName}' does not define a destination adapter.");

        return config.Destinations.Values.First();
    }

    public static SourceAdapterInstance GetSourceInstance(this InterfaceConfiguration config, string? instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return GetPrimarySource(config);
        }

        if (config.Sources.TryGetValue(instanceName, out var instance))
        {
            return instance;
        }

        throw new InvalidOperationException($"Source adapter '{instanceName}' was not found for interface '{config.InterfaceName}'.");
    }

    public static DestinationAdapterInstance GetDestinationInstance(this InterfaceConfiguration config, string? instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return GetPrimaryDestination(config);
        }

        if (config.Destinations.TryGetValue(instanceName, out var instance))
        {
            return instance;
        }

        throw new InvalidOperationException($"Destination adapter '{instanceName}' was not found for interface '{config.InterfaceName}'.");
    }
}





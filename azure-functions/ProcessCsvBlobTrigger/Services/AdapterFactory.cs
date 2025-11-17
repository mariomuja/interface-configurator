using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Adapters;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Data;

namespace ProcessCsvBlobTrigger.Services;

/// <summary>
/// Factory for creating adapter instances based on interface configuration
/// </summary>
public class AdapterFactory : IAdapterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdapterFactory>? _logger;

    public AdapterFactory(IServiceProvider serviceProvider, ILogger<AdapterFactory>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
    }

    public async Task<IAdapter> CreateSourceAdapterAsync(InterfaceConfiguration config, CancellationToken cancellationToken = default)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var adapterName = config.SourceAdapterName;
        var configJson = config.SourceConfiguration;

        try
        {
            var configDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson) 
                ?? new Dictionary<string, JsonElement>();

            return adapterName.ToUpperInvariant() switch
            {
                "CSV" => CreateCsvAdapter(config.InterfaceName, configDict),
                "SQLSERVER" => CreateSqlServerAdapter(config.InterfaceName, configDict),
                _ => throw new NotSupportedException($"Source adapter '{adapterName}' is not supported")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating source adapter '{AdapterName}' for interface '{InterfaceName}'", 
                adapterName, config.InterfaceName);
            throw;
        }
    }

    public async Task<IAdapter> CreateDestinationAdapterAsync(InterfaceConfiguration config, CancellationToken cancellationToken = default)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var adapterName = config.DestinationAdapterName;
        var configJson = config.DestinationConfiguration;

        try
        {
            var configDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson) 
                ?? new Dictionary<string, JsonElement>();

            return adapterName.ToUpperInvariant() switch
            {
                "CSV" => CreateCsvAdapter(config.InterfaceName, configDict),
                "SQLSERVER" => CreateSqlServerAdapter(config.InterfaceName, configDict),
                _ => throw new NotSupportedException($"Destination adapter '{adapterName}' is not supported")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating destination adapter '{AdapterName}' for interface '{InterfaceName}'", 
                adapterName, config.InterfaceName);
            throw;
        }
    }

    private CsvAdapter CreateCsvAdapter(string interfaceName, Dictionary<string, JsonElement> config)
    {
        var csvProcessingService = _serviceProvider.GetRequiredService<ICsvProcessingService>();
        var adapterConfig = _serviceProvider.GetRequiredService<IAdapterConfigurationService>();
        var blobServiceClient = _serviceProvider.GetRequiredService<Azure.Storage.Blobs.BlobServiceClient>();
        var messageBoxService = _serviceProvider.GetService<IMessageBoxService>();
        var subscriptionService = _serviceProvider.GetService<IMessageSubscriptionService>();
        var logger = _serviceProvider.GetService<ILogger<CsvAdapter>>();

        return new CsvAdapter(
            csvProcessingService,
            adapterConfig,
            blobServiceClient,
            messageBoxService,
            subscriptionService,
            interfaceName,
            logger);
    }

    private SqlServerAdapter CreateSqlServerAdapter(string interfaceName, Dictionary<string, JsonElement> config)
    {
        var context = _serviceProvider.GetService<ApplicationDbContext>();
        if (context == null)
            throw new InvalidOperationException("ApplicationDbContext is required for SqlServerAdapter");

        var dynamicTableService = _serviceProvider.GetRequiredService<IDynamicTableService>();
        var dataService = _serviceProvider.GetRequiredService<IDataService>();
        var messageBoxService = _serviceProvider.GetService<IMessageBoxService>();
        var subscriptionService = _serviceProvider.GetService<IMessageSubscriptionService>();
        var logger = _serviceProvider.GetService<ILogger<SqlServerAdapter>>();

        return new SqlServerAdapter(
            context,
            dynamicTableService,
            dataService,
            messageBoxService,
            subscriptionService,
            interfaceName,
            logger);
    }
}


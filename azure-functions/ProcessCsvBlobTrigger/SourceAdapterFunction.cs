using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using System.Text.Json;

namespace ProcessCsvBlobTrigger;

/// <summary>
/// Timer-triggered function that processes enabled Source adapters
/// Each Source adapter reads from its source and writes to MessageBox
/// </summary>
public class SourceAdapterFunction
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IAdapterFactory _adapterFactory;
    private readonly ILogger<SourceAdapterFunction> _logger;

    public SourceAdapterFunction(
        IInterfaceConfigurationService configService,
        IAdapterFactory adapterFactory,
        ILogger<SourceAdapterFunction> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("SourceAdapterFunction")]
    public async Task Run(
        [TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo, // Run every minute
        FunctionContext context)
    {
        _logger.LogInformation("SourceAdapterFunction triggered at: {Time}", DateTime.UtcNow);

        try
        {
            // Get all enabled interface configurations
            var configurations = await _configService.GetEnabledSourceConfigurationsAsync(context.CancellationToken);

            if (!configurations.Any())
            {
                _logger.LogInformation("No enabled source configurations found. Skipping processing.");
                return;
            }

            _logger.LogInformation("Processing {Count} enabled source configurations", configurations.Count);

            // Process each enabled source configuration
            foreach (var config in configurations)
            {
                try
                {
                    await ProcessSourceConfigurationAsync(config, context.CancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing source configuration '{InterfaceName}': {ErrorMessage}", 
                        config.InterfaceName, ex.Message);
                    // Continue with other configurations
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SourceAdapterFunction: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    private async Task ProcessSourceConfigurationAsync(InterfaceConfiguration config, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing source configuration: Interface={InterfaceName}, Adapter={AdapterName}", 
            config.InterfaceName, config.SourceAdapterName);

        try
        {
            // Parse source configuration
            var sourceConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config.SourceConfiguration) 
                ?? new Dictionary<string, JsonElement>();

            if (!sourceConfig.TryGetValue("source", out var sourceElement))
            {
                _logger.LogWarning("Source configuration missing 'source' property for interface '{InterfaceName}'", 
                    config.InterfaceName);
                return;
            }

            var source = sourceElement.GetString();
            if (string.IsNullOrWhiteSpace(source))
            {
                _logger.LogWarning("Source is empty for interface '{InterfaceName}'", config.InterfaceName);
                return;
            }

            // Create source adapter
            var adapter = await _adapterFactory.CreateSourceAdapterAsync(config, cancellationToken);

            // Read from source (adapter will automatically debatch and write to MessageBox)
            var (headers, records) = await adapter.ReadAsync(source, cancellationToken);

            _logger.LogInformation(
                "Successfully processed source configuration '{InterfaceName}': {RecordCount} records read and written to MessageBox",
                config.InterfaceName, records.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing source configuration '{InterfaceName}': {ErrorMessage}", 
                config.InterfaceName, ex.Message);
            throw;
        }
    }
}


using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Adapters;
using System.Text.Json;

namespace InterfaceConfigurator.Main;

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
        // Check if this adapter instance is enabled
        // This check is redundant since GetEnabledSourceConfigurationsAsync already filters, but provides extra safety
        if (!config.SourceIsEnabled)
        {
            _logger.LogDebug("Source adapter for interface '{InterfaceName}' is disabled. Skipping processing.", config.InterfaceName);
            return;
        }

        _logger.LogInformation("Processing source configuration: Interface={InterfaceName}, Adapter={AdapterName}", 
            config.InterfaceName, config.SourceAdapterName);

        try
        {
            // Parse source configuration
            var sourceConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config.SourceConfiguration) 
                ?? new Dictionary<string, JsonElement>();

            // Check adapter type for CSV adapter
            string? adapterType = null;
            if (config.SourceAdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase))
            {
                adapterType = config.CsvAdapterType ?? "FILE";
                
                // For RAW adapter type, process CsvData directly
                if (adapterType.Equals("RAW", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(config.CsvData))
                    {
                        _logger.LogWarning("CsvData is empty for RAW adapter type in interface '{InterfaceName}'", config.InterfaceName);
                        return;
                    }

                    _logger.LogInformation("Processing RAW adapter type: Interface={InterfaceName}, CsvDataLength={DataLength}", 
                        config.InterfaceName, config.CsvData.Length);

                    // Create adapter and process CsvData (will upload to csv-incoming)
                    var csvSourceAdapter = await _adapterFactory.CreateSourceAdapterAsync(config, cancellationToken);
                    if (csvSourceAdapter is CsvAdapter csvAdapter)
                    {
                        csvAdapter.CsvData = config.CsvData; // This will trigger upload to csv-incoming
                        _logger.LogInformation("CsvData set on adapter, file will be uploaded to csv-incoming and processed via blob trigger");
                    }
                    return; // File processing happens via blob trigger
                }
            }

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

            // If ReceiveFolder is configured, check for new files in that folder
            // For CSV adapter, ReceiveFolder is a blob storage path (e.g., "csv-files/csv-incoming")
            if (!string.IsNullOrWhiteSpace(config.SourceReceiveFolder) && config.SourceAdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Checking ReceiveFolder '{ReceiveFolder}' for new files: Interface={InterfaceName}, AdapterType={AdapterType}", 
                    config.SourceReceiveFolder, config.InterfaceName, adapterType);
                
                // For FILE adapter type: Poll folder and copy files to csv-incoming
                // For SFTP adapter type: Read from SFTP and upload files to csv-incoming
                // Note: In Azure Blob Storage, new files in csv-incoming trigger BlobTrigger automatically
                // This timer function will process files from other folders and copy them to csv-incoming
                source = config.SourceReceiveFolder;
                _logger.LogInformation("Using ReceiveFolder as source: {Source}", source);
            }

            // For SQL Server adapter with polling statement, use empty source
            // The adapter will execute the polling statement instead of reading from a table
            if (config.SourceAdapterName.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) && 
                !string.IsNullOrWhiteSpace(config.SqlPollingStatement))
            {
                _logger.LogInformation("SQL Server adapter with polling statement configured: Interface={InterfaceName}, PollingInterval={PollingInterval}s", 
                    config.InterfaceName, config.SqlPollingInterval);
                
                // Pass empty string - adapter will use polling statement internally
                source = string.Empty;
                _logger.LogInformation("Using polling statement for SQL Server adapter: {PollingStatement}", config.SqlPollingStatement);
            }

            // Create source adapter
            var adapter = await _adapterFactory.CreateSourceAdapterAsync(config, cancellationToken);

            // Check if adapter supports reading
            if (!adapter.SupportsRead)
            {
                _logger.LogError(
                    "Adapter '{AdapterName}' (Alias: '{AdapterAlias}') does not support reading. " +
                    "It cannot be used as a source adapter. Interface: '{InterfaceName}'",
                    adapter.AdapterName, adapter.AdapterAlias, config.InterfaceName);
                throw new NotSupportedException(
                    $"Adapter '{adapter.AdapterAlias}' does not support reading (cannot be used as source). " +
                    $"The ReadAsync functionality has not been implemented for this adapter.");
            }

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


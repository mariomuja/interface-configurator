using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using System.Text.Json;

namespace ProcessCsvBlobTrigger;

/// <summary>
/// Timer-triggered function that processes enabled Destination adapters
/// Each Destination adapter reads from MessageBox and writes to its destination
/// </summary>
public class DestinationAdapterFunction
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IAdapterFactory _adapterFactory;
    private readonly IMessageBoxService _messageBoxService;
    private readonly ILogger<DestinationAdapterFunction> _logger;

    public DestinationAdapterFunction(
        IInterfaceConfigurationService configService,
        IAdapterFactory adapterFactory,
        IMessageBoxService messageBoxService,
        ILogger<DestinationAdapterFunction> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("DestinationAdapterFunction")]
    public async Task Run(
        [TimerTrigger("0 */1 * * * *")] TimerInfo timerInfo, // Run every minute
        FunctionContext context)
    {
        _logger.LogInformation("DestinationAdapterFunction triggered at: {Time}", DateTime.UtcNow);

        try
        {
            // Get all enabled interface configurations
            var configurations = await _configService.GetEnabledDestinationConfigurationsAsync(context.CancellationToken);

            if (!configurations.Any())
            {
                _logger.LogInformation("No enabled destination configurations found. Skipping processing.");
                return;
            }

            _logger.LogInformation("Processing {Count} enabled destination configurations", configurations.Count);

            // Process each enabled destination configuration
            foreach (var config in configurations)
            {
                try
                {
                    await ProcessDestinationConfigurationAsync(config, context.CancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing destination configuration '{InterfaceName}': {ErrorMessage}", 
                        config.InterfaceName, ex.Message);
                    // Continue with other configurations
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DestinationAdapterFunction: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    private async Task ProcessDestinationConfigurationAsync(InterfaceConfiguration config, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing destination configuration: Interface={InterfaceName}, Adapter={AdapterName}", 
            config.InterfaceName, config.DestinationAdapterName);

        try
        {
            // Parse destination configuration
            var destConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config.DestinationConfiguration) 
                ?? new Dictionary<string, JsonElement>();

            if (!destConfig.TryGetValue("destination", out var destElement))
            {
                _logger.LogWarning("Destination configuration missing 'destination' property for interface '{InterfaceName}'", 
                    config.InterfaceName);
                return;
            }

            var destination = destElement.GetString();
            if (string.IsNullOrWhiteSpace(destination))
            {
                _logger.LogWarning("Destination is empty for interface '{InterfaceName}'", config.InterfaceName);
                return;
            }

            // Read pending messages from MessageBox for this interface
            var messages = await _messageBoxService.ReadMessagesAsync(config.InterfaceName, "Pending", cancellationToken);

            if (!messages.Any())
            {
                _logger.LogDebug("No pending messages found for interface '{InterfaceName}'", config.InterfaceName);
                return;
            }

            _logger.LogInformation("Found {MessageCount} pending messages for interface '{InterfaceName}'", 
                messages.Count, config.InterfaceName);

            // Create destination adapter
            var adapter = await _adapterFactory.CreateDestinationAdapterAsync(config, cancellationToken);

            // Write to destination (adapter will read from MessageBox, write to destination, and mark subscription as processed)
            // The adapter's WriteAsync method handles reading from MessageBox internally
            await adapter.WriteAsync(destination, new List<string>(), new List<Dictionary<string, string>>(), cancellationToken);

            _logger.LogInformation(
                "Successfully processed destination configuration '{InterfaceName}'",
                config.InterfaceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing destination configuration '{InterfaceName}': {ErrorMessage}", 
                config.InterfaceName, ex.Message);
            throw;
        }
    }
}


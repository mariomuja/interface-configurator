using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using System.Text.Json;

namespace InterfaceConfigurator.Main;

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
            // Release stale locks first (messages locked longer than timeout)
            var staleLocksReleased = await _messageBoxService.ReleaseStaleLocksAsync(lockTimeoutMinutes: 5, context.CancellationToken);
            if (staleLocksReleased > 0)
            {
                _logger.LogInformation("Released {Count} stale message locks", staleLocksReleased);
            }

            // Get all enabled interface configurations
            var configurations = await _configService.GetEnabledDestinationConfigurationsAsync(context.CancellationToken);

            if (!configurations.Any())
            {
                _logger.LogInformation("No enabled destination configurations found. Skipping processing.");
                return;
            }

            _logger.LogInformation("Processing {Count} enabled destination configurations", configurations.Count);

            // Process each enabled destination configuration
            // For each interface, process all its destination adapter instances
            foreach (var config in configurations)
            {
                try
                {
                    // Get all destination adapter instances for this interface
                    var destinationInstances = await _configService.GetDestinationAdapterInstancesAsync(config.InterfaceName, context.CancellationToken);
                    
                    // Filter to only enabled instances
                    var enabledInstances = destinationInstances.Where(i => i.IsEnabled).ToList();
                    
                    if (!enabledInstances.Any())
                    {
                        _logger.LogDebug("No enabled destination adapter instances found for interface '{InterfaceName}'", config.InterfaceName);
                        continue;
                    }
                    
                    _logger.LogInformation("Processing {InstanceCount} enabled destination adapter instances for interface '{InterfaceName}'", 
                        enabledInstances.Count, config.InterfaceName);
                    
                    // Process each destination adapter instance in parallel with optimized concurrency
                    // Limit concurrent processing to avoid overwhelming the database
                    const int maxConcurrency = 5; // Process up to 5 destination adapters concurrently
                    var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
                    
                    var tasks = enabledInstances.Select(async instance =>
                    {
                        await semaphore.WaitAsync(context.CancellationToken);
                        try
                        {
                            await ProcessDestinationAdapterInstanceAsync(config, instance, context.CancellationToken);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    await Task.WhenAll(tasks);
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

    private async Task ProcessDestinationAdapterInstanceAsync(
        InterfaceConfiguration interfaceConfig, 
        DestinationAdapterInstance instance, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing destination adapter instance: Interface={InterfaceName}, Instance={InstanceName}, Adapter={AdapterName}, Guid={AdapterInstanceGuid}", 
            interfaceConfig.InterfaceName, instance.InstanceName, instance.AdapterName, instance.AdapterInstanceGuid);

        try
        {
            // Parse destination configuration
            var destConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(instance.Configuration ?? "{}") 
                ?? new Dictionary<string, JsonElement>();

            // Get destination table name - prefer TableName from configuration, then destination property, then interface config
            string? destination = null;
            if (destConfig.TryGetValue("tableName", out var tableNameElement))
            {
                destination = tableNameElement.GetString();
            }
            else if (destConfig.TryGetValue("destination", out var destElement))
            {
                destination = destElement.GetString();
            }
            else if (!string.IsNullOrWhiteSpace(interfaceConfig.SqlTableName))
            {
                destination = interfaceConfig.SqlTableName;
            }

            if (string.IsNullOrWhiteSpace(destination))
            {
                _logger.LogWarning("Destination table name is empty for instance '{InstanceName}' in interface '{InterfaceName}'. Please configure TableName.", 
                    instance.InstanceName, interfaceConfig.InterfaceName);
                return;
            }

            // Read pending messages from MessageBox for this interface
            // All destination instances subscribe to the same MessageBox data
            var messages = await _messageBoxService.ReadMessagesAsync(interfaceConfig.InterfaceName, "Pending", cancellationToken);

            if (!messages.Any())
            {
                _logger.LogDebug("No pending messages found for interface '{InterfaceName}' (instance '{InstanceName}')", 
                    interfaceConfig.InterfaceName, instance.InstanceName);
                return;
            }

            _logger.LogInformation("Found {MessageCount} pending messages for interface '{InterfaceName}' (instance '{InstanceName}')", 
                messages.Count, interfaceConfig.InterfaceName, instance.InstanceName);

            // Create a temporary InterfaceConfiguration for this instance to pass to adapter factory
            var instanceConfig = new InterfaceConfiguration
            {
                InterfaceName = interfaceConfig.InterfaceName,
                DestinationAdapterName = instance.AdapterName,
                DestinationConfiguration = instance.Configuration ?? "{}",
                DestinationIsEnabled = instance.IsEnabled,
                DestinationInstanceName = instance.InstanceName,
                DestinationAdapterInstanceGuid = instance.AdapterInstanceGuid,
                // Copy source properties (for shared properties like SQL Server connection)
                SourceFieldSeparator = interfaceConfig.SourceFieldSeparator,
                SqlServerName = interfaceConfig.SqlServerName,
                SqlDatabaseName = interfaceConfig.SqlDatabaseName,
                SqlUserName = interfaceConfig.SqlUserName,
                SqlPassword = interfaceConfig.SqlPassword,
                SqlIntegratedSecurity = interfaceConfig.SqlIntegratedSecurity,
                SqlResourceGroup = interfaceConfig.SqlResourceGroup,
                SqlTableName = interfaceConfig.SqlTableName ?? destination, // Use interface config or instance destination
                DestinationReceiveFolder = interfaceConfig.DestinationReceiveFolder,
                DestinationFileMask = interfaceConfig.DestinationFileMask,
                SqlUseTransaction = interfaceConfig.SqlUseTransaction,
                SqlBatchSize = interfaceConfig.SqlBatchSize
            };

            // Create destination adapter for this instance
            var adapter = await _adapterFactory.CreateDestinationAdapterAsync(instanceConfig, cancellationToken);

            // Check if adapter supports writing
            if (!adapter.SupportsWrite)
            {
                _logger.LogError(
                    "Adapter '{AdapterName}' (Alias: '{AdapterAlias}') does not support writing. " +
                    "It cannot be used as a destination adapter. Interface: '{InterfaceName}', Instance: '{InstanceName}'",
                    adapter.AdapterName, adapter.AdapterAlias, interfaceConfig.InterfaceName, instance.InstanceName);
                throw new NotSupportedException(
                    $"Adapter '{adapter.AdapterAlias}' does not support writing (cannot be used as destination). " +
                    $"The WriteAsync functionality has not been implemented for this adapter.");
            }

            // Write to destination (adapter will read from MessageBox, write to destination, and mark subscription as processed)
            // The adapter's WriteAsync method handles reading from MessageBox internally
            await adapter.WriteAsync(destination, new List<string>(), new List<Dictionary<string, string>>(), cancellationToken);

            _logger.LogInformation(
                "Successfully processed destination adapter instance '{InstanceName}' ({AdapterName}) for interface '{InterfaceName}'",
                instance.InstanceName, instance.AdapterName, interfaceConfig.InterfaceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing destination adapter instance '{InstanceName}' for interface '{InterfaceName}': {ErrorMessage}", 
                instance.InstanceName, interfaceConfig.InterfaceName, ex.Message);
            throw;
        }
    }
}


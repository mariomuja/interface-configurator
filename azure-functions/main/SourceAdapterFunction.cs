using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
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
    private static readonly ConcurrentDictionary<string, DateTime> _lastCsvPollTimes = new();
    private static readonly ConcurrentDictionary<string, string> _lastCsvDataHashes = new();
    private static readonly ConcurrentDictionary<string, DateTime> _lastSqlServerPollTimes = new();

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
            // Get all enabled interface configurations (for background processing)
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
        var enabledSources = config.Sources.Values.Where(s => s.IsEnabled).ToList();
        if (!enabledSources.Any())
        {
            _logger.LogDebug("Source adapter for interface '{InterfaceName}' is disabled. Skipping processing.", config.InterfaceName);
            return;
        }

        // Process each enabled source instance
        foreach (var sourceInstance in enabledSources)
        {
            try
            {
                await ProcessSourceInstanceAsync(config, sourceInstance, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing source instance '{InstanceName}' for interface '{InterfaceName}': {ErrorMessage}", 
                    sourceInstance.InstanceName, config.InterfaceName, ex.Message);
                // Continue with other source instances
            }
        }
    }

    private async Task ProcessSourceInstanceAsync(InterfaceConfiguration config, SourceAdapterInstance sourceInstance, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing source instance: Interface={InterfaceName}, Instance={InstanceName}, Adapter={AdapterName}", 
            config.InterfaceName, sourceInstance.InstanceName, sourceInstance.AdapterName);

        try
        {
            var isCsvSource = sourceInstance.AdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase);
            var isSqlServerSource = sourceInstance.AdapterName.Equals("SqlServer", StringComparison.OrdinalIgnoreCase);
            var csvPollingInterval = sourceInstance.CsvPollingInterval > 0 ? sourceInstance.CsvPollingInterval : 10;
            var sqlPollingInterval = sourceInstance.SqlPollingInterval > 0 ? sourceInstance.SqlPollingInterval : 60;

            // Create a unique key for polling times (interface + instance)
            var pollKey = $"{config.InterfaceName}|{sourceInstance.InstanceName}";

            void UpdatePollTime()
            {
                if (isCsvSource)
                {
                    _lastCsvPollTimes[pollKey] = DateTime.UtcNow;
                }
                else if (isSqlServerSource)
                {
                    _lastSqlServerPollTimes[pollKey] = DateTime.UtcNow;
                }
            }

            // Check CSV polling interval
            if (isCsvSource)
            {
                var lastPoll = _lastCsvPollTimes.GetOrAdd(pollKey, DateTime.MinValue);
                var elapsedSeconds = (DateTime.UtcNow - lastPoll).TotalSeconds;
                if (elapsedSeconds < csvPollingInterval)
                {
                    _logger.LogDebug("Skipping CSV source '{InterfaceName}' instance '{InstanceName}' - waiting {RemainingSeconds:F1}s before next poll (interval {IntervalSeconds}s).",
                        config.InterfaceName, sourceInstance.InstanceName,
                        csvPollingInterval - elapsedSeconds,
                        csvPollingInterval);
                    return;
                }
            }

            // Check SqlServer polling interval
            if (isSqlServerSource && !string.IsNullOrWhiteSpace(sourceInstance.SqlPollingStatement))
            {
                var lastPoll = _lastSqlServerPollTimes.GetOrAdd(pollKey, DateTime.MinValue);
                var elapsedSeconds = (DateTime.UtcNow - lastPoll).TotalSeconds;
                if (elapsedSeconds < sqlPollingInterval)
                {
                    _logger.LogDebug("Skipping SqlServer source '{InterfaceName}' instance '{InstanceName}' - waiting {RemainingSeconds:F1}s before next poll (interval {IntervalSeconds}s).",
                        config.InterfaceName, sourceInstance.InstanceName,
                        sqlPollingInterval - elapsedSeconds,
                        sqlPollingInterval);
                    return;
                }
            }

            // Parse source configuration
            var sourceConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sourceInstance.Configuration) 
                ?? new Dictionary<string, JsonElement>();

            // Check adapter type for CSV adapter
            string? adapterType = null;
            if (isCsvSource)
            {
                adapterType = sourceInstance.CsvAdapterType ?? "FILE";
                
                // For RAW adapter type, process CsvData directly
                if (adapterType.Equals("RAW", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(sourceInstance.CsvData))
                    {
                        _logger.LogWarning("CsvData is empty for RAW adapter type in interface '{InterfaceName}', instance '{InstanceName}'", 
                            config.InterfaceName, sourceInstance.InstanceName);
                        return;
                    }

                    _logger.LogInformation("Processing RAW adapter type: Interface={InterfaceName}, Instance={InstanceName}, CsvDataLength={DataLength}", 
                        config.InterfaceName, sourceInstance.InstanceName, sourceInstance.CsvData.Length);

                    var currentHash = ComputeCsvHash(sourceInstance.CsvData);
                    var hashKey = $"{config.InterfaceName}|{sourceInstance.InstanceName}";
                    var lastHash = _lastCsvDataHashes.GetOrAdd(hashKey, string.Empty);
                    if (string.Equals(currentHash, lastHash, StringComparison.Ordinal))
                    {
                        _logger.LogInformation("CsvData has not changed since last processing for interface '{InterfaceName}', instance '{InstanceName}'. Skipping.", 
                            config.InterfaceName, sourceInstance.InstanceName);
                        UpdatePollTime();
                        return;
                    }

                    // Create temporary config for adapter factory
                    var tempConfig = CreateTempConfigForSource(config, sourceInstance);
                    
                    // Create adapter and process CsvData (will upload to csv-incoming)
                    var csvSourceAdapter = await _adapterFactory.CreateSourceAdapterAsync(tempConfig, cancellationToken);
                    if (csvSourceAdapter is CsvAdapter csvAdapter)
                    {
                        csvAdapter.CsvData = sourceInstance.CsvData; // This will trigger upload to csv-incoming
                        _logger.LogInformation("CsvData set on adapter, file will be uploaded to csv-incoming and processed via blob trigger");
                        if (!string.IsNullOrEmpty(currentHash))
                        {
                            _lastCsvDataHashes[hashKey] = currentHash;
                        }
                    }
                    UpdatePollTime();
                    return; // File processing happens via blob trigger
                }
            }

            if (!sourceConfig.TryGetValue("source", out var sourceElement))
            {
                _logger.LogWarning("Source configuration missing 'source' property for interface '{InterfaceName}', instance '{InstanceName}'", 
                    config.InterfaceName, sourceInstance.InstanceName);
                return;
            }

            var source = sourceElement.GetString();
            if (string.IsNullOrWhiteSpace(source))
            {
                _logger.LogWarning("Source is empty for interface '{InterfaceName}', instance '{InstanceName}'", 
                    config.InterfaceName, sourceInstance.InstanceName);
                return;
            }

            // If ReceiveFolder is configured, check for new files in that folder
            // For CSV adapter, ReceiveFolder is a blob storage path (e.g., "csv-files/csv-incoming")
            if (!string.IsNullOrWhiteSpace(sourceInstance.SourceReceiveFolder) && isCsvSource)
            {
                _logger.LogInformation("Checking ReceiveFolder '{ReceiveFolder}' for new files: Interface={InterfaceName}, Instance={InstanceName}, AdapterType={AdapterType}", 
                    sourceInstance.SourceReceiveFolder, config.InterfaceName, sourceInstance.InstanceName, adapterType);
                
                // For FILE adapter type: Poll folder and copy files to csv-incoming
                // For SFTP adapter type: Read from SFTP and upload files to csv-incoming
                // Note: In Azure Blob Storage, new files in csv-incoming trigger BlobTrigger automatically
                // This timer function will process files from other folders and copy them to csv-incoming
                source = sourceInstance.SourceReceiveFolder;
                _logger.LogInformation("Using ReceiveFolder as source: {Source}", source);
            }

            // For SQL Server adapter with polling statement, use empty source
            // The adapter will execute the polling statement instead of reading from a table
            if (isSqlServerSource && !string.IsNullOrWhiteSpace(sourceInstance.SqlPollingStatement))
            {
                _logger.LogInformation("SQL Server adapter with polling statement configured: Interface={InterfaceName}, Instance={InstanceName}, PollingInterval={PollingInterval}s", 
                    config.InterfaceName, sourceInstance.InstanceName, sourceInstance.SqlPollingInterval);
                
                // Pass empty string - adapter will use polling statement internally
                source = string.Empty;
                _logger.LogInformation("Using polling statement for SQL Server adapter: {PollingStatement}", sourceInstance.SqlPollingStatement);
            }

            // Create temporary config for adapter factory
            var tempConfigForAdapter = CreateTempConfigForSource(config, sourceInstance);

            // Create source adapter
            var adapter = await _adapterFactory.CreateSourceAdapterAsync(tempConfigForAdapter, cancellationToken);

            // Check if adapter supports reading
            if (!adapter.SupportsRead)
            {
                _logger.LogError(
                    "Adapter '{AdapterName}' (Alias: '{AdapterAlias}') does not support reading. " +
                    "It cannot be used as a source adapter. Interface: '{InterfaceName}', Instance: '{InstanceName}'",
                    adapter.AdapterName, adapter.AdapterAlias, config.InterfaceName, sourceInstance.InstanceName);
                throw new NotSupportedException(
                    $"Adapter '{adapter.AdapterAlias}' does not support reading (cannot be used as source). " +
                    $"The ReadAsync functionality has not been implemented for this adapter.");
            }

            // Read from source (adapter will automatically debatch and write to MessageBox)
            var (headers, records) = await adapter.ReadAsync(source, cancellationToken);

            _logger.LogInformation(
                "Successfully processed source instance '{InstanceName}' for interface '{InterfaceName}': {RecordCount} records read and written to MessageBox",
                sourceInstance.InstanceName, config.InterfaceName, records.Count);

            UpdatePollTime();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing source instance '{InstanceName}' for interface '{InterfaceName}': {ErrorMessage}", 
                sourceInstance.InstanceName, config.InterfaceName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Creates a temporary InterfaceConfiguration from a SourceAdapterInstance for use with AdapterFactory
    /// This is a compatibility layer until AdapterFactory is updated to work directly with SourceAdapterInstance
    /// </summary>
    private InterfaceConfiguration CreateTempConfigForSource(InterfaceConfiguration originalConfig, SourceAdapterInstance sourceInstance)
    {
        // Create a temporary config with properties from the source instance
        // This allows the AdapterFactory to work with the existing interface
        var tempConfig = new InterfaceConfiguration
        {
            InterfaceName = originalConfig.InterfaceName,
            Description = originalConfig.Description,
            CreatedAt = originalConfig.CreatedAt,
            UpdatedAt = originalConfig.UpdatedAt,
            // Set deprecated properties from source instance for backward compatibility
            SourceAdapterName = sourceInstance.AdapterName,
            SourceConfiguration = sourceInstance.Configuration,
            SourceIsEnabled = sourceInstance.IsEnabled,
            SourceInstanceName = sourceInstance.InstanceName,
            SourceAdapterInstanceGuid = sourceInstance.AdapterInstanceGuid,
            // Copy all source properties
            SourceReceiveFolder = sourceInstance.SourceReceiveFolder,
            SourceFileMask = sourceInstance.SourceFileMask,
            SourceBatchSize = sourceInstance.SourceBatchSize,
            SourceFieldSeparator = sourceInstance.SourceFieldSeparator,
            CsvData = sourceInstance.CsvData,
            CsvAdapterType = sourceInstance.CsvAdapterType,
            CsvPollingInterval = sourceInstance.CsvPollingInterval,
            SftpHost = sourceInstance.SftpHost,
            SftpPort = sourceInstance.SftpPort,
            SftpUsername = sourceInstance.SftpUsername,
            SftpPassword = sourceInstance.SftpPassword,
            SftpSshKey = sourceInstance.SftpSshKey,
            SftpFolder = sourceInstance.SftpFolder,
            SftpFileMask = sourceInstance.SftpFileMask,
            SftpMaxConnectionPoolSize = sourceInstance.SftpMaxConnectionPoolSize,
            SftpFileBufferSize = sourceInstance.SftpFileBufferSize,
            SqlServerName = sourceInstance.SqlServerName,
            SqlDatabaseName = sourceInstance.SqlDatabaseName,
            SqlUserName = sourceInstance.SqlUserName,
            SqlPassword = sourceInstance.SqlPassword,
            SqlIntegratedSecurity = sourceInstance.SqlIntegratedSecurity,
            SqlResourceGroup = sourceInstance.SqlResourceGroup,
            SqlPollingStatement = sourceInstance.SqlPollingStatement,
            SqlPollingInterval = sourceInstance.SqlPollingInterval,
            SqlTableName = sourceInstance.SqlTableName,
            SqlUseTransaction = sourceInstance.SqlUseTransaction,
            SqlBatchSize = sourceInstance.SqlBatchSize,
            SqlCommandTimeout = sourceInstance.SqlCommandTimeout,
            SqlFailOnBadStatement = sourceInstance.SqlFailOnBadStatement
        };
        
        // Also copy Sources and Destinations dictionaries
        tempConfig.Sources = originalConfig.Sources;
        tempConfig.Destinations = originalConfig.Destinations;
        
        return tempConfig;
    }

    private static string ComputeCsvHash(string? data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return string.Empty;
        }

        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(data);
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }
}


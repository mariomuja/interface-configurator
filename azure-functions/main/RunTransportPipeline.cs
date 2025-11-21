using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class RunTransportPipeline
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IAdapterFactory _adapterFactory;
    private readonly ILogger<RunTransportPipeline> _logger;

    public RunTransportPipeline(
        IInterfaceConfigurationService configService,
        IAdapterFactory adapterFactory,
        ILogger<RunTransportPipeline> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("RunTransportPipeline")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "RunTransportPipeline")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("RunTransportPipeline triggered");

        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<RunTransportRequest>(requestBody) ?? new RunTransportRequest();

            var interfaceName = string.IsNullOrWhiteSpace(request.InterfaceName)
                ? "FromCsvToSqlServerExample"
                : request.InterfaceName!;

            var response = req.CreateResponse();
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            var config = await EnsureInterfaceConfigurationAsync(interfaceName, executionContext.CancellationToken);

            // Ensure adapters remain enabled
            if (!config.Sources.Values.Any(s => s.IsEnabled))
            {
                await _configService.SetSourceEnabledAsync(interfaceName, true, executionContext.CancellationToken);
                // Re-fetch config to get updated state
                config = await _configService.GetConfigurationAsync(interfaceName, executionContext.CancellationToken) 
                    ?? throw new InvalidOperationException($"Configuration '{interfaceName}' not found after enabling source");
            }
            if (!config.Destinations.Values.Any(d => d.IsEnabled))
            {
                await _configService.SetDestinationEnabledAsync(interfaceName, true, executionContext.CancellationToken);
                // Re-fetch config to get updated state
                config = await _configService.GetConfigurationAsync(interfaceName, executionContext.CancellationToken) 
                    ?? throw new InvalidOperationException($"Configuration '{interfaceName}' not found after enabling destination");
            }

            // Get source field separator from first source instance
            var firstSource = config.Sources.Values.FirstOrDefault();
            var fieldSeparator = firstSource?.SourceFieldSeparator ?? "║";
            
            var csvContent = string.IsNullOrWhiteSpace(request.CsvContent)
                ? SampleCsvGenerator.GenerateSampleCsv(fieldSeparator)
                : request.CsvContent!;

            var sourceSummary = await WriteCsvToMessageBoxAsync(config, csvContent, executionContext.CancellationToken);
            var destinationSummary = await ProcessDestinationAdaptersAsync(config, executionContext.CancellationToken);

            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                interfaceName,
                sourceSummary.ProcessedRecords,
                destinationSummary.ProcessedAdapters,
                destinationSummary.DestinationTables,
                message = "Transport pipeline executed successfully"
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing transport pipeline");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = "Transport pipeline failed",
                details = ex.Message
            }));
            return errorResponse;
        }
    }

    private async Task<InterfaceConfiguration> EnsureInterfaceConfigurationAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var config = await _configService.GetConfigurationAsync(interfaceName, cancellationToken);
        if (config != null)
        {
            // Ensure Sources dictionary has at least one entry
            if (config.Sources.Count == 0)
            {
                var newSourceInstance = new SourceAdapterInstance
                {
                    InstanceName = "Source",
                    AdapterName = "CSV",
                    IsEnabled = true,
                    AdapterInstanceGuid = Guid.NewGuid(),
                    Configuration = JsonSerializer.Serialize(new { source = "csv-files/csv-incoming" }),
                    SourceFieldSeparator = "║",
                    CsvPollingInterval = 10,
                    CreatedAt = DateTime.UtcNow
                };
                config.Sources[newSourceInstance.InstanceName] = newSourceInstance;
                await _configService.SaveConfigurationAsync(config, cancellationToken);
            }
            
            // Ensure Destinations dictionary has at least one entry
            if (config.Destinations.Count == 0)
            {
                var newDestInstance = new DestinationAdapterInstance
                {
                    InstanceName = "Destination",
                    AdapterName = "SqlServer",
                    IsEnabled = true,
                    AdapterInstanceGuid = Guid.NewGuid(),
                    Configuration = JsonSerializer.Serialize(new { destination = "TransportData" }),
                    SqlTableName = "TransportData",
                    CreatedAt = DateTime.UtcNow
                };
                config.Destinations[newDestInstance.InstanceName] = newDestInstance;
                await _configService.SaveConfigurationAsync(config, cancellationToken);
            }
            
            return config;
        }

        // Create new configuration with new structure
        var newConfig = new InterfaceConfiguration
        {
            InterfaceName = interfaceName,
            CreatedAt = DateTime.UtcNow
        };

        // Create source instance
        var defaultSourceInstance = new SourceAdapterInstance
        {
            InstanceName = "Source",
            AdapterName = "CSV",
            IsEnabled = true,
            AdapterInstanceGuid = Guid.NewGuid(),
            Configuration = JsonSerializer.Serialize(new { source = "csv-files/csv-incoming" }),
            SourceFieldSeparator = "║",
            CsvPollingInterval = 10,
            CreatedAt = DateTime.UtcNow
        };
        newConfig.Sources[defaultSourceInstance.InstanceName] = defaultSourceInstance;

        // Create destination instance
        var defaultDestInstance = new DestinationAdapterInstance
        {
            InstanceName = "Destination",
            AdapterName = "SqlServer",
            IsEnabled = true,
            AdapterInstanceGuid = Guid.NewGuid(),
            Configuration = JsonSerializer.Serialize(new { destination = "TransportData" }),
            SqlTableName = "TransportData",
            CreatedAt = DateTime.UtcNow
        };
        newConfig.Destinations[defaultDestInstance.InstanceName] = defaultDestInstance;

        await _configService.SaveConfigurationAsync(newConfig, cancellationToken);
        return newConfig;
    }

    private async Task<SourceSummary> WriteCsvToMessageBoxAsync(
        InterfaceConfiguration config,
        string csvContent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Writing CSV content to MessageBox for interface {InterfaceName}", config.InterfaceName);

        // Get first enabled source instance
        var sourceInstance = config.Sources.Values.FirstOrDefault(s => s.IsEnabled);
        if (sourceInstance == null)
        {
            _logger.LogWarning("No enabled source instance found for interface {InterfaceName}", config.InterfaceName);
            return new SourceSummary(0);
        }

        // Create temporary config for adapter factory
        var tempConfig = CreateTempConfigForSource(config, sourceInstance);

        var adapter = await _adapterFactory.CreateSourceAdapterAsync(tempConfig, cancellationToken);
        int processedRecords = 0;

        if (adapter is CsvAdapter csvAdapter)
        {
            csvAdapter.CsvData = csvContent;
            await csvAdapter.ProcessCsvDataAsync(cancellationToken);

            // Estimate processed records by counting lines minus header
            processedRecords = Math.Max(0, csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1);
        }
        else
        {
            var sourcePath = ExtractSourcePath(sourceInstance.Configuration);
            var (_, records) = await adapter.ReadAsync(sourcePath, cancellationToken);
            processedRecords = records.Count;
        }

        _logger.LogInformation("CSV data processed and written to MessageBox: {RecordCount} records", processedRecords);
        return new SourceSummary(processedRecords);
    }

    /// <summary>
    /// Creates a temporary InterfaceConfiguration from a SourceAdapterInstance for use with AdapterFactory
    /// </summary>
    private InterfaceConfiguration CreateTempConfigForSource(InterfaceConfiguration originalConfig, SourceAdapterInstance sourceInstance)
    {
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

    private async Task<DestinationSummary> ProcessDestinationAdaptersAsync(
        InterfaceConfiguration config,
        CancellationToken cancellationToken)
    {
        var destinationInstances = await _configService.GetDestinationAdapterInstancesAsync(config.InterfaceName, cancellationToken);

        if (destinationInstances.Count == 0)
        {
            _logger.LogInformation("No destination adapter instances found. Creating default SqlServer destination.");
            var defaultConfig = JsonSerializer.Serialize(new
            {
                destination = "TransportData",
                tableName = "TransportData"
            });

            var created = await _configService.AddDestinationAdapterInstanceAsync(
                config.InterfaceName,
                "SqlServer",
                "Destination 1",
                defaultConfig,
                cancellationToken);

            destinationInstances.Add(created);
        }

        var enabledInstances = destinationInstances.Where(i => i.IsEnabled).ToList();
        if (!enabledInstances.Any())
        {
            _logger.LogWarning("No enabled destination adapters found for interface {InterfaceName}", config.InterfaceName);
            return new DestinationSummary(0, new List<string>());
        }

        var processedTables = new List<string>();
        foreach (var instance in enabledInstances)
        {
            var destination = ResolveDestinationTable(instance, config);
            processedTables.Add(destination);

            var instanceConfig = BuildInstanceConfiguration(config, instance, destination);
            var adapter = await _adapterFactory.CreateDestinationAdapterAsync(instanceConfig, cancellationToken);
            await adapter.WriteAsync(destination, new List<string>(), new List<Dictionary<string, string>>(), cancellationToken);
        }

        _logger.LogInformation("Processed {Count} destination adapters", enabledInstances.Count);
        return new DestinationSummary(enabledInstances.Count, processedTables);
    }

    private static InterfaceConfiguration BuildInstanceConfiguration(
        InterfaceConfiguration baseConfig,
        DestinationAdapterInstance instance,
        string destinationTable)
    {
        // Get source field separator from first source instance
        var firstSource = baseConfig.Sources.Values.FirstOrDefault();
        var sourceFieldSeparator = firstSource?.SourceFieldSeparator ?? "║";
        
        var tempConfig = new InterfaceConfiguration
        {
            InterfaceName = baseConfig.InterfaceName,
            Description = baseConfig.Description,
            CreatedAt = baseConfig.CreatedAt,
            UpdatedAt = baseConfig.UpdatedAt,
            // Set deprecated properties from destination instance for backward compatibility
            DestinationAdapterName = instance.AdapterName,
            DestinationConfiguration = instance.Configuration ?? "{}",
            DestinationIsEnabled = instance.IsEnabled,
            DestinationInstanceName = instance.InstanceName,
            DestinationAdapterInstanceGuid = instance.AdapterInstanceGuid,
            // Copy destination properties
            DestinationReceiveFolder = instance.DestinationReceiveFolder,
            DestinationFileMask = instance.DestinationFileMask,
            SqlServerName = instance.SqlServerName ?? baseConfig.SqlServerName,
            SqlDatabaseName = instance.SqlDatabaseName ?? baseConfig.SqlDatabaseName,
            SqlUserName = instance.SqlUserName ?? baseConfig.SqlUserName,
            SqlPassword = instance.SqlPassword ?? baseConfig.SqlPassword,
            SqlIntegratedSecurity = instance.SqlIntegratedSecurity,
            SqlResourceGroup = instance.SqlResourceGroup ?? baseConfig.SqlResourceGroup,
            SqlTableName = destinationTable,
            SqlUseTransaction = instance.SqlUseTransaction,
            SqlBatchSize = instance.SqlBatchSize,
            SqlCommandTimeout = instance.SqlCommandTimeout,
            SqlFailOnBadStatement = instance.SqlFailOnBadStatement,
            // Copy source properties from first source instance
            SourceFieldSeparator = sourceFieldSeparator
        };
        
        // Copy source properties if available
        if (firstSource != null)
        {
            tempConfig.SourceAdapterName = firstSource.AdapterName;
            tempConfig.SourceConfiguration = firstSource.Configuration;
            tempConfig.SourceIsEnabled = firstSource.IsEnabled;
            tempConfig.SourceInstanceName = firstSource.InstanceName;
            tempConfig.SourceAdapterInstanceGuid = firstSource.AdapterInstanceGuid;
        }
        
        // Also copy Sources and Destinations dictionaries
        tempConfig.Sources = baseConfig.Sources;
        tempConfig.Destinations = baseConfig.Destinations;
        
        return tempConfig;
    }

    private static string ResolveDestinationTable(DestinationAdapterInstance instance, InterfaceConfiguration interfaceConfig)
    {
        if (!string.IsNullOrWhiteSpace(instance.Configuration))
        {
            try
            {
                var configDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(instance.Configuration);
                if (configDict != null)
                {
                    if (configDict.TryGetValue("tableName", out var tableNameElement) && tableNameElement.ValueKind == JsonValueKind.String)
                    {
                        return tableNameElement.GetString() ?? interfaceConfig.SqlTableName ?? "TransportData";
                    }
                    if (configDict.TryGetValue("destination", out var destinationElement) && destinationElement.ValueKind == JsonValueKind.String)
                    {
                        return destinationElement.GetString() ?? interfaceConfig.SqlTableName ?? "TransportData";
                    }
                }
            }
            catch
            {
                // Ignore parse errors and fall back
            }
        }

        // Try to get SqlTableName from instance first, then from interface config
        return instance.SqlTableName ?? interfaceConfig.Destinations.Values.FirstOrDefault()?.SqlTableName ?? "TransportData";
    }

    private static string ExtractSourcePath(string sourceConfiguration)
    {
        if (string.IsNullOrWhiteSpace(sourceConfiguration))
        {
            return "csv-files/csv-incoming";
        }

        try
        {
            var configDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sourceConfiguration);
            if (configDict != null && configDict.TryGetValue("source", out var sourceElement) && sourceElement.ValueKind == JsonValueKind.String)
            {
                return sourceElement.GetString() ?? "csv-files/csv-incoming";
            }
        }
        catch
        {
            // Ignore and fall back
        }

        return "csv-files/csv-incoming";
    }

    private record RunTransportRequest
    {
        public string? InterfaceName { get; init; }
        public string? CsvContent { get; init; }
    }

    private record SourceSummary(int ProcessedRecords);

    private record DestinationSummary(int ProcessedAdapters, List<string> DestinationTables);

    private static class SampleCsvGenerator
    {
        public static string GenerateSampleCsv(string separator)
        {
            var headers = new[] { "id", "name", "email", "city" };
            var rows = new List<string>
            {
                string.Join(separator, headers),
                $"1{separator}John Doe{separator}john.doe@example.com{separator}Berlin",
                $"2{separator}Anna Smith{separator}anna.smith@example.com{separator}Munich",
                $"3{separator}Peter Mueller{separator}peter.mueller@example.com{separator}Hamburg"
            };
            return string.Join(Environment.NewLine, rows);
        }
    }
}


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
            if (!config.SourceIsEnabled)
            {
                await _configService.SetSourceEnabledAsync(interfaceName, true, executionContext.CancellationToken);
                config.SourceIsEnabled = true;
            }
            if (!config.DestinationIsEnabled)
            {
                await _configService.SetDestinationEnabledAsync(interfaceName, true, executionContext.CancellationToken);
                config.DestinationIsEnabled = true;
            }

            var csvContent = string.IsNullOrWhiteSpace(request.CsvContent)
                ? SampleCsvGenerator.GenerateSampleCsv(config.SourceFieldSeparator ?? "║")
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
            if (config.SourceAdapterInstanceGuid == Guid.Empty)
            {
                config.SourceAdapterInstanceGuid = Guid.NewGuid();
                await _configService.SaveConfigurationAsync(config, cancellationToken);
            }
            return config;
        }

        var newConfig = new InterfaceConfiguration
        {
            InterfaceName = interfaceName,
            SourceAdapterName = "CSV",
            SourceConfiguration = JsonSerializer.Serialize(new { source = "csv-files/csv-incoming" }),
            DestinationAdapterName = "SqlServer",
            DestinationConfiguration = JsonSerializer.Serialize(new { destination = "TransportData" }),
            SourceIsEnabled = true,
            DestinationIsEnabled = true,
            SourceInstanceName = "Source",
            DestinationInstanceName = "Destination",
            SourceAdapterInstanceGuid = Guid.NewGuid(),
            DestinationAdapterInstanceGuid = Guid.NewGuid(),
            CsvPollingInterval = 10,
            SqlTableName = "TransportData",
            CreatedAt = DateTime.UtcNow
        };

        await _configService.SaveConfigurationAsync(newConfig, cancellationToken);
        return newConfig;
    }

    private async Task<SourceSummary> WriteCsvToMessageBoxAsync(
        InterfaceConfiguration config,
        string csvContent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Writing CSV content to MessageBox for interface {InterfaceName}", config.InterfaceName);

        var adapter = await _adapterFactory.CreateSourceAdapterAsync(config, cancellationToken);
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
            var sourcePath = ExtractSourcePath(config.SourceConfiguration);
            var (_, records) = await adapter.ReadAsync(sourcePath, cancellationToken);
            processedRecords = records.Count;
        }

        _logger.LogInformation("CSV data processed and written to MessageBox: {RecordCount} records", processedRecords);
        return new SourceSummary(processedRecords);
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
        return new InterfaceConfiguration
        {
            InterfaceName = baseConfig.InterfaceName,
            DestinationAdapterName = instance.AdapterName,
            DestinationConfiguration = instance.Configuration ?? "{}",
            DestinationIsEnabled = instance.IsEnabled,
            DestinationInstanceName = instance.InstanceName,
            DestinationAdapterInstanceGuid = instance.AdapterInstanceGuid,
            SourceFieldSeparator = baseConfig.SourceFieldSeparator,
            SqlServerName = baseConfig.SqlServerName,
            SqlDatabaseName = baseConfig.SqlDatabaseName,
            SqlUserName = baseConfig.SqlUserName,
            SqlPassword = baseConfig.SqlPassword,
            SqlIntegratedSecurity = baseConfig.SqlIntegratedSecurity,
            SqlResourceGroup = baseConfig.SqlResourceGroup,
            SqlTableName = destinationTable,
            DestinationReceiveFolder = baseConfig.DestinationReceiveFolder,
            DestinationFileMask = baseConfig.DestinationFileMask,
            SqlUseTransaction = baseConfig.SqlUseTransaction,
            SqlBatchSize = baseConfig.SqlBatchSize,
            SourceAdapterInstanceGuid = baseConfig.SourceAdapterInstanceGuid,
            SourceAdapterName = baseConfig.SourceAdapterName,
            SourceConfiguration = baseConfig.SourceConfiguration,
            SourceInstanceName = baseConfig.SourceInstanceName,
            SourceIsEnabled = baseConfig.SourceIsEnabled
        };
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

        return interfaceConfig.SqlTableName ?? "TransportData";
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


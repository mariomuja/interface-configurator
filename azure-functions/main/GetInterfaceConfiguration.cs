using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class GetInterfaceConfiguration
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<GetInterfaceConfiguration> _logger;

    public GetInterfaceConfiguration(
        IInterfaceConfigurationService configService,
        ILogger<GetInterfaceConfiguration> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetInterfaceConfiguration")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "GetInterfaceConfiguration")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("GetInterfaceConfiguration function triggered");

        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var interfaceName = queryParams["interfaceName"];

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "interfaceName query parameter is required" }));
                return badRequestResponse;
            }

            var configuration = await _configService.GetConfigurationAsync(interfaceName, executionContext.CancellationToken);

            if (configuration == null)
            {
                var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                notFoundResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(notFoundResponse);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = $"Interface configuration '{interfaceName}' not found" }));
                return notFoundResponse;
            }

            // Build hierarchical structure
            var hierarchicalConfig = BuildHierarchicalConfiguration(configuration);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var jsonResponse = JsonSerializer.Serialize(hierarchicalConfig, options);
            await response.WriteStringAsync(jsonResponse);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting interface configuration");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private JsonObject BuildHierarchicalConfiguration(InterfaceConfiguration config)
    {
        var result = new JsonObject
        {
            ["interfaceName"] = config.InterfaceName ?? string.Empty,
            ["description"] = config.Description ?? string.Empty
        };

        // Build sources section
        var sources = new JsonObject();
        var sourceAdapterName = config.SourceAdapterName ?? "CSV";
        
        // Parse source configuration
        var sourceConfig = new Dictionary<string, JsonElement>();
        if (!string.IsNullOrWhiteSpace(config.SourceConfiguration))
        {
            try
            {
                sourceConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(config.SourceConfiguration) 
                    ?? new Dictionary<string, JsonElement>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse SourceConfiguration JSON: {SourceConfiguration}", config.SourceConfiguration);
            }
        }

        var sourceInstance = new JsonObject
        {
            ["adapterInstanceGuid"] = config.SourceAdapterInstanceGuid.ToString(),
            ["instanceName"] = config.SourceInstanceName ?? "Source",
            ["adapterName"] = sourceAdapterName,
            ["isEnabled"] = config.SourceIsEnabled
        };

        var sourceProperties = new JsonObject();

        // Add CSV-specific properties
        if (sourceAdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase))
        {
            sourceProperties["receiveFolder"] = config.SourceReceiveFolder ?? string.Empty;
            sourceProperties["fileMask"] = config.SourceFileMask ?? "*.txt";
            sourceProperties["batchSize"] = config.SourceBatchSize;
            sourceProperties["fieldSeparator"] = config.SourceFieldSeparator ?? "║";
            sourceProperties["csvAdapterType"] = config.CsvAdapterType ?? "RAW";
            sourceProperties["csvPollingInterval"] = config.CsvPollingInterval > 0 ? config.CsvPollingInterval : 10;
            
            if (!string.IsNullOrWhiteSpace(config.CsvData))
            {
                sourceProperties["csvData"] = config.CsvData;
            }

            // Add SFTP properties if applicable
            if (config.CsvAdapterType?.Equals("SFTP", StringComparison.OrdinalIgnoreCase) == true)
            {
                sourceProperties["sftpHost"] = config.SftpHost ?? string.Empty;
                sourceProperties["sftpPort"] = config.SftpPort;
                sourceProperties["sftpUsername"] = config.SftpUsername ?? string.Empty;
                sourceProperties["sftpPassword"] = config.SftpPassword != null ? "***" : string.Empty;
                sourceProperties["sftpSshKey"] = config.SftpSshKey != null ? "***" : string.Empty;
                sourceProperties["sftpFolder"] = config.SftpFolder ?? string.Empty;
                sourceProperties["sftpFileMask"] = config.SftpFileMask ?? "*.txt";
                sourceProperties["sftpMaxConnectionPoolSize"] = config.SftpMaxConnectionPoolSize;
                sourceProperties["sftpFileBufferSize"] = config.SftpFileBufferSize;
            }
        }

        // Add SQL Server-specific properties
        if (sourceAdapterName.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            sourceProperties["sqlServerName"] = config.SqlServerName ?? string.Empty;
            sourceProperties["sqlDatabaseName"] = config.SqlDatabaseName ?? string.Empty;
            sourceProperties["sqlUserName"] = config.SqlUserName ?? string.Empty;
            sourceProperties["sqlPassword"] = config.SqlPassword != null ? "***" : string.Empty;
            sourceProperties["sqlIntegratedSecurity"] = config.SqlIntegratedSecurity;
            sourceProperties["sqlResourceGroup"] = config.SqlResourceGroup ?? string.Empty;
            sourceProperties["sqlPollingStatement"] = config.SqlPollingStatement ?? string.Empty;
            sourceProperties["sqlPollingInterval"] = config.SqlPollingInterval > 0 ? config.SqlPollingInterval : 60;
            sourceProperties["sqlUseTransaction"] = config.SqlUseTransaction;
            sourceProperties["sqlBatchSize"] = config.SqlBatchSize;
            sourceProperties["sqlCommandTimeout"] = config.SqlCommandTimeout;
            sourceProperties["sqlFailOnBadStatement"] = config.SqlFailOnBadStatement;
        }

        sourceInstance["properties"] = sourceProperties;
        sources[sourceAdapterName] = sourceInstance;
        result["sources"] = sources;

        // Build destinations section
        var destinations = new JsonObject();
        
        // Get all destination adapter instances
        var destinationInstances = config.DestinationAdapterInstances ?? new List<DestinationAdapterInstance>();
        
        foreach (var instance in destinationInstances)
        {
            var adapterName = instance.AdapterName ?? "SqlServer";
            var instanceName = instance.InstanceName ?? "Destination";

            // Parse instance configuration
            var instanceConfig = new Dictionary<string, JsonElement>();
            if (!string.IsNullOrWhiteSpace(instance.Configuration))
            {
                try
                {
                    instanceConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(instance.Configuration) 
                        ?? new Dictionary<string, JsonElement>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse instance Configuration JSON: {Configuration}", instance.Configuration);
                }
            }

            // Initialize adapter type in destinations if not exists
            if (!destinations.ContainsKey(adapterName))
            {
                destinations[adapterName] = new JsonObject();
            }

            var destInstance = new JsonObject
            {
                ["adapterInstanceGuid"] = instance.AdapterInstanceGuid.ToString(),
                ["instanceName"] = instanceName,
                ["adapterName"] = adapterName,
                ["isEnabled"] = instance.IsEnabled
            };

            var destProperties = new JsonObject();

            // Add CSV-specific properties
            if (adapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase))
            {
                destProperties["receiveFolder"] = GetStringValue(instanceConfig, "receiveFolder") ?? 
                    GetStringValue(instanceConfig, "destination") ?? string.Empty;
                destProperties["fileMask"] = GetStringValue(instanceConfig, "fileMask") ?? "*.txt";
                destProperties["batchSize"] = GetIntValue(instanceConfig, "batchSize") ?? 100;
                destProperties["fieldSeparator"] = GetStringValue(instanceConfig, "fieldSeparator") ?? "║";
                destProperties["destinationReceiveFolder"] = GetStringValue(instanceConfig, "destinationReceiveFolder") ?? 
                    GetStringValue(instanceConfig, "receiveFolder") ?? string.Empty;
                destProperties["destinationFileMask"] = GetStringValue(instanceConfig, "destinationFileMask") ?? 
                    GetStringValue(instanceConfig, "fileMask") ?? "*.txt";
            }

            // Add SQL Server-specific properties
            if (adapterName.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                destProperties["destination"] = GetStringValue(instanceConfig, "tableName") ?? 
                    GetStringValue(instanceConfig, "destination") ?? config.SqlTableName ?? "TransportData";
                destProperties["tableName"] = GetStringValue(instanceConfig, "tableName") ?? 
                    GetStringValue(instanceConfig, "destination") ?? config.SqlTableName ?? "TransportData";
                destProperties["sqlServerName"] = GetStringValue(instanceConfig, "sqlServerName") ?? config.SqlServerName ?? string.Empty;
                destProperties["sqlDatabaseName"] = GetStringValue(instanceConfig, "sqlDatabaseName") ?? config.SqlDatabaseName ?? string.Empty;
                destProperties["sqlUserName"] = GetStringValue(instanceConfig, "sqlUserName") ?? config.SqlUserName ?? string.Empty;
                destProperties["sqlPassword"] = (GetStringValue(instanceConfig, "sqlPassword") != null || config.SqlPassword != null) ? "***" : string.Empty;
                destProperties["sqlIntegratedSecurity"] = GetBoolValue(instanceConfig, "sqlIntegratedSecurity") ?? config.SqlIntegratedSecurity;
                destProperties["sqlResourceGroup"] = GetStringValue(instanceConfig, "sqlResourceGroup") ?? config.SqlResourceGroup ?? string.Empty;
                destProperties["sqlPollingStatement"] = GetStringValue(instanceConfig, "sqlPollingStatement") ?? config.SqlPollingStatement ?? string.Empty;
                destProperties["sqlPollingInterval"] = GetIntValue(instanceConfig, "sqlPollingInterval") ?? (config.SqlPollingInterval > 0 ? config.SqlPollingInterval : 60);
                destProperties["sqlUseTransaction"] = GetBoolValue(instanceConfig, "sqlUseTransaction") ?? config.SqlUseTransaction;
                destProperties["sqlBatchSize"] = GetIntValue(instanceConfig, "sqlBatchSize") ?? config.SqlBatchSize;
                destProperties["sqlCommandTimeout"] = GetIntValue(instanceConfig, "sqlCommandTimeout") ?? config.SqlCommandTimeout;
                destProperties["sqlFailOnBadStatement"] = GetBoolValue(instanceConfig, "sqlFailOnBadStatement") ?? config.SqlFailOnBadStatement;
            }

            destInstance["properties"] = destProperties;
            
            // Add instance to destinations (use instanceName as key)
            var adapterDestinations = destinations[adapterName] as JsonObject;
            if (adapterDestinations != null)
            {
                adapterDestinations[instanceName] = destInstance;
            }
        }

        result["destinations"] = destinations;

        return result;
    }

    private string? GetStringValue(Dictionary<string, JsonElement> dict, string key)
    {
        if (dict.TryGetValue(key, out var element))
        {
            return element.GetString();
        }
        return null;
    }

    private int? GetIntValue(Dictionary<string, JsonElement> dict, string key)
    {
        if (dict.TryGetValue(key, out var element))
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt32();
            }
        }
        return null;
    }

    private bool? GetBoolValue(Dictionary<string, JsonElement> dict, string key)
    {
        if (dict.TryGetValue(key, out var element))
        {
            if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
            {
                return element.GetBoolean();
            }
        }
        return null;
    }
}


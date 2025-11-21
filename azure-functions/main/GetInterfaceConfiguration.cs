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

            // Build the structure exactly as stored in the JSON file (with Sources and Destinations dictionaries)
            // Use PascalCase to match the stored JSON format
            var storedFormatOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null, // Use PascalCase (no conversion)
                DictionaryKeyPolicy = null, // Use PascalCase for dictionary keys
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };

            // Build the structure matching the stored JSON format
            var storedFormat = new JsonObject
            {
                ["InterfaceName"] = configuration.InterfaceName ?? string.Empty,
                ["Description"] = configuration.Description ?? null,
                ["Sources"] = BuildSourcesDictionary(configuration),
                ["Destinations"] = BuildDestinationsDictionary(configuration),
                ["CreatedAt"] = configuration.CreatedAt != default ? JsonValue.Create(configuration.CreatedAt) : null,
                ["UpdatedAt"] = configuration.UpdatedAt.HasValue ? JsonValue.Create(configuration.UpdatedAt.Value) : null
            };

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            var jsonResponse = storedFormat.ToJsonString(storedFormatOptions);
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

    private JsonObject BuildSourcesDictionary(InterfaceConfiguration config)
    {
        var sources = new JsonObject();
        
        // Use the new Sources dictionary if available
        if (config.Sources != null && config.Sources.Count > 0)
        {
            foreach (var sourceEntry in config.Sources)
            {
                var sourceInstance = sourceEntry.Value;
                var sourceObj = new JsonObject
                {
                    ["InstanceName"] = sourceInstance.InstanceName ?? string.Empty,
                    ["AdapterName"] = sourceInstance.AdapterName ?? string.Empty,
                    ["IsEnabled"] = sourceInstance.IsEnabled,
                    ["AdapterInstanceGuid"] = sourceInstance.AdapterInstanceGuid.ToString(),
                    ["Configuration"] = sourceInstance.Configuration ?? string.Empty,
                    ["SourceReceiveFolder"] = sourceInstance.SourceReceiveFolder ?? null,
                    ["SourceFileMask"] = sourceInstance.SourceFileMask ?? "*.txt",
                    ["SourceBatchSize"] = sourceInstance.SourceBatchSize,
                    ["SourceFieldSeparator"] = sourceInstance.SourceFieldSeparator ?? "║",
                    ["CsvData"] = sourceInstance.CsvData ?? null,
                    ["CsvAdapterType"] = sourceInstance.CsvAdapterType ?? "FILE",
                    ["CsvPollingInterval"] = sourceInstance.CsvPollingInterval,
                    ["SftpHost"] = sourceInstance.SftpHost ?? null,
                    ["SftpPort"] = sourceInstance.SftpPort,
                    ["SftpUsername"] = sourceInstance.SftpUsername ?? null,
                    ["SftpPassword"] = sourceInstance.SftpPassword != null ? "***" : null,
                    ["SftpSshKey"] = sourceInstance.SftpSshKey != null ? "***" : null,
                    ["SftpFolder"] = sourceInstance.SftpFolder ?? null,
                    ["SftpFileMask"] = sourceInstance.SftpFileMask ?? "*.txt",
                    ["SftpMaxConnectionPoolSize"] = sourceInstance.SftpMaxConnectionPoolSize,
                    ["SftpFileBufferSize"] = sourceInstance.SftpFileBufferSize,
                    ["SqlServerName"] = sourceInstance.SqlServerName ?? null,
                    ["SqlDatabaseName"] = sourceInstance.SqlDatabaseName ?? null,
                    ["SqlUserName"] = sourceInstance.SqlUserName ?? null,
                    ["SqlPassword"] = sourceInstance.SqlPassword != null ? "***" : null,
                    ["SqlIntegratedSecurity"] = sourceInstance.SqlIntegratedSecurity,
                    ["SqlResourceGroup"] = sourceInstance.SqlResourceGroup ?? null,
                    ["SqlPollingStatement"] = sourceInstance.SqlPollingStatement ?? null,
                    ["SqlPollingInterval"] = sourceInstance.SqlPollingInterval,
                    ["SqlTableName"] = sourceInstance.SqlTableName ?? null,
                    ["SqlUseTransaction"] = sourceInstance.SqlUseTransaction,
                    ["SqlBatchSize"] = sourceInstance.SqlBatchSize,
                    ["SqlCommandTimeout"] = sourceInstance.SqlCommandTimeout,
                    ["SqlFailOnBadStatement"] = sourceInstance.SqlFailOnBadStatement,
                    ["CreatedAt"] = sourceInstance.CreatedAt != default ? JsonValue.Create(sourceInstance.CreatedAt) : null,
                    ["UpdatedAt"] = sourceInstance.UpdatedAt.HasValue ? JsonValue.Create(sourceInstance.UpdatedAt.Value) : null
                };
                sources[sourceEntry.Key] = sourceObj;
            }
        }
        else
        {
            // Fallback to old structure for backward compatibility
            var sourceInstanceName = config.SourceInstanceName ?? config.SourceAdapterName ?? "Source";
            var sourceObj = new JsonObject
            {
                ["InstanceName"] = sourceInstanceName,
                ["AdapterName"] = config.SourceAdapterName ?? "CSV",
                ["IsEnabled"] = config.SourceIsEnabled ?? true,
                ["AdapterInstanceGuid"] = (config.SourceAdapterInstanceGuid ?? Guid.Empty).ToString(),
                ["Configuration"] = config.SourceConfiguration ?? string.Empty,
                ["SourceReceiveFolder"] = config.SourceReceiveFolder ?? null,
                ["SourceFileMask"] = config.SourceFileMask ?? "*.txt",
                ["SourceBatchSize"] = config.SourceBatchSize ?? 100,
                ["SourceFieldSeparator"] = config.SourceFieldSeparator ?? "║",
                ["CsvData"] = config.CsvData ?? null,
                ["CsvAdapterType"] = config.CsvAdapterType ?? "FILE",
                ["CsvPollingInterval"] = config.CsvPollingInterval ?? 10,
                ["SftpHost"] = config.SftpHost ?? null,
                ["SftpPort"] = config.SftpPort ?? 22,
                ["SftpUsername"] = config.SftpUsername ?? null,
                ["SftpPassword"] = config.SftpPassword != null ? "***" : null,
                ["SftpSshKey"] = config.SftpSshKey != null ? "***" : null,
                ["SftpFolder"] = config.SftpFolder ?? null,
                ["SftpFileMask"] = config.SftpFileMask ?? "*.txt",
                ["SftpMaxConnectionPoolSize"] = config.SftpMaxConnectionPoolSize ?? 5,
                ["SftpFileBufferSize"] = config.SftpFileBufferSize ?? 8192,
                ["SqlServerName"] = config.SqlServerName ?? null,
                ["SqlDatabaseName"] = config.SqlDatabaseName ?? null,
                ["SqlUserName"] = config.SqlUserName ?? null,
                ["SqlPassword"] = config.SqlPassword != null ? "***" : null,
                ["SqlIntegratedSecurity"] = config.SqlIntegratedSecurity ?? false,
                ["SqlResourceGroup"] = config.SqlResourceGroup ?? null,
                ["SqlPollingStatement"] = config.SqlPollingStatement ?? null,
                ["SqlPollingInterval"] = config.SqlPollingInterval ?? 60,
                ["SqlTableName"] = config.SqlTableName ?? null,
                ["SqlUseTransaction"] = config.SqlUseTransaction ?? false,
                ["SqlBatchSize"] = config.SqlBatchSize ?? 1000,
                ["SqlCommandTimeout"] = config.SqlCommandTimeout ?? 30,
                ["SqlFailOnBadStatement"] = config.SqlFailOnBadStatement ?? false,
                ["CreatedAt"] = config.CreatedAt != default ? JsonValue.Create(config.CreatedAt) : null,
                ["UpdatedAt"] = config.UpdatedAt.HasValue ? JsonValue.Create(config.UpdatedAt.Value) : null
            };
            sources[sourceInstanceName] = sourceObj;
        }
        
        return sources;
    }

    private JsonObject BuildDestinationsDictionary(InterfaceConfiguration config)
    {
        var destinations = new JsonObject();
        
        // Use the new Destinations dictionary if available
        if (config.Destinations != null && config.Destinations.Count > 0)
        {
            foreach (var destEntry in config.Destinations)
            {
                var destInstance = destEntry.Value;
                var destObj = new JsonObject
                {
                    ["AdapterInstanceGuid"] = destInstance.AdapterInstanceGuid.ToString(),
                    ["InstanceName"] = destInstance.InstanceName ?? string.Empty,
                    ["AdapterName"] = destInstance.AdapterName ?? string.Empty,
                    ["IsEnabled"] = destInstance.IsEnabled,
                    ["Configuration"] = destInstance.Configuration ?? string.Empty,
                    ["DestinationReceiveFolder"] = destInstance.DestinationReceiveFolder ?? null,
                    ["DestinationFileMask"] = destInstance.DestinationFileMask ?? "*.txt",
                    ["SqlServerName"] = destInstance.SqlServerName ?? null,
                    ["SqlDatabaseName"] = destInstance.SqlDatabaseName ?? null,
                    ["SqlUserName"] = destInstance.SqlUserName ?? null,
                    ["SqlPassword"] = destInstance.SqlPassword != null ? "***" : null,
                    ["SqlIntegratedSecurity"] = destInstance.SqlIntegratedSecurity,
                    ["SqlResourceGroup"] = destInstance.SqlResourceGroup ?? null,
                    ["SqlTableName"] = destInstance.SqlTableName ?? null,
                    ["SqlUseTransaction"] = destInstance.SqlUseTransaction,
                    ["SqlBatchSize"] = destInstance.SqlBatchSize,
                    ["SqlCommandTimeout"] = destInstance.SqlCommandTimeout,
                    ["SqlFailOnBadStatement"] = destInstance.SqlFailOnBadStatement,
                    ["CreatedAt"] = destInstance.CreatedAt != default ? JsonValue.Create(destInstance.CreatedAt) : null,
                    ["UpdatedAt"] = destInstance.UpdatedAt.HasValue ? JsonValue.Create(destInstance.UpdatedAt.Value) : null
                };
                destinations[destEntry.Key] = destObj;
            }
        }
        else
        {
            // Fallback to old structure for backward compatibility
            // Try DestinationAdapterInstances list first
            if (config.DestinationAdapterInstances != null && config.DestinationAdapterInstances.Count > 0)
            {
                foreach (var instance in config.DestinationAdapterInstances)
                {
                    var instanceName = instance.InstanceName ?? "Destination";
                    var destObj = new JsonObject
                    {
                        ["AdapterInstanceGuid"] = instance.AdapterInstanceGuid.ToString(),
                        ["InstanceName"] = instanceName,
                        ["AdapterName"] = instance.AdapterName ?? string.Empty,
                        ["IsEnabled"] = instance.IsEnabled,
                        ["Configuration"] = instance.Configuration ?? string.Empty,
                        ["CreatedAt"] = instance.CreatedAt != default ? JsonValue.Create(instance.CreatedAt) : null,
                        ["UpdatedAt"] = instance.UpdatedAt.HasValue ? JsonValue.Create(instance.UpdatedAt.Value) : null
                    };
                    destinations[instanceName] = destObj;
                }
            }
            else if (!string.IsNullOrWhiteSpace(config.DestinationAdapterName))
            {
                // Fallback to old flat structure
                var destInstanceName = config.DestinationInstanceName ?? config.DestinationAdapterName ?? "Destination";
                var destObj = new JsonObject
                {
                    ["AdapterInstanceGuid"] = (config.DestinationAdapterInstanceGuid ?? Guid.Empty).ToString(),
                    ["InstanceName"] = destInstanceName,
                    ["AdapterName"] = config.DestinationAdapterName ?? "SqlServer",
                    ["IsEnabled"] = config.DestinationIsEnabled ?? true,
                    ["Configuration"] = config.DestinationConfiguration ?? string.Empty,
                    ["DestinationReceiveFolder"] = config.DestinationReceiveFolder ?? null,
                    ["DestinationFileMask"] = config.DestinationFileMask ?? "*.txt",
                    ["SqlServerName"] = config.SqlServerName ?? null,
                    ["SqlDatabaseName"] = config.SqlDatabaseName ?? null,
                    ["SqlUserName"] = config.SqlUserName ?? null,
                    ["SqlPassword"] = config.SqlPassword != null ? "***" : null,
                    ["SqlIntegratedSecurity"] = config.SqlIntegratedSecurity ?? false,
                    ["SqlResourceGroup"] = config.SqlResourceGroup ?? null,
                    ["SqlTableName"] = config.SqlTableName ?? null,
                    ["SqlUseTransaction"] = config.SqlUseTransaction ?? false,
                    ["SqlBatchSize"] = config.SqlBatchSize ?? 1000,
                    ["SqlCommandTimeout"] = config.SqlCommandTimeout ?? 30,
                    ["SqlFailOnBadStatement"] = config.SqlFailOnBadStatement ?? false,
                    ["CreatedAt"] = config.CreatedAt != default ? JsonValue.Create(config.CreatedAt) : null,
                    ["UpdatedAt"] = config.UpdatedAt.HasValue ? JsonValue.Create(config.UpdatedAt.Value) : null
                };
                destinations[destInstanceName] = destObj;
            }
        }
        
        return destinations;
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

        foreach (var property in sourceProperties)
        {
            sourceInstance[property.Key] = property.Value;
        }
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

            foreach (var property in destProperties)
            {
                destInstance[property.Key] = property.Value;
            }
            
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


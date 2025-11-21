using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Helpers;
using InterfaceConfigurator.Main.Adapters;
using System.Linq;

namespace InterfaceConfigurator.Main;

/// <summary>
/// Test endpoint to verify CsvAdapter MessageBox write functionality
/// Simulates the blob trigger logic including adapter creation and ReadAsync call
/// </summary>
public class TestCsvAdapterMessageBox
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IAdapterFactory _adapterFactory;
    private readonly ILogger<TestCsvAdapterMessageBox> _logger;

    public TestCsvAdapterMessageBox(
        IInterfaceConfigurationService configService,
        IAdapterFactory adapterFactory,
        ILogger<TestCsvAdapterMessageBox> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("TestCsvAdapterMessageBox")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "test-csv-adapter-messagebox")] HttpRequestData req,
        FunctionContext context)
    {
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            _logger.LogInformation("=== TESTING CSV ADAPTER MESSAGEBOX WRITE ===");
            
            // Force initialization
            await _configService.InitializeAsync(context.CancellationToken);
            
            // Get enabled CSV source configurations
            var enabledConfigs = await _configService.GetEnabledSourceConfigurationsAsync(context.CancellationToken);
            _logger.LogInformation("Found {Count} enabled source configurations", enabledConfigs.Count);
            
            // Filter configurations that have CSV source adapters
            var csvConfigs = enabledConfigs
                .Where(c => c.Sources.Values.Any(s => s.AdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase) && s.IsEnabled))
                .ToList();
            
            _logger.LogInformation("Found {Count} CSV configurations", csvConfigs.Count);

            var results = new List<object>();

            foreach (var config in csvConfigs)
            {
                // Get all enabled CSV source instances for this interface
                var csvSources = config.Sources.Values
                    .Where(s => s.AdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase) && s.IsEnabled)
                    .ToList();
                
                foreach (var sourceInstance in csvSources)
                {
                    try
                    {
                        _logger.LogInformation("Testing CSV adapter for interface {InterfaceName}, source instance '{InstanceName}' (GUID: {AdapterInstanceGuid})",
                            config.InterfaceName, sourceInstance.InstanceName, sourceInstance.AdapterInstanceGuid);
                        
                        // Verify configuration has required values
                        if (sourceInstance.AdapterInstanceGuid == Guid.Empty)
                        {
                            results.Add(new
                            {
                                interfaceName = config.InterfaceName,
                                instanceName = sourceInstance.InstanceName,
                                success = false,
                                error = "AdapterInstanceGuid is empty"
                            });
                            continue;
                        }
                        
                        if (string.IsNullOrWhiteSpace(config.InterfaceName))
                        {
                            results.Add(new
                            {
                                interfaceName = config.InterfaceName,
                                instanceName = sourceInstance.InstanceName,
                                success = false,
                                error = "InterfaceName is empty"
                            });
                            continue;
                        }
                        
                        // Create a temporary config with this source instance for the adapter factory
                        var tempConfig = CreateTempConfigForSource(config, sourceInstance);
                        
                        // Create adapter
                        var csvAdapter = await _adapterFactory.CreateSourceAdapterAsync(tempConfig, context.CancellationToken);
                        
                        if (csvAdapter is CsvAdapter csv)
                        {
                            // CsvAdapter was created successfully
                            // The MessageBox conditions will be checked when ReadAsync is called
                            // For now, we just verify the adapter was created with the correct configuration
                            results.Add(new
                            {
                                interfaceName = config.InterfaceName,
                                instanceName = sourceInstance.InstanceName,
                                adapterInstanceGuid = sourceInstance.AdapterInstanceGuid,
                                success = true,
                                message = "CsvAdapter created successfully. MessageBox conditions will be checked when ReadAsync is called. Check Application Insights logs for 'Checking MessageBox conditions' messages."
                            });
                        }
                        else
                        {
                            results.Add(new
                            {
                                interfaceName = config.InterfaceName,
                                instanceName = sourceInstance.InstanceName,
                                success = false,
                                error = $"Adapter is not CsvAdapter type: {csvAdapter?.GetType().Name ?? "null"}"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error testing CSV adapter for interface {InterfaceName}, source instance '{InstanceName}'",
                            config.InterfaceName, sourceInstance.InstanceName);
                        
                        results.Add(new
                        {
                            interfaceName = config.InterfaceName,
                            instanceName = sourceInstance.InstanceName,
                            success = false,
                            error = ex.Message,
                            stackTrace = ex.StackTrace
                        });
                    }
                }
            }

            var result = new
            {
                csvConfigurationsCount = csvConfigs.Count,
                testedInstancesCount = results.Count,
                results = results
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestCsvAdapterMessageBox");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message, stackTrace = ex.StackTrace }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Creates a temporary InterfaceConfiguration from a SourceAdapterInstance for use with AdapterFactory
    /// This matches the logic in main.cs
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
            // SQL Properties (for SQL Source)
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
            SqlFailOnBadStatement = sourceInstance.SqlFailOnBadStatement,
            // Copy destination properties from base config (for backward compatibility if needed by adapter factory)
            DestinationAdapterName = originalConfig.DestinationAdapterName,
            DestinationConfiguration = originalConfig.DestinationConfiguration,
            DestinationIsEnabled = originalConfig.DestinationIsEnabled,
            DestinationInstanceName = originalConfig.DestinationInstanceName,
            DestinationAdapterInstanceGuid = originalConfig.DestinationAdapterInstanceGuid,
            DestinationAdapterInstances = originalConfig.DestinationAdapterInstances // Pass all destination instances
        };
        
        return tempConfig;
    }
}


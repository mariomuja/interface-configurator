using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Adapters;
using System.Linq;

#pragma warning disable CS0618 // Type or member is obsolete - Deprecated properties are used for backward compatibility

namespace InterfaceConfigurator.Main;

/// <summary>
/// Test endpoint to manually trigger SourceAdapterFunction processing
/// </summary>
public class TestSourceAdapterProcess
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IAdapterFactory _adapterFactory;
    private readonly ILogger<TestSourceAdapterProcess> _logger;

    public TestSourceAdapterProcess(
        IInterfaceConfigurationService configService,
        IAdapterFactory adapterFactory,
        ILogger<TestSourceAdapterProcess> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("TestSourceAdapterProcess")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "test-source-adapter-process")] HttpRequestData req,
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
            _logger.LogInformation("=== TESTING SOURCE ADAPTER PROCESSING ===");
            
            // Get enabled source configurations
            var configurations = await _configService.GetEnabledSourceConfigurationsAsync(context.CancellationToken);
            
            if (!configurations.Any())
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(errorResponse);
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "No enabled source configurations found" }));
                return errorResponse;
            }

            var results = new List<object>();

            foreach (var config in configurations)
            {
                var enabledSources = config.Sources.Values.Where(s => s.IsEnabled).ToList();
                
                foreach (var sourceInstance in enabledSources)
                {
                    try
                    {
                        _logger.LogInformation("Processing source instance: Interface={InterfaceName}, Instance={InstanceName}, Adapter={AdapterName}, AdapterType={AdapterType}",
                            config.InterfaceName, sourceInstance.InstanceName, sourceInstance.AdapterName, sourceInstance.CsvAdapterType);

                        if (sourceInstance.AdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase))
                        {
                            var adapterType = sourceInstance.CsvAdapterType ?? "FILE";
                            
                            // For RAW adapter type, process CsvData
                            if (adapterType.Equals("RAW", StringComparison.OrdinalIgnoreCase))
                            {
                                if (string.IsNullOrWhiteSpace(sourceInstance.CsvData))
                                {
                                    results.Add(new
                                    {
                                        interfaceName = config.InterfaceName,
                                        instanceName = sourceInstance.InstanceName,
                                        success = false,
                                        error = "CsvData is empty for RAW adapter type"
                                    });
                                    continue;
                                }

                                // Create temporary config for adapter factory
                                var tempConfig = CreateTempConfigForSource(config, sourceInstance);
                                
                                // Create adapter
                                var csvSourceAdapter = await _adapterFactory.CreateSourceAdapterAsync(tempConfig, context.CancellationToken);
                                if (csvSourceAdapter is CsvAdapter csvAdapter)
                                {
                                    _logger.LogInformation("CsvAdapter created. Setting CsvData to trigger upload to csv-incoming...");
                                    csvAdapter.CsvData = sourceInstance.CsvData; // This will trigger upload to csv-incoming
                                    
                                    // Wait a bit for the upload to complete
                                    await Task.Delay(2000, context.CancellationToken);
                                    
                                    results.Add(new
                                    {
                                        interfaceName = config.InterfaceName,
                                        instanceName = sourceInstance.InstanceName,
                                        adapterType = adapterType,
                                        success = true,
                                        message = "CsvData set on adapter. File should be uploaded to csv-incoming and processed via blob trigger. Check Application Insights logs."
                                    });
                                }
                                else
                                {
                                    results.Add(new
                                    {
                                        interfaceName = config.InterfaceName,
                                        instanceName = sourceInstance.InstanceName,
                                        success = false,
                                        error = $"Adapter is not CsvAdapter type: {csvSourceAdapter?.GetType().Name ?? "null"}"
                                    });
                                }
                            }
                            else
                            {
                                results.Add(new
                                {
                                    interfaceName = config.InterfaceName,
                                    instanceName = sourceInstance.InstanceName,
                                    adapterType = adapterType,
                                    success = false,
                                    error = $"AdapterType is '{adapterType}', not 'RAW'. Only RAW adapter type processes CsvData."
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing source instance '{InstanceName}' for interface '{InterfaceName}'",
                            sourceInstance.InstanceName, config.InterfaceName);
                        
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
                configurationsProcessed = configurations.Count,
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
            _logger.LogError(ex, "Error in TestSourceAdapterProcess");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message, stackTrace = ex.StackTrace }));
            return errorResponse;
        }
    }

    private InterfaceConfiguration CreateTempConfigForSource(InterfaceConfiguration originalConfig, SourceAdapterInstance sourceInstance)
    {
        var tempConfig = new InterfaceConfiguration
        {
            InterfaceName = originalConfig.InterfaceName,
            Description = originalConfig.Description,
            CreatedAt = originalConfig.CreatedAt,
            UpdatedAt = originalConfig.UpdatedAt,
            SourceAdapterName = sourceInstance.AdapterName,
            SourceConfiguration = sourceInstance.Configuration,
            SourceIsEnabled = sourceInstance.IsEnabled,
            SourceInstanceName = sourceInstance.InstanceName,
            SourceAdapterInstanceGuid = sourceInstance.AdapterInstanceGuid,
            SourceReceiveFolder = sourceInstance.SourceReceiveFolder,
            SourceFileMask = sourceInstance.SourceFileMask,
            SourceBatchSize = sourceInstance.SourceBatchSize,
            SourceFieldSeparator = sourceInstance.SourceFieldSeparator,
            CsvData = sourceInstance.CsvData,
            CsvAdapterType = sourceInstance.CsvAdapterType,
            CsvPollingInterval = sourceInstance.CsvPollingInterval,
            DestinationAdapterName = originalConfig.DestinationAdapterName,
            DestinationConfiguration = originalConfig.DestinationConfiguration,
            DestinationIsEnabled = originalConfig.DestinationIsEnabled,
            DestinationInstanceName = originalConfig.DestinationInstanceName,
            DestinationAdapterInstanceGuid = originalConfig.DestinationAdapterInstanceGuid,
            DestinationAdapterInstances = originalConfig.DestinationAdapterInstances
        };
        
        return tempConfig;
    }
}

#pragma warning restore CS0618 // Type or member is obsolete


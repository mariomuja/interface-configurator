using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;
using System.Linq;

namespace InterfaceConfigurator.Main;

/// <summary>
/// Test endpoint to simulate blob trigger logic
/// </summary>
public class TestBlobTriggerLogic
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<TestBlobTriggerLogic> _logger;

    public TestBlobTriggerLogic(
        IInterfaceConfigurationService configService,
        ILogger<TestBlobTriggerLogic> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("TestBlobTriggerLogic")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "test-blob-trigger-logic")] HttpRequestData req,
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
            // Simulate the exact logic from the blob trigger
            _logger.LogInformation("=== TESTING BLOB TRIGGER LOGIC ===");
            
            // Debug: Get all configurations first to ensure they're loaded
            var allConfigs = await _configService.GetAllConfigurationsAsync(context.CancellationToken);
            _logger.LogInformation("DEBUG: Total configurations loaded: {TotalCount}", allConfigs.Count);
            foreach (var cfg in allConfigs)
            {
                _logger.LogInformation("DEBUG: Config {InterfaceName}: SourceIsEnabled={SourceIsEnabled} (type: {Type}), SourceAdapterName={SourceAdapterName}, SourceAdapterInstanceGuid={Guid}",
                    cfg.InterfaceName, cfg.SourceIsEnabled, cfg.SourceIsEnabled.GetType().Name, cfg.SourceAdapterName, cfg.SourceAdapterInstanceGuid);
            }
            
            // Now get enabled configurations
            var enabledConfigs = await _configService.GetEnabledSourceConfigurationsAsync(context.CancellationToken);
            _logger.LogInformation("DEBUG: GetEnabledSourceConfigurationsAsync returned {Count} configurations", enabledConfigs.Count);
            foreach (var cfg in enabledConfigs)
            {
                _logger.LogInformation("DEBUG: Enabled config {InterfaceName}: SourceIsEnabled={SourceIsEnabled}, SourceAdapterName={SourceAdapterName}",
                    cfg.InterfaceName, cfg.SourceIsEnabled, cfg.SourceAdapterName);
            }
            
            // Filter configurations that have CSV source adapters (using new Sources structure)
            var csvConfigs = enabledConfigs
                .Where(c => c.Sources.Values.Any(s => s.AdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase) && s.IsEnabled))
                .ToList();
            
            _logger.LogInformation("Found {TotalConfigs} enabled configurations, {CsvConfigs} CSV configurations",
                enabledConfigs.Count, csvConfigs.Count);

            // Get CSV source instances for each config
            var csvSourceInstances = new List<object>();
            foreach (var config in csvConfigs)
            {
                var csvSources = config.Sources.Values
                    .Where(s => s.AdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase) && s.IsEnabled)
                    .ToList();
                
                foreach (var sourceInstance in csvSources)
                {
                    csvSourceInstances.Add(new
                    {
                        interfaceName = config.InterfaceName,
                        instanceName = sourceInstance.InstanceName,
                        adapterName = sourceInstance.AdapterName,
                        isEnabled = sourceInstance.IsEnabled,
                        adapterInstanceGuid = sourceInstance.AdapterInstanceGuid,
                        sourceReceiveFolder = sourceInstance.SourceReceiveFolder,
                        sourceFileMask = sourceInstance.SourceFileMask
                    });
                }
            }

            var result = new
            {
                totalConfigurations = allConfigs.Count,
                enabledConfigurationsCount = enabledConfigs.Count,
                csvConfigurationsCount = csvConfigs.Count,
                csvSourceInstancesCount = csvSourceInstances.Count,
                allConfigurations = allConfigs.Select(c => new
                {
                    interfaceName = c.InterfaceName,
                    sourceAdapterName = c.SourceAdapterName,
                    sourceIsEnabled = c.SourceIsEnabled,
                    sourceAdapterInstanceGuid = c.SourceAdapterInstanceGuid,
                    sourcesCount = c.Sources.Count,
                    sources = c.Sources.Values.Select(s => new
                    {
                        instanceName = s.InstanceName,
                        adapterName = s.AdapterName,
                        isEnabled = s.IsEnabled,
                        adapterInstanceGuid = s.AdapterInstanceGuid
                    }).ToList()
                }).ToList(),
                enabledConfigurationsList = enabledConfigs.Select(c => new
                {
                    interfaceName = c.InterfaceName,
                    sourceAdapterName = c.SourceAdapterName,
                    sourceIsEnabled = c.SourceIsEnabled,
                    sourcesCount = c.Sources.Count,
                    enabledSourcesCount = c.Sources.Values.Count(s => s.IsEnabled)
                }).ToList(),
                csvConfigurationsList = csvConfigs.Select(c => new
                {
                    interfaceName = c.InterfaceName,
                    sourceAdapterName = c.SourceAdapterName,
                    sourceIsEnabled = c.SourceIsEnabled,
                    sourceAdapterInstanceGuid = c.SourceAdapterInstanceGuid,
                    sourcesCount = c.Sources.Count
                }).ToList(),
                csvSourceInstances = csvSourceInstances
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestBlobTriggerLogic");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message, stackTrace = ex.StackTrace }));
            return errorResponse;
        }
    }
}


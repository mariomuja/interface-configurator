using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// Test endpoint to debug configuration loading
/// </summary>
public class TestConfigLoading
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<TestConfigLoading> _logger;

    public TestConfigLoading(
        IInterfaceConfigurationService configService,
        ILogger<TestConfigLoading> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("TestConfigLoading")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "test-config-loading")] HttpRequestData req,
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
            var allConfigs = await _configService.GetAllConfigurationsAsync(context.CancellationToken);
            var enabledConfigs = await _configService.GetEnabledSourceConfigurationsAsync(context.CancellationToken);

            var result = new
            {
                totalConfigurations = allConfigs.Count,
                enabledSourceConfigurations = enabledConfigs.Count,
                allConfigurations = allConfigs.Select(c => new
                {
                    interfaceName = c.InterfaceName,
                    sourceAdapterName = c.SourceAdapterName,
                    sourceIsEnabled = c.SourceIsEnabled,
                    sourceIsEnabledType = c.SourceIsEnabled.GetType().Name,
                    sourceIsEnabledValue = c.SourceIsEnabled.ToString(),
                    sourceIsEnabledEqualsTrue = c.SourceIsEnabled == true,
                    sourceAdapterInstanceGuid = c.SourceAdapterInstanceGuid
                }).ToList(),
                enabledConfigurations = enabledConfigs.Select(c => new
                {
                    interfaceName = c.InterfaceName,
                    sourceAdapterName = c.SourceAdapterName,
                    sourceIsEnabled = c.SourceIsEnabled
                }).ToList()
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestConfigLoading");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message, stackTrace = ex.StackTrace }));
            return errorResponse;
        }
    }
}

#pragma warning restore CS0618 // Type or member is obsolete

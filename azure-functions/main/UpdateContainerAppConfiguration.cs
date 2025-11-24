using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP Function to update container app configuration when adapter instance settings change
/// </summary>
public class UpdateContainerAppConfiguration
{
    private readonly IContainerAppService _containerAppService;
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateContainerAppConfiguration> _logger;

    public UpdateContainerAppConfiguration(
        IContainerAppService containerAppService,
        IInterfaceConfigurationService configService,
        ILogger<UpdateContainerAppConfiguration> logger)
    {
        _containerAppService = containerAppService ?? throw new ArgumentNullException(nameof(containerAppService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateContainerAppConfiguration")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "UpdateContainerAppConfiguration")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateContainerAppConfiguration function triggered");

        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<UpdateContainerAppConfigurationRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || !request.AdapterInstanceGuid.HasValue || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "AdapterInstanceGuid and InterfaceName are required" }));
                return badRequestResponse;
            }

            // Get full adapter instance configuration
            var config = await _configService.GetConfigurationAsync(request.InterfaceName, executionContext.CancellationToken);
            if (config == null)
            {
                var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                notFoundResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(notFoundResponse);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = $"Interface '{request.InterfaceName}' not found" }));
                return notFoundResponse;
            }

            // Find the adapter instance (source or destination)
            object? adapterInstance = null;
            if (request.AdapterType?.Equals("Source", StringComparison.OrdinalIgnoreCase) == true)
            {
                adapterInstance = config.Sources.Values.FirstOrDefault(s => s.AdapterInstanceGuid == request.AdapterInstanceGuid.Value);
            }
            else
            {
                adapterInstance = config.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == request.AdapterInstanceGuid.Value);
            }

            if (adapterInstance == null)
            {
                var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                notFoundResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(notFoundResponse);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = $"Adapter instance '{request.AdapterInstanceGuid}' not found" }));
                return notFoundResponse;
            }

            // Update container app configuration
            await _containerAppService.UpdateContainerAppConfigurationAsync(
                request.AdapterInstanceGuid.Value,
                adapterInstance,
                executionContext.CancellationToken);

            _logger.LogInformation(
                "Container app configuration updated: Guid={Guid}",
                request.AdapterInstanceGuid);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { success = true }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating container app configuration");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateContainerAppConfigurationRequest
    {
        public Guid? AdapterInstanceGuid { get; set; }
        public string InterfaceName { get; set; } = string.Empty;
        public string? AdapterType { get; set; }
    }
}



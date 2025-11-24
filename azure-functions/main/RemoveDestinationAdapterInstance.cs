using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class RemoveDestinationAdapterInstance
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IContainerAppService _containerAppService;
    private readonly ILogger<RemoveDestinationAdapterInstance> _logger;

    public RemoveDestinationAdapterInstance(
        IInterfaceConfigurationService configService,
        IContainerAppService containerAppService,
        ILogger<RemoveDestinationAdapterInstance> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _containerAppService = containerAppService ?? throw new ArgumentNullException(nameof(containerAppService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("RemoveDestinationAdapterInstance")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", "options", Route = "RemoveDestinationAdapterInstance")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("RemoveDestinationAdapterInstance function triggered");

        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            // Parse query parameters instead of body for DELETE request
            var uri = new Uri(req.Url.ToString());
            var queryString = uri.Query;
            if (queryString.StartsWith("?"))
            {
                queryString = queryString.Substring(1);
            }
            var pairs = queryString.Split('&');
            string? interfaceName = null;
            string? adapterInstanceGuidStr = null;
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split('=');
                if (keyValue.Length == 2)
                {
                    var key = Uri.UnescapeDataString(keyValue[0]);
                    var value = Uri.UnescapeDataString(keyValue[1]);
                    if (key.Equals("interfaceName", StringComparison.OrdinalIgnoreCase))
                    {
                        interfaceName = value;
                    }
                    else if (key.Equals("adapterInstanceGuid", StringComparison.OrdinalIgnoreCase))
                    {
                        adapterInstanceGuidStr = value;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(interfaceName) || string.IsNullOrWhiteSpace(adapterInstanceGuidStr))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName and AdapterInstanceGuid query parameters are required" }));
                return badRequestResponse;
            }

            if (!Guid.TryParse(adapterInstanceGuidStr, out var adapterInstanceGuid))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Invalid AdapterInstanceGuid format" }));
                return badRequestResponse;
            }

            await _configService.RemoveDestinationAdapterInstanceAsync(
                interfaceName,
                adapterInstanceGuid,
                executionContext.CancellationToken);

            _logger.LogInformation("Removed destination adapter instance '{AdapterInstanceGuid}' from interface '{InterfaceName}'", 
                adapterInstanceGuid, interfaceName);

            // Delete container app for this adapter instance (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Deleting container app for destination adapter instance {Guid}", adapterInstanceGuid);
                    await _containerAppService.DeleteContainerAppAsync(adapterInstanceGuid, executionContext.CancellationToken);
                    _logger.LogInformation("Container app deleted successfully for adapter instance {Guid}", adapterInstanceGuid);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting container app for destination adapter instance {Guid}", adapterInstanceGuid);
                }
            }, CancellationToken.None);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Destination adapter instance '{adapterInstanceGuid}' removed successfully. Container app deletion initiated.",
                interfaceName = interfaceName,
                adapterInstanceGuid = adapterInstanceGuid
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing destination adapter instance");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}





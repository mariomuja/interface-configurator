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
    private readonly ILogger<RemoveDestinationAdapterInstance> _logger;

    public RemoveDestinationAdapterInstance(
        IInterfaceConfigurationService configService,
        ILogger<RemoveDestinationAdapterInstance> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
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
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var interfaceName = queryParams["interfaceName"];
            var adapterInstanceGuidStr = queryParams["adapterInstanceGuid"];

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

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Destination adapter instance '{adapterInstanceGuid}' removed successfully",
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





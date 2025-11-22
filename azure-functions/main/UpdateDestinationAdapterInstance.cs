using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateDestinationAdapterInstance
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateDestinationAdapterInstance> _logger;

    public UpdateDestinationAdapterInstance(
        IInterfaceConfigurationService configService,
        ILogger<UpdateDestinationAdapterInstance> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateDestinationAdapterInstance")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "options", Route = "UpdateDestinationAdapterInstance")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateDestinationAdapterInstance function triggered");

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
            var request = JsonSerializer.Deserialize<UpdateDestinationAdapterInstanceRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName) || request.AdapterInstanceGuid == Guid.Empty)
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName and AdapterInstanceGuid are required" }));
                return badRequestResponse;
            }

            await _configService.UpdateDestinationAdapterInstanceAsync(
                request.InterfaceName,
                request.AdapterInstanceGuid,
                request.InstanceName,
                request.IsEnabled,
                request.Configuration,
                executionContext.CancellationToken);

            _logger.LogInformation("Updated destination adapter instance '{AdapterInstanceGuid}' in interface '{InterfaceName}'", 
                request.AdapterInstanceGuid, request.InterfaceName);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Destination adapter instance '{request.AdapterInstanceGuid}' updated successfully",
                interfaceName = request.InterfaceName,
                adapterInstanceGuid = request.AdapterInstanceGuid
            }));
            return response;
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Destination adapter instance not found");
            var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            notFoundResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(notFoundResponse);
            await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return notFoundResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating destination adapter instance");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateDestinationAdapterInstanceRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public Guid AdapterInstanceGuid { get; set; }
        public string? InstanceName { get; set; }
        public bool? IsEnabled { get; set; }
        public string? Configuration { get; set; }
    }
}







using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;

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
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "RemoveDestinationAdapterInstance")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("RemoveDestinationAdapterInstance function triggered");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<RemoveDestinationAdapterInstanceRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName) || request.AdapterInstanceGuid == Guid.Empty)
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName and AdapterInstanceGuid are required" }));
                return badRequestResponse;
            }

            await _configService.RemoveDestinationAdapterInstanceAsync(
                request.InterfaceName,
                request.AdapterInstanceGuid,
                executionContext.CancellationToken);

            _logger.LogInformation("Removed destination adapter instance '{AdapterInstanceGuid}' from interface '{InterfaceName}'", 
                request.AdapterInstanceGuid, request.InterfaceName);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Destination adapter instance '{request.AdapterInstanceGuid}' removed successfully",
                interfaceName = request.InterfaceName,
                adapterInstanceGuid = request.AdapterInstanceGuid
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing destination adapter instance");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class RemoveDestinationAdapterInstanceRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public Guid AdapterInstanceGuid { get; set; }
    }
}





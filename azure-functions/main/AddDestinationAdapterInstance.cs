using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;

namespace ProcessCsvBlobTrigger;

public class AddDestinationAdapterInstance
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<AddDestinationAdapterInstance> _logger;

    public AddDestinationAdapterInstance(
        IInterfaceConfigurationService configService,
        ILogger<AddDestinationAdapterInstance> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("AddDestinationAdapterInstance")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "AddDestinationAdapterInstance")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("AddDestinationAdapterInstance function triggered");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<AddDestinationAdapterInstanceRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName) || string.IsNullOrWhiteSpace(request.AdapterName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName and AdapterName are required" }));
                return badRequestResponse;
            }

            var instance = await _configService.AddDestinationAdapterInstanceAsync(
                request.InterfaceName,
                request.AdapterName,
                request.InstanceName ?? string.Empty,
                request.Configuration ?? "{}",
                executionContext.CancellationToken);

            _logger.LogInformation("Added destination adapter instance '{InstanceName}' ({AdapterName}) to interface '{InterfaceName}'", 
                instance.InstanceName, instance.AdapterName, request.InterfaceName);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(instance));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding destination adapter instance");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class AddDestinationAdapterInstanceRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string AdapterName { get; set; } = string.Empty;
        public string? InstanceName { get; set; }
        public string? Configuration { get; set; }
    }
}





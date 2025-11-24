using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class AddDestinationAdapterInstance
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IContainerAppService _containerAppService;
    private readonly ILogger<AddDestinationAdapterInstance> _logger;

    public AddDestinationAdapterInstance(
        IInterfaceConfigurationService configService,
        IContainerAppService containerAppService,
        ILogger<AddDestinationAdapterInstance> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _containerAppService = containerAppService ?? throw new ArgumentNullException(nameof(containerAppService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("AddDestinationAdapterInstance")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "AddDestinationAdapterInstance")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("AddDestinationAdapterInstance function triggered");

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
            var request = JsonSerializer.Deserialize<AddDestinationAdapterInstanceRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName) || string.IsNullOrWhiteSpace(request.AdapterName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
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

            // Create container app for this adapter instance (fire and forget)
            if (instance.AdapterInstanceGuid.HasValue)
            {
                // Get full configuration to pass to container app
                var config = await _configService.GetConfigurationAsync(request.InterfaceName, executionContext.CancellationToken);
                var fullInstance = config?.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == instance.AdapterInstanceGuid.Value);
                
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Creating container app for destination adapter instance {Guid}", instance.AdapterInstanceGuid);
                        var containerAppInfo = await _containerAppService.CreateContainerAppAsync(
                            instance.AdapterInstanceGuid.Value,
                            instance.AdapterName,
                            "Destination",
                            request.InterfaceName,
                            instance.InstanceName,
                            fullInstance ?? instance, // Pass full instance configuration
                            executionContext.CancellationToken);
                        _logger.LogInformation("Container app created successfully: {Name}", containerAppInfo.ContainerAppName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating container app for destination adapter instance {Guid}", instance.AdapterInstanceGuid);
                    }
                }, CancellationToken.None);
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(instance));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding destination adapter instance");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
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





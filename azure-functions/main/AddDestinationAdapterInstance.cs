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
    private readonly IServiceBusSubscriptionService _subscriptionService;
    private readonly ILogger<AddDestinationAdapterInstance> _logger;

    public AddDestinationAdapterInstance(
        IInterfaceConfigurationService configService,
        IContainerAppService containerAppService,
        IServiceBusSubscriptionService subscriptionService,
        ILogger<AddDestinationAdapterInstance> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _containerAppService = containerAppService ?? throw new ArgumentNullException(nameof(containerAppService));
        _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
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

            // Create Service Bus subscription for this destination adapter instance
            if (instance.AdapterInstanceGuid != Guid.Empty)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("Creating Service Bus subscription for destination adapter instance {Guid}", instance.AdapterInstanceGuid);
                        await _subscriptionService.CreateSubscriptionAsync(
                            request.InterfaceName,
                            instance.AdapterInstanceGuid,
                            executionContext.CancellationToken);
                        _logger.LogInformation("Service Bus subscription created successfully for adapter instance {Guid}", instance.AdapterInstanceGuid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating Service Bus subscription for destination adapter instance {Guid}", instance.AdapterInstanceGuid);
                    }
                }, CancellationToken.None);
            }

            // Create container app for this adapter instance
            // IMPORTANT: Each adapter instance gets its own isolated container app
            // Container app name is derived from adapterInstanceGuid: ca-{guid}
            // This ensures complete process isolation between adapter instances
            string? containerAppStatus = null;
            string? containerAppError = null;
            string? containerAppName = null;
            
            if (instance.AdapterInstanceGuid != Guid.Empty)
            {
                try
                {
                    // Check if container app already exists (should not happen for new instances)
                    var exists = await _containerAppService.ContainerAppExistsAsync(
                        instance.AdapterInstanceGuid,
                        executionContext.CancellationToken);
                    
                    if (!exists)
                    {
                        // Get full configuration to pass to container app
                        var config = await _configService.GetConfigurationAsync(request.InterfaceName, executionContext.CancellationToken);
                        var fullInstance = config?.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == instance.AdapterInstanceGuid);
                        
                        _logger.LogInformation(
                            "Creating isolated container app for destination adapter instance {Guid} (InstanceName={InstanceName}, AdapterName={AdapterName})",
                            instance.AdapterInstanceGuid, instance.InstanceName, instance.AdapterName);
                        
                        var containerAppInfo = await _containerAppService.CreateContainerAppAsync(
                            instance.AdapterInstanceGuid,
                            instance.AdapterName,
                            "Destination",
                            request.InterfaceName,
                            instance.InstanceName,
                            fullInstance ?? instance, // Pass full instance configuration with ALL settings
                            executionContext.CancellationToken);
                        
                        containerAppStatus = "Created";
                        containerAppName = containerAppInfo.ContainerAppName;
                        _logger.LogInformation(
                            "Container app created successfully for destination adapter instance {Guid}: ContainerApp={ContainerAppName}",
                            instance.AdapterInstanceGuid, containerAppName);
                    }
                    else
                    {
                        containerAppStatus = "AlreadyExists";
                        containerAppName = _containerAppService.GetContainerAppName(instance.AdapterInstanceGuid);
                        _logger.LogWarning(
                            "Container app already exists for destination adapter instance {Guid}: ContainerApp={ContainerAppName}",
                            instance.AdapterInstanceGuid, containerAppName);
                    }
                }
                catch (Exception ex)
                {
                    containerAppStatus = "Error";
                    containerAppError = ex.Message;
                    _logger.LogError(ex, "Error creating container app for destination adapter instance {Guid}", instance.AdapterInstanceGuid);
                    // Don't fail the entire operation - instance is created even if container app creation fails
                }
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            
            // Include container app creation status in response
            var responseData = new Dictionary<string, object>
            {
                ["adapterInstanceGuid"] = instance.AdapterInstanceGuid,
                ["instanceName"] = instance.InstanceName,
                ["adapterName"] = instance.AdapterName,
                ["isEnabled"] = instance.IsEnabled,
                ["configuration"] = instance.Configuration,
                ["containerAppStatus"] = containerAppStatus ?? "NotCreated",
                ["containerAppName"] = containerAppName ?? "",
                ["containerAppError"] = containerAppError ?? ""
            };
            
            await response.WriteStringAsync(JsonSerializer.Serialize(responseData));
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





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
    private readonly IContainerAppService _containerAppService;
    private readonly ILogger<UpdateDestinationAdapterInstance> _logger;

    public UpdateDestinationAdapterInstance(
        IInterfaceConfigurationService configService,
        IContainerAppService containerAppService,
        ILogger<UpdateDestinationAdapterInstance> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _containerAppService = containerAppService ?? throw new ArgumentNullException(nameof(containerAppService));
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

            // Update container app configuration synchronously with comprehensive error handling
            string? containerAppStatus = null;
            string? containerAppError = null;
            string? containerAppName = null;
            bool containerAppExists = false;
            try
            {
                // Check if container app exists first
                containerAppExists = await _containerAppService.ContainerAppExistsAsync(
                    request.AdapterInstanceGuid,
                    executionContext.CancellationToken);

                if (containerAppExists)
                {
                    var config = await _configService.GetConfigurationAsync(request.InterfaceName, executionContext.CancellationToken);
                    var updatedInstance = config?.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == request.AdapterInstanceGuid);
                    if (updatedInstance != null)
                    {
                        // Update container app configuration with ALL settings from the adapter instance
                        await _containerAppService.UpdateContainerAppConfigurationAsync(
                            request.AdapterInstanceGuid,
                            updatedInstance, // Pass the complete adapter instance with all properties
                            executionContext.CancellationToken);
                        
                        containerAppStatus = "Updated";
                        containerAppName = _containerAppService.GetContainerAppName(request.AdapterInstanceGuid);
                        _logger.LogInformation(
                            "Container app configuration updated successfully for adapter instance {Guid}, ContainerApp={ContainerAppName}",
                            request.AdapterInstanceGuid, containerAppName);
                    }
                    else
                    {
                        containerAppStatus = "Skipped";
                        containerAppError = "Adapter instance not found in configuration after update";
                        _logger.LogWarning("Adapter instance {Guid} not found in configuration after update", request.AdapterInstanceGuid);
                    }
                }
                else
                {
                    // Container app doesn't exist yet - try to create it
                    _logger.LogInformation("Container app does not exist for adapter instance {Guid}, attempting to create it", request.AdapterInstanceGuid);
                    try
                    {
                        var config = await _configService.GetConfigurationAsync(request.InterfaceName, executionContext.CancellationToken);
                        var instance = config?.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == request.AdapterInstanceGuid);
                        if (instance != null)
                        {
                            var containerAppInfo = await _containerAppService.CreateContainerAppAsync(
                                request.AdapterInstanceGuid,
                                instance.AdapterName,
                                "Destination",
                                request.InterfaceName,
                                instance.InstanceName,
                                instance, // Pass complete instance configuration
                                executionContext.CancellationToken);
                            
                            containerAppStatus = "Created";
                            containerAppName = containerAppInfo.ContainerAppName;
                            _logger.LogInformation(
                                "Container app created successfully for adapter instance {Guid}, ContainerApp={ContainerAppName}",
                                request.AdapterInstanceGuid, containerAppName);
                        }
                        else
                        {
                            containerAppStatus = "NotCreated";
                            containerAppError = "Adapter instance not found - cannot create container app";
                            _logger.LogWarning("Adapter instance {Guid} not found - cannot create container app", request.AdapterInstanceGuid);
                        }
                    }
                    catch (Exception createEx)
                    {
                        containerAppStatus = "CreateError";
                        containerAppError = $"Failed to create container app: {createEx.Message}";
                        _logger.LogError(createEx, "Error creating container app for adapter instance {Guid}", request.AdapterInstanceGuid);
                    }
                }
            }
            catch (Exception ex)
            {
                containerAppStatus = "Error";
                containerAppError = $"Error updating container app: {ex.Message}";
                _logger.LogError(ex, "Error updating container app configuration for adapter instance {Guid}", request.AdapterInstanceGuid);
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Destination adapter instance '{request.AdapterInstanceGuid}' updated successfully.",
                interfaceName = request.InterfaceName,
                adapterInstanceGuid = request.AdapterInstanceGuid,
                containerAppStatus = containerAppStatus,
                containerAppError = containerAppError,
                containerAppName = containerAppName,
                containerAppExists = containerAppExists
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







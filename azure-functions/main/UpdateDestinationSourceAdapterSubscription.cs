using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main;

public class UpdateDestinationSourceAdapterSubscription
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IAdapterSubscriptionService _subscriptionService;
    private readonly ILogger<UpdateDestinationSourceAdapterSubscription> _logger;

    public UpdateDestinationSourceAdapterSubscription(
        IInterfaceConfigurationService configService,
        IAdapterSubscriptionService subscriptionService,
        ILogger<UpdateDestinationSourceAdapterSubscription> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateDestinationSourceAdapterSubscription")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "UpdateDestinationSourceAdapterSubscription")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateDestinationSourceAdapterSubscription function triggered");

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
            var request = JsonSerializer.Deserialize<UpdateDestinationSourceAdapterSubscriptionRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName) || string.IsNullOrWhiteSpace(request.AdapterInstanceGuid))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName and AdapterInstanceGuid are required" }));
                return badRequestResponse;
            }

            var destinationInstanceGuid = Guid.Parse(request.AdapterInstanceGuid);
            
            // Update the property in the configuration
            await _configService.UpdateDestinationSourceAdapterSubscriptionAsync(
                request.InterfaceName,
                destinationInstanceGuid,
                string.IsNullOrWhiteSpace(request.SourceAdapterSubscription) ? null : Guid.Parse(request.SourceAdapterSubscription),
                executionContext.CancellationToken);

            // Create or update subscription in MessageBox if source adapter is specified
            if (!string.IsNullOrWhiteSpace(request.SourceAdapterSubscription))
            {
                var sourceAdapterGuid = Guid.Parse(request.SourceAdapterSubscription);
                
                // Get the destination adapter instance to find its adapter name
                var config = await _configService.GetConfigurationAsync(request.InterfaceName, executionContext.CancellationToken);
                if (config != null)
                {
                    // Try to find destination instance in Destinations dictionary
                    DestinationAdapterInstance? destInstance = null;
                    if (config.Destinations != null)
                    {
                        destInstance = config.Destinations.Values.FirstOrDefault(d => d.AdapterInstanceGuid == destinationInstanceGuid);
                    }
                    
                    // Fallback to legacy DestinationAdapterInstances list
                    if (destInstance == null && config.DestinationAdapterInstances != null)
                    {
                        destInstance = config.DestinationAdapterInstances.FirstOrDefault(d => d.AdapterInstanceGuid == destinationInstanceGuid);
                    }
                    
                    if (destInstance != null)
                    {
                        // Create subscription for this destination adapter to receive messages from the source adapter
                        // Build filter criteria to only receive messages from the specified source adapter
                        var filterCriteria = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            SourceAdapterInstanceGuid = sourceAdapterGuid.ToString(),
                            InterfaceName = request.InterfaceName
                        });
                        
                        await _subscriptionService.CreateOrUpdateSubscriptionAsync(
                            destinationInstanceGuid,
                            request.InterfaceName,
                            destInstance.AdapterName,
                            filterCriteria: filterCriteria,
                            cancellationToken: executionContext.CancellationToken);
                        
                        _logger.LogInformation("Created subscription for destination adapter '{DestinationAdapterGuid}' to receive messages from source adapter '{SourceAdapterGuid}' in interface '{InterfaceName}'",
                            destinationInstanceGuid, sourceAdapterGuid, request.InterfaceName);
                    }
                    else
                    {
                        _logger.LogWarning("Destination adapter instance '{DestinationAdapterGuid}' not found in interface '{InterfaceName}'", 
                            destinationInstanceGuid, request.InterfaceName);
                    }
                }
            }
            else
            {
                // If SourceAdapterSubscription is cleared, disable the subscription
                var existingSubscriptions = await _subscriptionService.GetSubscriptionsForAdapterAsync(destinationInstanceGuid, executionContext.CancellationToken);
                foreach (var subscription in existingSubscriptions.Where(s => s.InterfaceName == request.InterfaceName))
                {
                    await _subscriptionService.EnableSubscriptionAsync(subscription.Id, false, executionContext.CancellationToken);
                    _logger.LogInformation("Disabled subscription for destination adapter '{DestinationAdapterGuid}' in interface '{InterfaceName}'",
                        destinationInstanceGuid, request.InterfaceName);
                }
            }

            _logger.LogInformation("Source Adapter Subscription for destination adapter '{AdapterInstanceGuid}' in interface '{InterfaceName}' updated to '{SourceAdapterSubscription}'", 
                request.AdapterInstanceGuid, request.InterfaceName, request.SourceAdapterSubscription ?? "null");

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Source Adapter Subscription for destination adapter '{request.AdapterInstanceGuid}' in interface '{request.InterfaceName}' updated",
                interfaceName = request.InterfaceName,
                adapterInstanceGuid = request.AdapterInstanceGuid,
                sourceAdapterSubscription = request.SourceAdapterSubscription
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Source Adapter Subscription");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateDestinationSourceAdapterSubscriptionRequest
    {
        public string? InterfaceName { get; set; }
        public string? AdapterInstanceGuid { get; set; }
        public string? SourceAdapterSubscription { get; set; }
    }
}


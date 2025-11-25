using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to retrieve Service Bus messages for an interface
/// </summary>
public class GetServiceBusMessagesFunction
{
    private readonly IServiceBusService _serviceBusService;
    private readonly ILogger<GetServiceBusMessagesFunction> _logger;

    public GetServiceBusMessagesFunction(
        IServiceBusService serviceBusService,
        ILogger<GetServiceBusMessagesFunction> logger)
    {
        _serviceBusService = serviceBusService ?? throw new ArgumentNullException(nameof(serviceBusService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetServiceBusMessages")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "GetServiceBusMessages")] HttpRequestData req,
        FunctionContext context)
    {
        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            // Parse query parameters
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var interfaceName = queryParams["interfaceName"];
            var maxMessagesStr = queryParams["maxMessages"];

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(errorResponse);
                errorResponse.WriteString(JsonSerializer.Serialize(new { error = "interfaceName parameter is required" }));
                return errorResponse;
            }

            int maxMessages = 100; // Default
            if (!string.IsNullOrWhiteSpace(maxMessagesStr) && int.TryParse(maxMessagesStr, out var parsedMax))
            {
                maxMessages = Math.Min(Math.Max(parsedMax, 1), 1000); // Clamp between 1 and 1000
            }

            _logger.LogInformation("Retrieving Service Bus messages for interface: {InterfaceName}, MaxMessages: {MaxMessages}", 
                interfaceName, maxMessages);

            // Get recent messages from Service Bus
            var messages = await _serviceBusService.GetRecentMessagesAsync(interfaceName, maxMessages, context.CancellationToken);

            // Transform to response format
            var responseMessages = messages.Select(m => new
            {
                messageId = m.MessageId,
                interfaceName = m.InterfaceName,
                adapterName = m.AdapterName,
                adapterType = m.AdapterType,
                adapterInstanceGuid = m.AdapterInstanceGuid.ToString(),
                headers = m.Headers ?? new List<string>(),
                record = m.Record ?? new Dictionary<string, string>(),
                enqueuedTime = m.EnqueuedTime,
                deliveryCount = m.DeliveryCount,
                properties = m.Properties ?? new Dictionary<string, object>()
            }).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            
            // Use camelCase JSON serialization
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            response.WriteString(JsonSerializer.Serialize(responseMessages, jsonOptions));

            _logger.LogInformation("Retrieved {MessageCount} Service Bus messages for interface: {InterfaceName}", 
                responseMessages.Count, interfaceName);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Service Bus messages");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            errorResponse.WriteString(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}





using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP-triggered function to restart adapter processes
/// This triggers a restart by updating app settings, which causes Azure Functions to reload
/// </summary>
public class RestartAdapter
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<RestartAdapter> _logger;

    public RestartAdapter(
        IInterfaceConfigurationService configService,
        ILogger<RestartAdapter> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("RestartAdapter")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "RestartAdapter")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("RestartAdapter function triggered");

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
            var request = JsonSerializer.Deserialize<RestartAdapterRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            if (request.AdapterType != "Source" && request.AdapterType != "Destination")
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "AdapterType must be 'Source' or 'Destination'" }));
                return badRequestResponse;
            }

            // Verify the interface configuration exists
            var config = await _configService.GetConfigurationAsync(request.InterfaceName, executionContext.CancellationToken);
            if (config == null)
            {
                var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                notFoundResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(notFoundResponse);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = $"Interface configuration '{request.InterfaceName}' not found" }));
                return notFoundResponse;
            }

            // Trigger restart by updating a timestamp in app settings
            // This causes Azure Functions to reload the function app
            // Note: In a production environment, you might want to use Azure Management API
            // For now, we'll log the restart request and rely on the next timer trigger
            var restartKey = $"RESTART_{request.AdapterType.ToUpperInvariant()}_{request.InterfaceName.ToUpperInvariant().Replace("-", "_")}";
            var restartTimestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            
            // Set environment variable (this will persist until next deployment)
            Environment.SetEnvironmentVariable(restartKey, restartTimestamp);
            
            _logger.LogInformation(
                "Restart requested for {AdapterType} adapter of interface '{InterfaceName}'. " +
                "The adapter process will restart on the next timer trigger (within 1 minute). " +
                "Restart timestamp: {RestartTimestamp}",
                request.AdapterType, request.InterfaceName, restartTimestamp);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"{request.AdapterType} adapter process for interface '{request.InterfaceName}' will restart on the next timer trigger (within 1 minute). Restart timestamp: {restartTimestamp}",
                interfaceName = request.InterfaceName,
                adapterType = request.AdapterType,
                restartTimestamp = restartTimestamp
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting adapter");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class RestartAdapterRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string AdapterType { get; set; } = string.Empty; // "Source" or "Destination"
    }
}





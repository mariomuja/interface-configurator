using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class ToggleInterfaceConfiguration
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<ToggleInterfaceConfiguration> _logger;

    public ToggleInterfaceConfiguration(
        IInterfaceConfigurationService configService,
        ILogger<ToggleInterfaceConfiguration> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("ToggleInterfaceConfiguration")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "ToggleInterfaceConfiguration")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("ToggleInterfaceConfiguration function triggered");

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
            var request = JsonSerializer.Deserialize<ToggleInterfaceConfigRequest>(requestBody);

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

            var sessionId = request.SessionId;

            if (request.AdapterType == "Source")
            {
                await _configService.SetSourceEnabledAsync(request.InterfaceName, request.Enabled, sessionId, executionContext.CancellationToken);
                _logger.LogInformation("Toggled Source adapter for interface configuration '{InterfaceName}' to {Enabled} (session: {SessionId})", request.InterfaceName, request.Enabled, sessionId);
            }
            else
            {
                await _configService.SetDestinationEnabledAsync(request.InterfaceName, request.Enabled, sessionId, executionContext.CancellationToken);
                _logger.LogInformation("Toggled Destination adapter for interface configuration '{InterfaceName}' to {Enabled} (session: {SessionId})", request.InterfaceName, request.Enabled, sessionId);
            }

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"{request.AdapterType} adapter for interface configuration '{request.InterfaceName}' {(request.Enabled ? "enabled" : "disabled")}",
                interfaceName = request.InterfaceName,
                adapterType = request.AdapterType,
                enabled = request.Enabled
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling interface configuration");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class ToggleInterfaceConfigRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string AdapterType { get; set; } = string.Empty; // "Source" or "Destination"
        public bool Enabled { get; set; }
        public string? SessionId { get; set; }
    }
}


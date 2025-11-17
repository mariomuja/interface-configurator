using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;

namespace ProcessCsvBlobTrigger;

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
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("ToggleInterfaceConfiguration function triggered");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<ToggleInterfaceConfigRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            await _configService.SetEnabledAsync(request.InterfaceName, request.Enabled, executionContext.CancellationToken);

            _logger.LogInformation("Toggled interface configuration '{InterfaceName}' to {Enabled}", request.InterfaceName, request.Enabled);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Interface configuration '{request.InterfaceName}' {(request.Enabled ? "enabled" : "disabled")}",
                interfaceName = request.InterfaceName,
                enabled = request.Enabled
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling interface configuration");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class ToggleInterfaceConfigRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public bool Enabled { get; set; }
    }
}


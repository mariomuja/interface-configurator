using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;

namespace ProcessCsvBlobTrigger;

public class UpdateInstanceName
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateInstanceName> _logger;

    public UpdateInstanceName(
        IInterfaceConfigurationService configService,
        ILogger<UpdateInstanceName> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateInstanceName")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateInstanceName function triggered");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<UpdateInstanceNameRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            if (request.InstanceType != "Source" && request.InstanceType != "Destination")
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InstanceType must be 'Source' or 'Destination'" }));
                return badRequestResponse;
            }

            // Get existing configuration
            var config = await _configService.GetConfigurationAsync(request.InterfaceName, executionContext.CancellationToken);
            if (config == null)
            {
                var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                notFoundResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = $"Interface configuration '{request.InterfaceName}' not found" }));
                return notFoundResponse;
            }

            // Update instance name
            if (request.InstanceType == "Source")
            {
                config.SourceInstanceName = request.InstanceName ?? "Source";
            }
            else
            {
                config.DestinationInstanceName = request.InstanceName ?? "Destination";
            }

            config.UpdatedAt = DateTime.UtcNow;
            await _configService.SaveConfigurationAsync(config, executionContext.CancellationToken);

            _logger.LogInformation("Updated {InstanceType} instance name for interface '{InterfaceName}' to '{InstanceName}'", 
                request.InstanceType, request.InterfaceName, request.InstanceName);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(config));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating instance name");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateInstanceNameRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string InstanceType { get; set; } = string.Empty; // "Source" or "Destination"
        public string? InstanceName { get; set; }
    }
}





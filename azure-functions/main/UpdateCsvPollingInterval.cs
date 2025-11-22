using System.Text.Json;
using System.IO;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateCsvPollingInterval
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateCsvPollingInterval> _logger;

    public UpdateCsvPollingInterval(
        IInterfaceConfigurationService configService,
        ILogger<UpdateCsvPollingInterval> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateCsvPollingInterval")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "options", Route = "UpdateCsvPollingInterval")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateCsvPollingInterval function triggered");

        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<UpdateCsvPollingIntervalRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequest = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequest.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequest);
                await badRequest.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequest;
            }

            var interval = request.PollingInterval > 0 ? request.PollingInterval : 10;

            await _configService.UpdateCsvPollingIntervalAsync(
                request.InterfaceName,
                interval,
                executionContext.CancellationToken);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                message = $"CSV polling interval for interface '{request.InterfaceName}' updated to {interval} seconds.",
                interfaceName = request.InterfaceName,
                pollingInterval = interval
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating CSV polling interval");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateCsvPollingIntervalRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public int PollingInterval { get; set; } = 10;
    }
}



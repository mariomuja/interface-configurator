using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateSqlPollingProperties
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateSqlPollingProperties> _logger;

    public UpdateSqlPollingProperties(
        IInterfaceConfigurationService configService,
        ILogger<UpdateSqlPollingProperties> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateSqlPollingProperties")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "options", Route = "UpdateSqlPollingProperties")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateSqlPollingProperties function triggered");

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
            var request = JsonSerializer.Deserialize<UpdateSqlPollingPropertiesRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            await _configService.UpdateSqlPollingPropertiesAsync(
                request.InterfaceName,
                request.PollingStatement,
                request.PollingInterval,
                executionContext.CancellationToken);

            _logger.LogInformation("SQL polling properties for interface '{InterfaceName}' updated", request.InterfaceName);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"SQL polling properties for interface '{request.InterfaceName}' updated successfully",
                interfaceName = request.InterfaceName
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SQL polling properties");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateSqlPollingPropertiesRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string? PollingStatement { get; set; }
        public int? PollingInterval { get; set; }
    }
}





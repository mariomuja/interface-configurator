using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateFieldSeparator
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateFieldSeparator> _logger;

    public UpdateFieldSeparator(
        IInterfaceConfigurationService configService,
        ILogger<UpdateFieldSeparator> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateFieldSeparator")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "options", Route = "UpdateFieldSeparator")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateFieldSeparator function triggered");

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
            var request = JsonSerializer.Deserialize<UpdateFieldSeparatorRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            await _configService.UpdateFieldSeparatorAsync(
                request.InterfaceName, 
                request.FieldSeparator ?? "║", 
                executionContext.CancellationToken);

            _logger.LogInformation("Field separator for interface '{InterfaceName}' updated to '{FieldSeparator}'", request.InterfaceName, request.FieldSeparator ?? "║");

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Field separator for interface '{request.InterfaceName}' updated to '{request.FieldSeparator ?? "║"}'",
                interfaceName = request.InterfaceName,
                fieldSeparator = request.FieldSeparator ?? "║"
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating field separator");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateFieldSeparatorRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string? FieldSeparator { get; set; }
    }
}





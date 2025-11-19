using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateFileMask
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateFileMask> _logger;

    public UpdateFileMask(
        IInterfaceConfigurationService configService,
        ILogger<UpdateFileMask> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateFileMask")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "options", Route = "UpdateFileMask")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateFileMask function triggered");

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
            var request = JsonSerializer.Deserialize<UpdateFileMaskRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            await _configService.UpdateFileMaskAsync(
                request.InterfaceName, 
                request.FileMask ?? "*.txt", 
                executionContext.CancellationToken);

            _logger.LogInformation("File mask for interface '{InterfaceName}' updated to '{FileMask}'", request.InterfaceName, request.FileMask ?? "*.txt");

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"File mask for interface '{request.InterfaceName}' updated to '{request.FileMask ?? "*.txt"}'",
                interfaceName = request.InterfaceName,
                fileMask = request.FileMask ?? "*.txt"
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating file mask");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateFileMaskRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string? FileMask { get; set; }
        public string? SessionId { get; set; }
    }
}


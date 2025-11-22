using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateDestinationFileMask
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateDestinationFileMask> _logger;

    public UpdateDestinationFileMask(
        IInterfaceConfigurationService configService,
        ILogger<UpdateDestinationFileMask> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateDestinationFileMask")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "options", Route = "UpdateDestinationFileMask")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateDestinationFileMask function triggered");

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
            var request = JsonSerializer.Deserialize<UpdateDestinationFileMaskRequest>(requestBody, new JsonSerializerOptions
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

            await _configService.UpdateDestinationFileMaskAsync(
                request.InterfaceName, 
                request.DestinationFileMask ?? "*.txt", 
                executionContext.CancellationToken);

            _logger.LogInformation("Destination file mask for interface '{InterfaceName}' updated to '{DestinationFileMask}'", request.InterfaceName, request.DestinationFileMask ?? "*.txt");

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Destination file mask for interface '{request.InterfaceName}' updated to '{request.DestinationFileMask ?? "*.txt"}'",
                interfaceName = request.InterfaceName,
                destinationFileMask = request.DestinationFileMask ?? "*.txt"
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating destination file mask");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateDestinationFileMaskRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string? DestinationFileMask { get; set; }
    }
}





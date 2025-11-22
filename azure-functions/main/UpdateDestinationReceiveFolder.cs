using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateDestinationReceiveFolder
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateDestinationReceiveFolder> _logger;

    public UpdateDestinationReceiveFolder(
        IInterfaceConfigurationService configService,
        ILogger<UpdateDestinationReceiveFolder> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateDestinationReceiveFolder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "options", Route = "UpdateDestinationReceiveFolder")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateDestinationReceiveFolder function triggered");

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
            var request = JsonSerializer.Deserialize<UpdateDestinationReceiveFolderRequest>(requestBody, new JsonSerializerOptions
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

            await _configService.UpdateDestinationReceiveFolderAsync(
                request.InterfaceName, 
                request.DestinationReceiveFolder ?? string.Empty, 
                executionContext.CancellationToken);

            _logger.LogInformation("Destination receive folder for interface '{InterfaceName}' updated to '{DestinationReceiveFolder}'", request.InterfaceName, request.DestinationReceiveFolder ?? "null");

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Destination receive folder for interface '{request.InterfaceName}' updated",
                interfaceName = request.InterfaceName,
                destinationReceiveFolder = request.DestinationReceiveFolder
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating destination receive folder");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateDestinationReceiveFolderRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string? DestinationReceiveFolder { get; set; }
    }
}





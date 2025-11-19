using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateInterfaceName
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateInterfaceName> _logger;

    public UpdateInterfaceName(
        IInterfaceConfigurationService configService,
        ILogger<UpdateInterfaceName> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateInterfaceName")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "UpdateInterfaceName")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateInterfaceName function triggered");

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
            var request = JsonSerializer.Deserialize<UpdateInterfaceNameRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.OldInterfaceName) || string.IsNullOrWhiteSpace(request.NewInterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "OldInterfaceName and NewInterfaceName are required" }));
                return badRequestResponse;
            }

            // Get existing configuration
            var config = await _configService.GetConfigurationAsync(request.OldInterfaceName, executionContext.CancellationToken);
            if (config == null)
            {
                var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                notFoundResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(notFoundResponse);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = $"Interface configuration '{request.OldInterfaceName}' not found" }));
                return notFoundResponse;
            }

            // Check if new name already exists
            var existingWithNewName = await _configService.GetConfigurationAsync(request.NewInterfaceName, executionContext.CancellationToken);
            if (existingWithNewName != null && existingWithNewName.InterfaceName != request.OldInterfaceName)
            {
                var conflictResponse = req.CreateResponse(System.Net.HttpStatusCode.Conflict);
                conflictResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(conflictResponse);
                await conflictResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = $"Interface configuration '{request.NewInterfaceName}' already exists" }));
                return conflictResponse;
            }

            // Update interface name using service method
            await _configService.UpdateInterfaceNameAsync(request.OldInterfaceName, request.NewInterfaceName, executionContext.CancellationToken);

            // Get updated configuration
            config = await _configService.GetConfigurationAsync(request.NewInterfaceName, executionContext.CancellationToken);

            _logger.LogInformation("Updated interface name from '{OldName}' to '{NewName}'", request.OldInterfaceName, request.NewInterfaceName);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(config));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating interface name");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateInterfaceNameRequest
    {
        public string OldInterfaceName { get; set; } = string.Empty;
        public string NewInterfaceName { get; set; } = string.Empty;
    }
}





using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class DeleteInterfaceConfiguration
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<DeleteInterfaceConfiguration> _logger;

    public DeleteInterfaceConfiguration(
        IInterfaceConfigurationService configService,
        ILogger<DeleteInterfaceConfiguration> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("DeleteInterfaceConfiguration")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", "options", Route = "DeleteInterfaceConfiguration")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("DeleteInterfaceConfiguration function triggered");

        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var interfaceName = queryParams["interfaceName"];

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "interfaceName query parameter is required" }));
                return badRequestResponse;
            }

            var existing = await _configService.GetConfigurationAsync(interfaceName, executionContext.CancellationToken);
            if (existing == null)
            {
                var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                notFoundResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(notFoundResponse);
                await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = $"Interface configuration '{interfaceName}' not found" }));
                return notFoundResponse;
            }

            await _configService.DeleteConfigurationAsync(interfaceName, executionContext.CancellationToken);

            _logger.LogInformation("Deleted interface configuration: {InterfaceName}", interfaceName);

            var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            successResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(successResponse);
            await successResponse.WriteStringAsync(JsonSerializer.Serialize(new { message = $"Interface configuration '{interfaceName}' deleted successfully" }));
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting interface configuration");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


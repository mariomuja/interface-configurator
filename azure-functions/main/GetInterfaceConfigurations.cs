using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class GetInterfaceConfigurations
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<GetInterfaceConfigurations> _logger;

    public GetInterfaceConfigurations(
        IInterfaceConfigurationService configService,
        ILogger<GetInterfaceConfigurations> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetInterfaceConfigurations")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "GetInterfaceConfigurations")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("GetInterfaceConfigurations function triggered");

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
            var sessionId = queryParams["sessionId"];

            var configurations = await _configService.GetAllConfigurationsAsync(sessionId, executionContext.CancellationToken);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            var jsonResponse = JsonSerializer.Serialize(configurations);
            await response.WriteStringAsync(jsonResponse);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting interface configurations: {Message}", ex.Message);
            _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
            
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            
            var errorDetails = new
            {
                error = new
                {
                    code = "500",
                    message = "A server error has occurred",
                    details = ex.Message,
                    type = ex.GetType().Name
                }
            };
            
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(errorDetails));
            return errorResponse;
        }
    }
}





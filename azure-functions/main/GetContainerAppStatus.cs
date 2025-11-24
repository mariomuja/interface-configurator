using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP Function to get container app status for an adapter instance
/// </summary>
public class GetContainerAppStatus
{
    private readonly IContainerAppService _containerAppService;
    private readonly ILogger<GetContainerAppStatus> _logger;

    public GetContainerAppStatus(
        IContainerAppService containerAppService,
        ILogger<GetContainerAppStatus> logger)
    {
        _containerAppService = containerAppService ?? throw new ArgumentNullException(nameof(containerAppService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetContainerAppStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "GetContainerAppStatus")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("GetContainerAppStatus function triggered");

        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var guidParam = query["adapterInstanceGuid"];
            
            // Alternative parsing if System.Web.HttpUtility is not available
            if (string.IsNullOrWhiteSpace(guidParam))
            {
                var uri = new Uri(req.Url.ToString());
                var queryString = uri.Query;
                if (queryString.StartsWith("?"))
                {
                    queryString = queryString.Substring(1);
                }
                var pairs = queryString.Split('&');
                foreach (var pair in pairs)
                {
                    var keyValue = pair.Split('=');
                    if (keyValue.Length == 2 && keyValue[0].Equals("adapterInstanceGuid", StringComparison.OrdinalIgnoreCase))
                    {
                        guidParam = Uri.UnescapeDataString(keyValue[1]);
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(guidParam) || !Guid.TryParse(guidParam, out var adapterInstanceGuid))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "adapterInstanceGuid query parameter is required and must be a valid GUID" }));
                return badRequestResponse;
            }

            var status = await _containerAppService.GetContainerAppStatusAsync(
                adapterInstanceGuid,
                executionContext.CancellationToken);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(status));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting container app status");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


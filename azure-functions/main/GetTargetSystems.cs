using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main;

/// <summary>
/// Azure Function to get available target systems and their endpoints
/// Used by the frontend to populate dropdowns in the adapter settings dialog
/// </summary>
public class GetTargetSystems
{
    private readonly ILogger<GetTargetSystems> _logger;

    public GetTargetSystems(ILogger<GetTargetSystems> logger)
    {
        _logger = logger;
    }

    [Function("GetTargetSystems")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetTargetSystems")] HttpRequestData req)
    {
        _logger.LogInformation("GetTargetSystems function called");

        try
        {
            var targetSystems = TargetSystemService.GetAvailableTargetSystems();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.WriteString(JsonSerializer.Serialize(targetSystems, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting target systems");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            errorResponse.WriteString(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


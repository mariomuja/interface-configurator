using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;

namespace ProcessCsvBlobTrigger;

public class GetDestinationAdapterInstances
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<GetDestinationAdapterInstances> _logger;

    public GetDestinationAdapterInstances(
        IInterfaceConfigurationService configService,
        ILogger<GetDestinationAdapterInstances> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetDestinationAdapterInstances")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetDestinationAdapterInstances")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("GetDestinationAdapterInstances function triggered");

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var interfaceName = queryParams["interfaceName"];

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "interfaceName query parameter is required" }));
                return badRequestResponse;
            }

            var instances = await _configService.GetDestinationAdapterInstancesAsync(interfaceName, executionContext.CancellationToken);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(instances));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting destination adapter instances");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}





using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP Function to delete a container app for an adapter instance
/// </summary>
public class DeleteContainerApp
{
    private readonly IContainerAppService _containerAppService;
    private readonly ILogger<DeleteContainerApp> _logger;

    public DeleteContainerApp(
        IContainerAppService containerAppService,
        ILogger<DeleteContainerApp> logger)
    {
        _containerAppService = containerAppService ?? throw new ArgumentNullException(nameof(containerAppService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("DeleteContainerApp")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "DeleteContainerApp")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("DeleteContainerApp function triggered");

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
            var request = JsonSerializer.Deserialize<DeleteContainerAppRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || !request.AdapterInstanceGuid.HasValue)
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "AdapterInstanceGuid is required" }));
                return badRequestResponse;
            }

            await _containerAppService.DeleteContainerAppAsync(
                request.AdapterInstanceGuid.Value,
                executionContext.CancellationToken);

            _logger.LogInformation(
                "Container app deletion initiated: Guid={Guid}",
                request.AdapterInstanceGuid);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { success = true }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting container app");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class DeleteContainerAppRequest
    {
        public Guid? AdapterInstanceGuid { get; set; }
    }
}



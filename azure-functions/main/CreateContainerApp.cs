using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP Function to create a container app for an adapter instance
/// </summary>
public class CreateContainerApp
{
    private readonly IContainerAppService _containerAppService;
    private readonly ILogger<CreateContainerApp> _logger;

    public CreateContainerApp(
        IContainerAppService containerAppService,
        ILogger<CreateContainerApp> logger)
    {
        _containerAppService = containerAppService ?? throw new ArgumentNullException(nameof(containerAppService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("CreateContainerApp")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "CreateContainerApp")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("CreateContainerApp function triggered");

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
            var request = JsonSerializer.Deserialize<CreateContainerAppRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || !request.AdapterInstanceGuid.HasValue || string.IsNullOrWhiteSpace(request.AdapterName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "AdapterInstanceGuid and AdapterName are required" }));
                return badRequestResponse;
            }

            var containerAppInfo = await _containerAppService.CreateContainerAppAsync(
                request.AdapterInstanceGuid.Value,
                request.AdapterName,
                request.AdapterType ?? "Source",
                request.InterfaceName ?? string.Empty,
                request.InstanceName ?? string.Empty,
                executionContext.CancellationToken);

            _logger.LogInformation(
                "Container app created: Name={Name}, Guid={Guid}",
                containerAppInfo.ContainerAppName, request.AdapterInstanceGuid);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(containerAppInfo));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating container app");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class CreateContainerAppRequest
    {
        public Guid? AdapterInstanceGuid { get; set; }
        public string AdapterName { get; set; } = string.Empty;
        public string? AdapterType { get; set; }
        public string? InterfaceName { get; set; }
        public string? InstanceName { get; set; }
    }
}



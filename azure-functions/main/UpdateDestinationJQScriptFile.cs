using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateDestinationJQScriptFile
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateDestinationJQScriptFile> _logger;

    public UpdateDestinationJQScriptFile(
        IInterfaceConfigurationService configService,
        ILogger<UpdateDestinationJQScriptFile> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateDestinationJQScriptFile")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "UpdateDestinationJQScriptFile")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateDestinationJQScriptFile function triggered");

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
            var request = JsonSerializer.Deserialize<UpdateDestinationJQScriptFileRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName) || string.IsNullOrWhiteSpace(request.AdapterInstanceGuid))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName and AdapterInstanceGuid are required" }));
                return badRequestResponse;
            }

            await _configService.UpdateDestinationJQScriptFileAsync(
                request.InterfaceName,
                Guid.Parse(request.AdapterInstanceGuid),
                request.JQScriptFile ?? string.Empty,
                executionContext.CancellationToken);

            _logger.LogInformation("JQ Script File for destination adapter '{AdapterInstanceGuid}' in interface '{InterfaceName}' updated to '{JQScriptFile}'", 
                request.AdapterInstanceGuid, request.InterfaceName, request.JQScriptFile ?? "null");

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"JQ Script File for destination adapter '{request.AdapterInstanceGuid}' in interface '{request.InterfaceName}' updated",
                interfaceName = request.InterfaceName,
                adapterInstanceGuid = request.AdapterInstanceGuid,
                jqScriptFile = request.JQScriptFile
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating JQ Script File");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateDestinationJQScriptFileRequest
    {
        public string? InterfaceName { get; set; }
        public string? AdapterInstanceGuid { get; set; }
        public string? JQScriptFile { get; set; }
    }
}



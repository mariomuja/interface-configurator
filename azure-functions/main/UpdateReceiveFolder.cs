using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;

namespace InterfaceConfigurator.Main;

public class UpdateReceiveFolder
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateReceiveFolder> _logger;

    public UpdateReceiveFolder(
        IInterfaceConfigurationService configService,
        ILogger<UpdateReceiveFolder> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateReceiveFolder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateReceiveFolder function triggered");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<UpdateReceiveFolderRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            await _configService.UpdateReceiveFolderAsync(request.InterfaceName, request.ReceiveFolder ?? string.Empty, executionContext.CancellationToken);

            _logger.LogInformation("Receive folder for interface '{InterfaceName}' updated to '{ReceiveFolder}'", request.InterfaceName, request.ReceiveFolder);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Receive folder for interface '{request.InterfaceName}' updated to '{request.ReceiveFolder}'",
                interfaceName = request.InterfaceName,
                receiveFolder = request.ReceiveFolder
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating receive folder");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateReceiveFolderRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string? ReceiveFolder { get; set; }
    }
}





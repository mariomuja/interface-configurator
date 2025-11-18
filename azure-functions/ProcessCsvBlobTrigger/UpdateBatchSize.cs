using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;

namespace ProcessCsvBlobTrigger;

public class UpdateBatchSize
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateBatchSize> _logger;

    public UpdateBatchSize(
        IInterfaceConfigurationService configService,
        ILogger<UpdateBatchSize> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateBatchSize")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "UpdateBatchSize")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateBatchSize function triggered");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<UpdateBatchSizeRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            if (request.BatchSize <= 0)
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "BatchSize must be greater than 0" }));
                return badRequestResponse;
            }

            await _configService.UpdateBatchSizeAsync(
                request.InterfaceName, 
                request.BatchSize, 
                executionContext.CancellationToken);

            _logger.LogInformation("Batch size for interface '{InterfaceName}' updated to {BatchSize}", request.InterfaceName, request.BatchSize);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"Batch size for interface '{request.InterfaceName}' updated to {request.BatchSize}",
                interfaceName = request.InterfaceName,
                batchSize = request.BatchSize
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating batch size");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateBatchSizeRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public int BatchSize { get; set; }
    }
}





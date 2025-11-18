using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;

namespace ProcessCsvBlobTrigger;

public class UpdateSqlTransactionProperties
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateSqlTransactionProperties> _logger;

    public UpdateSqlTransactionProperties(
        IInterfaceConfigurationService configService,
        ILogger<UpdateSqlTransactionProperties> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateSqlTransactionProperties")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "UpdateSqlTransactionProperties")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateSqlTransactionProperties function triggered");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<UpdateSqlTransactionPropertiesRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            await _configService.UpdateSqlTransactionPropertiesAsync(
                request.InterfaceName,
                request.UseTransaction,
                request.BatchSize,
                executionContext.CancellationToken);

            _logger.LogInformation("SQL transaction properties for interface '{InterfaceName}' updated: UseTransaction={UseTransaction}, BatchSize={BatchSize}", 
                request.InterfaceName, request.UseTransaction ?? false, request.BatchSize ?? 1000);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"SQL transaction properties for interface '{request.InterfaceName}' updated",
                interfaceName = request.InterfaceName,
                useTransaction = request.UseTransaction,
                batchSize = request.BatchSize
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SQL transaction properties");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateSqlTransactionPropertiesRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public bool? UseTransaction { get; set; }
        public int? BatchSize { get; set; }
    }
}





using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;

namespace ProcessCsvBlobTrigger;

/// <summary>
/// HTTP endpoint to clear process logs from in-memory store
/// </summary>
public class ClearProcessLogsFunction
{
    private readonly IInMemoryLoggingService _loggingService;
    private readonly ILogger<ClearProcessLogsFunction> _logger;

    public ClearProcessLogsFunction(
        IInMemoryLoggingService loggingService,
        ILogger<ClearProcessLogsFunction> logger)
    {
        _loggingService = loggingService;
        _logger = logger;
    }

    [Function("ClearProcessLogs")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ClearProcessLogs")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            _loggingService.ClearLogs();
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.WriteString(System.Text.Json.JsonSerializer.Serialize(new { message = "Logs cleared successfully" }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing process logs");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            errorResponse.WriteString(System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


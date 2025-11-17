using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;

namespace ProcessCsvBlobTrigger;

/// <summary>
/// HTTP endpoint to retrieve process logs from in-memory store
/// </summary>
public class GetProcessLogsFunction
{
    private readonly IInMemoryLoggingService _loggingService;
    private readonly ILogger<GetProcessLogsFunction> _logger;

    public GetProcessLogsFunction(
        IInMemoryLoggingService loggingService,
        ILogger<GetProcessLogsFunction> logger)
    {
        _loggingService = loggingService;
        _logger = logger;
    }

    [Function("GetProcessLogs")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetProcessLogs")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            var logs = _loggingService.GetAllLogs();
            
            // Order by datetime_created descending (newest first)
            var orderedLogs = logs.OrderByDescending(l => l.datetime_created).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.WriteString(System.Text.Json.JsonSerializer.Serialize(orderedLogs));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process logs");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            errorResponse.WriteString(System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


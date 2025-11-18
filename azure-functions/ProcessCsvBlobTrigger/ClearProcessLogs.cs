using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Data;

namespace ProcessCsvBlobTrigger;

/// <summary>
/// HTTP endpoint to clear process logs from MessageBox database
/// </summary>
public class ClearProcessLogsFunction
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<ClearProcessLogsFunction> _logger;

    public ClearProcessLogsFunction(
        MessageBoxDbContext context,
        ILogger<ClearProcessLogsFunction> logger)
    {
        _context = context;
        _logger = logger;
    }

    [Function("ClearProcessLogs")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ClearProcessLogs")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            // Delete all logs from MessageBox database
            var logs = await _context.ProcessLogs.ToListAsync();
            _context.ProcessLogs.RemoveRange(logs);
            await _context.SaveChangesAsync();
            
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


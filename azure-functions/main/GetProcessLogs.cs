using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to retrieve process logs from MessageBox database
/// </summary>
public class GetProcessLogsFunction
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<GetProcessLogsFunction> _logger;

    public GetProcessLogsFunction(
        MessageBoxDbContext context,
        ILogger<GetProcessLogsFunction> logger)
    {
        _context = context;
        _logger = logger;
    }

    [Function("GetProcessLogs")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "GetProcessLogs")] HttpRequestData req,
        FunctionContext context)
    {
        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            // Ensure database and tables exist before querying
            try
            {
                var created = await _context.Database.EnsureCreatedAsync(context.CancellationToken);
                if (created)
                {
                    _logger.LogInformation("MessageBox database and tables created automatically. Tables: Messages, MessageSubscriptions, ProcessLogs");
                }
            }
            catch (Exception ensureEx)
            {
                _logger.LogWarning(ensureEx, "Could not ensure ProcessLogs table exists. Will attempt query anyway.");
            }

            var logs = await _context.ProcessLogs
                .OrderByDescending(l => l.datetime_created)
                .Select(l => new
                {
                    id = l.Id,
                    datetime_created = l.datetime_created,
                    level = l.Level,
                    message = l.Message,
                    details = l.Details,
                    component = l.Component,
                    interfaceName = l.InterfaceName,
                    messageId = l.MessageId
                })
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            
            // Use camelCase JSON serialization
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            response.WriteString(System.Text.Json.JsonSerializer.Serialize(logs, jsonOptions));

            return response;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Handle SQL errors gracefully - if table doesn't exist, return empty array
            if (sqlEx.Number == 208 || sqlEx.Message.Contains("Invalid object name") || sqlEx.Message.Contains("does not exist"))
            {
                _logger.LogWarning("ProcessLogs table does not exist yet. Returning empty array. Tables will be created automatically.");
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(response);
                response.WriteString("[]");
                return response;
            }
            
            _logger.LogError(sqlEx, "SQL error retrieving process logs: {ErrorNumber} - {Message}", sqlEx.Number, sqlEx.Message);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            errorResponse.WriteString(System.Text.Json.JsonSerializer.Serialize(new { error = sqlEx.Message }));
            return errorResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process logs");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            errorResponse.WriteString(System.Text.Json.JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


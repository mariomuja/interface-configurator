using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Data;
using Microsoft.EntityFrameworkCore;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class CheckMessageBox
{
    private readonly MessageBoxDbContext _messageBoxContext;
    private readonly ILogger<CheckMessageBox> _logger;

    public CheckMessageBox(
        MessageBoxDbContext messageBoxContext,
        ILogger<CheckMessageBox> logger)
    {
        _messageBoxContext = messageBoxContext ?? throw new ArgumentNullException(nameof(messageBoxContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("CheckMessageBox")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "CheckMessageBox")] HttpRequestData req,
        FunctionContext executionContext)
    {
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var interfaceName = queryParams["interfaceName"];

            var result = new
            {
                totalMessages = await _messageBoxContext.Messages.CountAsync(),
                messagesByInterface = await _messageBoxContext.Messages
                    .GroupBy(m => m.InterfaceName)
                    .Select(g => new { interfaceName = g.Key, count = g.Count() })
                    .ToListAsync(),
                messagesByStatus = await _messageBoxContext.Messages
                    .GroupBy(m => m.Status)
                    .Select(g => new { status = g.Key, count = g.Count() })
                    .ToListAsync(),
                messagesByAdapter = await _messageBoxContext.Messages
                    .GroupBy(m => new { m.InterfaceName, m.AdapterInstanceGuid, m.AdapterName })
                    .Select(g => new 
                    { 
                        interfaceName = g.Key.InterfaceName, 
                        adapterInstanceGuid = g.Key.AdapterInstanceGuid,
                        adapterName = g.Key.AdapterName,
                        count = g.Count() 
                    })
                    .ToListAsync(),
                recentMessages = await _messageBoxContext.Messages
                    .OrderByDescending(m => m.datetime_created)
                    .Take(10)
                    .Select(m => new
                    {
                        messageId = m.MessageId,
                        interfaceName = m.InterfaceName,
                        adapterName = m.AdapterName,
                        adapterType = m.AdapterType,
                        adapterInstanceGuid = m.AdapterInstanceGuid,
                        status = m.Status,
                        datetimeCreated = m.datetime_created,
                        datetimeProcessed = m.datetime_processed
                    })
                    .ToListAsync(),
                subscriptions = await _messageBoxContext.MessageSubscriptions
                    .GroupBy(s => s.Status)
                    .Select(g => new { status = g.Key, count = g.Count() })
                    .ToListAsync()
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking MessageBox");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


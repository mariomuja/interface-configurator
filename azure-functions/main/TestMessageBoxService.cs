using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;
using InterfaceConfigurator.Main.Data;
using Microsoft.EntityFrameworkCore;

namespace InterfaceConfigurator.Main;

/// <summary>
/// Test endpoint to verify MessageBoxService availability
/// </summary>
public class TestMessageBoxService
{
    private readonly IMessageBoxService? _messageBoxService;
    private readonly MessageBoxDbContext? _messageBoxContext;
    private readonly ILogger<TestMessageBoxService> _logger;

    public TestMessageBoxService(
        IMessageBoxService? messageBoxService,
        MessageBoxDbContext? messageBoxContext,
        ILogger<TestMessageBoxService> logger)
    {
        _messageBoxService = messageBoxService;
        _messageBoxContext = messageBoxContext;
        _logger = logger;
    }

    [Function("TestMessageBoxService")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "test-messagebox-service")] HttpRequestData req,
        FunctionContext context)
    {
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            _logger.LogInformation("=== TESTING MESSAGEBOX SERVICE AVAILABILITY ===");
            
            var result = new
            {
                messageBoxServiceAvailable = _messageBoxService != null,
                messageBoxContextAvailable = _messageBoxContext != null,
                messageBoxContextCanConnect = false,
                connectionString = "Not available",
                error = (string?)null
            };

            if (_messageBoxContext != null)
            {
                try
                {
                    var canConnect = await _messageBoxContext.Database.CanConnectAsync(context.CancellationToken);
                    result = new
                    {
                        messageBoxServiceAvailable = _messageBoxService != null,
                        messageBoxContextAvailable = true,
                        messageBoxContextCanConnect = canConnect,
                        connectionString = _messageBoxContext.Database.GetConnectionString()?.Substring(0, Math.Min(100, _messageBoxContext.Database.GetConnectionString()?.Length ?? 0)) ?? "Not available",
                        error = (string?)null
                    };
                }
                catch (Exception ex)
                {
                    result = new
                    {
                        messageBoxServiceAvailable = _messageBoxService != null,
                        messageBoxContextAvailable = true,
                        messageBoxContextCanConnect = false,
                        connectionString = "Error getting connection string",
                        error = ex.Message
                    };
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestMessageBoxService");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message, stackTrace = ex.StackTrace }));
            return errorResponse;
        }
    }
}


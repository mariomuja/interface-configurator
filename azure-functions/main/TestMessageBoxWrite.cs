using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// Test endpoint to verify MessageBox write functionality
/// </summary>
public class TestMessageBoxWrite
{
    private readonly IMessageBoxService? _messageBoxService;
    private readonly ILogger<TestMessageBoxWrite> _logger;

    public TestMessageBoxWrite(
        IMessageBoxService? messageBoxService,
        ILogger<TestMessageBoxWrite> logger)
    {
        _messageBoxService = messageBoxService;
        _logger = logger;
    }

    [Function("TestMessageBoxWrite")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "options", Route = "test-messagebox-write")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("TestMessageBoxWrite endpoint called");

        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            if (_messageBoxService == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.ServiceUnavailable, 
                    "MessageBoxService is not available");
            }

            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var interfaceName = queryParams["interfaceName"] ?? "TestInterface";
            var adapterInstanceGuidValue = queryParams["adapterInstanceGuid"];
            
            if (string.IsNullOrWhiteSpace(adapterInstanceGuidValue) || !Guid.TryParse(adapterInstanceGuidValue, out var adapterInstanceGuid))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                    "adapterInstanceGuid query parameter is required and must be a valid GUID");
            }

            // Create test data
            var headers = new List<string> { "Column1", "Column2", "Column3" };
            var testRecord = new Dictionary<string, string>
            {
                { "Column1", "TestValue1" },
                { "Column2", "TestValue2" },
                { "Column3", "TestValue3" }
            };

            _logger.LogInformation("Attempting to write test message: Interface={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}",
                interfaceName, adapterInstanceGuid);

            // Write test message
            var messageId = await _messageBoxService.WriteSingleRecordMessageAsync(
                interfaceName,
                "CSV",
                "Source",
                adapterInstanceGuid,
                headers,
                testRecord,
                executionContext.CancellationToken);

            _logger.LogInformation("Successfully wrote test message: MessageId={MessageId}", messageId);

            var result = new
            {
                success = true,
                messageId = messageId,
                interfaceName = interfaceName,
                adapterInstanceGuid = adapterInstanceGuid,
                message = "Test message written successfully"
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(result));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestMessageBoxWrite: {ErrorMessage}", ex.Message);
            
            var errorResult = new
            {
                success = false,
                error = ex.Message,
                errorType = ex.GetType().Name,
                stackTrace = ex.StackTrace
            };

            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(errorResult));

            return response;
        }
    }

    private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        CorsHelper.AddCorsHeaders(response);
        
        var errorResult = new { success = false, error = message };
        await response.WriteStringAsync(JsonSerializer.Serialize(errorResult));
        
        return response;
    }
}


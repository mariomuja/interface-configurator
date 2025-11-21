using System.Linq;
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class GetMessageBoxMessages
{
    private readonly IMessageBoxService _messageBoxService;
    private readonly ILogger<GetMessageBoxMessages> _logger;

    public GetMessageBoxMessages(
        IMessageBoxService messageBoxService,
        ILogger<GetMessageBoxMessages> logger)
    {
        _messageBoxService = messageBoxService ?? throw new ArgumentNullException(nameof(messageBoxService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetMessageBoxMessages")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "GetMessageBoxMessages")] HttpRequestData req,
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
            var adapterInstanceGuidValue = queryParams["adapterInstanceGuid"];
            var adapterType = queryParams["adapterType"];
            var status = queryParams["status"]; // Optional status filter

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, "interfaceName query parameter is required");
            }

            List<MessageBoxMessage> messages;
            
            // If adapterInstanceGuid is provided, filter by adapter instance
            // Otherwise, load all messages for the interface
            if (!string.IsNullOrWhiteSpace(adapterInstanceGuidValue) && Guid.TryParse(adapterInstanceGuidValue, out var adapterInstanceGuid))
            {
                messages = await _messageBoxService.ReadMessagesByAdapterAsync(
                    interfaceName,
                    adapterInstanceGuid,
                    adapterType,
                    executionContext.CancellationToken);
            }
            else
            {
                // Load all messages for the interface (useful for UI display)
                messages = await _messageBoxService.ReadMessagesAsync(
                    interfaceName,
                    status,
                    executionContext.CancellationToken);
            }
            
            // Apply status filter if provided and not already filtered by adapter
            if (!string.IsNullOrWhiteSpace(status) && string.IsNullOrWhiteSpace(adapterInstanceGuidValue))
            {
                messages = messages.Where(m => m.Status == status).ToList();
            }

            var results = messages.Select(m =>
            {
                List<string>? headers = null;
                Dictionary<string, string>? record = null;
                try
                {
                    var payload = JsonSerializer.Deserialize<MessagePayload>(m.MessageData);
                    headers = payload?.headers;
                    record = payload?.record;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse MessageData for MessageId={MessageId}", m.MessageId);
                }

                return new
                {
                    messageId = m.MessageId,
                    interfaceName = m.InterfaceName,
                    adapterName = m.AdapterName,
                    adapterType = m.AdapterType,
                    adapterInstanceGuid = m.AdapterInstanceGuid,
                    status = m.Status,
                    datetimeCreated = m.datetime_created,
                    datetimeProcessed = m.datetime_processed,
                    errorMessage = m.ErrorMessage,
                    retryCount = m.RetryCount,
                    headers = headers ?? new List<string>(),
                    record = record ?? new Dictionary<string, string>(),
                    rawMessageData = m.MessageData
                };
            });

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(results, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving message box messages");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var errorResponse = req.CreateResponse(statusCode);
        errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
        CorsHelper.AddCorsHeaders(errorResponse);
        await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = message }));
        return errorResponse;
    }

    private class MessagePayload
    {
        public List<string>? headers { get; set; }
        public Dictionary<string, string>? record { get; set; }
    }
}



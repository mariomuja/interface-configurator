using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main.Helpers;

public static class ErrorResponseHelper
{
    public static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message,
        Exception? exception = null,
        ILogger? logger = null)
    {
        logger?.LogError(exception, "Error: {Message}", message);

        var errorResponse = req.CreateResponse(statusCode);
        errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
        CorsHelper.AddCorsHeaders(errorResponse);

        var errorDetails = new
        {
            error = new
            {
                code = ((int)statusCode).ToString(),
                message = message,
                details = exception?.Message,
                type = exception?.GetType().Name,
                timestamp = DateTime.UtcNow
            }
        };

        await errorResponse.WriteStringAsync(JsonSerializer.Serialize(errorDetails));
        return errorResponse;
    }

    public static async Task<HttpResponseData> CreateValidationErrorResponse(
        HttpRequestData req,
        string field,
        string message)
    {
        return await CreateErrorResponse(
            req,
            HttpStatusCode.BadRequest,
            $"Validation failed for field '{field}': {message}");
    }
}




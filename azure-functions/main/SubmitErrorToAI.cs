using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Helpers;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Models;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to submit error reports - stores errors in ProcessLogs table
/// </summary>
public class SubmitErrorToAIFunction
{
    private readonly ILogger<SubmitErrorToAIFunction> _logger;
    private readonly InterfaceConfigDbContext _context;

    public SubmitErrorToAIFunction(
        ILogger<SubmitErrorToAIFunction> logger,
        InterfaceConfigDbContext context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    [Function("SubmitErrorToAI")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "SubmitErrorToAI")] HttpRequestData req,
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
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return await ErrorResponseHelper.CreateValidationErrorResponse(
                    req, "body", "Request body is required");
            }

            var errorReport = JsonSerializer.Deserialize<ErrorReport>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (errorReport == null)
            {
                return await ErrorResponseHelper.CreateValidationErrorResponse(
                    req, "body", "Invalid error report format");
            }

            // Extract error information
            var currentError = errorReport.CurrentError;
            var errorMessage = currentError?.Error?.Message ?? "Unknown error";
            var functionName = currentError?.FunctionName ?? "Unknown";
            var component = currentError?.Component ?? "Unknown";
            var stackTrace = currentError?.Stack ?? string.Empty;
            
            // Create error details JSON
            var errorDetails = JsonSerializer.Serialize(new
            {
                errorId = errorReport.ErrorId,
                functionName = functionName,
                component = component,
                errorMessage = errorMessage,
                stackTrace = stackTrace,
                timestamp = DateTime.UtcNow,
                applicationState = errorReport.ApplicationState,
                functionCallHistory = errorReport.FunctionCallHistory?.Count ?? 0
            }, new JsonSerializerOptions { WriteIndented = true });

            // Truncate if too long
            if (errorDetails.Length > 4000)
            {
                errorDetails = errorDetails.Substring(0, 4000) + "... [truncated]";
            }

            // Store error in ProcessLogs table
            var processLog = new InterfaceConfigurator.Main.Core.Models.ProcessLog
            {
                Timestamp = DateTime.UtcNow,
                Level = "Error",
                Message = $"Error Report: {errorMessage}",
                Details = errorDetails,
                Component = component.Length > 200 ? component.Substring(0, 200) : component,
                InterfaceName = null // Can be extracted from errorReport if available
            };

            // Truncate Message if too long
            if (processLog.Message.Length > 4000)
            {
                processLog.Message = processLog.Message.Substring(0, 4000) + "... [truncated]";
            }

            try
            {
                _context.ProcessLogs.Add(processLog);
                await _context.SaveChangesAsync(context.CancellationToken);
                
                _logger.LogInformation(
                    "Error report stored in ProcessLogs - ErrorId: {ErrorId}, Function: {FunctionName}, Component: {Component}",
                    errorReport.ErrorId,
                    functionName,
                    component);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store error report in ProcessLogs");
                // Continue anyway - we still want to return success
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            var responseBody = new
            {
                success = true,
                message = "Error report stored in ProcessLogs",
                errorId = errorReport.ErrorId,
                timestamp = DateTime.UtcNow
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(responseBody));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AI error submission");
            return await ErrorResponseHelper.CreateErrorResponse(
                req, HttpStatusCode.InternalServerError, "Failed to process error report", ex, _logger);
        }
    }

}


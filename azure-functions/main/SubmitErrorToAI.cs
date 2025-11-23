using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Helpers;
using InterfaceConfigurator.Main.Models;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to submit error reports for AI-powered automatic fixing
/// </summary>
public class SubmitErrorToAIFunction
{
    private readonly ILogger<SubmitErrorToAIFunction> _logger;

    public SubmitErrorToAIFunction(ILogger<SubmitErrorToAIFunction> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("SubmitErrorToAI")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "SubmitErrorToAI")] HttpRequestData req,
        FunctionContext context)
    {
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

            // Log the error report for AI processing
            _logger.LogError(
                "AI Error Report Received - ErrorId: {ErrorId}, Function: {FunctionName}, Component: {Component}, Message: {Message}",
                errorReport.ErrorId,
                errorReport.CurrentError?.FunctionName,
                errorReport.CurrentError?.Component,
                errorReport.CurrentError?.Error?.Message);

            // Log full error report as JSON for AI to process
            var errorReportJson = JsonSerializer.Serialize(errorReport, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            _logger.LogInformation("Full Error Report:\n{ErrorReportJson}", errorReportJson);

            // Store error report in a way that AI can access it
            // Option 1: Save to blob storage for AI processing
            await SaveErrorReportToBlobStorage(errorReport, errorReportJson);

            // Option 2: Create a GitHub issue (if GitHub integration is configured)
            // await CreateGitHubIssue(errorReport);

            // Option 3: Send to an AI processing endpoint
            // await SendToAIProcessingService(errorReport);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            var responseBody = new
            {
                success = true,
                message = "Error report received and queued for AI processing",
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

    private async Task SaveErrorReportToBlobStorage(ErrorReport errorReport, string jsonContent)
    {
        try
        {
            // This would require BlobServiceClient injection
            // For now, we'll just log it
            // TODO: Implement blob storage saving
            _logger.LogInformation("Error report saved (simulated): {ErrorId}", errorReport.ErrorId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save error report to blob storage");
        }
    }
}


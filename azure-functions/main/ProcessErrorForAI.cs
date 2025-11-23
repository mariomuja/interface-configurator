using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Helpers;
using InterfaceConfigurator.Main.Models;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to process error reports through AI pipeline: Analyze -> Fix -> Test
/// </summary>
public class ProcessErrorForAIFunction
{
    private readonly ILogger<ProcessErrorForAIFunction> _logger;
    private readonly ErrorAnalysisService _analysisService;
    private readonly AutoFixService _fixService;
    private readonly AutoTestService _testService;

    public ProcessErrorForAIFunction(
        ILogger<ProcessErrorForAIFunction> logger,
        ErrorAnalysisService analysisService,
        AutoFixService fixService,
        AutoTestService testService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
        _fixService = fixService ?? throw new ArgumentNullException(nameof(fixService));
        _testService = testService ?? throw new ArgumentNullException(nameof(testService));
    }

    [Function("ProcessErrorForAI")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ProcessErrorForAI")] HttpRequestData req,
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

            _logger.LogInformation("Processing error for AI: {ErrorId}", errorReport.ErrorId);

            // Step 1: Analyze error
            var analysisResult = _analysisService.AnalyzeError(errorReport);
            _logger.LogInformation(
                "Analysis completed: {ErrorId}, Files: {FileCount}, Fixes: {FixCount}",
                analysisResult.ErrorId,
                analysisResult.AffectedFiles.Count,
                analysisResult.SuggestedFixes.Count);

            // Step 2: Apply fixes
            FixApplicationResult? fixResult = null;
            if (analysisResult.SuggestedFixes.Count > 0)
            {
                fixResult = await _fixService.ApplyFixesAsync(analysisResult);
                _logger.LogInformation(
                    "Fixes applied: {ErrorId}, Success: {Success}, Applied: {Applied}, Failed: {Failed}",
                    analysisResult.ErrorId,
                    fixResult.Success,
                    fixResult.AppliedFixes.Count,
                    fixResult.FailedFixes.Count);

                // Step 3: Commit fixes
                if (fixResult.Success)
                {
                    var commitSuccess = await _fixService.CommitFixesAsync(fixResult, errorReport.ErrorId);
                    _logger.LogInformation(
                        "Fixes committed: {ErrorId}, Success: {Success}",
                        errorReport.ErrorId,
                        commitSuccess);
                }
            }

            // Step 4: Run tests
            TestResult? testResult = null;
            if (fixResult?.Success == true)
            {
                var affectedFiles = analysisResult.AffectedFiles.Select(f => f.FilePath).ToList();
                testResult = await _testService.RunTestsAsync(errorReport.ErrorId, affectedFiles);
                _logger.LogInformation(
                    "Tests completed: {ErrorId}, Success: {Success}",
                    errorReport.ErrorId,
                    testResult.OverallSuccess);
            }

            // Build response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);

            var responseBody = new
            {
                success = true,
                errorId = errorReport.ErrorId,
                analysis = new
                {
                    affectedFiles = analysisResult.AffectedFiles.Count,
                    suggestedFixes = analysisResult.SuggestedFixes.Count,
                    confidenceScore = analysisResult.ConfidenceScore,
                    rootCause = analysisResult.RootCause.Category
                },
                fixes = fixResult != null ? new
                {
                    applied = fixResult.AppliedFixes.Count,
                    failed = fixResult.FailedFixes.Count,
                    success = fixResult.Success
                } : null,
                tests = testResult != null ? new
                {
                    success = testResult.OverallSuccess,
                    summary = testResult.Summary,
                    testSuites = testResult.TestResults.Count
                } : null,
                timestamp = DateTime.UtcNow
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(responseBody, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing error for AI");
            return await ErrorResponseHelper.CreateErrorResponse(
                req, HttpStatusCode.InternalServerError, "Failed to process error for AI", ex, _logger);
        }
    }
}

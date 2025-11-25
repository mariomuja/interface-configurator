using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.IO;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP-triggered Azure Function to retrieve the latest test report
/// Returns test results in a Visual Studio-like format
/// </summary>
public class GetTestReport
{
    private readonly ILogger<GetTestReport>? _logger;

    public GetTestReport(ILogger<GetTestReport>? logger = null)
    {
        _logger = logger;
    }

    [Function("GetTestReport")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "test-report")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger?.LogInformation("GetTestReport function processed a request.");

        try
        {
            // Look for test results in TestResults directory
            var testResultsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "TestResults");
            if (!Directory.Exists(testResultsPath))
            {
                testResultsData = req.CreateResponse(HttpStatusCode.NotFound);
                await response.WriteStringAsync("Test results not found. Please run tests first.");
                return response;
            }

            // Find the latest TRX file
            var trxFiles = Directory.GetFiles(testResultsPath, "*.trx", SearchOption.AllDirectories)
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            if (trxFiles.Count == 0)
            {
                var response = req.CreateResponse(HttpStatusCode.NotFound);
                await response.WriteStringAsync("No test results found. Please run tests first.");
                return response;
            }

            var latestTrxFile = trxFiles.First();
            var trxContent = await File.ReadAllTextAsync(latestTrxFile);

            // Parse TRX file and create Visual Studio-like report
            var testReport = ParseTrxFile(trxContent, latestTrxFile);

            var jsonResponse = req.CreateResponse(HttpStatusCode.OK);
            jsonResponse.Headers.Add("Content-Type", "application/json");
            await jsonResponse.WriteStringAsync(JsonSerializer.Serialize(testReport, new JsonSerializerOptions { WriteIndented = true }));

            return jsonResponse;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error retrieving test report");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error retrieving test report: {ex.Message}");
            return errorResponse;
        }
    }

    private TestReport ParseTrxFile(string trxContent, string filePath)
    {
        // Parse TRX XML and extract test results
        // TRX format is XML-based
        var doc = new System.Xml.XmlDocument();
        doc.LoadXml(trxContent);

        var testReport = new TestReport
        {
            ReportPath = filePath,
            GeneratedAt = File.GetLastWriteTime(filePath),
            TotalTests = 0,
            PassedTests = 0,
            FailedTests = 0,
            SkippedTests = 0,
            TestResults = new List<TestResult>()
        };

        // Extract test results from TRX XML
        var testDefinitions = doc.SelectNodes("//TestDefinition");
        var unitTestResults = doc.SelectNodes("//UnitTestResult");

        if (unitTestResults != null)
        {
            foreach (System.Xml.XmlNode result in unitTestResults)
            {
                var testName = result.Attributes?["testName"]?.Value ?? "Unknown";
                var outcome = result.Attributes?["outcome"]?.Value ?? "Unknown";
                var duration = result.Attributes?["duration"]?.Value ?? "00:00:00";
                var startTime = result.Attributes?["startTime"]?.Value ?? DateTime.UtcNow.ToString();

                var testResult = new TestResult
                {
                    TestName = testName,
                    Outcome = outcome,
                    Duration = duration,
                    StartTime = DateTime.TryParse(startTime, out var parsedTime) ? parsedTime : DateTime.UtcNow,
                    ErrorMessage = result.SelectSingleNode(".//Message")?.InnerText,
                    StackTrace = result.SelectSingleNode(".//StackTrace")?.InnerText
                };

                testReport.TestResults.Add(testResult);

                testReport.TotalTests++;
                if (outcome == "Passed") testReport.PassedTests++;
                else if (outcome == "Failed") testReport.FailedTests++;
                else if (outcome == "NotExecuted") testReport.SkippedTests++;
            }
        }

        return testReport;
    }
}

public class TestReport
{
    public string ReportPath { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int SkippedTests { get; set; }
    public List<TestResult> TestResults { get; set; } = new();
    public double PassRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
}

public class TestResult
{
    public string TestName { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
}


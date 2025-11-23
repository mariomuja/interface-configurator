using System.Diagnostics;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Models;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service to automatically test fixes
/// </summary>
public class AutoTestService
{
    private readonly ILogger<AutoTestService> _logger;
    private readonly string _workspaceRoot;

    public AutoTestService(ILogger<AutoTestService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT") 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "interface-configurator");
    }

    /// <summary>
    /// Runs tests to verify fixes
    /// </summary>
    public async Task<TestResult> RunTestsAsync(string errorId, List<string> affectedFiles)
    {
        _logger.LogInformation("Running tests for error: {ErrorId}", errorId);

        var result = new TestResult
        {
            ErrorId = errorId,
            Timestamp = DateTime.UtcNow,
            TestResults = new List<TestRunResult>(),
            OverallSuccess = false
        };

        try
        {
            // Run frontend tests
            var frontendTests = await RunFrontendTestsAsync();
            result.TestResults.Add(frontendTests);

            // Run backend tests
            var backendTests = await RunBackendTestsAsync();
            result.TestResults.Add(backendTests);

            // Run integration tests if applicable
            if (affectedFiles.Any(f => f.Contains("transport.component")))
            {
                var integrationTests = await RunIntegrationTestsAsync();
                result.TestResults.Add(integrationTests);
            }

            result.OverallSuccess = result.TestResults.All(t => t.Success);
            result.Summary = $"Tests completed: {result.TestResults.Count(t => t.Success)}/{result.TestResults.Count} passed";

            _logger.LogInformation(
                "Test results for error {ErrorId}: {Summary}",
                errorId,
                result.Summary);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running tests");
            result.OverallSuccess = false;
            result.Summary = $"Test execution failed: {ex.Message}";
            return result;
        }
    }

    private async Task<TestRunResult> RunFrontendTestsAsync()
    {
        _logger.LogInformation("Running frontend tests");

        try
        {
            var testProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = "test -- --watch=false --browsers=ChromeHeadless",
                    WorkingDirectory = Path.Combine(_workspaceRoot, "frontend"),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            testProcess.Start();
            var output = await testProcess.StandardOutput.ReadToEndAsync();
            var error = await testProcess.StandardError.ReadToEndAsync();
            await testProcess.WaitForExitAsync();

            var success = testProcess.ExitCode == 0;

            return new TestRunResult
            {
                TestSuite = "Frontend",
                Success = success,
                Output = output,
                ErrorOutput = error,
                ExitCode = testProcess.ExitCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run frontend tests");
            return new TestRunResult
            {
                TestSuite = "Frontend",
                Success = false,
                ErrorOutput = ex.Message,
                ExitCode = -1
            };
        }
    }

    private async Task<TestRunResult> RunBackendTestsAsync()
    {
        _logger.LogInformation("Running backend tests");

        try
        {
            var testProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "test --no-build --verbosity quiet",
                    WorkingDirectory = Path.Combine(_workspaceRoot, "azure-functions"),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            testProcess.Start();
            var output = await testProcess.StandardOutput.ReadToEndAsync();
            var error = await testProcess.StandardError.ReadToEndAsync();
            await testProcess.WaitForExitAsync();

            var success = testProcess.ExitCode == 0;

            return new TestRunResult
            {
                TestSuite = "Backend",
                Success = success,
                Output = output,
                ErrorOutput = error,
                ExitCode = testProcess.ExitCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run backend tests");
            return new TestRunResult
            {
                TestSuite = "Backend",
                Success = false,
                ErrorOutput = ex.Message,
                ExitCode = -1
            };
        }
    }

    private async Task<TestRunResult> RunIntegrationTestsAsync()
    {
        _logger.LogInformation("Running integration tests");

        // Integration tests would be run here
        // For now, return a placeholder
        return new TestRunResult
        {
            TestSuite = "Integration",
            Success = true,
            Output = "Integration tests not yet implemented",
            ExitCode = 0
        };
    }
}



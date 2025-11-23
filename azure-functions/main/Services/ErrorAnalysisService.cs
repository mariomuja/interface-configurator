using System.Text.Json;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Models;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service to analyze error reports and identify affected code locations
/// </summary>
public class ErrorAnalysisService
{
    private readonly ILogger<ErrorAnalysisService> _logger;

    public ErrorAnalysisService(ILogger<ErrorAnalysisService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes an error report and identifies affected code locations
    /// </summary>
    public ErrorAnalysisResult AnalyzeError(ErrorReport errorReport)
    {
        _logger.LogInformation("Analyzing error report: {ErrorId}", errorReport.ErrorId);

        var result = new ErrorAnalysisResult
        {
            ErrorId = errorReport.ErrorId,
            AnalysisTimestamp = DateTime.UtcNow,
            AffectedFiles = new List<AffectedFile>(),
            RootCause = new RootCauseAnalysis(),
            SuggestedFixes = new List<SuggestedFix>(),
            ConfidenceScore = 0.0
        };

        try
        {
            // Extract error information
            var currentError = errorReport.CurrentError;
            if (currentError == null)
            {
                result.RootCause.Summary = "No error information available";
                return result;
            }

            var errorMessage = currentError.Error?.Message ?? string.Empty;
            var stackTrace = currentError.Stack ?? string.Empty;
            var functionName = currentError.FunctionName ?? string.Empty;
            var component = currentError.Component ?? string.Empty;

            // Analyze stack trace to identify file locations
            result.AffectedFiles = AnalyzeStackTrace(stackTrace, functionName, component);

            // Analyze error message to determine root cause
            result.RootCause = AnalyzeRootCause(errorMessage, stackTrace, functionName);

            // Generate suggested fixes
            result.SuggestedFixes = GenerateSuggestedFixes(errorReport, result.RootCause, result.AffectedFiles);

            // Calculate confidence score
            result.ConfidenceScore = CalculateConfidenceScore(result);

            _logger.LogInformation(
                "Error analysis completed: {ErrorId}, AffectedFiles: {FileCount}, Fixes: {FixCount}, Confidence: {Confidence}",
                errorReport.ErrorId,
                result.AffectedFiles.Count,
                result.SuggestedFixes.Count,
                result.ConfidenceScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during error analysis");
            result.RootCause.Summary = $"Analysis failed: {ex.Message}";
            return result;
        }
    }

    private List<AffectedFile> AnalyzeStackTrace(string stackTrace, string functionName, string component)
    {
        var affectedFiles = new List<AffectedFile>();

        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            // If no stack trace, infer from function name and component
            var inferredFile = InferFileFromFunction(functionName, component);
            if (inferredFile != null)
            {
                affectedFiles.Add(inferredFile);
            }
            return affectedFiles;
        }

        // Parse stack trace to extract file paths and line numbers
        var lines = stackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var fileInfo = ExtractFileInfoFromStackTraceLine(line);
            if (fileInfo != null && !affectedFiles.Any(f => f.FilePath == fileInfo.FilePath && f.LineNumber == fileInfo.LineNumber))
            {
                affectedFiles.Add(fileInfo);
            }
        }

        // If no files found in stack trace, try to infer from function name
        if (affectedFiles.Count == 0)
        {
            var inferredFile = InferFileFromFunction(functionName, component);
            if (inferredFile != null)
            {
                affectedFiles.Add(inferredFile);
            }
        }

        return affectedFiles;
    }

    private AffectedFile? ExtractFileInfoFromStackTraceLine(string line)
    {
        // Common stack trace patterns:
        // at ClassName.MethodName(FilePath:line number)
        // at ClassName.MethodName in FilePath:line number
        // FilePath(line number, column number)

        try
        {
            // Pattern 1: FilePath:line number
            var colonIndex = line.LastIndexOf(':');
            if (colonIndex > 0)
            {
                var afterColon = line.Substring(colonIndex + 1).Trim();
                if (int.TryParse(afterColon.Split(new[] { ',', ')' }, StringSplitOptions.RemoveEmptyEntries)[0], out var lineNumber))
                {
                    var beforeColon = line.Substring(0, colonIndex);
                    
                    // Try to extract file path
                    var pathPatterns = new[]
                    {
                        @"([a-zA-Z]:\\(?:[^\\/:*?""<>|\r\n]+\\)*[^\\/:*?""<>|\r\n]+\.(ts|js|cs|tsx|jsx))",
                        @"([\/][^\/]+\.(ts|js|cs|tsx|jsx))",
                        @"([^\/\\]+\.(ts|js|cs|tsx|jsx))"
                    };

                    foreach (var pattern in pathPatterns)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(beforeColon, pattern);
                        if (match.Success)
                        {
                            var filePath = match.Groups[1].Value;
                            
                            // Normalize path
                            if (filePath.Contains("frontend") || filePath.Contains("azure-functions"))
                            {
                                return new AffectedFile
                                {
                                    FilePath = filePath,
                                    LineNumber = lineNumber,
                                    ColumnNumber = null,
                                    FunctionName = ExtractFunctionName(line),
                                    Severity = DetermineSeverity(line)
                                };
                            }
                        }
                    }
                }
            }

            // Pattern 2: Try to find file name in parentheses
            var parenMatch = System.Text.RegularExpressions.Regex.Match(line, @"\(([^)]+\.(ts|js|cs|tsx|jsx)):(\d+)\)");
            if (parenMatch.Success)
            {
                return new AffectedFile
                {
                    FilePath = parenMatch.Groups[1].Value,
                    LineNumber = int.Parse(parenMatch.Groups[3].Value),
                    ColumnNumber = null,
                    FunctionName = ExtractFunctionName(line),
                    Severity = DetermineSeverity(line)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse stack trace line: {Line}", line);
        }

        return null;
    }

    private AffectedFile? InferFileFromFunction(string functionName, string component)
    {
        if (string.IsNullOrWhiteSpace(functionName))
            return null;

        // Infer file path from function name and component
        string? filePath = null;

        if (functionName.Contains("TransportComponent") || component == "TransportComponent")
        {
            filePath = "frontend/src/app/components/transport/transport.component.ts";
        }
        else if (functionName.Contains("Service") || component.Contains("Service"))
        {
            // Try to infer service file
            var serviceName = functionName.Replace("Service", "").Replace("Component", "");
            filePath = $"frontend/src/app/services/{serviceName.ToLower()}.service.ts";
        }
        else if (functionName.Contains("Adapter"))
        {
            filePath = "azure-functions/main/Adapters/";
        }

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return new AffectedFile
            {
                FilePath = filePath,
                LineNumber = null, // Unknown
                ColumnNumber = null,
                FunctionName = functionName,
                Severity = "medium"
            };
        }

        return null;
    }

    private string ExtractFunctionName(string stackLine)
    {
        // Extract function name from stack trace line
        // Pattern: at ClassName.MethodName
        var atMatch = System.Text.RegularExpressions.Regex.Match(stackLine, @"at\s+([^\s(]+)");
        if (atMatch.Success)
        {
            return atMatch.Groups[1].Value;
        }
        return string.Empty;
    }

    private string DetermineSeverity(string stackLine)
    {
        // Determine severity based on stack trace location
        if (stackLine.Contains("Error") || stackLine.Contains("Exception"))
            return "high";
        if (stackLine.Contains("Warning") || stackLine.Contains("null"))
            return "medium";
        return "low";
    }

    private RootCauseAnalysis AnalyzeRootCause(string errorMessage, string stackTrace, string functionName)
    {
        var analysis = new RootCauseAnalysis
        {
            Summary = "Unknown error",
            Category = "Unknown",
            LikelyCauses = new List<string>(),
            ErrorPattern = string.Empty
        };

        // Analyze error message patterns
        var lowerMessage = errorMessage.ToLowerInvariant();

        // Null reference errors
        if (lowerMessage.Contains("null") || lowerMessage.Contains("undefined") || 
            lowerMessage.Contains("cannot read") || lowerMessage.Contains("cannot access"))
        {
            analysis.Category = "NullReference";
            analysis.Summary = "Null or undefined reference error";
            analysis.LikelyCauses.Add("Missing null check before accessing property");
            analysis.LikelyCauses.Add("Object not initialized before use");
            analysis.LikelyCauses.Add("Optional property accessed without checking");
            analysis.ErrorPattern = "NullReference";
        }
        // Type errors
        else if (lowerMessage.Contains("type") && (lowerMessage.Contains("error") || lowerMessage.Contains("cannot")))
        {
            analysis.Category = "TypeError";
            analysis.Summary = "Type mismatch or incorrect type usage";
            analysis.LikelyCauses.Add("Incorrect type casting");
            analysis.LikelyCauses.Add("Property type mismatch");
            analysis.LikelyCauses.Add("Function parameter type mismatch");
            analysis.ErrorPattern = "TypeError";
        }
        // Network/HTTP errors
        else if (lowerMessage.Contains("network") || lowerMessage.Contains("fetch") || 
                 lowerMessage.Contains("http") || lowerMessage.Contains("connection"))
        {
            analysis.Category = "NetworkError";
            analysis.Summary = "Network or HTTP request error";
            analysis.LikelyCauses.Add("Server unavailable or unreachable");
            analysis.LikelyCauses.Add("Network timeout");
            analysis.LikelyCauses.Add("CORS configuration issue");
            analysis.ErrorPattern = "NetworkError";
        }
        // Validation errors
        else if (lowerMessage.Contains("validation") || lowerMessage.Contains("invalid") || 
                 lowerMessage.Contains("required") || lowerMessage.Contains("missing"))
        {
            analysis.Category = "ValidationError";
            analysis.Summary = "Input validation error";
            analysis.LikelyCauses.Add("Missing required parameter");
            analysis.LikelyCauses.Add("Invalid input format");
            analysis.LikelyCauses.Add("Value out of allowed range");
            analysis.ErrorPattern = "ValidationError";
        }
        // Generic error
        else
        {
            analysis.Category = "GenericError";
            analysis.Summary = errorMessage;
            analysis.LikelyCauses.Add("Unknown error - requires manual investigation");
            analysis.ErrorPattern = "Generic";
        }

        return analysis;
    }

    private List<SuggestedFix> GenerateSuggestedFixes(
        ErrorReport errorReport,
        RootCauseAnalysis rootCause,
        List<AffectedFile> affectedFiles)
    {
        var fixes = new List<SuggestedFix>();

        if (affectedFiles.Count == 0)
        {
            return fixes;
        }

        // Generate fixes based on root cause category
        switch (rootCause.Category)
        {
            case "NullReference":
                fixes.AddRange(GenerateNullReferenceFixes(affectedFiles, errorReport));
                break;
            case "TypeError":
                fixes.AddRange(GenerateTypeErrorFixes(affectedFiles, errorReport));
                break;
            case "NetworkError":
                fixes.AddRange(GenerateNetworkErrorFixes(affectedFiles, errorReport));
                break;
            case "ValidationError":
                fixes.AddRange(GenerateValidationErrorFixes(affectedFiles, errorReport));
                break;
            default:
                fixes.Add(new SuggestedFix
                {
                    Description = "Review error and apply appropriate fix",
                    CodeChanges = new List<CodeChange>(),
                    Priority = "medium"
                });
                break;
        }

        return fixes;
    }

    private List<SuggestedFix> GenerateNullReferenceFixes(List<AffectedFile> files, ErrorReport errorReport)
    {
        var fixes = new List<SuggestedFix>();

        foreach (var file in files)
        {
            if (file.LineNumber.HasValue)
            {
                fixes.Add(new SuggestedFix
                {
                    Description = $"Add null check before accessing property at line {file.LineNumber}",
                    CodeChanges = new List<CodeChange>
                    {
                        new CodeChange
                        {
                            FilePath = file.FilePath,
                            LineNumber = file.LineNumber.Value,
                            ChangeType = "AddNullCheck",
                            OldCode = "// Code that accesses property",
                            NewCode = "if (object != null) { /* code */ }"
                        }
                    },
                    Priority = "high",
                    EstimatedImpact = "Prevents null reference exceptions"
                });
            }
        }

        return fixes;
    }

    private List<SuggestedFix> GenerateTypeErrorFixes(List<AffectedFile> files, ErrorReport errorReport)
    {
        var fixes = new List<SuggestedFix>();

        foreach (var file in files)
        {
            fixes.Add(new SuggestedFix
            {
                Description = "Add type checking or type conversion",
                CodeChanges = new List<CodeChange>
                {
                    new CodeChange
                    {
                        FilePath = file.FilePath,
                        LineNumber = file.LineNumber ?? 0,
                        ChangeType = "AddTypeCheck",
                        OldCode = "// Code with type issue",
                        NewCode = "// Add proper type checking"
                    }
                },
                Priority = "medium",
                EstimatedImpact = "Prevents type errors"
            });
        }

        return fixes;
    }

    private List<SuggestedFix> GenerateNetworkErrorFixes(List<AffectedFile> files, ErrorReport errorReport)
    {
        var fixes = new List<SuggestedFix>();

        fixes.Add(new SuggestedFix
        {
            Description = "Add retry logic and error handling for network requests",
            CodeChanges = new List<CodeChange>
            {
                new CodeChange
                {
                    FilePath = "frontend/src/app/services/transport.service.ts",
                    LineNumber = 0,
                    ChangeType = "AddRetryLogic",
                    OldCode = "// HTTP request",
                    NewCode = "// Add retry with exponential backoff"
                }
            },
            Priority = "high",
            EstimatedImpact = "Improves resilience to network issues"
        });

        return fixes;
    }

    private List<SuggestedFix> GenerateValidationErrorFixes(List<AffectedFile> files, ErrorReport errorReport)
    {
        var fixes = new List<SuggestedFix>();

        foreach (var file in files)
        {
            fixes.Add(new SuggestedFix
            {
                Description = "Add input validation before processing",
                CodeChanges = new List<CodeChange>
                {
                    new CodeChange
                    {
                        FilePath = file.FilePath,
                        LineNumber = file.LineNumber ?? 0,
                        ChangeType = "AddValidation",
                        OldCode = "// Code without validation",
                        NewCode = "// Add validation checks"
                    }
                },
                Priority = "medium",
                EstimatedImpact = "Prevents invalid input errors"
            });
        }

        return fixes;
    }

    private double CalculateConfidenceScore(ErrorAnalysisResult result)
    {
        double score = 0.0;

        // Base score
        score += 0.2;

        // More affected files = higher confidence
        if (result.AffectedFiles.Count > 0)
            score += 0.2;

        // Has line numbers = higher confidence
        if (result.AffectedFiles.Any(f => f.LineNumber.HasValue))
            score += 0.3;

        // Has suggested fixes = higher confidence
        if (result.SuggestedFixes.Count > 0)
            score += 0.2;

        // Root cause identified = higher confidence
        if (result.RootCause.Category != "Unknown")
            score += 0.1;

        return Math.Min(1.0, score);
    }
}



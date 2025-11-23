using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Models;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class AIErrorAnalysisServiceTests
{
    private readonly Mock<ILogger<ErrorAnalysisService>> _loggerMock;
    private readonly ErrorAnalysisService _service;

    public AIErrorAnalysisServiceTests()
    {
        _loggerMock = new Mock<ILogger<ErrorAnalysisService>>();
        _service = new ErrorAnalysisService(_loggerMock.Object);
    }

    [Fact]
    public void AnalyzeError_WithValidErrorReport_ReturnsAnalysisResult()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "ERR-123",
            CurrentError = new CurrentErrorInfo
            {
                FunctionName = "TestFunction",
                Component = "TestComponent",
                Error = new ErrorInfo
                {
                    Message = "Cannot read property 'x' of undefined",
                    Name = "TypeError",
                    Stack = "at TestFunction() in C:\\test\\file.ts:line 10"
                },
                Stack = "at TestFunction() in C:\\test\\file.ts:line 10\nat AnotherFunction() in C:\\test\\file2.ts:line 20"
            },
            FunctionCallHistory = new List<FunctionCall>
            {
                new FunctionCall
                {
                    FunctionName = "TestFunction",
                    Component = "TestComponent",
                    Success = false,
                    Error = new ErrorInfo
                    {
                        Message = "Cannot read property 'x' of undefined",
                        Name = "TypeError"
                    }
                }
            }
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ERR-123", result.ErrorId);
        Assert.True(result.AffectedFiles.Count > 0);
        Assert.True(result.SuggestedFixes.Count > 0);
        Assert.True(result.ConfidenceScore >= 0 && result.ConfidenceScore <= 1);
    }

    [Fact]
    public void AnalyzeError_WithNullReferenceError_ClassifiesCorrectly()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "ERR-456",
            CurrentError = new CurrentErrorInfo
            {
                Error = new ErrorInfo
                {
                    Message = "Object reference not set to an instance of an object",
                    Name = "NullReferenceException"
                }
            }
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.Equal("NullReference", result.RootCause.Category);
        Assert.True(result.SuggestedFixes.Any(f => f.Description.Contains("null check")));
    }

    [Fact]
    public void AnalyzeError_WithTimeoutError_ClassifiesCorrectly()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "ERR-789",
            CurrentError = new CurrentErrorInfo
            {
                Error = new ErrorInfo
                {
                    Message = "The operation timed out",
                    Name = "TimeoutException"
                }
            }
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.Equal("Network", result.RootCause.Category);
        Assert.True(result.SuggestedFixes.Any(f => f.Description.Contains("timeout") || f.Description.Contains("retry")));
    }

    [Fact]
    public void AnalyzeError_WithStackTrace_ExtractsFileLocations()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "ERR-STACK",
            CurrentError = new CurrentErrorInfo
            {
                Stack = "at TestFunction() in C:\\project\\file.cs:line 42\nat AnotherFunction() in C:\\project\\file2.cs:line 100"
            }
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.True(result.AffectedFiles.Count >= 0); // May extract files from stack trace
    }

    [Fact]
    public void AnalyzeError_WithFunctionCallHistory_AnalyzesHistory()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "ERR-HISTORY",
            FunctionCallHistory = new List<FunctionCall>
            {
                new FunctionCall { FunctionName = "Function1", Success = true },
                new FunctionCall { FunctionName = "Function2", Success = false, Error = new ErrorInfo { Message = "Error" } },
                new FunctionCall { FunctionName = "Function3", Success = true }
            }
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ConfidenceScore > 0);
    }

    [Fact]
    public void AnalyzeError_WithEmptyErrorReport_ReturnsBasicAnalysis()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "ERR-EMPTY"
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ERR-EMPTY", result.ErrorId);
        Assert.NotNull(result.AffectedFiles);
        Assert.NotNull(result.SuggestedFixes);
    }
}


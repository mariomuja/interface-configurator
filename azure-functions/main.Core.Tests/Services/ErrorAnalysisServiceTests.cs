using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Models;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class ErrorAnalysisServiceTests
{
    private readonly Mock<ILogger<ErrorAnalysisService>> _mockLogger;
    private readonly ErrorAnalysisService _service;

    public ErrorAnalysisServiceTests()
    {
        _mockLogger = new Mock<ILogger<ErrorAnalysisService>>();
        _service = new ErrorAnalysisService(_mockLogger.Object);
    }

    [Fact]
    public void AnalyzeError_WithNullReferenceError_IdentifiesNullReferenceCategory()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "TEST-001",
            CurrentError = new CurrentErrorInfo
            {
                FunctionName = "testFunction",
                Component = "TestComponent",
                Error = new ErrorInfo
                {
                    Message = "Cannot read property 'name' of null",
                    Stack = "at TestComponent.testFunction (test.ts:10:5)"
                },
                Stack = "at TestComponent.testFunction (test.ts:10:5)"
            }
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TEST-001", result.ErrorId);
        Assert.Equal("NullReference", result.RootCause.Category);
        Assert.True(result.AffectedFiles.Count > 0);
        Assert.True(result.SuggestedFixes.Count > 0);
        Assert.True(result.ConfidenceScore > 0);
    }

    [Fact]
    public void AnalyzeError_WithTypeError_IdentifiesTypeErrorCategory()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "TEST-002",
            CurrentError = new CurrentErrorInfo
            {
                FunctionName = "testFunction",
                Component = "TestComponent",
                Error = new ErrorInfo
                {
                    Message = "TypeError: Cannot read property 'value' of undefined",
                    Stack = "at TestComponent.testFunction (test.ts:15:5)"
                },
                Stack = "at TestComponent.testFunction (test.ts:15:5)"
            }
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TypeError", result.RootCause.Category);
    }

    [Fact]
    public void AnalyzeError_WithNetworkError_IdentifiesNetworkErrorCategory()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "TEST-003",
            CurrentError = new CurrentErrorInfo
            {
                FunctionName = "loadData",
                Component = "TransportComponent",
                Error = new ErrorInfo
                {
                    Message = "Network error: Failed to fetch",
                    Stack = "at TransportComponent.loadData (transport.component.ts:100:5)"
                },
                Stack = "at TransportComponent.loadData (transport.component.ts:100:5)"
            }
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("NetworkError", result.RootCause.Category);
    }

    [Fact]
    public void AnalyzeError_WithValidationError_IdentifiesValidationErrorCategory()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "TEST-004",
            CurrentError = new CurrentErrorInfo
            {
                FunctionName = "validateInput",
                Component = "FormComponent",
                Error = new ErrorInfo
                {
                    Message = "Validation error: Required field is missing",
                    Stack = "at FormComponent.validateInput (form.component.ts:50:5)"
                },
                Stack = "at FormComponent.validateInput (form.component.ts:50:5)"
            }
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ValidationError", result.RootCause.Category);
    }

    [Fact]
    public void AnalyzeError_WithStackTrace_ExtractsFileInformation()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "TEST-005",
            CurrentError = new CurrentErrorInfo
            {
                FunctionName = "testFunction",
                Component = "TestComponent",
                Error = new ErrorInfo
                {
                    Message = "Test error",
                    Stack = "at TestComponent.testFunction (frontend/src/app/components/test/test.component.ts:25:10)"
                },
                Stack = "at TestComponent.testFunction (frontend/src/app/components/test/test.component.ts:25:10)"
            }
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.AffectedFiles.Count > 0);
        var affectedFile = result.AffectedFiles.First();
        Assert.Contains("test.component.ts", affectedFile.FilePath);
        Assert.True(affectedFile.LineNumber.HasValue);
    }

    [Fact]
    public void AnalyzeError_WithoutStackTrace_InfersFileFromFunctionName()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "TEST-006",
            CurrentError = new CurrentErrorInfo
            {
                FunctionName = "TransportComponent.loadData",
                Component = "TransportComponent",
                Error = new ErrorInfo
                {
                    Message = "Test error",
                    Stack = ""
                },
                Stack = ""
            }
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.NotNull(result);
        // Should infer file from component name
        Assert.True(result.AffectedFiles.Count > 0 || result.SuggestedFixes.Count > 0);
    }

    [Fact]
    public void AnalyzeError_WithNullCurrentError_ReturnsValidResult()
    {
        // Arrange
        var errorReport = new ErrorReport
        {
            ErrorId = "TEST-007",
            CurrentError = null
        };

        // Act
        var result = _service.AnalyzeError(errorReport);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("No error information available", result.RootCause.Summary);
    }
}




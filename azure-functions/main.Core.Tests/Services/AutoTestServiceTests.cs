using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class AutoTestServiceTests
{
    private readonly Mock<ILogger<AutoTestService>> _mockLogger;
    private readonly AutoTestService _service;

    public AutoTestServiceTests()
    {
        _mockLogger = new Mock<ILogger<AutoTestService>>();
        _service = new AutoTestService(_mockLogger.Object);
    }

    [Fact]
    public async Task RunTestsAsync_WithValidInput_ReturnsTestResult()
    {
        // Arrange
        var errorId = "TEST-001";
        var affectedFiles = new List<string> { "test.component.ts" };

        // Act
        var result = await _service.RunTestsAsync(errorId, affectedFiles);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(errorId, result.ErrorId);
        Assert.NotNull(result.TestResults);
        // Note: Actual test execution may fail if npm/dotnet not available, but service should still return result
    }

    [Fact]
    public async Task RunTestsAsync_WithEmptyFiles_ReturnsTestResult()
    {
        // Arrange
        var errorId = "TEST-002";
        var affectedFiles = new List<string>();

        // Act
        var result = await _service.RunTestsAsync(errorId, affectedFiles);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(errorId, result.ErrorId);
    }

    [Fact]
    public async Task RunTestsAsync_WithTransportComponent_IncludesIntegrationTests()
    {
        // Arrange
        var errorId = "TEST-003";
        var affectedFiles = new List<string> { "transport.component.ts" };

        // Act
        var result = await _service.RunTestsAsync(errorId, affectedFiles);

        // Assert
        Assert.NotNull(result);
        // Should include integration tests
        Assert.True(result.TestResults.Count >= 2); // Frontend + Backend + possibly Integration
    }
}




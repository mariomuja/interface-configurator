using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

using InterfaceConfigurator.Main.Models;

public class AutoTestingServiceTests
{
    private readonly Mock<ILogger<AutoTestService>> _loggerMock;
    private readonly AutoTestService _service;

    public AutoTestingServiceTests()
    {
        _loggerMock = new Mock<ILogger<AutoTestService>>();
        _service = new AutoTestService(_loggerMock.Object);
    }

    [Fact]
    public async Task RunTestsAsync_ReturnsTestResult()
    {
        // Act
        var result = await _service.RunTestsAsync("TEST-123", new List<string>());

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.TestResults);
        Assert.Equal("TEST-123", result.ErrorId);
        // Note: Actual test execution may fail if test environment is not set up
        // This test verifies the method doesn't throw and returns a result
    }

    [Fact]
    public async Task RunTestsAsync_WithAffectedFiles_RunsTests()
    {
        // Act
        var result = await _service.RunTestsAsync("TEST-456", new List<string> { "test-file.ts" });

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.TestResults);
        // Should handle test execution gracefully
    }

    [Fact]
    public async Task RunTestsAsync_HandlesMissingTestDirectories()
    {
        // Act
        var result = await _service.RunTestsAsync("TEST-789", new List<string>());

        // Assert
        Assert.NotNull(result);
        // Should handle missing directories gracefully
    }
}


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Services;
using Xunit;

namespace ProcessCsvBlobTrigger.Core.Tests.Services;

public class LoggingServiceAdapterTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task LogAsync_WithContext_LogsToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<LoggingServiceAdapter>>();
        var adapter = new LoggingServiceAdapter(context, loggerMock.Object);

        // Act
        await adapter.LogAsync("info", "Test message", "Test details");

        // Assert
        var logs = await context.ProcessLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("info", logs[0].Level);
        Assert.Equal("Test message", logs[0].Message);
        Assert.Equal("Test details", logs[0].Details);
    }

    [Fact]
    public async Task LogAsync_WithoutContext_LogsToLogger()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<LoggingServiceAdapter>>();
        var adapter = new LoggingServiceAdapter(null, loggerMock.Object);

        // Act
        await adapter.LogAsync("error", "Test error", "Error details");

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Test error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task LogAsync_WithNullLevel_UsesUnknown()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<LoggingServiceAdapter>>();
        var adapter = new LoggingServiceAdapter(context, loggerMock.Object);

        // Act
        await adapter.LogAsync(null!, "Test message");

        // Assert
        var logs = await context.ProcessLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("unknown", logs[0].Level);
    }

    [Fact]
    public async Task LogAsync_WithEmptyMessage_UsesDefaultMessage()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<LoggingServiceAdapter>>();
        var adapter = new LoggingServiceAdapter(context, loggerMock.Object);

        // Act
        await adapter.LogAsync("info", "");

        // Assert
        var logs = await context.ProcessLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("No message provided", logs[0].Message);
    }

    [Fact]
    public async Task LogAsync_WithDifferentLevels_LogsCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<LoggingServiceAdapter>>();
        var adapter = new LoggingServiceAdapter(context, loggerMock.Object);

        // Act
        await adapter.LogAsync("error", "Error message");
        await adapter.LogAsync("warning", "Warning message");
        await adapter.LogAsync("info", "Info message");
        await adapter.LogAsync("debug", "Debug message");

        // Assert
        var logs = await context.ProcessLogs.ToListAsync();
        Assert.Equal(4, logs.Count);
        Assert.Contains(logs, l => l.Level == "error");
        Assert.Contains(logs, l => l.Level == "warning");
        Assert.Contains(logs, l => l.Level == "info");
        Assert.Contains(logs, l => l.Level == "debug");
    }

    [Fact]
    public async Task LogAsync_DatabaseFailure_FallsBackToLogger()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        context.Dispose(); // Dispose to cause database errors

        var loggerMock = new Mock<ILogger<LoggingServiceAdapter>>();
        var adapter = new LoggingServiceAdapter(context, loggerMock.Object);

        // Act
        await adapter.LogAsync("info", "Test message");

        // Assert - Should fall back to logger
        loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<LoggingServiceAdapter>>();
        var adapter = new LoggingServiceAdapter(context, loggerMock.Object);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - TaskCanceledException is derived from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => adapter.LogAsync("info", "Test", cancellationToken: cts.Token));
    }
}


using Microsoft.EntityFrameworkCore;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Services;
using Xunit;

namespace ProcessCsvBlobTrigger.Core.Tests.Services;

public class LoggingServiceTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task LogAsync_WithValidData_LogsToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var service = new LoggingService(context);

        // Act
        await service.LogAsync("info", "Test message", "Test details");

        // Assert
        var logs = await context.ProcessLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("info", logs[0].Level);
        Assert.Equal("Test message", logs[0].Message);
        Assert.Equal("Test details", logs[0].Details);
        Assert.True(logs[0].Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public async Task LogAsync_WithNullDetails_LogsWithoutDetails()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var service = new LoggingService(context);

        // Act
        await service.LogAsync("error", "Error message", null);

        // Assert
        var logs = await context.ProcessLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Null(logs[0].Details);
    }

    [Fact]
    public async Task LogBatchAsync_WithMultipleLogs_LogsAll()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var service = new LoggingService(context);

        var logs = new List<(string Level, string Message, string? Details)>
        {
            ("info", "Message 1", "Details 1"),
            ("warning", "Message 2", "Details 2"),
            ("error", "Message 3", null)
        };

        // Act
        await service.LogBatchAsync(logs);

        // Assert
        var dbLogs = await context.ProcessLogs.ToListAsync();
        Assert.Equal(3, dbLogs.Count);
        Assert.Contains(dbLogs, l => l.Message == "Message 1");
        Assert.Contains(dbLogs, l => l.Message == "Message 2");
        Assert.Contains(dbLogs, l => l.Message == "Message 3");
    }

    [Fact]
    public async Task LogBatchAsync_WithEmptyList_DoesNotThrow()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var service = new LoggingService(context);

        // Act
        await service.LogBatchAsync(Enumerable.Empty<(string, string, string?)>());

        // Assert
        var logs = await context.ProcessLogs.ToListAsync();
        Assert.Empty(logs);
    }

    [Fact]
    public async Task LogAsync_DatabaseFailure_DoesNotThrow()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options);
        context.Dispose(); // Dispose to cause database errors

        var service = new LoggingService(context);

        // Act & Assert - Should not throw, should log to console
        await service.LogAsync("info", "Test message");
    }
}


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

/// <summary>
/// Unit tests for DeadLetterMonitor
/// </summary>
public class DeadLetterMonitorTests : IDisposable
{
    private readonly MessageBoxDbContext _context;
    private readonly IMessageBoxService _messageBoxService;
    private readonly Mock<ILogger<DeadLetterMonitor>> _mockLogger;
    private readonly DeadLetterMonitor _monitor;

    public DeadLetterMonitorTests()
    {
        var options = new DbContextOptionsBuilder<MessageBoxDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MessageBoxDbContext(options);
        var logger = new Mock<ILogger<MessageBoxService>>();
        var mockEventQueue = new Mock<IEventQueue>();
        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _messageBoxService = new MessageBoxService(_context, mockEventQueue.Object, mockSubscriptionService.Object, logger.Object);
        _mockLogger = new Mock<ILogger<DeadLetterMonitor>>();
        _monitor = new DeadLetterMonitor(_messageBoxService, _mockLogger.Object);

        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetDeadLetterCountAsync_WithNoDeadLetters_ShouldReturnZero()
    {
        // Act
        var count = await _monitor.GetDeadLetterCountAsync();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetDeadLetterCountAsync_WithDeadLetters_ShouldReturnCount()
    {
        // Arrange
        var adapterInstanceGuid = Guid.NewGuid();
        await _messageBoxService.WriteSingleRecordMessageAsync(
            "TestInterface",
            "CSV",
            "Source",
            adapterInstanceGuid,
            new List<string> { "Name", "Age" },
            new Dictionary<string, string> { { "Name", "Test" }, { "Age", "30" } }
        );

        // Mark as dead letter by setting retry count to max
        var message = await _context.Messages.FirstAsync();
        message.RetryCount = message.MaxRetries;
        message.Status = "DeadLetter";
        await _context.SaveChangesAsync();

        // Act
        var count = await _monitor.GetDeadLetterCountAsync();

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetDeadLetterCountAsync_WithInterfaceFilter_ShouldReturnFilteredCount()
    {
        // Arrange
        var adapterInstanceGuid1 = Guid.NewGuid();
        var adapterInstanceGuid2 = Guid.NewGuid();

        await _messageBoxService.WriteSingleRecordMessageAsync(
            "Interface1",
            "CSV",
            "Source",
            adapterInstanceGuid1,
            new List<string> { "Name" },
            new Dictionary<string, string> { { "Name", "Test1" } }
        );

        await _messageBoxService.WriteSingleRecordMessageAsync(
            "Interface2",
            "CSV",
            "Source",
            adapterInstanceGuid2,
            new List<string> { "Name" },
            new Dictionary<string, string> { { "Name", "Test2" } }
        );

        // Mark both as dead letters
        var messages = await _context.Messages.ToListAsync();
        foreach (var msg in messages)
        {
            msg.RetryCount = msg.MaxRetries;
            msg.Status = "DeadLetter";
        }
        await _context.SaveChangesAsync();

        // Act
        var count1 = await _monitor.GetDeadLetterCountAsync("Interface1");
        var count2 = await _monitor.GetDeadLetterCountAsync("Interface2");
        var totalCount = await _monitor.GetDeadLetterCountAsync();

        // Assert
        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
        Assert.Equal(2, totalCount);
    }

    [Fact]
    public async Task GetDeadLetterCountAsync_OnError_ShouldReturnZero()
    {
        // Arrange - Create monitor with null messageBoxService to trigger error
        var errorMonitor = new DeadLetterMonitor(null!, _mockLogger.Object);

        // Act
        var count = await errorMonitor.GetDeadLetterCountAsync();

        // Assert
        Assert.Equal(0, count);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

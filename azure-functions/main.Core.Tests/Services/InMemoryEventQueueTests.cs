using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Services;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

/// <summary>
/// Unit tests for InMemoryEventQueue
/// </summary>
public class InMemoryEventQueueTests
{
    private readonly Mock<ILogger<InMemoryEventQueue>> _mockLogger;
    private readonly InMemoryEventQueue _queue;

    public InMemoryEventQueueTests()
    {
        _mockLogger = new Mock<ILogger<InMemoryEventQueue>>();
        _queue = new InMemoryEventQueue(_mockLogger.Object);
    }

    [Fact]
    public void PendingEventCount_Initially_ShouldBeZero()
    {
        // Assert
        Assert.Equal(0, _queue.PendingEventCount);
    }

    [Fact]
    public async Task EnqueueMessageEventAsync_ShouldIncreasePendingCount()
    {
        // Act
        await _queue.EnqueueMessageEventAsync(Guid.NewGuid(), "TestInterface");

        // Assert
        Assert.Equal(1, _queue.PendingEventCount);
    }

    [Fact]
    public async Task DequeueMessageEventAsync_WithNoEvents_ShouldReturnNull()
    {
        // Act
        var result = await _queue.DequeueMessageEventAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DequeueMessageEventAsync_WithEvents_ShouldReturnEvent()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var interfaceName = "TestInterface";
        
        // Act
        await _queue.EnqueueMessageEventAsync(messageId, interfaceName);
        var result = await _queue.DequeueMessageEventAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(messageId, result.MessageId);
        Assert.Equal(interfaceName, result.InterfaceName);
        Assert.Equal(0, _queue.PendingEventCount);
    }

    [Fact]
    public async Task EnqueueDequeue_WithMultipleEvents_ShouldProcessInOrder()
    {
        // Arrange
        var messageId1 = Guid.NewGuid();
        var messageId2 = Guid.NewGuid();
        var messageId3 = Guid.NewGuid();

        // Act
        await _queue.EnqueueMessageEventAsync(messageId1, "Interface1");
        await _queue.EnqueueMessageEventAsync(messageId2, "Interface2");
        await _queue.EnqueueMessageEventAsync(messageId3, "Interface3");

        // Assert
        Assert.Equal(3, _queue.PendingEventCount);
        
        var event1 = await _queue.DequeueMessageEventAsync();
        Assert.NotNull(event1);
        Assert.Equal(messageId1, event1.MessageId);
        Assert.Equal(2, _queue.PendingEventCount);

        var event2 = await _queue.DequeueMessageEventAsync();
        Assert.NotNull(event2);
        Assert.Equal(messageId2, event2.MessageId);
        Assert.Equal(1, _queue.PendingEventCount);

        var event3 = await _queue.DequeueMessageEventAsync();
        Assert.NotNull(event3);
        Assert.Equal(messageId3, event3.MessageId);
        Assert.Equal(0, _queue.PendingEventCount);
    }

    [Fact]
    public async Task EnqueueMessageEventAsync_ShouldSetEnqueuedAt()
    {
        // Arrange
        var beforeEnqueue = DateTime.UtcNow;
        
        // Act
        await _queue.EnqueueMessageEventAsync(Guid.NewGuid(), "TestInterface");
        var result = await _queue.DequeueMessageEventAsync();
        var afterDequeue = DateTime.UtcNow;

        // Assert
        Assert.NotNull(result);
        Assert.True(result.EnqueuedAt >= beforeEnqueue);
        Assert.True(result.EnqueuedAt <= afterDequeue);
    }
}

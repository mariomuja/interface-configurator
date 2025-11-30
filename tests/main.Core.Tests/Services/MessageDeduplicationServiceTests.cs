using Xunit;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading.Tasks;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class MessageDeduplicationServiceTests : IDisposable
{
    private readonly MessageDeduplicationService _service;
    private readonly InterfaceConfigDbContext _context;
    private readonly Mock<ILogger<MessageDeduplicationService>> _loggerMock;

    public MessageDeduplicationServiceTests()
    {
        var options = new DbContextOptionsBuilder<InterfaceConfigDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new InterfaceConfigDbContext(options);
        _loggerMock = new Mock<ILogger<MessageDeduplicationService>>();
        _service = new MessageDeduplicationService(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task CheckForDuplicateAsync_ShouldReturnFalse_ForNewMessage()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();

        // Act
        var isDuplicate = await _service.CheckForDuplicateAsync(idempotencyKey);

        // Assert
        Assert.False(isDuplicate);
    }

    [Fact]
    public async Task CheckForDuplicateAsync_ShouldReturnTrue_ForDuplicateMessage()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        await _service.MarkAsProcessedAsync(idempotencyKey, "test-interface", "test-adapter");

        // Act
        var isDuplicate = await _service.CheckForDuplicateAsync(idempotencyKey);

        // Assert
        Assert.True(isDuplicate);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_ShouldStoreIdempotencyKey()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var interfaceName = "test-interface";
        var adapterName = "test-adapter";

        // Act
        await _service.MarkAsProcessedAsync(idempotencyKey, interfaceName, adapterName);

        // Assert
        var isDuplicate = await _service.CheckForDuplicateAsync(idempotencyKey);
        Assert.True(isDuplicate);
    }

    [Fact]
    public async Task CheckForDuplicateAsync_ShouldHandleNullKey()
    {
        // Act
        var isDuplicate = await _service.CheckForDuplicateAsync(null!);

        // Assert
        Assert.False(isDuplicate);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_ShouldHandleNullKey()
    {
        // Act & Assert - Should not throw
        await _service.MarkAsProcessedAsync(null!, "test-interface", "test-adapter");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}






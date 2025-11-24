using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class ServiceBusLockTrackingServiceTests : IDisposable
{
    private readonly InterfaceConfigDbContext _context;
    private readonly Mock<ILogger<ServiceBusLockTrackingService>> _loggerMock;
    private readonly ServiceBusLockTrackingService _service;

    public ServiceBusLockTrackingServiceTests()
    {
        var options = new DbContextOptionsBuilder<InterfaceConfigDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new InterfaceConfigDbContext(options);
        _loggerMock = new Mock<ILogger<ServiceBusLockTrackingService>>();
        _service = new ServiceBusLockTrackingService(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task RecordMessageLockAsync_ShouldCreateNewLock()
    {
        // Arrange
        var messageId = "msg-123";
        var lockToken = "lock-token-abc";
        var lockExpiresAt = DateTime.UtcNow.AddSeconds(60);

        // Act
        await _service.RecordMessageLockAsync(
            messageId,
            lockToken,
            "topic-test",
            "subscription-test",
            "interface-test",
            Guid.NewGuid(),
            lockExpiresAt,
            1
        );

        // Assert
        var lockRecord = await _context.ServiceBusMessageLocks
            .FirstOrDefaultAsync(l => l.MessageId == messageId);

        Assert.NotNull(lockRecord);
        Assert.Equal(lockToken, lockRecord.LockToken);
        Assert.Equal("Active", lockRecord.Status);
        Assert.Equal(1, lockRecord.DeliveryCount);
    }

    [Fact]
    public async Task RecordMessageLockAsync_ShouldUpdateExistingLock()
    {
        // Arrange
        var messageId = "msg-456";
        var existingLock = new ServiceBusMessageLock
        {
            MessageId = messageId,
            LockToken = "old-token",
            TopicName = "topic-test",
            SubscriptionName = "subscription-test",
            InterfaceName = "interface-test",
            AdapterInstanceGuid = Guid.NewGuid(),
            LockExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Status = "Active",
            DeliveryCount = 1
        };
        _context.ServiceBusMessageLocks.Add(existingLock);
        await _context.SaveChangesAsync();

        var newLockToken = "new-token-xyz";
        var newExpiresAt = DateTime.UtcNow.AddSeconds(60);

        // Act
        await _service.RecordMessageLockAsync(
            messageId,
            newLockToken,
            "topic-test",
            "subscription-test",
            "interface-test",
            existingLock.AdapterInstanceGuid,
            newExpiresAt,
            2
        );

        // Assert
        var updatedLock = await _context.ServiceBusMessageLocks
            .FirstOrDefaultAsync(l => l.MessageId == messageId);

        Assert.NotNull(updatedLock);
        Assert.Equal(newLockToken, updatedLock.LockToken);
        Assert.Equal(2, updatedLock.DeliveryCount);
        Assert.NotNull(updatedLock.LastRenewedAt);
    }

    [Fact]
    public async Task UpdateLockStatusAsync_ShouldUpdateStatus()
    {
        // Arrange
        var messageId = "msg-789";
        var lockRecord = new ServiceBusMessageLock
        {
            MessageId = messageId,
            LockToken = "token-789",
            TopicName = "topic-test",
            SubscriptionName = "subscription-test",
            InterfaceName = "interface-test",
            AdapterInstanceGuid = Guid.NewGuid(),
            LockExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = "Active"
        };
        _context.ServiceBusMessageLocks.Add(lockRecord);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateLockStatusAsync(messageId, "Completed", "Success");

        // Assert
        var updated = await _context.ServiceBusMessageLocks
            .FirstOrDefaultAsync(l => l.MessageId == messageId);

        Assert.NotNull(updated);
        Assert.Equal("Completed", updated.Status);
        Assert.Equal("Success", updated.CompletionReason);
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task UpdateLockStatusAsync_ShouldNotUpdateIfLockNotFound()
    {
        // Arrange
        var messageId = "non-existent";

        // Act
        await _service.UpdateLockStatusAsync(messageId, "Completed", "Success");

        // Assert
        var lockRecord = await _context.ServiceBusMessageLocks
            .FirstOrDefaultAsync(l => l.MessageId == messageId);

        Assert.Null(lockRecord);
    }

    [Fact]
    public async Task RenewLockAsync_ShouldRenewActiveLock()
    {
        // Arrange
        var messageId = "msg-renew";
        var lockRecord = new ServiceBusMessageLock
        {
            MessageId = messageId,
            LockToken = "token-renew",
            TopicName = "topic-test",
            SubscriptionName = "subscription-test",
            InterfaceName = "interface-test",
            AdapterInstanceGuid = Guid.NewGuid(),
            LockExpiresAt = DateTime.UtcNow.AddSeconds(30),
            Status = "Active",
            RenewalCount = 0
        };
        _context.ServiceBusMessageLocks.Add(lockRecord);
        await _context.SaveChangesAsync();

        var newExpiresAt = DateTime.UtcNow.AddSeconds(90);

        // Act
        var result = await _service.RenewLockAsync(messageId, newExpiresAt);

        // Assert
        Assert.True(result);
        var renewed = await _context.ServiceBusMessageLocks
            .FirstOrDefaultAsync(l => l.MessageId == messageId);

        Assert.NotNull(renewed);
        Assert.Equal(newExpiresAt, renewed.LockExpiresAt, TimeSpan.FromSeconds(1));
        Assert.Equal(1, renewed.RenewalCount);
        Assert.NotNull(renewed.LastRenewedAt);
    }

    [Fact]
    public async Task RenewLockAsync_ShouldReturnFalseForNonActiveLock()
    {
        // Arrange
        var messageId = "msg-completed";
        var lockRecord = new ServiceBusMessageLock
        {
            MessageId = messageId,
            LockToken = "token-completed",
            TopicName = "topic-test",
            SubscriptionName = "subscription-test",
            InterfaceName = "interface-test",
            AdapterInstanceGuid = Guid.NewGuid(),
            LockExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = "Completed"
        };
        _context.ServiceBusMessageLocks.Add(lockRecord);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.RenewLockAsync(messageId, DateTime.UtcNow.AddSeconds(90));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetLocksNeedingRenewalAsync_ShouldReturnLocksExpiringSoon()
    {
        // Arrange
        var soonExpiring = new ServiceBusMessageLock
        {
            MessageId = "msg-soon",
            LockToken = "token-soon",
            TopicName = "topic-test",
            SubscriptionName = "subscription-test",
            InterfaceName = "interface-test",
            AdapterInstanceGuid = Guid.NewGuid(),
            LockExpiresAt = DateTime.UtcNow.AddSeconds(20), // Expires in 20 seconds
            Status = "Active"
        };

        var laterExpiring = new ServiceBusMessageLock
        {
            MessageId = "msg-later",
            LockToken = "token-later",
            TopicName = "topic-test",
            SubscriptionName = "subscription-test",
            InterfaceName = "interface-test",
            AdapterInstanceGuid = Guid.NewGuid(),
            LockExpiresAt = DateTime.UtcNow.AddSeconds(60), // Expires in 60 seconds
            Status = "Active"
        };

        _context.ServiceBusMessageLocks.AddRange(soonExpiring, laterExpiring);
        await _context.SaveChangesAsync();

        // Act
        var locks = await _service.GetLocksNeedingRenewalAsync(TimeSpan.FromSeconds(30));

        // Assert
        Assert.Single(locks);
        Assert.Equal("msg-soon", locks[0].MessageId);
    }

    [Fact]
    public async Task GetExpiredLocksAsync_ShouldMarkExpiredLocks()
    {
        // Arrange
        var expiredLock = new ServiceBusMessageLock
        {
            MessageId = "msg-expired",
            LockToken = "token-expired",
            TopicName = "topic-test",
            SubscriptionName = "subscription-test",
            InterfaceName = "interface-test",
            AdapterInstanceGuid = Guid.NewGuid(),
            LockExpiresAt = DateTime.UtcNow.AddSeconds(-10), // Already expired
            Status = "Active"
        };
        _context.ServiceBusMessageLocks.Add(expiredLock);
        await _context.SaveChangesAsync();

        // Act
        var expired = await _service.GetExpiredLocksAsync();

        // Assert
        Assert.Single(expired);
        var updated = await _context.ServiceBusMessageLocks
            .FirstOrDefaultAsync(l => l.MessageId == "msg-expired");

        Assert.NotNull(updated);
        Assert.Equal("Expired", updated.Status);
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task CleanupOldLocksAsync_ShouldRemoveOldCompletedLocks()
    {
        // Arrange
        var oldCompleted = new ServiceBusMessageLock
        {
            MessageId = "msg-old",
            LockToken = "token-old",
            TopicName = "topic-test",
            SubscriptionName = "subscription-test",
            InterfaceName = "interface-test",
            AdapterInstanceGuid = Guid.NewGuid(),
            LockExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = "Completed",
            CompletedAt = DateTime.UtcNow.AddDays(-10) // 10 days ago
        };

        var recentCompleted = new ServiceBusMessageLock
        {
            MessageId = "msg-recent",
            LockToken = "token-recent",
            TopicName = "topic-test",
            SubscriptionName = "subscription-test",
            InterfaceName = "interface-test",
            AdapterInstanceGuid = Guid.NewGuid(),
            LockExpiresAt = DateTime.UtcNow.AddSeconds(60),
            Status = "Completed",
            CompletedAt = DateTime.UtcNow.AddDays(-1) // 1 day ago
        };

        _context.ServiceBusMessageLocks.AddRange(oldCompleted, recentCompleted);
        await _context.SaveChangesAsync();

        // Act
        var cleaned = await _service.CleanupOldLocksAsync(TimeSpan.FromDays(7));

        // Assert
        Assert.Equal(1, cleaned);
        var remaining = await _context.ServiceBusMessageLocks
            .FirstOrDefaultAsync(l => l.MessageId == "msg-recent");

        Assert.NotNull(remaining);
        Assert.Null(await _context.ServiceBusMessageLocks
            .FirstOrDefaultAsync(l => l.MessageId == "msg-old"));
    }
}


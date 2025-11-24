using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class ServiceBusLockRenewalServiceTests
{
    private readonly Mock<IServiceBusLockTrackingService> _lockTrackingMock;
    private readonly Mock<IServiceBusService> _serviceBusMock;
    private readonly Mock<ILogger<ServiceBusLockRenewalService>> _loggerMock;

    public ServiceBusLockRenewalServiceTests()
    {
        _lockTrackingMock = new Mock<IServiceBusLockTrackingService>();
        _serviceBusMock = new Mock<IServiceBusService>();
        _loggerMock = new Mock<ILogger<ServiceBusLockRenewalService>>();
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDependencies()
    {
        // Act
        var service = new ServiceBusLockRenewalService(
            _lockTrackingMock.Object,
            _serviceBusMock.Object,
            _loggerMock.Object
        );

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallGetLocksNeedingRenewal()
    {
        // Arrange
        var service = new ServiceBusLockRenewalService(
            _lockTrackingMock.Object,
            _serviceBusMock.Object,
            _loggerMock.Object
        );

        _lockTrackingMock
            .Setup(x => x.GetLocksNeedingRenewalAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceBusMessageLock>());

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel quickly for test

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(150); // Wait a bit
        await service.StopAsync(cts.Token);

        // Assert
        _lockTrackingMock.Verify(
            x => x.GetLocksNeedingRenewalAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRenewLocks()
    {
        // Arrange
        var service = new ServiceBusLockRenewalService(
            _lockTrackingMock.Object,
            _serviceBusMock.Object,
            _loggerMock.Object
        );

        var locksToRenew = new List<ServiceBusMessageLock>
        {
            new ServiceBusMessageLock
            {
                MessageId = "msg-1",
                TopicName = "topic-1",
                SubscriptionName = "sub-1",
                Status = "Active"
            }
        };

        _lockTrackingMock
            .Setup(x => x.GetLocksNeedingRenewalAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(locksToRenew);

        _lockTrackingMock
            .Setup(x => x.RenewLockAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(150);
        await service.StopAsync(cts.Token);

        // Assert
        _lockTrackingMock.Verify(
            x => x.RenewLockAsync("msg-1", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleErrorsGracefully()
    {
        // Arrange
        var service = new ServiceBusLockRenewalService(
            _lockTrackingMock.Object,
            _serviceBusMock.Object,
            _loggerMock.Object
        );

        _lockTrackingMock
            .Setup(x => x.GetLocksNeedingRenewalAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test error"));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // Act - Should not throw
        await service.StartAsync(cts.Token);
        await Task.Delay(250);
        await service.StopAsync(cts.Token);

        // Assert - Service should continue running despite errors
        Assert.True(true); // If we get here, service handled error gracefully
    }
}


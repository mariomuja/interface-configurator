using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class ServiceBusDeadLetterMonitoringServiceTests
{
    private readonly Mock<ILogger<ServiceBusDeadLetterMonitoringService>> _loggerMock;

    public ServiceBusDeadLetterMonitoringServiceTests()
    {
        _loggerMock = new Mock<ILogger<ServiceBusDeadLetterMonitoringService>>();
    }

    [Fact]
    public void Constructor_ShouldInitializeWithLogger()
    {
        // Act
        var service = new ServiceBusDeadLetterMonitoringService(_loggerMock.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_ShouldHandleMissingConnectionString()
    {
        // Arrange - Temporarily remove connection string
        var originalValue = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
        Environment.SetEnvironmentVariable("ServiceBusConnectionString", null);

        try
        {
            // Act
            var service = new ServiceBusDeadLetterMonitoringService(_loggerMock.Object);

            // Assert
            Assert.NotNull(service);
        }
        finally
        {
            // Restore
            if (originalValue != null)
            {
                Environment.SetEnvironmentVariable("ServiceBusConnectionString", originalValue);
            }
        }
    }

    // Note: Full integration tests would require actual Service Bus connection
    // These tests verify the service can be instantiated and basic structure
}


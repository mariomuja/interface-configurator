using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessCsvBlobTrigger;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using Xunit;

namespace ProcessCsvBlobTrigger.Core.Tests.Functions;

/// <summary>
/// Unit tests for DestinationAdapterFunction
/// Tests timer-triggered destination adapter processing
/// </summary>
public class DestinationAdapterFunctionTests
{
    private readonly Mock<IInterfaceConfigurationService> _mockConfigService;
    private readonly Mock<IAdapterFactory> _mockAdapterFactory;
    private readonly Mock<IMessageBoxService> _mockMessageBoxService;
    private readonly Mock<ILogger<DestinationAdapterFunction>> _mockLogger;
    private readonly Mock<IAdapter> _mockAdapter;
    private readonly DestinationAdapterFunction _function;

    public DestinationAdapterFunctionTests()
    {
        _mockConfigService = new Mock<IInterfaceConfigurationService>();
        _mockAdapterFactory = new Mock<IAdapterFactory>();
        _mockMessageBoxService = new Mock<IMessageBoxService>();
        _mockLogger = new Mock<ILogger<DestinationAdapterFunction>>();
        _mockAdapter = new Mock<IAdapter>();

        _function = new DestinationAdapterFunction(
            _mockConfigService.Object,
            _mockAdapterFactory.Object,
            _mockMessageBoxService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Run_WithNoEnabledConfigurations_ShouldSkipProcessing()
    {
        // Arrange
        _mockConfigService
            .Setup(x => x.GetEnabledDestinationConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration>());

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _function.Run(timerInfo.Object, functionContext.Object);

        // Assert
        _mockAdapterFactory.Verify(x => x.CreateDestinationAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_WithNoDestinationInstances_ShouldSkipProcessing()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            DestinationAdapterName = "CSV",
            DestinationIsEnabled = true
        };

        _mockConfigService
            .Setup(x => x.GetEnabledDestinationConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration> { config });

        _mockConfigService
            .Setup(x => x.GetDestinationAdapterInstancesAsync(config.InterfaceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DestinationAdapterInstance>());

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _function.Run(timerInfo.Object, functionContext.Object);

        // Assert
        _mockAdapterFactory.Verify(x => x.CreateDestinationAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_WithEnabledDestinationInstance_ShouldProcessDestination()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            DestinationAdapterName = "CSV",
            DestinationIsEnabled = true
        };

        var instance = new DestinationAdapterInstance
        {
            AdapterInstanceGuid = Guid.NewGuid(),
            InstanceName = "Destination1",
            AdapterName = "CSV",
            IsEnabled = true,
            Configuration = "{\"destination\": \"csv-files/csv-outgoing\"}"
        };

        var messages = new List<MessageBoxMessage>
        {
            new MessageBoxMessage
            {
                MessageId = Guid.NewGuid(),
                InterfaceName = "TestInterface",
                Status = "Pending"
            }
        };

        _mockConfigService
            .Setup(x => x.GetEnabledDestinationConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration> { config });

        _mockConfigService
            .Setup(x => x.GetDestinationAdapterInstancesAsync(config.InterfaceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DestinationAdapterInstance> { instance });

        _mockMessageBoxService
            .Setup(x => x.ReadMessagesAsync(config.InterfaceName, "Pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        _mockAdapter.Setup(x => x.AdapterName).Returns("CSV");
        _mockAdapter.Setup(x => x.AdapterAlias).Returns("CSV");
        _mockAdapter.Setup(x => x.SupportsWrite).Returns(true);
        _mockAdapter
            .Setup(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<List<Dictionary<string, string>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockAdapterFactory
            .Setup(x => x.CreateDestinationAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockAdapter.Object);

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _function.Run(timerInfo.Object, functionContext.Object);

        // Assert
        _mockAdapterFactory.Verify(x => x.CreateDestinationAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockAdapter.Verify(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<List<Dictionary<string, string>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_WithDisabledDestinationInstance_ShouldSkipInstance()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            DestinationAdapterName = "CSV",
            DestinationIsEnabled = true
        };

        var instance = new DestinationAdapterInstance
        {
            AdapterInstanceGuid = Guid.NewGuid(),
            InstanceName = "Destination1",
            AdapterName = "CSV",
            IsEnabled = false, // Disabled
            Configuration = "{\"destination\": \"csv-files/csv-outgoing\"}"
        };

        _mockConfigService
            .Setup(x => x.GetEnabledDestinationConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration> { config });

        _mockConfigService
            .Setup(x => x.GetDestinationAdapterInstancesAsync(config.InterfaceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DestinationAdapterInstance> { instance });

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _function.Run(timerInfo.Object, functionContext.Object);

        // Assert
        _mockAdapterFactory.Verify(x => x.CreateDestinationAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_WithNoPendingMessages_ShouldSkipProcessing()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            DestinationAdapterName = "CSV",
            DestinationIsEnabled = true
        };

        var instance = new DestinationAdapterInstance
        {
            AdapterInstanceGuid = Guid.NewGuid(),
            InstanceName = "Destination1",
            AdapterName = "CSV",
            IsEnabled = true,
            Configuration = "{\"destination\": \"csv-files/csv-outgoing\"}"
        };

        _mockConfigService
            .Setup(x => x.GetEnabledDestinationConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration> { config });

        _mockConfigService
            .Setup(x => x.GetDestinationAdapterInstancesAsync(config.InterfaceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DestinationAdapterInstance> { instance });

        _mockMessageBoxService
            .Setup(x => x.ReadMessagesAsync(config.InterfaceName, "Pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MessageBoxMessage>()); // No messages

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _function.Run(timerInfo.Object, functionContext.Object);

        // Assert
        _mockAdapterFactory.Verify(x => x.CreateDestinationAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_WithAdapterNotSupportingWrite_ShouldThrowNotSupportedException()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            DestinationAdapterName = "CSV",
            DestinationIsEnabled = true
        };

        var instance = new DestinationAdapterInstance
        {
            AdapterInstanceGuid = Guid.NewGuid(),
            InstanceName = "Destination1",
            AdapterName = "CSV",
            IsEnabled = true,
            Configuration = "{\"destination\": \"csv-files/csv-outgoing\"}"
        };

        var messages = new List<MessageBoxMessage>
        {
            new MessageBoxMessage
            {
                MessageId = Guid.NewGuid(),
                InterfaceName = "TestInterface",
                Status = "Pending"
            }
        };

        _mockConfigService
            .Setup(x => x.GetEnabledDestinationConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration> { config });

        _mockConfigService
            .Setup(x => x.GetDestinationAdapterInstancesAsync(config.InterfaceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DestinationAdapterInstance> { instance });

        _mockMessageBoxService
            .Setup(x => x.ReadMessagesAsync(config.InterfaceName, "Pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        _mockAdapter.Setup(x => x.AdapterName).Returns("CSV");
        _mockAdapter.Setup(x => x.AdapterAlias).Returns("CSV");
        _mockAdapter.Setup(x => x.SupportsWrite).Returns(false); // Does not support write

        _mockAdapterFactory
            .Setup(x => x.CreateDestinationAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockAdapter.Object);

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => 
            _function.Run(timerInfo.Object, functionContext.Object));
    }

    [Fact]
    public async Task Run_WithMultipleDestinationInstances_ShouldProcessAll()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            DestinationAdapterName = "CSV",
            DestinationIsEnabled = true
        };

        var instance1 = new DestinationAdapterInstance
        {
            AdapterInstanceGuid = Guid.NewGuid(),
            InstanceName = "Destination1",
            AdapterName = "CSV",
            IsEnabled = true,
            Configuration = "{\"destination\": \"csv-files/csv-outgoing\"}"
        };

        var instance2 = new DestinationAdapterInstance
        {
            AdapterInstanceGuid = Guid.NewGuid(),
            InstanceName = "Destination2",
            AdapterName = "CSV",
            IsEnabled = true,
            Configuration = "{\"destination\": \"csv-files/csv-outgoing2\"}"
        };

        var messages = new List<MessageBoxMessage>
        {
            new MessageBoxMessage
            {
                MessageId = Guid.NewGuid(),
                InterfaceName = "TestInterface",
                Status = "Pending"
            }
        };

        _mockConfigService
            .Setup(x => x.GetEnabledDestinationConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration> { config });

        _mockConfigService
            .Setup(x => x.GetDestinationAdapterInstancesAsync(config.InterfaceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DestinationAdapterInstance> { instance1, instance2 });

        _mockMessageBoxService
            .Setup(x => x.ReadMessagesAsync(config.InterfaceName, "Pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        _mockAdapter.Setup(x => x.AdapterName).Returns("CSV");
        _mockAdapter.Setup(x => x.AdapterAlias).Returns("CSV");
        _mockAdapter.Setup(x => x.SupportsWrite).Returns(true);
        _mockAdapter
            .Setup(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<List<Dictionary<string, string>>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockAdapterFactory
            .Setup(x => x.CreateDestinationAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockAdapter.Object);

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _function.Run(timerInfo.Object, functionContext.Object);

        // Assert
        _mockAdapterFactory.Verify(x => x.CreateDestinationAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockAdapter.Verify(x => x.WriteAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<List<Dictionary<string, string>>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}




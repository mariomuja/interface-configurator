using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Functions;

/// <summary>
/// Unit tests for SourceAdapterFunction
/// Tests timer-triggered source adapter processing
/// </summary>
public class SourceAdapterFunctionTests
{
    private readonly Mock<IInterfaceConfigurationService> _mockConfigService;
    private readonly Mock<IAdapterFactory> _mockAdapterFactory;
    private readonly Mock<ILogger<SourceAdapterFunction>> _mockLogger;
    private readonly Mock<IAdapter> _mockAdapter;
    private readonly SourceAdapterFunction _function;

    public SourceAdapterFunctionTests()
    {
        _mockConfigService = new Mock<IInterfaceConfigurationService>();
        _mockAdapterFactory = new Mock<IAdapterFactory>();
        _mockLogger = new Mock<ILogger<SourceAdapterFunction>>();
        _mockAdapter = new Mock<IAdapter>();

        _function = new SourceAdapterFunction(
            _mockConfigService.Object,
            _mockAdapterFactory.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Run_WithNoEnabledConfigurations_ShouldSkipProcessing()
    {
        // Arrange
        _mockConfigService
            .Setup(x => x.GetEnabledSourceConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration>());

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _function.Run(timerInfo.Object, functionContext.Object);

        // Assert
        _mockAdapterFactory.Verify(x => x.CreateSourceAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_WithEnabledConfiguration_ShouldProcessSource()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "CSV",
            SourceConfiguration = "{\"source\": \"csv-files/csv-incoming\"}",
            SourceIsEnabled = true
        };

        var headers = new List<string> { "Name", "Age" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" }, { "Age", "30" } }
        };

        _mockConfigService
            .Setup(x => x.GetEnabledSourceConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration> { config });

        _mockAdapter.Setup(x => x.AdapterName).Returns("CSV");
        _mockAdapter.Setup(x => x.AdapterAlias).Returns("CSV");
        _mockAdapter.Setup(x => x.SupportsRead).Returns(true);
        _mockAdapter
            .Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((headers, records));

        _mockAdapterFactory
            .Setup(x => x.CreateSourceAdapterAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockAdapter.Object);

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _function.Run(timerInfo.Object, functionContext.Object);

        // Assert
        _mockAdapterFactory.Verify(x => x.CreateSourceAdapterAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        _mockAdapter.Verify(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_WithDisabledConfiguration_ShouldSkipProcessing()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "CSV",
            SourceConfiguration = "{\"source\": \"csv-files/csv-incoming\"}",
            SourceIsEnabled = false
        };

        _mockConfigService
            .Setup(x => x.GetEnabledSourceConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration>());

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _function.Run(timerInfo.Object, functionContext.Object);

        // Assert
        _mockAdapterFactory.Verify(x => x.CreateSourceAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_WithAdapterNotSupportingRead_ShouldThrowNotSupportedException()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "CSV",
            SourceConfiguration = "{\"source\": \"csv-files/csv-incoming\"}",
            SourceIsEnabled = true
        };

        _mockConfigService
            .Setup(x => x.GetEnabledSourceConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration> { config });

        _mockAdapter.Setup(x => x.AdapterName).Returns("CSV");
        _mockAdapter.Setup(x => x.AdapterAlias).Returns("CSV");
        _mockAdapter.Setup(x => x.SupportsRead).Returns(false);

        _mockAdapterFactory
            .Setup(x => x.CreateSourceAdapterAsync(config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockAdapter.Object);

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => 
            _function.Run(timerInfo.Object, functionContext.Object));
    }

    [Fact]
    public async Task Run_WithMultipleConfigurations_ShouldProcessAll()
    {
        // Arrange
        var config1 = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface1",
            SourceAdapterName = "CSV",
            SourceConfiguration = "{\"source\": \"csv-files/csv-incoming\"}",
            SourceIsEnabled = true
        };

        var config2 = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface2",
            SourceAdapterName = "SqlServer",
            SourceConfiguration = "{\"source\": \"TransportData\"}",
            SourceIsEnabled = true
        };

        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>>();

        _mockConfigService
            .Setup(x => x.GetEnabledSourceConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration> { config1, config2 });

        _mockAdapter.Setup(x => x.AdapterName).Returns("CSV");
        _mockAdapter.Setup(x => x.AdapterAlias).Returns("CSV");
        _mockAdapter.Setup(x => x.SupportsRead).Returns(true);
        _mockAdapter
            .Setup(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((headers, records));

        _mockAdapterFactory
            .Setup(x => x.CreateSourceAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockAdapter.Object);

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _function.Run(timerInfo.Object, functionContext.Object);

        // Assert
        _mockAdapterFactory.Verify(x => x.CreateSourceAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Run_WithErrorInOneConfiguration_ShouldContinueWithOthers()
    {
        // Arrange
        var config1 = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface1",
            SourceAdapterName = "CSV",
            SourceConfiguration = "{\"source\": \"csv-files/csv-incoming\"}",
            SourceIsEnabled = true
        };

        var config2 = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface2",
            SourceAdapterName = "CSV",
            SourceConfiguration = "{\"source\": \"csv-files/csv-incoming\"}",
            SourceIsEnabled = true
        };

        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>>();

        _mockConfigService
            .Setup(x => x.GetEnabledSourceConfigurationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InterfaceConfiguration> { config1, config2 });

        _mockAdapter.Setup(x => x.AdapterName).Returns("CSV");
        _mockAdapter.Setup(x => x.AdapterAlias).Returns("CSV");
        _mockAdapter.Setup(x => x.SupportsRead).Returns(true);
        _mockAdapter
            .SetupSequence(x => x.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test error"))
            .ReturnsAsync((headers, records));

        _mockAdapterFactory
            .Setup(x => x.CreateSourceAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockAdapter.Object);

        var timerInfo = new Mock<TimerInfo>();
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await _function.Run(timerInfo.Object, functionContext.Object);

        // Assert
        _mockAdapterFactory.Verify(x => x.CreateSourceAdapterAsync(It.IsAny<InterfaceConfiguration>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}




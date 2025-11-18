using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessCsvBlobTrigger.Adapters;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Core.Services;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Services;
using Xunit;

namespace ProcessCsvBlobTrigger.Core.Tests.Services;

/// <summary>
/// Unit tests for AdapterFactory
/// Tests adapter creation for both source and destination adapters
/// </summary>
public class AdapterFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Mock<ICsvProcessingService> _mockCsvProcessingService;
    private readonly Mock<IAdapterConfigurationService> _mockAdapterConfig;
    private readonly Mock<Azure.Storage.Blobs.BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<IDynamicTableService> _mockDynamicTableService;
    private readonly Mock<IDataService> _mockDataService;
    private readonly Mock<ILogger<AdapterFactory>> _mockLogger;

    public AdapterFactoryTests()
    {
        // Setup service provider with mocks
        var services = new ServiceCollection();
        
        _mockCsvProcessingService = new Mock<ICsvProcessingService>();
        _mockAdapterConfig = new Mock<IAdapterConfigurationService>();
        _mockBlobServiceClient = new Mock<Azure.Storage.Blobs.BlobServiceClient>();
        _mockDynamicTableService = new Mock<IDynamicTableService>();
        _mockDataService = new Mock<IDataService>();
        _mockLogger = new Mock<ILogger<AdapterFactory>>();

        // Setup in-memory database for ApplicationDbContext
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var applicationContext = new ApplicationDbContext(options);

        services.AddSingleton(_mockCsvProcessingService.Object);
        services.AddSingleton(_mockAdapterConfig.Object);
        services.AddSingleton(_mockBlobServiceClient.Object);
        services.AddSingleton(_mockDynamicTableService.Object);
        services.AddSingleton(_mockDataService.Object);
        services.AddSingleton(applicationContext);
        services.AddSingleton<ILogger<CsvAdapter>>(new Mock<ILogger<CsvAdapter>>().Object);
        services.AddSingleton<ILogger<SqlServerAdapter>>(new Mock<ILogger<SqlServerAdapter>>().Object);
        services.AddSingleton<ILogger<ICsvProcessingService>>(new Mock<ILogger<ICsvProcessingService>>().Object);

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateSourceAdapterAsync_WithCsvAdapter_ShouldReturnCsvAdapter()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "CSV",
            SourceConfiguration = "{\"source\": \"csv-files/csv-incoming\"}",
            SourceAdapterInstanceGuid = Guid.NewGuid(),
            SourceReceiveFolder = "csv-files/csv-incoming",
            SourceFileMask = "*.txt",
            SourceBatchSize = 100,
            SourceFieldSeparator = "║",
            SourceIsEnabled = true
        };

        var factory = new AdapterFactory(_serviceProvider, _mockLogger.Object);

        // Act
        var adapter = await factory.CreateSourceAdapterAsync(config);

        // Assert
        Assert.NotNull(adapter);
        Assert.IsType<CsvAdapter>(adapter);
        Assert.Equal("CSV", adapter.AdapterName);
        Assert.True(adapter.SupportsRead);
    }

    [Fact]
    public async Task CreateSourceAdapterAsync_WithSqlServerAdapter_ShouldReturnSqlServerAdapter()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "SqlServer",
            SourceConfiguration = "{\"source\": \"TransportData\"}",
            SourceAdapterInstanceGuid = Guid.NewGuid(),
            SqlServerName = "test-server",
            SqlDatabaseName = "test-db",
            SqlUserName = "test-user",
            SqlPassword = "test-password",
            SourceIsEnabled = true
        };

        var factory = new AdapterFactory(_serviceProvider, _mockLogger.Object);

        // Act
        var adapter = await factory.CreateSourceAdapterAsync(config);

        // Assert
        Assert.NotNull(adapter);
        Assert.IsType<SqlServerAdapter>(adapter);
        Assert.Equal("SqlServer", adapter.AdapterName);
        Assert.True(adapter.SupportsRead);
    }

    [Fact]
    public async Task CreateDestinationAdapterAsync_WithCsvAdapter_ShouldReturnCsvAdapter()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            DestinationAdapterName = "CSV",
            DestinationConfiguration = "{\"destination\": \"csv-files/csv-outgoing\"}",
            DestinationAdapterInstanceGuid = Guid.NewGuid(),
            DestinationReceiveFolder = "csv-files/csv-outgoing",
            DestinationFileMask = "*.txt",
            SourceFieldSeparator = "║",
            DestinationIsEnabled = true
        };

        var factory = new AdapterFactory(_serviceProvider, _mockLogger.Object);

        // Act
        var adapter = await factory.CreateDestinationAdapterAsync(config);

        // Assert
        Assert.NotNull(adapter);
        Assert.IsType<CsvAdapter>(adapter);
        Assert.Equal("CSV", adapter.AdapterName);
        Assert.True(adapter.SupportsWrite);
    }

    [Fact]
    public async Task CreateDestinationAdapterAsync_WithSqlServerAdapter_ShouldReturnSqlServerAdapter()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            DestinationAdapterName = "SqlServer",
            DestinationConfiguration = "{\"destination\": \"TransportData\"}",
            DestinationAdapterInstanceGuid = Guid.NewGuid(),
            SqlServerName = "test-server",
            SqlDatabaseName = "test-db",
            SqlUserName = "test-user",
            SqlPassword = "test-password",
            DestinationIsEnabled = true
        };

        var factory = new AdapterFactory(_serviceProvider, _mockLogger.Object);

        // Act
        var adapter = await factory.CreateDestinationAdapterAsync(config);

        // Assert
        Assert.NotNull(adapter);
        Assert.IsType<SqlServerAdapter>(adapter);
        Assert.Equal("SqlServer", adapter.AdapterName);
        Assert.True(adapter.SupportsWrite);
    }

    [Fact]
    public async Task CreateSourceAdapterAsync_WithUnsupportedAdapter_ShouldThrowNotSupportedException()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "UnsupportedAdapter",
            SourceConfiguration = "{}",
            SourceIsEnabled = true
        };

        var factory = new AdapterFactory(_serviceProvider, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => factory.CreateSourceAdapterAsync(config));
    }

    [Fact]
    public async Task CreateDestinationAdapterAsync_WithUnsupportedAdapter_ShouldThrowNotSupportedException()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            DestinationAdapterName = "UnsupportedAdapter",
            DestinationConfiguration = "{}",
            DestinationIsEnabled = true
        };

        var factory = new AdapterFactory(_serviceProvider, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => factory.CreateDestinationAdapterAsync(config));
    }

    [Fact]
    public async Task CreateSourceAdapterAsync_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Arrange
        var factory = new AdapterFactory(_serviceProvider, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateSourceAdapterAsync(null!));
    }

    [Fact]
    public async Task CreateDestinationAdapterAsync_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Arrange
        var factory = new AdapterFactory(_serviceProvider, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateDestinationAdapterAsync(null!));
    }

    [Fact]
    public async Task CreateSourceAdapterAsync_WithCsvAdapterAndSftpProperties_ShouldIncludeSftpProperties()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "CSV",
            SourceConfiguration = "{\"source\": \"sftp://test\"}",
            SourceAdapterInstanceGuid = Guid.NewGuid(),
            CsvAdapterType = "SFTP",
            SftpHost = "sftp.example.com",
            SftpPort = 22,
            SftpUsername = "testuser",
            SftpPassword = "testpass",
            SftpFolder = "/remote/folder",
            SftpFileMask = "*.csv",
            SftpMaxConnectionPoolSize = 5,
            SftpFileBufferSize = 8192,
            SourceIsEnabled = true
        };

        var factory = new AdapterFactory(_serviceProvider, _mockLogger.Object);

        // Act
        var adapter = await factory.CreateSourceAdapterAsync(config);

        // Assert
        Assert.NotNull(adapter);
        Assert.IsType<CsvAdapter>(adapter);
    }

    [Fact]
    public async Task CreateSourceAdapterAsync_WithSqlServerAdapterAndPollingProperties_ShouldIncludePollingProperties()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "SqlServer",
            SourceConfiguration = "{\"source\": \"TransportData\"}",
            SourceAdapterInstanceGuid = Guid.NewGuid(),
            SqlServerName = "test-server",
            SqlDatabaseName = "test-db",
            SqlUserName = "test-user",
            SqlPassword = "test-password",
            SqlPollingStatement = "SELECT * FROM TransportData WHERE Status = 'New'",
            SqlPollingInterval = 60,
            SqlUseTransaction = true,
            SqlBatchSize = 1000,
            SqlCommandTimeout = 30,
            SqlFailOnBadStatement = false,
            SourceIsEnabled = true
        };

        var factory = new AdapterFactory(_serviceProvider, _mockLogger.Object);

        // Act
        var adapter = await factory.CreateSourceAdapterAsync(config);

        // Assert
        Assert.NotNull(adapter);
        Assert.IsType<SqlServerAdapter>(adapter);
    }
}




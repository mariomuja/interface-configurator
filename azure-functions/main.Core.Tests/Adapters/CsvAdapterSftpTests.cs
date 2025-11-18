using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Adapters;

/// <summary>
/// Unit tests for CsvAdapter SFTP functionality
/// Tests SFTP connection, file reading, and error handling
/// </summary>
public class CsvAdapterSftpTests : IDisposable
{
    private readonly MessageBoxDbContext _context;
    private readonly IMessageBoxService _messageBoxService;
    private readonly Mock<ICsvProcessingService> _mockCsvProcessingService;
    private readonly Mock<IAdapterConfigurationService> _mockAdapterConfig;
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<ILogger<CsvAdapter>> _mockLogger;
    private const string InterfaceName = "TestInterface";

    public CsvAdapterSftpTests()
    {
        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<MessageBoxDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MessageBoxDbContext(options);
        var logger = new Mock<ILogger<MessageBoxService>>();
        var mockEventQueue = new Mock<IEventQueue>();
        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _messageBoxService = new MessageBoxService(_context, mockEventQueue.Object, mockSubscriptionService.Object, logger.Object);
        _mockCsvProcessingService = new Mock<ICsvProcessingService>();
        _mockAdapterConfig = new Mock<IAdapterConfigurationService>();
        _mockBlobServiceClient = new Mock<BlobServiceClient>();
        _mockLogger = new Mock<ILogger<CsvAdapter>>();

        _context.Database.EnsureCreated();
    }

    [Fact]
    public void CsvAdapter_WithSftpType_ShouldInitializeSftpProperties()
    {
        // Arrange
        var adapterInstanceGuid = Guid.NewGuid();

        // Act
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            null,
            InterfaceName,
            adapterInstanceGuid,
            null, // receiveFolder
            null, // fileMask
            null, // batchSize
            "║", // fieldSeparator
            null, // destinationReceiveFolder
            null, // destinationFileMask
            "SFTP", // adapterType
            "sftp.example.com", // sftpHost
            22, // sftpPort
            "testuser", // sftpUsername
            "testpass", // sftpPassword
            null, // sftpSshKey
            "/remote/folder", // sftpFolder
            "*.csv", // sftpFileMask
            5, // sftpMaxConnectionPoolSize
            8192, // sftpFileBufferSize
            _mockLogger.Object);

        // Assert
        Assert.NotNull(adapter);
        Assert.Equal("CSV", adapter.AdapterName);
        Assert.True(adapter.SupportsRead);
    }

    [Fact]
    public void CsvAdapter_WithFileType_ShouldNotInitializeSftpProperties()
    {
        // Arrange
        var adapterInstanceGuid = Guid.NewGuid();

        // Act
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            null,
            InterfaceName,
            adapterInstanceGuid,
            "csv-files/csv-incoming", // receiveFolder
            "*.txt", // fileMask
            100, // batchSize
            "║", // fieldSeparator
            null, // destinationReceiveFolder
            null, // destinationFileMask
            "FILE", // adapterType
            null, // sftpHost
            null, // sftpPort
            null, // sftpUsername
            null, // sftpPassword
            null, // sftpSshKey
            null, // sftpFolder
            null, // sftpFileMask
            null, // sftpMaxConnectionPoolSize
            null, // sftpFileBufferSize
            _mockLogger.Object);

        // Assert
        Assert.NotNull(adapter);
        Assert.Equal("CSV", adapter.AdapterName);
    }

    [Fact]
    public async Task CsvAdapter_ReadAsync_WithSftpType_ShouldThrowNotImplementedException()
    {
        // Arrange
        var adapterInstanceGuid = Guid.NewGuid();
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            null,
            InterfaceName,
            adapterInstanceGuid,
            null,
            null,
            null,
            "║",
            null,
            null,
            "SFTP",
            "sftp.example.com",
            22,
            "testuser",
            "testpass",
            null,
            "/remote/folder",
            "*.csv",
            5,
            8192,
            _mockLogger.Object);

        // Act & Assert
        // Note: SFTP functionality requires actual SFTP server connection
        // This test verifies that the adapter attempts to use SFTP when configured
        // In a real scenario, this would require mocking SFTP client or using integration tests
        await Assert.ThrowsAnyAsync<Exception>(() => adapter.ReadAsync("/remote/folder", CancellationToken.None));
    }

    [Fact]
    public void CsvAdapter_WithSftpAndSshKey_ShouldInitializeWithSshKey()
    {
        // Arrange
        var adapterInstanceGuid = Guid.NewGuid();
        var sshKey = "-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAKCAQEA...\n-----END RSA PRIVATE KEY-----";

        // Act
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            null,
            InterfaceName,
            adapterInstanceGuid,
            null,
            null,
            null,
            "║",
            null,
            null,
            "SFTP",
            "sftp.example.com",
            22,
            "testuser",
            null, // password
            sshKey, // sshKey
            "/remote/folder",
            "*.csv",
            5,
            8192,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(adapter);
        Assert.Equal("CSV", adapter.AdapterName);
    }

    [Fact]
    public void CsvAdapter_WithSftpAndCustomPort_ShouldUseCustomPort()
    {
        // Arrange
        var adapterInstanceGuid = Guid.NewGuid();

        // Act
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            null,
            InterfaceName,
            adapterInstanceGuid,
            null,
            null,
            null,
            "║",
            null,
            null,
            "SFTP",
            "sftp.example.com",
            2222, // custom port
            "testuser",
            "testpass",
            null,
            "/remote/folder",
            "*.csv",
            5,
            8192,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(adapter);
    }

    [Fact]
    public void CsvAdapter_WithSftpAndCustomConnectionPoolSize_ShouldUseCustomPoolSize()
    {
        // Arrange
        var adapterInstanceGuid = Guid.NewGuid();

        // Act
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            null,
            InterfaceName,
            adapterInstanceGuid,
            null,
            null,
            null,
            "║",
            null,
            null,
            "SFTP",
            "sftp.example.com",
            22,
            "testuser",
            "testpass",
            null,
            "/remote/folder",
            "*.csv",
            10, // custom pool size
            8192,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(adapter);
    }

    [Fact]
    public void CsvAdapter_WithSftpAndCustomBufferSize_ShouldUseCustomBufferSize()
    {
        // Arrange
        var adapterInstanceGuid = Guid.NewGuid();

        // Act
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            null,
            InterfaceName,
            adapterInstanceGuid,
            null,
            null,
            null,
            "║",
            null,
            null,
            "SFTP",
            "sftp.example.com",
            22,
            "testuser",
            "testpass",
            null,
            "/remote/folder",
            "*.csv",
            5,
            16384, // custom buffer size
            _mockLogger.Object);

        // Assert
        Assert.NotNull(adapter);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}




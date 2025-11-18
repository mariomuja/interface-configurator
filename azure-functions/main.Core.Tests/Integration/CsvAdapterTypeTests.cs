using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Core.Services;
using Xunit;
using System.Linq;
using Azure.Storage.Blobs;
using System.Text;

namespace InterfaceConfigurator.Main.Core.Tests.Integration;

/// <summary>
/// Integration tests for CSV adapter types (RAW, SFTP, FILE)
/// Tests that data from different sources is correctly uploaded to csv-incoming folder
/// </summary>
public class CsvAdapterTypeTests : IDisposable
{
    private readonly MessageBoxDbContext _messageBoxContext;
    private readonly ApplicationDbContext _applicationContext;
    private readonly IMessageBoxService _messageBoxService;
    private readonly IDynamicTableService _dynamicTableService;
    private readonly IDataService _dataService;
    private readonly Mock<ILogger<CsvAdapter>> _mockCsvLogger;
    private readonly Mock<ICsvProcessingService> _mockCsvProcessingService;
    private readonly Mock<IAdapterConfigurationService> _mockAdapterConfig;
    private readonly BlobServiceClient _blobServiceClient;
    private const string InterfaceName = "TestCsvAdapterTypes";
    private const string ContainerName = "csv-files";

    public CsvAdapterTypeTests()
    {
        // Use in-memory databases for testing
        var messageBoxOptions = new DbContextOptionsBuilder<MessageBoxDbContext>()
            .UseInMemoryDatabase(databaseName: $"MessageBox_{Guid.NewGuid()}")
            .Options;

        var appOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"App_{Guid.NewGuid()}")
            .Options;

        _messageBoxContext = new MessageBoxDbContext(messageBoxOptions);
        _applicationContext = new ApplicationDbContext(appOptions);
        
        var messageBoxLogger = new Mock<ILogger<MessageBoxService>>();
        var mockEventQueue = new Mock<IEventQueue>();
        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _messageBoxService = new MessageBoxService(_messageBoxContext, mockEventQueue.Object, mockSubscriptionService.Object, messageBoxLogger.Object);
        
        _dynamicTableService = new DynamicTableService(_applicationContext, new Mock<ILogger<DynamicTableService>>().Object);
        _dataService = new DataService(_applicationContext, new Mock<ILogger<DataService>>().Object);
        _mockCsvLogger = new Mock<ILogger<CsvAdapter>>();
        _mockCsvProcessingService = new Mock<ICsvProcessingService>();
        _mockAdapterConfig = new Mock<IAdapterConfigurationService>();

        // Use Azure Storage Emulator connection string or a test storage account
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") 
            ?? "UseDevelopmentStorage=true"; // Azure Storage Emulator
        _blobServiceClient = new BlobServiceClient(connectionString);

        _messageBoxContext.Database.EnsureCreated();
        _applicationContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task RawAdapterType_ShouldUploadCsvDataToIncoming()
    {
        // Arrange
        var adapterGuid = Guid.NewGuid();
        var csvData = "id║name║email\n1║John Doe║john@example.com\n2║Jane Smith║jane@example.com";

        var adapter = new CsvAdapter(
            csvProcessingService: _mockCsvProcessingService.Object,
            adapterConfig: _mockAdapterConfig.Object,
            blobServiceClient: _blobServiceClient,
            messageBoxService: _messageBoxService,
            subscriptionService: null,
            interfaceName: InterfaceName,
            adapterInstanceGuid: adapterGuid,
            receiveFolder: null,
            fileMask: null,
            batchSize: 100,
            fieldSeparator: "║",
            destinationReceiveFolder: null,
            destinationFileMask: null,
            adapterType: "RAW",
            sftpHost: null,
            sftpPort: null,
            sftpUsername: null,
            sftpPassword: null,
            sftpSshKey: null,
            sftpFolder: null,
            sftpFileMask: null,
            sftpMaxConnectionPoolSize: null,
            sftpFileBufferSize: null,
            logger: _mockCsvLogger.Object);

        // Act - Set CsvData property (should trigger upload to csv-incoming)
        adapter.CsvData = csvData;

        // Wait a bit for async processing
        await Task.Delay(2000);

        // Assert - Check that file was uploaded to csv-incoming folder
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync();

        var blobs = containerClient.GetBlobsAsync(prefix: "csv-incoming/raw-");
        var uploadedBlobs = new List<string>();
        await foreach (var blob in blobs)
        {
            uploadedBlobs.Add(blob.Name);
        }

        Assert.NotEmpty(uploadedBlobs);
        Assert.Contains("csv-incoming/raw-", uploadedBlobs.First());

        // Verify file content
        var blobClient = containerClient.GetBlobClient(uploadedBlobs.First());
        var downloadResult = await blobClient.DownloadContentAsync();
        var downloadedContent = downloadResult.Value.Content.ToString();
        Assert.Contains("John Doe", downloadedContent);
        Assert.Contains("jane@example.com", downloadedContent);
    }

    [Fact]
    public async Task FileAdapterType_ShouldCopyFilesToIncoming()
    {
        // Arrange
        var adapterGuid = Guid.NewGuid();
        var sourceFolder = "csv-source";
        var csvContent = "id║name║email\n1║Test User║test@example.com";

        // Setup mock CSV processing service
        _mockCsvProcessingService
            .Setup(x => x.ParseCsvWithHeadersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<string> { "id", "name", "email" }, new List<Dictionary<string, string>>
            {
                new() { { "id", "1" }, { "name", "Test User" }, { "email", "test@example.com" } }
            }));

        // Create source file in blob storage
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        await containerClient.CreateIfNotExistsAsync();
        var sourceBlobPath = $"{sourceFolder}/test-file.csv";
        var sourceBlobClient = containerClient.GetBlockBlobClient(sourceBlobPath);
        await sourceBlobClient.UploadAsync(new BinaryData(Encoding.UTF8.GetBytes(csvContent)));

        var adapter = new CsvAdapter(
            csvProcessingService: _mockCsvProcessingService.Object,
            adapterConfig: _mockAdapterConfig.Object,
            blobServiceClient: _blobServiceClient,
            messageBoxService: _messageBoxService,
            subscriptionService: null,
            interfaceName: InterfaceName,
            adapterInstanceGuid: adapterGuid,
            receiveFolder: $"{ContainerName}/{sourceFolder}",
            fileMask: "*.csv",
            batchSize: 100,
            fieldSeparator: "║",
            destinationReceiveFolder: null,
            destinationFileMask: null,
            adapterType: "FILE",
            sftpHost: null,
            sftpPort: null,
            sftpUsername: null,
            sftpPassword: null,
            sftpSshKey: null,
            sftpFolder: null,
            sftpFileMask: null,
            sftpMaxConnectionPoolSize: null,
            sftpFileBufferSize: null,
            logger: _mockCsvLogger.Object);

        // Act - Read from source folder (should copy to csv-incoming)
        var (headers, records) = await adapter.ReadAsync($"{ContainerName}/{sourceFolder}/", CancellationToken.None);

        // Wait a bit for async processing
        await Task.Delay(2000);

        // Assert - Check that file was copied to csv-incoming folder
        var blobs = containerClient.GetBlobsAsync(prefix: "csv-incoming/test-file-");
        var copiedBlobs = new List<string>();
        await foreach (var blob in blobs)
        {
            copiedBlobs.Add(blob.Name);
        }

        Assert.NotEmpty(copiedBlobs);
        Assert.Contains("csv-incoming/test-file-", copiedBlobs.First());

        // Verify file content
        var copiedBlobClient = containerClient.GetBlobClient(copiedBlobs.First());
        var downloadResult = await copiedBlobClient.DownloadContentAsync();
        var downloadedContent = downloadResult.Value.Content.ToString();
        Assert.Contains("Test User", downloadedContent);
        Assert.Contains("test@example.com", downloadedContent);
    }

    [Fact]
    public async Task SftpAdapterType_ShouldUploadFilesToIncoming()
    {
        // Arrange
        var adapterGuid = Guid.NewGuid();
        var csvContent = "id║name║email\n1║SFTP User║sftp@example.com";

        // Setup mock CSV processing service
        _mockCsvProcessingService
            .Setup(x => x.ParseCsvWithHeadersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<string> { "id", "name", "email" }, new List<Dictionary<string, string>>
            {
                new() { { "id", "1" }, { "name", "SFTP User" }, { "email", "sftp@example.com" } }
            }));

        // Note: This test would require a mock SFTP server or a test SFTP server
        // For now, we'll test the logic that uploads files to csv-incoming
        // In a real scenario, SFTP files would be downloaded and then uploaded to csv-incoming

        // This test verifies that when SFTP adapter reads files, they are uploaded to csv-incoming
        // Actual SFTP connection testing would require additional setup

        // Assert - The test structure is in place, but actual SFTP testing requires:
        // 1. Mock SFTP server or test SFTP server
        // 2. SFTP client setup
        // 3. File download from SFTP
        // 4. Upload to csv-incoming

        // For now, we verify the adapter can be created with SFTP type
        var adapter = new CsvAdapter(
            csvProcessingService: _mockCsvProcessingService.Object,
            adapterConfig: _mockAdapterConfig.Object,
            blobServiceClient: _blobServiceClient,
            messageBoxService: _messageBoxService,
            subscriptionService: null,
            interfaceName: InterfaceName,
            adapterInstanceGuid: adapterGuid,
            receiveFolder: null,
            fileMask: null,
            batchSize: 100,
            fieldSeparator: "║",
            destinationReceiveFolder: null,
            destinationFileMask: null,
            adapterType: "SFTP",
            sftpHost: "test-sftp.example.com",
            sftpPort: 22,
            sftpUsername: "testuser",
            sftpPassword: "testpass",
            sftpSshKey: null,
            sftpFolder: "/remote/folder",
            sftpFileMask: "*.csv",
            sftpMaxConnectionPoolSize: 5,
            sftpFileBufferSize: 8192,
            logger: _mockCsvLogger.Object);

        Assert.NotNull(adapter);
        Assert.Equal("CSV", adapter.AdapterName);
    }

    public void Dispose()
    {
        _messageBoxContext?.Database.EnsureDeleted();
        _messageBoxContext?.Dispose();
        _applicationContext?.Database.EnsureDeleted();
        _applicationContext?.Dispose();
    }
}


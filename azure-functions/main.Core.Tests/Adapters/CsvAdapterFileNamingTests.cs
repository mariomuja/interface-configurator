using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using System.Text.RegularExpressions;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Adapters;

/// <summary>
/// Unit tests for CsvAdapter file naming format
/// Tests that files are named with format: transport-{year}_{month}_{day}_{hour}_{minute}_{second}_{milliseconds}.csv
/// </summary>
public class CsvAdapterFileNamingTests : IDisposable
{
    private readonly MessageBoxDbContext _context;
    private readonly IMessageBoxService _messageBoxService;
    private readonly Mock<ICsvProcessingService> _mockCsvProcessingService;
    private readonly Mock<IAdapterConfigurationService> _mockAdapterConfig;
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<ILogger<CsvAdapter>> _mockLogger;
    private readonly Mock<BlobContainerClient> _mockContainerClient;
    private readonly Mock<BlobClient> _mockBlobClient;
    private const string InterfaceName = "TestInterface";

    public CsvAdapterFileNamingTests()
    {
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
        _mockContainerClient = new Mock<BlobContainerClient>();
        _mockBlobClient = new Mock<BlobClient>();

        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CsvAdapter_UploadCsvDataToIncoming_ShouldUseCorrectFileNameFormat()
    {
        // Arrange
        var adapterInstanceGuid = Guid.NewGuid();
        var csvData = "id║name║email\n1║Test║test@test.com";
        var uploadedBlobPath = "";
        var uploadedFileName = "";

        _mockAdapterConfig
            .Setup(x => x.GetCsvFieldSeparatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("║");

        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_mockContainerClient.Object);

        _mockContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<Azure.Storage.Blobs.Models.PublicAccessType>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Azure.Response<Azure.Storage.Blobs.Models.BlobContainerInfo>>());

        _mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns((string path) =>
            {
                uploadedBlobPath = path;
                uploadedFileName = path.Split('/').Last();
                return _mockBlobClient.Object;
            });

        _mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<Azure.Storage.Blobs.Models.BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Azure.Response<Azure.Storage.Blobs.Models.BlobContentInfo>>());

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
            null, // fieldSeparator
            null, // destinationReceiveFolder
            null, // destinationFileMask
            "RAW", // adapterType
            null, // sftpHost
            null, // sftpPort
            null, // sftpUsername
            null, // sftpPassword
            null, // sftpSshKey
            null, // sftpFolder
            null, // sftpFileMask
            null, // sftpMaxConnectionPoolSize
            null, // sftpFileBufferSize
            _mockLogger.Object
        );

        // Act
        adapter.CsvData = csvData;
        await adapter.ReadAsync("", CancellationToken.None);

        // Assert
        Assert.True(!string.IsNullOrEmpty(uploadedBlobPath), "Blob path should not be empty");
        Assert.True(uploadedBlobPath.StartsWith("csv-incoming/"), "Blob path should start with csv-incoming/");
        
        // Verify filename format: transport-{year}_{month}_{day}_{hour}_{minute}_{second}_{milliseconds}.csv
        var fileNamePattern = @"^transport-\d{4}_\d{2}_\d{2}_\d{2}_\d{2}_\d{2}_\d{3}\.csv$";
        Assert.True(Regex.IsMatch(uploadedFileName, fileNamePattern), 
            $"Filename '{uploadedFileName}' should match pattern 'transport-YYYY_MM_DD_HH_mm_ss_fff.csv'");
        
        // Verify filename starts with "transport-"
        Assert.True(uploadedFileName.StartsWith("transport-"), "Filename should start with 'transport-'");
        
        // Verify filename ends with ".csv"
        Assert.True(uploadedFileName.EndsWith(".csv"), "Filename should end with '.csv'");
    }

    [Fact]
    public void CsvAdapter_FileNameFormat_ShouldContainDateTimeComponents()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var fileName = $"transport-{now:yyyy_MM_dd_HH_mm_ss_fff}.csv";

        // Assert
        Assert.Contains(now.Year.ToString(), fileName);
        Assert.Contains(now.Month.ToString("00"), fileName);
        Assert.Contains(now.Day.ToString("00"), fileName);
        Assert.Contains(now.Hour.ToString("00"), fileName);
        Assert.Contains(now.Minute.ToString("00"), fileName);
        Assert.Contains(now.Second.ToString("00"), fileName);
        Assert.Contains(now.Millisecond.ToString("000"), fileName);
    }

    [Fact]
    public void CsvAdapter_FileNameFormat_ShouldNotContainGuid()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var fileName = $"transport-{now:yyyy_MM_dd_HH_mm_ss_fff}.csv";
        var guidPattern = @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";

        // Assert
        Assert.False(Regex.IsMatch(fileName, guidPattern, RegexOptions.IgnoreCase), 
            "Filename should not contain GUID");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}


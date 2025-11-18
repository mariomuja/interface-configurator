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
/// Unit tests for CsvAdapter edge cases and error scenarios
/// </summary>
public class CsvAdapterEdgeCasesTests : IDisposable
{
    private readonly MessageBoxDbContext _context;
    private readonly IMessageBoxService _messageBoxService;
    private readonly Mock<ICsvProcessingService> _mockCsvProcessingService;
    private readonly Mock<IAdapterConfigurationService> _mockAdapterConfig;
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<ILogger<CsvAdapter>> _mockLogger;

    public CsvAdapterEdgeCasesTests()
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

        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CsvAdapter_ReadAsync_WithEmptySource_ShouldThrowArgumentException()
    {
        // Arrange
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "FILE",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.ReadAsync("", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.ReadAsync("   ", CancellationToken.None));
    }

    [Fact]
    public async Task CsvAdapter_ReadAsync_WithInvalidPathFormat_ShouldThrowArgumentException()
    {
        // Arrange
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "FILE",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.ReadAsync("invalid-path", CancellationToken.None));
    }

    [Fact]
    public async Task CsvAdapter_WriteAsync_WithEmptyDestination_ShouldThrowArgumentException()
    {
        // Arrange
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "FILE",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _mockLogger.Object);

        var headers = new List<string> { "Name", "Age" };
        var records = new List<Dictionary<string, string>>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.WriteAsync("", headers, records, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.WriteAsync("   ", headers, records, CancellationToken.None));
    }

    [Fact]
    public async Task CsvAdapter_WriteAsync_WithEmptyHeaders_ShouldThrowArgumentException()
    {
        // Arrange
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "FILE",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _mockLogger.Object);

        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.WriteAsync("csv-files/csv-outgoing", headers, records, CancellationToken.None));
    }

    [Fact]
    public async Task CsvAdapter_WriteAsync_WithNullHeaders_ShouldThrowArgumentException()
    {
        // Arrange
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "FILE",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _mockLogger.Object);

        var records = new List<Dictionary<string, string>>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.WriteAsync("csv-files/csv-outgoing", null!, records, CancellationToken.None));
    }

    [Fact]
    public void CsvAdapter_WithNullCsvProcessingService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CsvAdapter(
            null!,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "FILE",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _mockLogger.Object));
    }

    [Fact]
    public void CsvAdapter_WithNullAdapterConfig_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CsvAdapter(
            _mockCsvProcessingService.Object,
            null!,
            _mockBlobServiceClient.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "FILE",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _mockLogger.Object));
    }

    [Fact]
    public void CsvAdapter_WithNullBlobServiceClient_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            null!,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "FILE",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _mockLogger.Object));
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}




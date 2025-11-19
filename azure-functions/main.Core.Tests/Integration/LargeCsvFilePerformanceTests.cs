using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Adapters;
using Xunit;
using System.Diagnostics;
using System.Text;

namespace InterfaceConfigurator.Main.Core.Tests.Integration;

/// <summary>
/// Integration tests for large CSV file processing performance
/// Tests end-to-end performance with bulk inserts, streaming parsing, and parallel processing
/// </summary>
public class LargeCsvFilePerformanceTests : IDisposable
{
    private readonly MessageBoxDbContext _messageBoxContext;
    private readonly ApplicationDbContext _applicationContext;
    private readonly IMessageBoxService _messageBoxService;
    private readonly IDynamicTableService _dynamicTableService;
    private readonly IDataService _dataService;
    private readonly ICsvProcessingService _csvProcessingService;
    private readonly Mock<ILogger<SqlServerAdapter>> _mockSqlLogger;
    private readonly Mock<ILogger<CsvAdapter>> _mockCsvLogger;
    private readonly Mock<IAdapterConfigurationService> _mockAdapterConfig;
    private const string InterfaceName = "LargeCsvPerformanceTest";

    public LargeCsvFilePerformanceTests()
    {
        var messageBoxOptions = new DbContextOptionsBuilder<MessageBoxDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var appOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _messageBoxContext = new MessageBoxDbContext(messageBoxOptions);
        _applicationContext = new ApplicationDbContext(appOptions);
        
        var logger = new Mock<ILogger<MessageBoxService>>();
        var mockEventQueue = new Mock<IEventQueue>();
        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _messageBoxService = new MessageBoxService(_messageBoxContext, mockEventQueue.Object, mockSubscriptionService.Object, logger.Object);
        
        _dynamicTableService = new DynamicTableService(_applicationContext, new Mock<ILogger<DynamicTableService>>().Object);
        _dataService = new DataServiceAdapter(_applicationContext, new Mock<ILoggingService>().Object, new Mock<ILogger<DataServiceAdapter>>().Object);
        
        _mockAdapterConfig = new Mock<IAdapterConfigurationService>();
        _mockAdapterConfig.Setup(x => x.GetCsvFieldSeparatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("║");
        _csvProcessingService = new CsvProcessingService(_mockAdapterConfig.Object, new Mock<ILogger<CsvProcessingService>>().Object);
        
        _mockSqlLogger = new Mock<ILogger<SqlServerAdapter>>();
        _mockCsvLogger = new Mock<ILogger<CsvAdapter>>();

        _messageBoxContext.Database.EnsureCreated();
        _applicationContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task ProcessLargeCsvFile_EndToEnd_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        var csvContent = GenerateLargeCsvContent(10000); // 10,000 rows
        var adapterInstanceGuid = Guid.NewGuid();
        var mockBlobServiceClient = new Mock<Azure.Storage.Blobs.BlobServiceClient>();
        
        var csvAdapter = new CsvAdapter(
            _csvProcessingService,
            _mockAdapterConfig.Object,
            mockBlobServiceClient.Object,
            _messageBoxService,
            null,
            InterfaceName,
            adapterInstanceGuid,
            null,
            "*.txt",
            1000, // Batch size 1000
            "║",
            null,
            null,
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
            _mockCsvLogger.Object);
        csvAdapter.CsvData = csvContent; // Set CSV data via property

        var sqlAdapter = new SqlServerAdapter(
            _applicationContext,
            _dynamicTableService,
            _dataService,
            _messageBoxService,
            null,
            InterfaceName,
            adapterInstanceGuid,
            null, // connectionString
            null, // pollingStatement
            null, // pollingInterval
            "TransportData", // tableName
            null, // useTransaction
            null, // batchSize
            null, // commandTimeout
            null, // failOnBadStatement
            null, // configService
            _mockSqlLogger.Object,
            null); // statisticsService

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        // Read CSV and write to MessageBox
        var (headers, records) = await csvAdapter.ReadAsync("", CancellationToken.None);
        await csvAdapter.WriteAsync("TransportData", headers, records, CancellationToken.None);
        
        // Read from MessageBox and write to SQL
        var (sqlHeaders, sqlRecords) = await sqlAdapter.ReadAsync("TransportData", CancellationToken.None);
        await sqlAdapter.WriteAsync("TransportData", sqlHeaders, sqlRecords, CancellationToken.None);
        
        stopwatch.Stop();

        // Assert
        var insertedRows = await _applicationContext.TransportData.CountAsync();
        Assert.True(insertedRows >= 10000, $"Expected at least 10000 rows, got {insertedRows}");
        Assert.True(stopwatch.ElapsedMilliseconds < 60000, $"Processing took {stopwatch.ElapsedMilliseconds}ms, expected < 60000ms");
    }

    [Fact]
    public async Task ProcessVeryLargeCsvFile_WithBulkInsert_ShouldCompleteSuccessfully()
    {
        // Arrange
        var csvContent = GenerateLargeCsvContent(50000); // 50,000 rows
        var adapterInstanceGuid = Guid.NewGuid();
        
        var mockBlobServiceClient = new Mock<Azure.Storage.Blobs.BlobServiceClient>();
        
        var csvAdapter = new CsvAdapter(
            _csvProcessingService,
            _mockAdapterConfig.Object,
            mockBlobServiceClient.Object,
            _messageBoxService,
            null,
            InterfaceName,
            adapterInstanceGuid,
            null,
            "*.txt",
            1000, // Batch size 1000
            "║",
            null,
            null,
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
            _mockCsvLogger.Object);
        csvAdapter.CsvData = csvContent; // Set CSV data via property

        var sqlAdapter = new SqlServerAdapter(
            _applicationContext,
            _dynamicTableService,
            _dataService,
            _messageBoxService,
            null,
            InterfaceName,
            adapterInstanceGuid,
            null, // connectionString
            null, // pollingStatement
            null, // pollingInterval
            "TransportData", // tableName
            null, // useTransaction
            null, // batchSize
            null, // commandTimeout
            null, // failOnBadStatement
            null, // configService
            _mockSqlLogger.Object,
            null); // statisticsService

        // Act
        var (headers, records) = await csvAdapter.ReadAsync("", CancellationToken.None);
        await csvAdapter.WriteAsync("TransportData", headers, records, CancellationToken.None);
        
        var (sqlHeaders, sqlRecords) = await sqlAdapter.ReadAsync("TransportData", CancellationToken.None);
        await sqlAdapter.WriteAsync("TransportData", sqlHeaders, sqlRecords, CancellationToken.None);

        // Assert
        var insertedRows = await _applicationContext.TransportData.CountAsync();
        Assert.True(insertedRows >= 50000, $"Expected at least 50000 rows, got {insertedRows}");
    }

    private string GenerateLargeCsvContent(int rowCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id║name║email║age║city");
        
        for (int i = 0; i < rowCount; i++)
        {
            sb.AppendLine($"{i}║Name{i}║email{i}@example.com║{20 + (i % 50)}║City{i % 10}");
        }
        
        return sb.ToString();
    }

    public void Dispose()
    {
        _messageBoxContext?.Dispose();
        _applicationContext?.Dispose();
    }
}


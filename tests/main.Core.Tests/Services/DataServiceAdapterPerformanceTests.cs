using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using Xunit;
using System.Diagnostics;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

/// <summary>
/// Unit tests for DataServiceAdapter performance optimizations
/// Tests bulk insert functionality and batch size optimizations
/// </summary>
public class DataServiceAdapterPerformanceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IDataService _dataService;
    private readonly Mock<ILoggingService> _mockLoggingService;
    private readonly Mock<ILogger<DataServiceAdapter>> _mockLogger;

    public DataServiceAdapterPerformanceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLoggingService = new Mock<ILoggingService>();
        _mockLogger = new Mock<ILogger<DataServiceAdapter>>();
        _dataService = new DataServiceAdapter(_context, _mockLoggingService.Object, _mockLogger.Object);

        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task InsertRowsAsync_WithLargeBatch_ShouldUseBulkInsert()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>();
        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        // Create 1000 rows
        for (int i = 0; i < 1000; i++)
        {
            rows.Add(new Dictionary<string, string>
            {
                { "id", i.ToString() },
                { "name", $"Name{i}" },
                { "email", $"email{i}@example.com" }
            });
        }

        columnTypes["id"] = new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.INT };
        columnTypes["name"] = new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR };
        columnTypes["email"] = new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR };

        // Act
        var stopwatch = Stopwatch.StartNew();
        await _dataService.InsertRowsAsync(rows, columnTypes, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 10000, $"Bulk insert took {stopwatch.ElapsedMilliseconds}ms, expected < 10000ms");
        
        var insertedRows = await _context.TransportData.CountAsync();
        Assert.True(insertedRows >= 1000, $"Expected at least 1000 rows, got {insertedRows}");
    }

    [Fact]
    public async Task InsertRowsAsync_WithVeryLargeBatch_ShouldProcessInBatches()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>();
        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        // Create 10000 rows (should be processed in batches of 5000)
        for (int i = 0; i < 10000; i++)
        {
            rows.Add(new Dictionary<string, string>
            {
                { "id", i.ToString() },
                { "name", $"Name{i}" }
            });
        }

        columnTypes["id"] = new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.INT };
        columnTypes["name"] = new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR };

        // Act
        await _dataService.InsertRowsAsync(rows, columnTypes, CancellationToken.None);

        // Assert
        var insertedRows = await _context.TransportData.CountAsync();
        Assert.Equal(10000, insertedRows);
    }

    [Fact]
    public async Task InsertRowsAsync_WithMixedDataTypes_ShouldHandleCorrectly()
    {
        // Arrange
        var rows = new List<Dictionary<string, string>>();
        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>();

        for (int i = 0; i < 100; i++)
        {
            rows.Add(new Dictionary<string, string>
            {
                { "id", i.ToString() },
                { "price", (i * 1.5).ToString() },
                { "isActive", (i % 2 == 0).ToString() },
                { "created", DateTime.UtcNow.AddDays(-i).ToString("yyyy-MM-dd HH:mm:ss") }
            });
        }

        columnTypes["id"] = new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.INT };
        columnTypes["price"] = new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.DECIMAL };
        columnTypes["isActive"] = new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.BIT };
        columnTypes["created"] = new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.DATETIME2 };

        // Act
        await _dataService.InsertRowsAsync(rows, columnTypes, CancellationToken.None);

        // Assert
        var insertedRows = await _context.TransportData.CountAsync();
        Assert.Equal(100, insertedRows);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}


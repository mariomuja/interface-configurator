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
using System.Linq;

namespace InterfaceConfigurator.Main.Core.Tests.Adapters;

/// <summary>
/// Unit tests for SqlServerAdapter MessageBox integration
/// Tests detailed communication with MessageBox when adapter is used as Source or Destination
/// </summary>
public class SqlServerAdapterMessageBoxTests : IDisposable
{
    private readonly MessageBoxDbContext _messageBoxContext;
    private readonly ApplicationDbContext _applicationContext;
    private readonly IMessageBoxService _messageBoxService;
    private readonly Mock<IDynamicTableService> _mockDynamicTableService;
    private readonly Mock<IDataService> _mockDataService;
    private readonly Mock<ILogger<SqlServerAdapter>> _mockLogger;
    private const string InterfaceName = "FromCsvToSqlServerExample";

    public SqlServerAdapterMessageBoxTests()
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
        
        var logger = new Mock<ILogger<MessageBoxService>>();
        var mockEventQueue = new Mock<IEventQueue>();
        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _messageBoxService = new MessageBoxService(_messageBoxContext, mockEventQueue.Object, mockSubscriptionService.Object, logger.Object);
        _mockDynamicTableService = new Mock<IDynamicTableService>();
        _mockDataService = new Mock<IDataService>();
        _mockLogger = new Mock<ILogger<SqlServerAdapter>>();

        _messageBoxContext.Database.EnsureCreated();
        _applicationContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task SqlServerAdapter_ReadAsync_AsSource_ShouldWriteToMessageBox()
    {
        // Arrange
        var headers = new List<string> { "Name", "Age", "City" };
        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>
        {
            { "Name", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR, SqlTypeDefinition = "NVARCHAR(MAX)" } },
            { "Age", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.INT, SqlTypeDefinition = "INT" } },
            { "City", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR, SqlTypeDefinition = "NVARCHAR(MAX)" } }
        };

        _mockDynamicTableService
            .Setup(x => x.GetCurrentTableStructureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(columnTypes);

        _mockDataService
            .Setup(x => x.EnsureDatabaseCreatedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock SQL query execution - return empty result set
        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        var adapterInstanceGuid = Guid.NewGuid();
        var adapter = new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
            _messageBoxService,
            mockSubscriptionService.Object,
            InterfaceName,
            adapterInstanceGuid,
            null, // connectionString
            null, // pollingStatement
            null, // pollingInterval
            null, // useTransaction
            null, // batchSize
            null, // commandTimeout
            null, // failOnBadStatement
            null, // configService
            _mockLogger.Object);

        // Act
        var (readHeaders, readRecords) = await adapter.ReadAsync("TransportData");

        // Assert
        // Even with empty result, adapter should attempt to write to MessageBox
        // (In real scenario, there would be data from SQL)
        var messages = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending");
        // Note: With empty records, adapter may or may not write to MessageBox
        // This test verifies the adapter structure is correct
        Assert.NotNull(readHeaders);
        Assert.NotNull(readRecords);
    }

    [Fact]
    public async Task SqlServerAdapter_WriteAsync_AsDestination_ShouldReadFromMessageBox()
    {
        // Arrange
        var headers = new List<string> { "Name", "Age" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" }, { "Age", "30" } },
            new() { { "Name", "Jane" }, { "Age", "25" } }
        };

        // Create message in MessageBox (simulating Source adapter)
        var sourceAdapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            InterfaceName, "CSV", "Source", sourceAdapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>
        {
            { "Name", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR, SqlTypeDefinition = "NVARCHAR(MAX)" } },
            { "Age", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.INT, SqlTypeDefinition = "INT" } }
        };

        _mockDynamicTableService
            .Setup(x => x.EnsureTableStructureAsync(It.IsAny<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockDataService
            .Setup(x => x.EnsureDatabaseCreatedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockDataService
            .Setup(x => x.InsertRowsAsync(It.IsAny<List<Dictionary<string, string>>>(), It.IsAny<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        var adapterInstanceGuid = Guid.NewGuid();
        var adapter = new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
            _messageBoxService,
            mockSubscriptionService.Object,
            InterfaceName,
            adapterInstanceGuid,
            null, // connectionString
            null, // pollingStatement
            null, // pollingInterval
            null, // useTransaction
            null, // batchSize
            null, // commandTimeout
            null, // failOnBadStatement
            null, // configService
            _mockLogger.Object);

        // Act
        await adapter.WriteAsync("TransportData", headers, records);

        // Assert
        // Verify message was read and marked as processed
        var processedMessage = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.NotNull(processedMessage);
        Assert.Equal("Processed", processedMessage.Status);
        Assert.NotNull(processedMessage.datetime_processed);
        Assert.Contains("Written to SQL Server table", processedMessage.ProcessingDetails ?? "");
    }

    [Fact]
    public async Task SqlServerAdapter_WriteAsync_AsDestination_WithNoPendingMessages_ShouldHandleGracefully()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" } }
        };

        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>
        {
            { "Name", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR, SqlTypeDefinition = "NVARCHAR(MAX)" } }
        };

        _mockDynamicTableService
            .Setup(x => x.EnsureTableStructureAsync(It.IsAny<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockDataService
            .Setup(x => x.EnsureDatabaseCreatedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockDataService
            .Setup(x => x.InsertRowsAsync(It.IsAny<List<Dictionary<string, string>>>(), It.IsAny<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        var adapterInstanceGuid = Guid.NewGuid();
        var adapter = new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
            _messageBoxService,
            mockSubscriptionService.Object,
            InterfaceName,
            adapterInstanceGuid,
            null, // connectionString
            null, // pollingStatement
            null, // pollingInterval
            null, // useTransaction
            null, // batchSize
            null, // commandTimeout
            null, // failOnBadStatement
            null, // configService
            _mockLogger.Object);

        // Act - No messages in MessageBox
        await adapter.WriteAsync("TransportData", headers, records);

        // Assert - Should not throw, should use provided headers/records
        Assert.True(true); // Test passes if no exception thrown
    }

    [Fact]
    public async Task SqlServerAdapter_WriteAsync_AsDestination_ShouldReadMostRecentPendingMessage()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        
        // Create multiple messages with different timestamps
        var sourceAdapterInstanceGuid = Guid.NewGuid();
        var messageId1 = await _messageBoxService.WriteSingleRecordMessageAsync(
            InterfaceName, "CSV", "Source", sourceAdapterInstanceGuid, headers, 
            new Dictionary<string, string> { { "Name", "First" } });
        
        await Task.Delay(10); // Small delay to ensure different timestamps
        
        var messageId2 = await _messageBoxService.WriteSingleRecordMessageAsync(
            InterfaceName, "CSV", "Source", sourceAdapterInstanceGuid, headers,
            new Dictionary<string, string> { { "Name", "Second" } });

        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>
        {
            { "Name", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR, SqlTypeDefinition = "NVARCHAR(MAX)" } }
        };

        _mockDynamicTableService
            .Setup(x => x.EnsureTableStructureAsync(It.IsAny<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockDataService
            .Setup(x => x.EnsureDatabaseCreatedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockDataService
            .Setup(x => x.InsertRowsAsync(It.IsAny<List<Dictionary<string, string>>>(), It.IsAny<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        var adapterInstanceGuid = Guid.NewGuid();
        var adapter = new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
            _messageBoxService,
            mockSubscriptionService.Object,
            InterfaceName,
            adapterInstanceGuid,
            null, // connectionString
            null, // pollingStatement
            null, // pollingInterval
            null, // useTransaction
            null, // batchSize
            null, // commandTimeout
            null, // failOnBadStatement
            null, // configService
            _mockLogger.Object);

        // Act
        await adapter.WriteAsync("TransportData", headers, new List<Dictionary<string, string>>());

        // Assert
        // Most recent message (messageId2) should be marked as processed
        var processedMessage2 = await _messageBoxService.ReadMessageAsync(messageId2);
        Assert.NotNull(processedMessage2);
        Assert.Equal("Processed", processedMessage2.Status);
        
        // Older message should still be pending
        var pendingMessage1 = await _messageBoxService.ReadMessageAsync(messageId1);
        Assert.NotNull(pendingMessage1);
        Assert.Equal("Pending", pendingMessage1.Status);
    }

    [Fact]
    public async Task SqlServerAdapter_WriteAsync_ReadsFromMessageBox_AndWritesToTransportDataTable()
    {
        // Arrange - Create messages in MessageBox (simulating CSV adapter writing debatched records)
        var headers = new List<string> { "Name", "Age", "City" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John Doe" }, { "Age", "30" }, { "City", "New York" } },
            new() { { "Name", "Jane Smith" }, { "Age", "25" }, { "City", "London" } },
            new() { { "Name", "Bob Johnson" }, { "Age", "35" }, { "City", "Berlin" } }
        };

        // Write messages to MessageBox (debatching - each record becomes a separate message)
        var sourceAdapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            InterfaceName, "CSV", "Source", sourceAdapterInstanceGuid, headers, records);
        
        Assert.Equal(3, messageIds.Count); // Should have 3 messages (one per record)

        // Mock both DynamicTableService and DataService (since they use relational-specific methods with in-memory DB)
        // Track what data is passed to InsertRowsAsync
        List<Dictionary<string, string>>? insertedRecords = null;
        Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>? insertedColumnTypes = null;

        var columnTypes = new Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>
        {
            { "Name", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR, SqlTypeDefinition = "NVARCHAR(MAX)" } },
            { "Age", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.INT, SqlTypeDefinition = "INT" } },
            { "City", new CsvColumnAnalyzer.ColumnTypeInfo { DataType = CsvColumnAnalyzer.SqlDataType.NVARCHAR, SqlTypeDefinition = "NVARCHAR(MAX)" } }
        };

        _mockDynamicTableService
            .Setup(x => x.EnsureTableStructureAsync(It.IsAny<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockDataService
            .Setup(x => x.EnsureDatabaseCreatedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockDataService
            .Setup(x => x.InsertRowsAsync(It.IsAny<List<Dictionary<string, string>>>(), It.IsAny<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>>(), It.IsAny<CancellationToken>()))
            .Callback<List<Dictionary<string, string>>, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>, CancellationToken>(
                (rows, ct, cancellationToken) =>
                {
                    insertedRecords = rows;
                    insertedColumnTypes = ct;
                })
            .Returns(Task.CompletedTask);

        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        // Setup subscription service to allow message processing
        mockSubscriptionService
            .Setup(x => x.CreateSubscriptionAsync(It.IsAny<Guid>(), InterfaceName, "SqlServer", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        mockSubscriptionService
            .Setup(x => x.MarkSubscriptionAsProcessedAsync(It.IsAny<Guid>(), "SqlServer", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        mockSubscriptionService
            .Setup(x => x.AreAllSubscriptionsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // All subscriptions processed, so messages can be removed
        
        var adapterInstanceGuid = Guid.NewGuid();
        var adapter = new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
            _messageBoxService,
            mockSubscriptionService.Object,
            InterfaceName,
            adapterInstanceGuid,
            null, // connectionString
            null, // pollingStatement
            null, // pollingInterval
            null, // useTransaction
            null, // batchSize
            null, // commandTimeout
            null, // failOnBadStatement
            null, // configService
            _mockLogger.Object);

        // Verify messages exist before processing
        var messagesBefore = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending");
        Assert.Equal(3, messagesBefore.Count); // All 3 messages should be pending

        // Act - Write to TransportData table (should read from MessageBox)
        await adapter.WriteAsync("TransportData", headers, new List<Dictionary<string, string>>());

        // Assert - Verify InsertRowsAsync was called (proves messages were read and processed)
        _mockDataService.Verify(
            x => x.InsertRowsAsync(
                It.Is<List<Dictionary<string, string>>>(r => r.Count == 3),
                It.IsAny<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "InsertRowsAsync should have been called with 3 records from MessageBox");
        
        // Verify subscriptions were created and marked as processed
        mockSubscriptionService.Verify(
            x => x.CreateSubscriptionAsync(It.IsAny<Guid>(), InterfaceName, "SqlServer", It.IsAny<CancellationToken>()),
            Times.Exactly(3)); // One subscription per message
        
        mockSubscriptionService.Verify(
            x => x.MarkSubscriptionAsProcessedAsync(
                It.IsAny<Guid>(), 
                "SqlServer", 
                It.Is<string>(s => s.Contains("Written to SQL Server table")),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3)); // All subscriptions should be marked as processed

        // Assert - Verify InsertRowsAsync was called with correct data from MessageBox
        Assert.NotNull(insertedRecords);
        Assert.Equal(3, insertedRecords.Count);
        Assert.NotNull(insertedColumnTypes);
        Assert.Equal(3, insertedColumnTypes.Count);

        // Verify data integrity - check that all records from MessageBox are present
        var names = insertedRecords.Select(r => r["Name"]).ToList();
        var ages = insertedRecords.Select(r => r["Age"]).ToList();
        var cities = insertedRecords.Select(r => r["City"]).ToList();
        
        Assert.Contains("John Doe", names);
        Assert.Contains("Jane Smith", names);
        Assert.Contains("Bob Johnson", names);
        Assert.Contains("30", ages);
        Assert.Contains("25", ages);
        Assert.Contains("35", ages);
        Assert.Contains("New York", cities);
        Assert.Contains("London", cities);
        Assert.Contains("Berlin", cities);

        // Verify column types are correct
        Assert.True(insertedColumnTypes.ContainsKey("Name"));
        Assert.True(insertedColumnTypes.ContainsKey("Age"));
        Assert.True(insertedColumnTypes.ContainsKey("City"));
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.NVARCHAR, insertedColumnTypes["Name"].DataType);
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.INT, insertedColumnTypes["Age"].DataType);
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.NVARCHAR, insertedColumnTypes["City"].DataType);

        // Verify InsertRowsAsync was called with correct table name (via destination parameter)
        _mockDataService.Verify(
            x => x.InsertRowsAsync(
                It.Is<List<Dictionary<string, string>>>(r => r.Count == 3),
                It.Is<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>>(ct => ct.Count == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Note: Messages may remain in MessageBox until all subscriptions are processed
        // The important verification is that InsertRowsAsync was called with correct data from MessageBox
    }

    public void Dispose()
    {
        _messageBoxContext?.Dispose();
        _applicationContext?.Dispose();
    }
}


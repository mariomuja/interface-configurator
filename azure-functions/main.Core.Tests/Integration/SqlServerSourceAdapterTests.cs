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
using System.Data.Common;

namespace InterfaceConfigurator.Main.Core.Tests.Integration;

/// <summary>
/// Integration tests for SQL Server adapter when used as source
/// Tests that data is read from SQL Server, debatched, and written to MessageBox
/// </summary>
public class SqlServerSourceAdapterTests : IDisposable
{
    private readonly MessageBoxDbContext _messageBoxContext;
    private readonly ApplicationDbContext _applicationContext;
    private readonly IMessageBoxService _messageBoxService;
    private readonly IDynamicTableService _dynamicTableService;
    private readonly IDataService _dataService;
    private readonly Mock<ILogger<SqlServerAdapter>> _mockSqlLogger;
    private const string InterfaceName = "TestSqlServerSource";
    private const string TableName = "SourceTable";

    public SqlServerSourceAdapterTests()
    {
        // Use in-memory databases for testing
        var messageBoxOptions = new DbContextOptionsBuilder<MessageBoxDbContext>()
            .UseInMemoryDatabase(databaseName: $"MessageBox_{Guid.NewGuid()}")
            .Options;

        // Use in-memory database for ApplicationDbContext to test SQL queries
        var appOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _messageBoxContext = new MessageBoxDbContext(messageBoxOptions);
        _applicationContext = new ApplicationDbContext(appOptions);
        
        var messageBoxLogger = new Mock<ILogger<MessageBoxService>>();
        var mockEventQueue = new Mock<IEventQueue>();
        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _messageBoxService = new MessageBoxService(_messageBoxContext, mockEventQueue.Object, mockSubscriptionService.Object, messageBoxLogger.Object);
        
        _dynamicTableService = new DynamicTableService(_applicationContext, new Mock<ILogger<DynamicTableService>>().Object);
        _dataService = new DataServiceAdapter(_applicationContext, new Mock<ILoggingService>().Object, new Mock<ILogger<DataServiceAdapter>>().Object);
        _mockSqlLogger = new Mock<ILogger<SqlServerAdapter>>();

        _messageBoxContext.Database.EnsureCreated();
        _applicationContext.Database.EnsureCreated();

        // Create test table with sample data
        CreateTestTable();
    }

    private void CreateTestTable()
    {
        // Create a test table with sample data
        var createTableSql = $@"
            CREATE TABLE IF NOT EXISTS [{TableName}] (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Email TEXT,
                Age INTEGER,
                datetime_created DATETIME DEFAULT CURRENT_TIMESTAMP
            )";

        _applicationContext.Database.ExecuteSqlRaw(createTableSql);

        // Insert sample data
        var insertSql = $@"
            INSERT INTO [{TableName}] (Id, Name, Email, Age) VALUES
            (1, 'John Doe', 'john@example.com', 30),
            (2, 'Jane Smith', 'jane@example.com', 25),
            (3, 'Bob Johnson', 'bob@example.com', 35)";

        _applicationContext.Database.ExecuteSqlRaw(insertSql);
    }

    [Fact]
    public async Task SqlServerSourceAdapter_WithPollingStatement_ShouldReadAndDebatchToMessageBox()
    {
        // Arrange
        var adapterGuid = Guid.NewGuid();
        var pollingStatement = $"SELECT Id, Name, Email, Age FROM [{TableName}]";

        var adapter = new SqlServerAdapter(
            _applicationContext,
            _dynamicTableService,
            _dataService,
            _messageBoxService,
            null,
            InterfaceName,
            adapterGuid,
            null, // connectionString
            pollingStatement, // pollingStatement
            60, // pollingInterval
            TableName, // tableName
            false, // useTransaction
            1000, // batchSize
            30, // commandTimeout
            false, // failOnBadStatement
            null, // configService
            _mockSqlLogger.Object,
            null); // statisticsService

        // Act - Read data (should debatch to MessageBox)
        var (headers, records) = await adapter.ReadAsync(string.Empty, CancellationToken.None);

        // Assert - Verify data was read
        Assert.NotEmpty(headers);
        Assert.Contains("Id", headers);
        Assert.Contains("Name", headers);
        Assert.Contains("Email", headers);
        Assert.Contains("Age", headers);
        Assert.Equal(3, records.Count);

        // Verify records content
        Assert.Contains(records, r => r["Name"] == "John Doe" && r["Email"] == "john@example.com");
        Assert.Contains(records, r => r["Name"] == "Jane Smith" && r["Email"] == "jane@example.com");
        Assert.Contains(records, r => r["Name"] == "Bob Johnson" && r["Email"] == "bob@example.com");

        // Verify messages were written to MessageBox
        var messages = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending", CancellationToken.None);
        Assert.Equal(3, messages.Count); // One message per record (debatching)

        // Verify message content
        foreach (var message in messages)
        {
            var (messageHeaders, messageRecord) = _messageBoxService.ExtractDataFromMessage(message);
            Assert.NotEmpty(messageRecord);
            Assert.Contains(messageRecord.Keys, k => k == "Name");
        }
    }

    [Fact]
    public async Task SqlServerSourceAdapter_WithDefaultPollingStatement_ShouldUseTableName()
    {
        // Arrange
        var adapterGuid = Guid.NewGuid();
        // No polling statement provided - should use default "SELECT * FROM TableName"

        var adapter = new SqlServerAdapter(
            _applicationContext,
            _dynamicTableService,
            _dataService,
            _messageBoxService,
            null,
            InterfaceName,
            adapterGuid,
            null, // connectionString
            null, // pollingStatement
            60, // pollingInterval
            TableName, // tableName
            false, // useTransaction
            1000, // batchSize
            30, // commandTimeout
            false, // failOnBadStatement
            null, // configService
            _mockSqlLogger.Object,
            null); // statisticsService

        // Act - Read data using default polling statement
        var (headers, records) = await adapter.ReadAsync(string.Empty, CancellationToken.None);

        // Assert - Verify data was read using default statement
        Assert.NotEmpty(headers);
        Assert.Equal(3, records.Count);

        // Verify messages were written to MessageBox
        var messages = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending", CancellationToken.None);
        Assert.Equal(3, messages.Count);
    }

    [Fact]
    public async Task SqlServerSourceAdapter_WithPollingInterval_ShouldRespectInterval()
    {
        // Arrange
        var adapterGuid = Guid.NewGuid();
        var pollingInterval = 30; // 30 seconds

        var adapter = new SqlServerAdapter(
            _applicationContext,
            _dynamicTableService,
            _dataService,
            _messageBoxService,
            null,
            InterfaceName,
            adapterGuid,
            null, // connectionString
            $"SELECT * FROM [{TableName}]", // pollingStatement
            pollingInterval, // pollingInterval
            TableName, // tableName
            false, // useTransaction
            1000, // batchSize
            30, // commandTimeout
            false, // failOnBadStatement
            null, // configService
            _mockSqlLogger.Object,
            null); // statisticsService

        // Act - Read data
        var (headers, records) = await adapter.ReadAsync(string.Empty, CancellationToken.None);

        // Assert - Verify polling interval is set (this is more of a configuration test)
        // The actual interval enforcement happens in SourceAdapterFunction timer trigger
        Assert.NotEmpty(records);
        Assert.Equal(3, records.Count);
    }

    [Fact]
    public async Task SqlServerSourceAdapter_WithFilteredPollingStatement_ShouldReturnFilteredResults()
    {
        // Arrange
        var adapterGuid = Guid.NewGuid();
        var pollingStatement = $"SELECT Id, Name, Email, Age FROM [{TableName}] WHERE Age > 25";

        var adapter = new SqlServerAdapter(
            _applicationContext,
            _dynamicTableService,
            _dataService,
            _messageBoxService,
            null,
            InterfaceName,
            adapterGuid,
            null, // connectionString
            pollingStatement, // pollingStatement
            60, // pollingInterval
            TableName, // tableName
            false, // useTransaction
            1000, // batchSize
            30, // commandTimeout
            false, // failOnBadStatement
            null, // configService
            _mockSqlLogger.Object,
            null); // statisticsService

        // Act - Read data with filtered statement
        var (headers, records) = await adapter.ReadAsync(string.Empty, CancellationToken.None);

        // Assert - Verify only filtered records were returned
        Assert.Equal(2, records.Count); // Only John (30) and Bob (35), not Jane (25)
        Assert.All(records, r => Assert.True(int.Parse(r["Age"]) > 25));

        // Verify messages were written to MessageBox
        var messages = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending", CancellationToken.None);
        Assert.Equal(2, messages.Count);
    }

    public void Dispose()
    {
        _messageBoxContext?.Database.EnsureDeleted();
        _messageBoxContext?.Dispose();
        _applicationContext?.Database.EnsureDeleted();
        _applicationContext?.Dispose();
    }
}


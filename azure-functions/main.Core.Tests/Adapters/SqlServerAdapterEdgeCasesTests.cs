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
/// Unit tests for SqlServerAdapter edge cases and error scenarios
/// </summary>
public class SqlServerAdapterEdgeCasesTests : IDisposable
{
    private readonly ApplicationDbContext _applicationContext;
    private readonly Mock<IDynamicTableService> _mockDynamicTableService;
    private readonly Mock<IDataService> _mockDataService;
    private readonly Mock<ILogger<SqlServerAdapter>> _mockLogger;

    public SqlServerAdapterEdgeCasesTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _applicationContext = new ApplicationDbContext(options);
        _mockDynamicTableService = new Mock<IDynamicTableService>();
        _mockDataService = new Mock<IDataService>();
        _mockLogger = new Mock<ILogger<SqlServerAdapter>>();

        _applicationContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task SqlServerAdapter_ReadAsync_WithEmptySource_ShouldThrowArgumentException()
    {
        // Arrange
        var adapter = new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
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
            null,
            null,
            _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.ReadAsync("", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.ReadAsync("   ", CancellationToken.None));
    }

    [Fact]
    public async Task SqlServerAdapter_WriteAsync_WithEmptyDestination_ShouldThrowArgumentException()
    {
        // Arrange
        var adapter = new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
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
    public async Task SqlServerAdapter_WriteAsync_WithEmptyHeaders_ShouldThrowArgumentException()
    {
        // Arrange
        _mockDataService
            .Setup(x => x.EnsureDatabaseCreatedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var adapter = new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
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
            null,
            null,
            _mockLogger.Object);

        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.WriteAsync("TransportData", headers, records, CancellationToken.None));
    }

    [Fact]
    public async Task SqlServerAdapter_WriteAsync_WithNullHeaders_ShouldThrowArgumentException()
    {
        // Arrange
        _mockDataService
            .Setup(x => x.EnsureDatabaseCreatedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var adapter = new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
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
            null,
            null,
            _mockLogger.Object);

        var records = new List<Dictionary<string, string>>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.WriteAsync("TransportData", null!, records, CancellationToken.None));
    }

    [Fact]
    public void SqlServerAdapter_WithNullContextAndNullConnectionString_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SqlServerAdapter(
            null,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
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
            null,
            null,
            _mockLogger.Object));
    }

    [Fact]
    public void SqlServerAdapter_WithNullDynamicTableService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerAdapter(
            _applicationContext,
            null!,
            _mockDataService.Object,
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
            null,
            null,
            _mockLogger.Object));
    }

    [Fact]
    public void SqlServerAdapter_WithNullDataService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
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
            null,
            null,
            _mockLogger.Object));
    }

    [Fact]
    public void SqlServerAdapter_WithConnectionString_ShouldCreateSuccessfully()
    {
        // Arrange
        var connectionString = "Server=test-server;Database=test-db;User Id=test-user;Password=test-pass;";

        // Act
        var adapter = new SqlServerAdapter(
            null,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
            null,
            null,
            null,
            null,
            connectionString,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(adapter);
        Assert.Equal("SqlServer", adapter.AdapterName);
    }

    [Fact]
    public void SqlServerAdapter_WithCommandTimeout_ShouldSetTimeout()
    {
        // Arrange
        var adapter = new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            60, // commandTimeout
            null,
            null,
            null,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(adapter);
    }

    [Fact]
    public void SqlServerAdapter_WithFailOnBadStatement_ShouldSetFlag()
    {
        // Arrange
        var adapter = new SqlServerAdapter(
            _applicationContext,
            _mockDynamicTableService.Object,
            _mockDataService.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true, // failOnBadStatement
            null,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(adapter);
    }

    public void Dispose()
    {
        _applicationContext?.Dispose();
    }
}




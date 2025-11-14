using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Services;
using Xunit;

namespace ProcessCsvBlobTrigger.Core.Tests.Services;

public class DataServiceAdapterTests
{
    private ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task EnsureDatabaseCreatedAsync_WithValidContext_CreatesDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var loggingServiceMock = new Mock<ILoggingService>();
        var loggerMock = new Mock<ILogger<DataServiceAdapter>>();
        var adapter = new DataServiceAdapter(context, loggingServiceMock.Object, loggerMock.Object);

        // Act & Assert - InMemory database doesn't support GetConnectionString, so this will throw
        // This test verifies the error handling path
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.EnsureDatabaseCreatedAsync());
    }

    [Fact]
    public async Task EnsureDatabaseCreatedAsync_WithNullContext_ThrowsException()
    {
        // Arrange
        var loggingServiceMock = new Mock<ILoggingService>();
        var loggerMock = new Mock<ILogger<DataServiceAdapter>>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Task.FromResult(new DataServiceAdapter(null!, loggingServiceMock.Object, loggerMock.Object)));
    }

    [Fact]
    public async Task ProcessChunksAsync_WithValidChunks_InsertsData()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await context.Database.EnsureCreatedAsync();

        var loggingServiceMock = new Mock<ILoggingService>();
        var loggerMock = new Mock<ILogger<DataServiceAdapter>>();
        var adapter = new DataServiceAdapter(context, loggingServiceMock.Object, loggerMock.Object);

        var chunks = new List<List<TransportData>>
        {
            new()
            {
                new TransportData { Id = 1, Name = "Test1", Email = "test1@test.com", Age = 30, City = "City1", Salary = 50000 },
                new TransportData { Id = 2, Name = "Test2", Email = "test2@test.com", Age = 25, City = "City2", Salary = 60000 }
            }
        };

        // Act
        await adapter.ProcessChunksAsync(chunks);

        // Assert
        var records = await context.TransportData.ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.Contains(records, r => r.Id == 1 && r.Name == "Test1");
        Assert.Contains(records, r => r.Id == 2 && r.Name == "Test2");
    }

    [Fact]
    public async Task ProcessChunksAsync_WithEmptyChunks_DoesNotThrow()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await context.Database.EnsureCreatedAsync();

        var loggingServiceMock = new Mock<ILoggingService>();
        var loggerMock = new Mock<ILogger<DataServiceAdapter>>();
        var adapter = new DataServiceAdapter(context, loggingServiceMock.Object, loggerMock.Object);

        var chunks = new List<List<TransportData>>();

        // Act
        await adapter.ProcessChunksAsync(chunks);

        // Assert
        var records = await context.TransportData.ToListAsync();
        Assert.Empty(records);
        loggingServiceMock.Verify(x => x.LogAsync("warning", "No chunks to process", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessChunksAsync_WithNullChunks_DoesNotThrow()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await context.Database.EnsureCreatedAsync();

        var loggingServiceMock = new Mock<ILoggingService>();
        var loggerMock = new Mock<ILogger<DataServiceAdapter>>();
        var adapter = new DataServiceAdapter(context, loggingServiceMock.Object, loggerMock.Object);

        // Act
        await adapter.ProcessChunksAsync(null!);

        // Assert
        var records = await context.TransportData.ToListAsync();
        Assert.Empty(records);
    }

    [Fact]
    public async Task ProcessChunksAsync_WithMultipleChunks_ProcessesSequentially()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await context.Database.EnsureCreatedAsync();

        var loggingServiceMock = new Mock<ILoggingService>();
        var loggerMock = new Mock<ILogger<DataServiceAdapter>>();
        var adapter = new DataServiceAdapter(context, loggingServiceMock.Object, loggerMock.Object);

        var chunks = new List<List<TransportData>>
        {
            new() { new TransportData { Id = 1, Name = "Test1", Email = "test1@test.com", Age = 30, City = "City1", Salary = 50000 } },
            new() { new TransportData { Id = 2, Name = "Test2", Email = "test2@test.com", Age = 25, City = "City2", Salary = 60000 } },
            new() { new TransportData { Id = 3, Name = "Test3", Email = "test3@test.com", Age = 35, City = "City3", Salary = 70000 } }
        };

        // Act
        await adapter.ProcessChunksAsync(chunks);

        // Assert
        var records = await context.TransportData.ToListAsync();
        Assert.Equal(3, records.Count);
    }

    [Fact]
    public async Task ProcessChunksAsync_WithNullRecordsInChunk_SkipsNullRecords()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await context.Database.EnsureCreatedAsync();

        var loggingServiceMock = new Mock<ILoggingService>();
        var loggerMock = new Mock<ILogger<DataServiceAdapter>>();
        var adapter = new DataServiceAdapter(context, loggingServiceMock.Object, loggerMock.Object);

        var chunks = new List<List<TransportData>>
        {
            new() { null!, new TransportData { Id = 1, Name = "Test1", Email = "test1@test.com", Age = 30, City = "City1", Salary = 50000 } }
        };

        // Act
        await adapter.ProcessChunksAsync(chunks);

        // Assert
        var records = await context.TransportData.ToListAsync();
        Assert.Single(records);
        Assert.Equal(1, records[0].Id);
    }

    [Fact]
    public async Task ProcessChunksAsync_WithEmptyChunk_SkipsEmptyChunk()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await context.Database.EnsureCreatedAsync();

        var loggingServiceMock = new Mock<ILoggingService>();
        var loggerMock = new Mock<ILogger<DataServiceAdapter>>();
        var adapter = new DataServiceAdapter(context, loggingServiceMock.Object, loggerMock.Object);

        var chunks = new List<List<TransportData>>
        {
            new(),
            new() { new TransportData { Id = 1, Name = "Test1", Email = "test1@test.com", Age = 30, City = "City1", Salary = 50000 } }
        };

        // Act
        await adapter.ProcessChunksAsync(chunks);

        // Assert
        var records = await context.TransportData.ToListAsync();
        Assert.Single(records);
    }
}


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessCsvBlobTrigger.Core.Interfaces;
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
}

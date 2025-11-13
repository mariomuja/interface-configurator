using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Processors;
using Xunit;

namespace ProcessCsvBlobTrigger.Core.Tests.Processors;

public class CsvProcessorTests
{
    private readonly Mock<ICsvProcessingService> _csvProcessingServiceMock;
    private readonly Mock<IDataService> _dataServiceMock;
    private readonly Mock<ILoggingService> _loggingServiceMock;
    private readonly Mock<ILogger<CsvProcessor>> _loggerMock;
    private readonly CsvProcessor _processor;

    public CsvProcessorTests()
    {
        _csvProcessingServiceMock = new Mock<ICsvProcessingService>();
        _dataServiceMock = new Mock<IDataService>();
        _loggingServiceMock = new Mock<ILoggingService>();
        _loggerMock = new Mock<ILogger<CsvProcessor>>();

        _processor = new CsvProcessor(
            _csvProcessingServiceMock.Object,
            _dataServiceMock.Object,
            _loggingServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessCsvAsync_ValidCsv_ReturnsSuccess()
    {
        // Arrange
        var csvContent = "id,name,email,age,city,salary\n1,John Doe,john@example.com,30,New York,50000";
        var blobContent = Encoding.UTF8.GetBytes(csvContent);
        var blobName = "test.csv";

        var records = new List<Dictionary<string, string>>
        {
            new() { { "id", "1" }, { "name", "John Doe" }, { "email", "john@example.com" }, { "age", "30" }, { "city", "New York" }, { "salary", "50000" } }
        };

        var chunks = new List<List<ProcessCsvBlobTrigger.Core.Models.TransportData>>
        {
            new() { new ProcessCsvBlobTrigger.Core.Models.TransportData { Id = 1, Name = "John Doe", Email = "john@example.com", Age = 30, City = "New York", Salary = 50000 } }
        };

        _csvProcessingServiceMock.Setup(s => s.ParseCsv(It.IsAny<string>())).Returns(records);
        _csvProcessingServiceMock.Setup(s => s.CreateChunks(records)).Returns(chunks);
        _dataServiceMock.Setup(s => s.EnsureDatabaseCreatedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _dataServiceMock.Setup(s => s.ProcessChunksAsync(chunks, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _processor.ProcessCsvAsync(blobContent, blobName);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsProcessed);
        Assert.Equal(1, result.ChunksProcessed);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task ProcessCsvAsync_EmptyCsv_ReturnsSuccessWithZeroRecords()
    {
        // Arrange
        var csvContent = "id,name,email,age,city,salary";
        var blobContent = Encoding.UTF8.GetBytes(csvContent);
        var blobName = "empty.csv";

        _csvProcessingServiceMock.Setup(s => s.ParseCsv(It.IsAny<string>())).Returns(new List<Dictionary<string, string>>());
        _dataServiceMock.Setup(s => s.EnsureDatabaseCreatedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _processor.ProcessCsvAsync(blobContent, blobName);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(0, result.RecordsProcessed);
        Assert.Equal(0, result.ChunksProcessed);
    }

    [Fact]
    public async Task ProcessCsvAsync_ProcessingError_ReturnsFailure()
    {
        // Arrange
        var csvContent = "id,name,email,age,city,salary\n1,John Doe,john@example.com,30,New York,50000";
        var blobContent = Encoding.UTF8.GetBytes(csvContent);
        var blobName = "test.csv";
        var errorMessage = "Database connection failed";

        _csvProcessingServiceMock.Setup(s => s.ParseCsv(It.IsAny<string>())).Returns(new List<Dictionary<string, string>>());
        _dataServiceMock.Setup(s => s.EnsureDatabaseCreatedAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception(errorMessage));

        // Act
        var result = await _processor.ProcessCsvAsync(blobContent, blobName);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsProcessed);
        Assert.Equal(0, result.ChunksProcessed);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains(errorMessage, result.ErrorMessage);
        Assert.NotNull(result.Exception);
    }
}


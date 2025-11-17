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
    private readonly Mock<IDynamicTableService> _dynamicTableServiceMock;
    private readonly Mock<IErrorRowService> _errorRowServiceMock;
    private readonly Mock<ILoggingService> _loggingServiceMock;
    private readonly Mock<ILogger<CsvProcessor>> _loggerMock;
    private readonly CsvProcessor _processor;

    public CsvProcessorTests()
    {
        _csvProcessingServiceMock = new Mock<ICsvProcessingService>();
        _dataServiceMock = new Mock<IDataService>();
        _dynamicTableServiceMock = new Mock<IDynamicTableService>();
        _errorRowServiceMock = new Mock<IErrorRowService>();
        _loggingServiceMock = new Mock<ILoggingService>();
        _loggerMock = new Mock<ILogger<CsvProcessor>>();

        _processor = new CsvProcessor(
            _csvProcessingServiceMock.Object,
            _dataServiceMock.Object,
            _dynamicTableServiceMock.Object,
            _errorRowServiceMock.Object,
            _loggingServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessCsvAsync_NullBlobContent_ReturnsFailure()
    {
        // Arrange
        byte[]? blobContent = null;
        var blobName = "test.csv";

        // Act
        var result = await _processor.ProcessCsvAsync(blobContent!, blobName);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsProcessed);
        Assert.Equal(0, result.ChunksProcessed);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("null", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Exception);
        Assert.IsType<ArgumentNullException>(result.Exception);
    }

    [Fact]
    public async Task ProcessCsvAsync_EmptyCsv_ReturnsSuccessWithZeroRecords()
    {
        // Arrange
        var csvContent = "id,name,email";
        var blobContent = Encoding.UTF8.GetBytes(csvContent);
        var blobName = "empty.csv";

        _csvProcessingServiceMock.Setup(s => s.ParseCsvWithHeaders(It.IsAny<string>()))
            .Returns((new List<string> { "id", "name", "email" }, new List<Dictionary<string, string>>()));
        _dataServiceMock.Setup(s => s.EnsureDatabaseCreatedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _dynamicTableServiceMock.Setup(s => s.EnsureTableStructureAsync(It.IsAny<Dictionary<string, ProcessCsvBlobTrigger.Core.Services.CsvColumnAnalyzer.ColumnTypeInfo>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
        var csvContent = "id,name,email\n1,John Doe,john@example.com";
        var blobContent = Encoding.UTF8.GetBytes(csvContent);
        var blobName = "test.csv";
        var errorMessage = "Database connection failed";

        _csvProcessingServiceMock.Setup(s => s.ParseCsvWithHeaders(It.IsAny<string>()))
            .Returns((new List<string>(), new List<Dictionary<string, string>>()));
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
    }
}

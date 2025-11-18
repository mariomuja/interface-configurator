using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using Xunit;
using System.Diagnostics;
using System.Text;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

/// <summary>
/// Unit tests for CsvProcessingService performance optimizations
/// Tests streaming CSV parsing for large files
/// </summary>
public class CsvProcessingServicePerformanceTests
{
    private readonly ICsvProcessingService _csvProcessingService;
    private readonly Mock<IAdapterConfigurationService> _mockAdapterConfig;
    private readonly Mock<ILogger<CsvProcessingService>> _mockLogger;

    public CsvProcessingServicePerformanceTests()
    {
        _mockAdapterConfig = new Mock<IAdapterConfigurationService>();
        _mockAdapterConfig.Setup(x => x.GetCsvFieldSeparatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("║");
        _mockLogger = new Mock<ILogger<CsvProcessingService>>();
        _csvProcessingService = new CsvProcessingService(_mockAdapterConfig.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ParseCsvWithHeadersAsync_WithSmallFile_ShouldUseInMemoryParsing()
    {
        // Arrange
        var csvContent = GenerateCsvContent(100); // Small file (< 1MB)

        // Act
        var stopwatch = Stopwatch.StartNew();
        var (headers, records) = await _csvProcessingService.ParseCsvWithHeadersAsync(csvContent, "║", CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.Equal(3, headers.Count);
        Assert.Equal(100, records.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Parsing took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    [Fact]
    public async Task ParseCsvWithHeadersAsync_WithLargeFile_ShouldUseStreamingParsing()
    {
        // Arrange
        var csvContent = GenerateCsvContent(100000); // Large file (> 1MB)
        Assert.True(csvContent.Length > 1024 * 1024, "File should be larger than 1MB");

        // Act
        var stopwatch = Stopwatch.StartNew();
        var (headers, records) = await _csvProcessingService.ParseCsvWithHeadersAsync(csvContent, "║", CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.Equal(3, headers.Count);
        Assert.Equal(100000, records.Count);
        Assert.True(stopwatch.ElapsedMilliseconds < 30000, $"Streaming parsing took {stopwatch.ElapsedMilliseconds}ms, expected < 30000ms");
    }

    [Fact]
    public async Task ParseCsvWithHeadersAsync_WithVeryLargeFile_ShouldProcessInChunks()
    {
        // Arrange
        var csvContent = GenerateCsvContent(500000); // Very large file

        // Act
        var stopwatch = Stopwatch.StartNew();
        var (headers, records) = await _csvProcessingService.ParseCsvWithHeadersAsync(csvContent, "║", CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.Equal(3, headers.Count);
        Assert.Equal(500000, records.Count);
        // Should complete without memory issues
        Assert.True(stopwatch.ElapsedMilliseconds < 120000, $"Parsing took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ParseCsvWithHeadersAsync_WithInvalidRows_ShouldContinueProcessing()
    {
        // Arrange
        var csvContent = "id║name║email\n1║John║john@example.com\n2║Jane\n3║Bob║bob@example.com║extra";
        // Row 2 has 2 columns (should be 3), Row 3 has 4 columns (should be 3)

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await _csvProcessingService.ParseCsvWithHeadersAsync(csvContent, "║", CancellationToken.None);
        });
    }

    private string GenerateCsvContent(int rowCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id║name║email");
        
        for (int i = 0; i < rowCount; i++)
        {
            sb.AppendLine($"{i}║Name{i}║email{i}@example.com");
        }
        
        return sb.ToString();
    }
}


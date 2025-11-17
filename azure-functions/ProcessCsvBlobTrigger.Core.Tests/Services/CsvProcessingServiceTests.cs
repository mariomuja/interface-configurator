using ProcessCsvBlobTrigger.Core.Services;
using Xunit;

namespace ProcessCsvBlobTrigger.Core.Tests.Services;

public class CsvProcessingServiceTests
{
    private readonly CsvProcessingService _service;

    public CsvProcessingServiceTests()
    {
        _service = new CsvProcessingService();
    }

    [Fact]
    public void ParseCsv_ValidCsv_ReturnsRecords()
    {
        // Arrange
        var csvContent = "id,name,email,age,city,salary\n1,John Doe,john@example.com,30,New York,50000\n2,Jane Smith,jane@example.com,25,Los Angeles,60000";

        // Act
        var result = _service.ParseCsv(csvContent);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("1", result[0]["id"]);
        Assert.Equal("John Doe", result[0]["name"]);
        Assert.Equal("john@example.com", result[0]["email"]);
    }

    [Fact]
    public void ParseCsvWithHeaders_ValidCsv_ReturnsHeadersAndRecords()
    {
        // Arrange
        var csvContent = "id,name,email\n1,John Doe,john@example.com\n2,Jane Smith,jane@example.com";

        // Act
        var (headers, records) = _service.ParseCsvWithHeaders(csvContent);

        // Assert
        Assert.NotNull(headers);
        Assert.Equal(3, headers.Count);
        Assert.Contains("id", headers);
        Assert.Contains("name", headers);
        Assert.Contains("email", headers);
        Assert.NotNull(records);
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public void ParseCsv_EmptyContent_ReturnsEmptyList()
    {
        // Arrange
        var csvContent = "";

        // Act
        var result = _service.ParseCsv(csvContent);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseCsv_OnlyHeaders_ReturnsEmptyList()
    {
        // Arrange
        var csvContent = "id,name,email,age,city,salary";

        // Act
        var result = _service.ParseCsv(csvContent);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseCsv_WithQuotedValues_HandlesQuotes()
    {
        // Arrange
        var csvContent = "id,name,email\n\"1\",\"John Doe\",\"john@example.com\"";

        // Act
        var result = _service.ParseCsv(csvContent);

        // Assert
        Assert.Single(result);
        Assert.Equal("1", result[0]["id"]);
        Assert.Equal("John Doe", result[0]["name"]);
        Assert.Equal("john@example.com", result[0]["email"]);
    }

    [Fact]
    public void ParseCsv_WithWhitespace_TrimsValues()
    {
        // Arrange
        var csvContent = "id,name,email\n 1 , John Doe , john@example.com ";

        // Act
        var result = _service.ParseCsv(csvContent);

        // Assert
        Assert.Single(result);
        Assert.Equal("1", result[0]["id"]);
        Assert.Equal("John Doe", result[0]["name"]);
        Assert.Equal("john@example.com", result[0]["email"]);
    }

    [Fact]
    public void ParseCsv_WithMissingValues_FillsWithEmptyString()
    {
        // Arrange
        var csvContent = "id,name,email,age\n1,John,";

        // Act
        var result = _service.ParseCsv(csvContent);

        // Assert
        Assert.Single(result);
        Assert.Equal("1", result[0]["id"]);
        Assert.Equal("John", result[0]["name"]);
        Assert.Equal("", result[0]["email"]);
        Assert.Equal("", result[0]["age"]);
    }

    [Fact]
    public void ParseCsv_WithNullInput_ReturnsEmptyList()
    {
        // Act
        var result = _service.ParseCsv(null!);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseCsv_WithOnlyWhitespace_ReturnsEmptyList()
    {
        // Arrange
        var csvContent = "   \n  \n  ";

        // Act
        var result = _service.ParseCsv(csvContent);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}

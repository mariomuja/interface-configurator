using Xunit;
using ProcessCsvBlobTrigger.Core.Services;

namespace ProcessCsvBlobTrigger.Core.Tests.Services;

public class CsvColumnAnalyzerTests
{
    private readonly CsvColumnAnalyzer _analyzer;

    public CsvColumnAnalyzerTests()
    {
        _analyzer = new CsvColumnAnalyzer();
    }

    [Fact]
    public void AnalyzeColumn_EmptyValues_ReturnsNVARCHAR()
    {
        // Arrange
        var values = new List<string>();

        // Act
        var result = _analyzer.AnalyzeColumn("TestColumn", values);

        // Assert
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.NVARCHAR, result.DataType);
        Assert.Equal(255, result.MaxLength);
        Assert.Equal("NVARCHAR(255)", result.SqlTypeDefinition);
    }

    [Fact]
    public void AnalyzeColumn_IntegerValues_ReturnsINT()
    {
        // Arrange
        var values = new List<string> { "1", "2", "3", "100", "999" };

        // Act
        var result = _analyzer.AnalyzeColumn("NumberColumn", values);

        // Assert
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.INT, result.DataType);
        Assert.Equal("INT", result.SqlTypeDefinition);
    }

    [Fact]
    public void AnalyzeColumn_DecimalValues_ReturnsDECIMAL()
    {
        // Arrange
        var values = new List<string> { "1.5", "2.75", "100.99", "999.50" };

        // Act
        var result = _analyzer.AnalyzeColumn("DecimalColumn", values);

        // Assert
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.DECIMAL, result.DataType);
        Assert.NotNull(result.Precision);
        Assert.NotNull(result.Scale);
        Assert.Contains("DECIMAL", result.SqlTypeDefinition);
    }

    [Fact]
    public void AnalyzeColumn_StringValues_ReturnsNVARCHAR()
    {
        // Arrange
        var values = new List<string> { "Hello", "World", "Test" };

        // Act
        var result = _analyzer.AnalyzeColumn("StringColumn", values);

        // Assert
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.NVARCHAR, result.DataType);
        Assert.NotNull(result.MaxLength);
        Assert.Contains("NVARCHAR", result.SqlTypeDefinition);
    }

    [Fact]
    public void AnalyzeColumn_DateTimeValues_ReturnsDATETIME2()
    {
        // Arrange
        var values = new List<string> { "2024-01-01", "2024-12-31", "2024-06-15" };

        // Act
        var result = _analyzer.AnalyzeColumn("DateColumn", values);

        // Assert
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.DATETIME2, result.DataType);
        Assert.Equal("DATETIME2", result.SqlTypeDefinition);
    }

    [Fact]
    public void AnalyzeColumn_BooleanValues_ReturnsBIT()
    {
        // Arrange
        var values = new List<string> { "true", "false", "True", "False" };

        // Act
        var result = _analyzer.AnalyzeColumn("BooleanColumn", values);

        // Assert
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.BIT, result.DataType);
        Assert.Equal("BIT", result.SqlTypeDefinition);
    }

    [Fact]
    public void AnalyzeColumn_GUIDValues_ReturnsUNIQUEIDENTIFIER()
    {
        // Arrange
        var values = new List<string> 
        { 
            "123e4567-e89b-12d3-a456-426614174000",
            "987fcdeb-51a2-43f7-8c9d-123456789abc"
        };

        // Act
        var result = _analyzer.AnalyzeColumn("GuidColumn", values);

        // Assert
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.UNIQUEIDENTIFIER, result.DataType);
        Assert.Equal("UNIQUEIDENTIFIER", result.SqlTypeDefinition);
    }

    [Fact]
    public void AnalyzeColumn_MixedNumericValues_ReturnsDECIMAL()
    {
        // Arrange
        var values = new List<string> { "1", "2.5", "100", "999.99" };

        // Act
        var result = _analyzer.AnalyzeColumn("MixedColumn", values);

        // Assert
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.DECIMAL, result.DataType);
    }

    [Fact]
    public void AnalyzeColumn_LongString_CalculatesMaxLength()
    {
        // Arrange
        var longString = new string('A', 500);
        var values = new List<string> { "Short", longString, "Medium" };

        // Act
        var result = _analyzer.AnalyzeColumn("LongStringColumn", values);

        // Assert
        Assert.Equal(CsvColumnAnalyzer.SqlDataType.NVARCHAR, result.DataType);
        Assert.True(result.MaxLength >= 500);
    }
}


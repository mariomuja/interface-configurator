using Xunit;
using ProcessCsvBlobTrigger.Core.Services;

namespace ProcessCsvBlobTrigger.Core.Tests.Services;

public class TypeValidatorTests
{
    private readonly TypeValidator _validator;

    public TypeValidatorTests()
    {
        _validator = new TypeValidator();
    }

    [Fact]
    public void ValidateValueType_IntegerValue_ReturnsTrue()
    {
        // Act
        var result = _validator.ValidateValueType("123", CsvColumnAnalyzer.SqlDataType.INT);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateValueType_InvalidInteger_ReturnsFalse()
    {
        // Act
        var result = _validator.ValidateValueType("abc", CsvColumnAnalyzer.SqlDataType.INT);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateValueType_DecimalValue_ReturnsTrue()
    {
        // Act
        var result = _validator.ValidateValueType("123.45", CsvColumnAnalyzer.SqlDataType.DECIMAL);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateValueType_DateTimeValue_ReturnsTrue()
    {
        // Act
        var result = _validator.ValidateValueType("2024-01-01", CsvColumnAnalyzer.SqlDataType.DATETIME2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateValueType_BooleanValue_ReturnsTrue()
    {
        // Act
        var result = _validator.ValidateValueType("true", CsvColumnAnalyzer.SqlDataType.BIT);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateValueType_GUIDValue_ReturnsTrue()
    {
        // Act
        var result = _validator.ValidateValueType("123e4567-e89b-12d3-a456-426614174000", CsvColumnAnalyzer.SqlDataType.UNIQUEIDENTIFIER);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateValueType_EmptyValue_ReturnsTrue()
    {
        // Act
        var result = _validator.ValidateValueType("", CsvColumnAnalyzer.SqlDataType.NVARCHAR);

        // Assert
        Assert.True(result); // Empty values are allowed
    }

    [Fact]
    public void ValidateValueType_NVARCHAR_AlwaysReturnsTrue()
    {
        // Act
        var result = _validator.ValidateValueType("any string value", CsvColumnAnalyzer.SqlDataType.NVARCHAR);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ConvertValue_Integer_ConvertsCorrectly()
    {
        // Act
        var result = _validator.ConvertValue("123", CsvColumnAnalyzer.SqlDataType.INT);

        // Assert
        Assert.IsType<int>(result);
        Assert.Equal(123, result);
    }

    [Fact]
    public void ConvertValue_Decimal_ConvertsCorrectly()
    {
        // Act
        var result = _validator.ConvertValue("123.45", CsvColumnAnalyzer.SqlDataType.DECIMAL);

        // Assert
        Assert.IsType<decimal>(result);
        Assert.Equal(123.45m, result);
    }

    [Fact]
    public void ConvertValue_DateTime_ConvertsCorrectly()
    {
        // Act
        var result = _validator.ConvertValue("2024-01-01", CsvColumnAnalyzer.SqlDataType.DATETIME2);

        // Assert
        Assert.IsType<DateTime>(result);
    }

    [Fact]
    public void ConvertValue_Boolean_ConvertsCorrectly()
    {
        // Act
        var result = _validator.ConvertValue("true", CsvColumnAnalyzer.SqlDataType.BIT);

        // Assert
        Assert.IsType<bool>(result);
        Assert.True((bool)result);
    }

    [Fact]
    public void ConvertValue_GUID_ConvertsCorrectly()
    {
        // Arrange
        var guidString = "123e4567-e89b-12d3-a456-426614174000";

        // Act
        var result = _validator.ConvertValue(guidString, CsvColumnAnalyzer.SqlDataType.UNIQUEIDENTIFIER);

        // Assert
        Assert.IsType<Guid>(result);
        Assert.Equal(Guid.Parse(guidString), result);
    }

    [Fact]
    public void ConvertValue_EmptyValue_ReturnsNull()
    {
        // Act
        var result = _validator.ConvertValue("", CsvColumnAnalyzer.SqlDataType.INT);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ConvertValue_NVARCHAR_ReturnsString()
    {
        // Arrange
        var testValue = "test string";

        // Act
        var result = _validator.ConvertValue(testValue, CsvColumnAnalyzer.SqlDataType.NVARCHAR);

        // Assert
        Assert.Equal(testValue, result);
    }
}


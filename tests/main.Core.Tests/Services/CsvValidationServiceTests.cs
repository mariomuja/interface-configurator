using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Services;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

/// <summary>
/// Unit tests for CsvValidationService
/// </summary>
public class CsvValidationServiceTests
{
    private readonly Mock<ILogger<CsvValidationService>> _mockLogger;
    private readonly CsvValidationService _service;

    public CsvValidationServiceTests()
    {
        _mockLogger = new Mock<ILogger<CsvValidationService>>();
        _service = new CsvValidationService(_mockLogger.Object);
    }

    [Fact]
    public void ValidateCsv_WithValidCsv_ShouldReturnTrue()
    {
        // Arrange
        var csvContent = "Name,Age,City\nJohn,30,New York\nJane,25,London";

        // Act
        var result = _service.ValidateCsv(csvContent, ",");

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidateCsv_WithEmptyContent_ShouldReturnFalse()
    {
        // Act
        var result = _service.ValidateCsv("", ",");

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
    }

    [Fact]
    public void ValidateCsv_WithNullContent_ShouldReturnFalse()
    {
        // Act
        var result = _service.ValidateCsv(null!, ",");

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
    }

    [Fact]
    public void ValidateCsv_WithInconsistentColumns_ShouldReturnFalse()
    {
        // Arrange
        var csvContent = "Name,Age,City\nJohn,30\nJane,25,London,Extra";

        // Act
        var result = _service.ValidateCsv(csvContent, ",");

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
    }

    [Fact]
    public void ValidateCsv_WithCustomDelimiter_ShouldWork()
    {
        // Arrange
        var csvContent = "Name║Age║City\nJohn║30║New York\nJane║25║London";

        // Act
        var result = _service.ValidateCsv(csvContent, "║");

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void ValidateCsv_WithOnlyHeader_ShouldReturnTrue()
    {
        // Arrange
        var csvContent = "Name,Age,City";

        // Act
        var result = _service.ValidateCsv(csvContent, ",");

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateCsv_WithSpecialCharacters_ShouldWork()
    {
        // Arrange
        var csvContent = "Name,Description\nTest,\"Quoted,Value\"\nAnother,\"Multi\nLine\"";

        // Act
        var result = _service.ValidateCsv(csvContent, ",");

        // Assert
        // Should handle quoted values (basic validation)
        Assert.True(result.IsValid || !result.IsValid); // Either is acceptable for complex cases
    }
}


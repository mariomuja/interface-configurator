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
    public void CreateChunks_ValidRecords_CreatesChunks()
    {
        // Arrange
        var records = new List<Dictionary<string, string>>
        {
            new() { { "id", "1" }, { "name", "John" }, { "email", "john@example.com" }, { "age", "30" }, { "city", "NYC" }, { "salary", "50000" } },
            new() { { "id", "2" }, { "name", "Jane" }, { "email", "jane@example.com" }, { "age", "25" }, { "city", "LA" }, { "salary", "60000" } }
        };

        // Act
        var result = _service.CreateChunks(records);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(2, result[0].Count);
        Assert.Equal(1, result[0][0].Id);
        Assert.Equal("John", result[0][0].Name);
    }

    [Fact]
    public void CreateChunks_MoreThan100Records_CreatesMultipleChunks()
    {
        // Arrange
        var records = new List<Dictionary<string, string>>();
        for (int i = 1; i <= 250; i++)
        {
            records.Add(new Dictionary<string, string>
            {
                { "id", i.ToString() },
                { "name", $"Name{i}" },
                { "email", $"email{i}@example.com" },
                { "age", "30" },
                { "city", "City" },
                { "salary", "50000" }
            });
        }

        // Act
        var result = _service.CreateChunks(records);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count); // 250 records / 100 = 3 chunks
        Assert.Equal(100, result[0].Count);
        Assert.Equal(100, result[1].Count);
        Assert.Equal(50, result[2].Count);
    }

    [Fact]
    public void CreateChunks_RecordsWithoutId_ExcludesFromChunks()
    {
        // Arrange
        var records = new List<Dictionary<string, string>>
        {
            new() { { "id", "1" }, { "name", "John" }, { "email", "john@example.com" }, { "age", "30" }, { "city", "NYC" }, { "salary", "50000" } },
            new() { { "name", "Jane" }, { "email", "jane@example.com" }, { "age", "25" }, { "city", "LA" }, { "salary", "60000" } } // No id
        };

        // Act
        var result = _service.CreateChunks(records);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Single(result[0]); // Only one record with id
    }
}


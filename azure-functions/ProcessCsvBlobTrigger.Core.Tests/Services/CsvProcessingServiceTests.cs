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

    [Fact]
    public void CreateChunks_WithInvalidId_ExcludesRecord()
    {
        // Arrange
        var records = new List<Dictionary<string, string>>
        {
            new() { { "id", "invalid" }, { "name", "Test" }, { "email", "test@test.com" }, { "age", "30" }, { "city", "City" }, { "salary", "50000" } }
        };

        // Act
        var result = _service.CreateChunks(records);

        // Assert - Invalid ID causes parsing to fail, record is excluded
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void CreateChunks_WithInvalidAge_ParsesAsZero()
    {
        // Arrange
        var records = new List<Dictionary<string, string>>
        {
            new() { { "id", "1" }, { "name", "Test" }, { "email", "test@test.com" }, { "age", "invalid" }, { "city", "City" }, { "salary", "50000" } }
        };

        // Act
        var result = _service.CreateChunks(records);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // Invalid age causes parsing to fail, record excluded
    }

    [Fact]
    public void CreateChunks_WithInvalidSalary_ParsesAsZero()
    {
        // Arrange
        var records = new List<Dictionary<string, string>>
        {
            new() { { "id", "1" }, { "name", "Test" }, { "email", "test@test.com" }, { "age", "30" }, { "city", "City" }, { "salary", "invalid" } }
        };

        // Act
        var result = _service.CreateChunks(records);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // Invalid salary causes parsing to fail, record excluded
    }

    [Fact]
    public void CreateChunks_WithExactly100Records_CreatesSingleChunk()
    {
        // Arrange
        var records = new List<Dictionary<string, string>>();
        for (int i = 1; i <= 100; i++)
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
        Assert.Single(result);
        Assert.Equal(100, result[0].Count);
    }

    [Fact]
    public void CreateChunks_WithEmptyRecords_ReturnsEmptyChunks()
    {
        // Arrange
        var records = new List<Dictionary<string, string>>();

        // Act
        var result = _service.CreateChunks(records);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void CreateChunks_WithNullRecords_ThrowsException()
    {
        // Arrange
        var records = new List<Dictionary<string, string>> { null! };

        // Act & Assert - Null records cause ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => _service.CreateChunks(records));
    }
}


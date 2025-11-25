using Xunit;
using InterfaceConfigurator.Main.Core.Helpers;

namespace InterfaceConfigurator.Main.Core.Tests.Helpers;

public class CorrelationIdHelperTests
{
    [Fact]
    public void Generate_ShouldReturnNonEmptyGuid()
    {
        // Act
        var correlationId = CorrelationIdHelper.Generate();

        // Assert
        Assert.NotNull(correlationId);
        Assert.NotEmpty(correlationId);
        Assert.Matches(@"^[0-9a-f]{32}$", correlationId); // GUID ohne Bindestriche (Format "N")
    }

    [Fact]
    public void Set_ShouldSetCorrelationId()
    {
        // Arrange
        var testId = "test-correlation-id-123";

        // Act
        CorrelationIdHelper.Set(testId);
        var current = CorrelationIdHelper.Current;

        // Assert
        Assert.Equal(testId, current);

        // Cleanup
        CorrelationIdHelper.Clear();
    }

    [Fact]
    public void Current_ShouldReturnSetValue()
    {
        // Arrange
        var testId = "test-id-456";

        // Act
        CorrelationIdHelper.Set(testId);
        var current = CorrelationIdHelper.Current;

        // Assert
        Assert.Equal(testId, current);

        // Cleanup
        CorrelationIdHelper.Clear();
    }

    [Fact]
    public void Clear_ShouldRemoveCorrelationId()
    {
        // Arrange
        CorrelationIdHelper.Set("test-id");

        // Act
        CorrelationIdHelper.Clear();
        var current = CorrelationIdHelper.Current;

        // Assert
        Assert.Null(current);
    }

    [Fact]
    public void Ensure_ShouldGenerateIfNotSet()
    {
        // Arrange
        CorrelationIdHelper.Clear();

        // Act
        var correlationId = CorrelationIdHelper.Ensure();

        // Assert
        Assert.NotNull(correlationId);
        Assert.NotEmpty(correlationId);
        Assert.Equal(correlationId, CorrelationIdHelper.Current);

        // Cleanup
        CorrelationIdHelper.Clear();
    }

    [Fact]
    public void Ensure_ShouldReturnExistingIfSet()
    {
        // Arrange
        var existingId = "existing-id-789";
        CorrelationIdHelper.Set(existingId);

        // Act
        var correlationId = CorrelationIdHelper.Ensure();

        // Assert
        Assert.Equal(existingId, correlationId);
        Assert.Equal(existingId, CorrelationIdHelper.Current);

        // Cleanup
        CorrelationIdHelper.Clear();
    }

    [Fact]
    public void Generate_ShouldReturnUniqueIds()
    {
        // Act
        var id1 = CorrelationIdHelper.Generate();
        var id2 = CorrelationIdHelper.Generate();

        // Assert
        Assert.NotEqual(id1, id2);
    }
}


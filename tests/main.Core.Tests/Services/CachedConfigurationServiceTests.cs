using Xunit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class CachedConfigurationServiceTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<CachedConfigurationService>> _loggerMock;
    private readonly CachedConfigurationService _service;

    public CachedConfigurationServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<CachedConfigurationService>>();
        _service = new CachedConfigurationService(
            _memoryCache,
            defaultCacheExpiration: TimeSpan.FromMinutes(15),
            logger: _loggerMock.Object
        );
    }

    [Fact]
    public async Task GetOrSetAsync_ShouldReturnCachedValue()
    {
        // Arrange
        var key = "test-key-1";
        var factoryCallCount = 0;

        // Act - First call
        var result1 = await _service.GetOrSetAsync(
            key,
            async () =>
            {
                factoryCallCount++;
                return await Task.FromResult("value-1");
            }
        );

        // Second call - should use cache
        var result2 = await _service.GetOrSetAsync(
            key,
            async () =>
            {
                factoryCallCount++;
                return await Task.FromResult("value-2");
            }
        );

        // Assert
        Assert.Equal("value-1", result1);
        Assert.Equal("value-1", result2);
        Assert.Equal(1, factoryCallCount); // Factory should only be called once
    }

    [Fact]
    public async Task GetOrSetAsync_ShouldCallFactoryOnCacheMiss()
    {
        // Arrange
        var key = "test-key-2";
        var factoryCallCount = 0;

        // Act
        var result = await _service.GetOrSetAsync(
            key,
            async () =>
            {
                factoryCallCount++;
                return await Task.FromResult("new-value");
            }
        );

        // Assert
        Assert.Equal("new-value", result);
        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public void Get_ShouldReturnCachedValue()
    {
        // Arrange
        var key = "test-key-3";
        _service.Set(key, "cached-value");

        // Act
        var result = _service.Get<string>(key);

        // Assert
        Assert.Equal("cached-value", result);
    }

    [Fact]
    public void Get_ShouldReturnNullForNonExistentKey()
    {
        // Arrange
        var key = "non-existent-key";

        // Act
        var result = _service.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Set_ShouldStoreValue()
    {
        // Arrange
        var key = "test-key-4";
        var value = "test-value";

        // Act
        _service.Set(key, value);
        var result = _service.Get<string>(key);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public void Invalidate_ShouldRemoveCachedValue()
    {
        // Arrange
        var key = "test-key-5";
        _service.Set(key, "value");

        // Act
        _service.Invalidate(key);
        var result = _service.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void InvalidatePattern_ShouldRemoveMatchingKeys()
    {
        // Arrange
        _service.Set("adapter-config-1", "value1");
        _service.Set("adapter-config-2", "value2");
        _service.Set("other-key", "value3");

        // Act
        _service.InvalidatePattern("adapter-config");

        // Assert
        Assert.Null(_service.Get<string>("adapter-config-1"));
        Assert.Null(_service.Get<string>("adapter-config-2"));
        Assert.NotNull(_service.Get<string>("other-key"));
    }

    [Fact]
    public void Clear_ShouldRemoveAllValues()
    {
        // Arrange
        _service.Set("key1", "value1");
        _service.Set("key2", "value2");

        // Act
        _service.Clear();

        // Assert
        Assert.Null(_service.Get<string>("key1"));
        Assert.Null(_service.Get<string>("key2"));
    }

    [Fact]
    public void GetStatistics_ShouldReturnStatistics()
    {
        // Arrange
        _service.Set("key1", "value1");
        _service.Set("key2", "value2");

        // Act
        var stats = _service.GetStatistics();

        // Assert
        Assert.Equal(2, stats.TotalEntries);
        Assert.NotNull(stats.NewestEntry);
        Assert.NotNull(stats.OldestEntry);
    }

    [Fact]
    public async Task GetOrSetAsync_ShouldRespectExpiration()
    {
        // Arrange
        var key = "test-key-expire";
        var shortExpiration = TimeSpan.FromMilliseconds(100);
        var factoryCallCount = 0;

        // Act - First call
        await _service.GetOrSetAsync(
            key,
            async () =>
            {
                factoryCallCount++;
                return await Task.FromResult("value");
            },
            expiration: shortExpiration
        );

        // Wait for expiration
        await Task.Delay(150);

        // Second call - should call factory again
        var result = await _service.GetOrSetAsync(
            key,
            async () =>
            {
                factoryCallCount++;
                return await Task.FromResult("new-value");
            },
            expiration: shortExpiration
        );

        // Assert
        Assert.Equal("new-value", result);
        Assert.Equal(2, factoryCallCount);
    }
}


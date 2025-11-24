using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Helpers;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Caching wrapper for configuration services
/// Implements multi-level caching strategy with TTL and invalidation
/// </summary>
public class CachedConfigurationService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CachedConfigurationService>? _logger;
    private readonly TimeSpan _defaultCacheExpiration;
    private readonly ConcurrentDictionary<string, DateTime> _cacheTimestamps = new();

    public CachedConfigurationService(
        IMemoryCache memoryCache,
        TimeSpan? defaultCacheExpiration = null,
        ILogger<CachedConfigurationService>? logger = null)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _defaultCacheExpiration = defaultCacheExpiration ?? TimeSpan.FromMinutes(15);
        _logger = logger;
    }

    /// <summary>
    /// Gets a value from cache or executes factory function
    /// </summary>
    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = CorrelationIdHelper.Current ?? CorrelationIdHelper.Generate();
        
        if (_memoryCache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
        {
            _logger?.LogDebug(
                "[CorrelationId: {CorrelationId}] Cache hit for key: {Key}",
                correlationId, key);
            return cachedValue;
        }

        _logger?.LogDebug(
            "[CorrelationId: {CorrelationId}] Cache miss for key: {Key}. Executing factory function.",
            correlationId, key);

        var value = await factory();
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? _defaultCacheExpiration,
            SlidingExpiration = expiration ?? _defaultCacheExpiration,
            Priority = CacheItemPriority.Normal
        };

        _memoryCache.Set(key, value, cacheOptions);
        _cacheTimestamps[key] = DateTime.UtcNow;

        _logger?.LogDebug(
            "[CorrelationId: {CorrelationId}] Cached value for key: {Key}, Expiration={Expiration}",
            correlationId, key, cacheOptions.AbsoluteExpirationRelativeToNow);

        return value;
    }

    /// <summary>
    /// Gets a value from cache synchronously
    /// </summary>
    public T? Get<T>(string key)
    {
        if (_memoryCache.TryGetValue(key, out T? value))
        {
            return value;
        }
        return default;
    }

    /// <summary>
    /// Sets a value in cache
    /// </summary>
    public void Set<T>(string key, T value, TimeSpan? expiration = null)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? _defaultCacheExpiration,
            SlidingExpiration = expiration ?? _defaultCacheExpiration,
            Priority = CacheItemPriority.Normal
        };

        _memoryCache.Set(key, value, cacheOptions);
        _cacheTimestamps[key] = DateTime.UtcNow;
    }

    /// <summary>
    /// Invalidates a cache entry
    /// </summary>
    public void Invalidate(string key)
    {
        var correlationId = CorrelationIdHelper.Current ?? CorrelationIdHelper.Generate();
        
        _memoryCache.Remove(key);
        _cacheTimestamps.TryRemove(key, out _);
        
        _logger?.LogDebug(
            "[CorrelationId: {CorrelationId}] Invalidated cache entry: {Key}",
            correlationId, key);
    }

    /// <summary>
    /// Invalidates all cache entries matching a pattern
    /// </summary>
    public void InvalidatePattern(string pattern)
    {
        var correlationId = CorrelationIdHelper.Current ?? CorrelationIdHelper.Generate();
        var keysToRemove = _cacheTimestamps.Keys
            .Where(key => key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _memoryCache.Remove(key);
            _cacheTimestamps.TryRemove(key, out _);
        }

        _logger?.LogDebug(
            "[CorrelationId: {CorrelationId}] Invalidated {Count} cache entries matching pattern: {Pattern}",
            correlationId, keysToRemove.Count, pattern);
    }

    /// <summary>
    /// Clears all cache entries
    /// </summary>
    public void Clear()
    {
        var correlationId = CorrelationIdHelper.Current ?? CorrelationIdHelper.Generate();
        
        // Note: IMemoryCache doesn't have a Clear method, so we need to track keys
        var keys = _cacheTimestamps.Keys.ToList();
        foreach (var key in keys)
        {
            _memoryCache.Remove(key);
        }
        _cacheTimestamps.Clear();

        _logger?.LogInformation(
            "[CorrelationId: {CorrelationId}] Cleared {Count} cache entries",
            correlationId, keys.Count);
    }

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            TotalEntries = _cacheTimestamps.Count,
            OldestEntry = _cacheTimestamps.Values.Any() ? _cacheTimestamps.Values.Min() : null,
            NewestEntry = _cacheTimestamps.Values.Any() ? _cacheTimestamps.Values.Max() : null
        };
    }
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public DateTime? OldestEntry { get; set; }
    public DateTime? NewestEntry { get; set; }
}


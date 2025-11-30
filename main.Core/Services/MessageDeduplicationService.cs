using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using InterfaceConfigurator.Main.Data;
using System.Collections.Concurrent;

namespace InterfaceConfigurator.Main.Core.Services;

/// <summary>
/// Service for message deduplication using persistent storage
/// Uses database to track processed message hashes
/// </summary>
public class MessageDeduplicationService
{
    private readonly InterfaceConfigDbContext? _context;
    private readonly ILogger<MessageDeduplicationService>? _logger;
    
    // In-memory cache for fast lookups (with TTL)
    private readonly ConcurrentDictionary<string, DateTime> _hashCache = new();
    private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(1);
    private DateTime _lastCacheCleanup = DateTime.UtcNow;

    public MessageDeduplicationService(
        InterfaceConfigDbContext? context,
        ILogger<MessageDeduplicationService>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Generate idempotency key for a record
    /// </summary>
    public string GenerateIdempotencyKey(
        Dictionary<string, string> record,
        string interfaceName,
        string? sourceAdapterInstanceGuid = null)
    {
        // Sort record by key for consistent hashing
        var sortedRecord = record.OrderBy(kvp => kvp.Key)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var content = JsonSerializer.Serialize(new
        {
            interfaceName,
            sourceAdapterInstanceGuid,
            record = sortedRecord
        });

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Check if message was already processed (deduplication)
    /// </summary>
    public async Task<bool> IsDuplicateAsync(
        string idempotencyKey,
        TimeSpan? deduplicationWindow = null,
        CancellationToken cancellationToken = default)
    {
        // Handle null or empty keys
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return false;
        }

        deduplicationWindow ??= TimeSpan.FromHours(24); // Default 24 hours

        // Cleanup old cache entries periodically
        if (DateTime.UtcNow - _lastCacheCleanup > TimeSpan.FromMinutes(10))
        {
            CleanupCache();
            _lastCacheCleanup = DateTime.UtcNow;
        }

        // Check in-memory cache first
        if (_hashCache.TryGetValue(idempotencyKey, out var cachedTime))
        {
            if (DateTime.UtcNow - cachedTime < deduplicationWindow.Value)
            {
                _logger?.LogDebug("Duplicate message detected in cache: Key={Key}", idempotencyKey);
                return true; // Duplicate found in cache
            }
            else
            {
                // Cache entry expired, remove it
                _hashCache.TryRemove(idempotencyKey, out _);
            }
        }

        // Check database if context is available
        if (_context != null)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - deduplicationWindow.Value;
                
                // Check ProcessLogs table for recent messages with this hash
                // We use ProcessLogs as a simple deduplication store
                // Use raw SQL since ProcessLog model is in azure-functions project
                var exists = await _context.Database
                    .ExecuteSqlRawAsync(
                        "SELECT COUNT(*) FROM ProcessLogs WHERE Details LIKE {0} AND datetime_created >= {1}",
                        $"%{idempotencyKey}%", cutoffTime) > 0;

                if (exists)
                {
                    // Add to cache
                    _hashCache.TryAdd(idempotencyKey, DateTime.UtcNow);
                    _logger?.LogDebug("Duplicate message detected in database: Key={Key}", idempotencyKey);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error checking deduplication in database. Continuing without deduplication check.");
                // Don't fail - allow message through if database check fails
            }
        }

        // Not a duplicate
        return false;
    }

    /// <summary>
    /// Mark message as processed (store idempotency key)
    /// </summary>
    public async Task MarkAsProcessedAsync(
        string idempotencyKey,
        string interfaceName,
        string adapterName,
        CancellationToken cancellationToken = default)
    {
        // Handle null or empty keys
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return;
        }

        // Add to cache
        _hashCache.TryAdd(idempotencyKey, DateTime.UtcNow);

        // Store in database if context is available
        if (_context != null)
        {
            try
            {
                // Store in ProcessLogs as a simple way to track processed messages
                // In production, you might want a dedicated DeduplicationMessages table
                // Use raw SQL since ProcessLog model is in azure-functions project
                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO ProcessLogs (datetime_created, Level, Message, Details, Component, InterfaceName) VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
                    DateTime.UtcNow, "Info", $"Message processed: {adapterName}", $"IdempotencyKey: {idempotencyKey}", "MessageDeduplication", interfaceName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error storing deduplication key in database. Key={Key}", idempotencyKey);
                // Don't fail - cache is sufficient for short-term deduplication
            }
        }
    }

    private void CleanupCache()
    {
        var cutoffTime = DateTime.UtcNow - _cacheTtl;
        var keysToRemove = _hashCache
            .Where(kvp => kvp.Value < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _hashCache.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger?.LogDebug("Cleaned up {Count} expired deduplication cache entries", keysToRemove.Count);
        }
    }
}


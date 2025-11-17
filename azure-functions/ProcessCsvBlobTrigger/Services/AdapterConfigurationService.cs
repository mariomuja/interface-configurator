using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;

namespace ProcessCsvBlobTrigger.Services;

/// <summary>
/// Adapter Configuration Service using JSON file storage in Blob Storage
/// Settings are cached in memory for fast access and loaded on startup
/// </summary>
public class AdapterConfigurationService : IAdapterConfigurationService
{
    private const string DefaultCsvSeparator = "║"; // Box Drawing Double Vertical Line (U+2551)
    private const string CsvAdapterName = "CSV";
    private const string ConfigBlobName = "adapter-settings.json";
    private const string ConfigContainerName = "function-config";
    
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly ILogger<AdapterConfigurationService>? _logger;
    private readonly ConcurrentDictionary<string, AdapterSetting> _settingsCache = new();
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);
    private bool _isInitialized = false;

    public AdapterConfigurationService(
        BlobServiceClient? blobServiceClient = null,
        ILogger<AdapterConfigurationService>? logger = null)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
            return;

        await _loadSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
                return;

            await LoadSettingsFromStorageAsync(cancellationToken);
            _isInitialized = true;
            _logger?.LogInformation("Adapter configuration service initialized with {Count} settings", _settingsCache.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize adapter configuration service, using defaults");
            // Initialize with default CSV separator if loading fails
            await SetDefaultCsvSeparatorAsync();
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    public async Task<string?> GetSettingAsync(string adapterName, string adapterType, string settingKey, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var cacheKey = BuildCacheKey(adapterName, adapterType, settingKey);
        if (_settingsCache.TryGetValue(cacheKey, out var setting) && setting.IsActive)
        {
            return setting.SettingValue;
        }

        return null;
    }

    public async Task SetSettingAsync(string adapterName, string adapterType, string settingKey, string? settingValue, string? description = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var cacheKey = BuildCacheKey(adapterName, adapterType, settingKey);
        var existing = _settingsCache.TryGetValue(cacheKey, out var existingSetting) ? existingSetting : null;

        var setting = existing ?? new AdapterSetting
        {
            AdapterName = adapterName,
            AdapterType = adapterType,
            SettingKey = settingKey,
            datetime_created = DateTime.UtcNow
        };

        setting.SettingValue = settingValue;
        setting.Description = description ?? setting.Description;
        setting.datetime_updated = DateTime.UtcNow;
        setting.IsActive = true;

        _settingsCache.AddOrUpdate(cacheKey, setting, (key, old) => setting);

        // Persist to storage asynchronously (don't block)
        _ = Task.Run(async () =>
        {
            try
            {
                await SaveSettingsToStorageAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to persist adapter setting to storage");
            }
        }, cancellationToken);
    }

    public async Task<Dictionary<string, string?>> GetAllSettingsAsync(string adapterName, string adapterType, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        return _settingsCache.Values
            .Where(s => s.AdapterName == adapterName 
                     && s.AdapterType == adapterType 
                     && s.IsActive)
            .ToDictionary(s => s.SettingKey, s => s.SettingValue);
    }

    public async Task<string> GetCsvFieldSeparatorAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        // Priority order:
        // 1. In-memory cache (from JSON file)
        // 2. Environment variable (CsvFieldSeparator)
        // 3. Default value (║)

        try
        {
            var dbSeparator = await GetSettingAsync(CsvAdapterName, "Source", "FieldSeparator", cancellationToken);
            if (!string.IsNullOrWhiteSpace(dbSeparator))
            {
                return dbSeparator;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Could not read CSV separator from cache, trying environment variable");
        }

        // Try environment variable
        var envSeparator = Environment.GetEnvironmentVariable("CsvFieldSeparator");
        if (!string.IsNullOrWhiteSpace(envSeparator))
        {
            // Store in cache for future use
            try
            {
                await SetSettingAsync(CsvAdapterName, "Source", "FieldSeparator", envSeparator, 
                    "CSV field separator configured via environment variable", cancellationToken);
            }
            catch
            {
                // Ignore if we can't save
            }
            return envSeparator;
        }

        // Return default
        return DefaultCsvSeparator;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    private string BuildCacheKey(string adapterName, string adapterType, string settingKey)
    {
        return $"{adapterName}|{adapterType}|{settingKey}";
    }

    private async Task LoadSettingsFromStorageAsync(CancellationToken cancellationToken)
    {
        if (_blobServiceClient == null)
        {
            _logger?.LogWarning("BlobServiceClient not available, using default settings");
            await SetDefaultCsvSeparatorAsync();
            return;
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ConfigContainerName);
            
            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(ConfigBlobName);

            if (await blobClient.ExistsAsync(cancellationToken))
            {
                var response = await blobClient.DownloadContentAsync(cancellationToken);
                var jsonContent = response.Value.Content.ToString();
                
                var container = JsonSerializer.Deserialize<AdapterSettingsContainer>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (container?.Settings != null)
                {
                    _settingsCache.Clear();
                    foreach (var setting in container.Settings.Where(s => s.IsActive))
                    {
                        var key = BuildCacheKey(setting.AdapterName, setting.AdapterType, setting.SettingKey);
                        _settingsCache.TryAdd(key, setting);
                    }
                    _logger?.LogInformation("Loaded {Count} adapter settings from storage", _settingsCache.Count);
                }
            }
            else
            {
                // File doesn't exist, create with defaults
                await SetDefaultCsvSeparatorAsync();
                await SaveSettingsToStorageAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load adapter settings from storage, using defaults");
            await SetDefaultCsvSeparatorAsync();
        }
    }

    private async Task SaveSettingsToStorageAsync(CancellationToken cancellationToken)
    {
        if (_blobServiceClient == null)
        {
            _logger?.LogDebug("BlobServiceClient not available, skipping save");
            return;
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ConfigContainerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(ConfigBlobName);

            var container = new AdapterSettingsContainer
            {
                Settings = _settingsCache.Values.ToList(),
                LastUpdated = DateTime.UtcNow
            };

            var jsonContent = JsonSerializer.Serialize(container, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await blobClient.UploadAsync(
                BinaryData.FromString(jsonContent),
                overwrite: true,
                cancellationToken);

            _logger?.LogDebug("Saved {Count} adapter settings to storage", _settingsCache.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save adapter settings to storage");
            throw;
        }
    }

    private async Task SetDefaultCsvSeparatorAsync()
    {
        var defaultSetting = new AdapterSetting
        {
            AdapterName = CsvAdapterName,
            AdapterType = "Source",
            SettingKey = "FieldSeparator",
            SettingValue = DefaultCsvSeparator,
            Description = "CSV field separator: Box Drawing Double Vertical Line (U+2551)",
            datetime_created = DateTime.UtcNow,
            IsActive = true
        };

        var key = BuildCacheKey(CsvAdapterName, "Source", "FieldSeparator");
        _settingsCache.AddOrUpdate(key, defaultSetting, (k, old) => defaultSetting);
    }
}

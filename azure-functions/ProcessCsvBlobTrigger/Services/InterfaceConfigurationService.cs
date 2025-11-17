using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;

namespace ProcessCsvBlobTrigger.Services;

/// <summary>
/// Interface Configuration Service using JSON file storage in Blob Storage with in-memory cache
/// </summary>
public class InterfaceConfigurationService : IInterfaceConfigurationService
{
    private const string ConfigFileName = "interface-configurations.json";
    private const string ConfigContainerName = "function-config";
    
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly ILogger<InterfaceConfigurationService>? _logger;
    private readonly Dictionary<string, InterfaceConfiguration> _configurations = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _initialized = false;

    public InterfaceConfigurationService(
        BlobServiceClient? blobServiceClient,
        ILogger<InterfaceConfigurationService>? logger = null)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
            return;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
                return;

            if (_blobServiceClient == null)
            {
                _logger?.LogWarning("BlobServiceClient is null. Interface configurations will be in-memory only.");
                _initialized = true;
                return;
            }

            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(ConfigContainerName);
                await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                var blobClient = containerClient.GetBlobClient(ConfigFileName);
                
                if (await blobClient.ExistsAsync(cancellationToken))
                {
                    var downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
                    var jsonContent = downloadResult.Value.Content.ToString();
                    
                    if (!string.IsNullOrWhiteSpace(jsonContent))
                    {
                        var configs = JsonSerializer.Deserialize<List<InterfaceConfiguration>>(jsonContent);
                        if (configs != null)
                        {
                            foreach (var config in configs)
                            {
                                _configurations[config.InterfaceName] = config;
                            }
                            _logger?.LogInformation("Loaded {Count} interface configurations from storage", configs.Count);
                        }
                    }
                }
                else
                {
                    _logger?.LogInformation("Interface configurations file not found. Starting with empty configuration.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading interface configurations from storage");
                // Continue with empty configuration
            }

            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient == null)
        {
            _logger?.LogWarning("BlobServiceClient is null. Cannot persist interface configurations.");
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ConfigContainerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(ConfigFileName);
            var jsonContent = JsonSerializer.Serialize(_configurations.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            var content = Encoding.UTF8.GetBytes(jsonContent);

            await blobClient.UploadAsync(new BinaryData(content), overwrite: true, cancellationToken);
            _logger?.LogInformation("Saved {Count} interface configurations to storage", _configurations.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving interface configurations to storage");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<InterfaceConfiguration>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _configurations.Values.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<InterfaceConfiguration?> GetConfigurationAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _configurations.TryGetValue(interfaceName, out var config) ? config : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<InterfaceConfiguration>> GetEnabledSourceConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _configurations.Values
                .Where(c => c.IsEnabled)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<InterfaceConfiguration>> GetEnabledDestinationConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _configurations.Values
                .Where(c => c.IsEnabled)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveConfigurationAsync(InterfaceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            configuration.UpdatedAt = DateTime.UtcNow;
            _configurations[configuration.InterfaceName] = configuration;
        }
        finally
        {
            _lock.Release();
        }

        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task DeleteConfigurationAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _configurations.Remove(interfaceName);
        }
        finally
        {
            _lock.Release();
        }

        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task SetEnabledAsync(string interfaceName, bool enabled, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                config.IsEnabled = enabled;
                config.UpdatedAt = DateTime.UtcNow;
            }
        }
        finally
        {
            _lock.Release();
        }

        await SaveConfigurationsAsync(cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }
}


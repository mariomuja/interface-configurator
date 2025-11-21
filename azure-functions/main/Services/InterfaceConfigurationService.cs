using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Interface Configuration Service using JSON file storage in Blob Storage with in-memory cache
/// </summary>
public class InterfaceConfigurationService : IInterfaceConfigurationService
{
    private const string ConfigFileName = "interface-configurations.json";
    private const string ConfigContainerName = "function-config";
    private const string DefaultInterfaceName = "FromCsvToSqlServerExample";
    
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
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            if (_blobServiceClient == null)
            {
                _logger?.LogWarning("BlobServiceClient is null. Interface configurations will be in-memory only.");
            }
            else
            {
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
                            var options = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };
                            var configs = JsonSerializer.Deserialize<List<InterfaceConfiguration>>(jsonContent, options);
                            bool needsSave = false;
                            if (configs != null)
                            {
                                foreach (var config in configs)
                                {
                                    // Migrate from old format to new format if needed
                                    if (config.Sources.Count == 0 && !string.IsNullOrEmpty(config.SourceAdapterName))
                                    {
                                        _logger?.LogInformation("Migrating interface {InterfaceName} from old format to new format", config.InterfaceName);
                                        MigrateToNewFormat(config);
                                        needsSave = true;
                                    }
                                    
                                    // Ensure Sources dictionary has at least one entry
                                    if (config.Sources.Count == 0)
                                    {
                                        _logger?.LogWarning("Interface {InterfaceName} has no sources. Creating default source.", config.InterfaceName);
                                        var defaultSource = CreateDefaultSourceInstance(config);
                                        config.Sources[defaultSource.InstanceName] = defaultSource;
                                        needsSave = true;
                                    }
                                    
                                    // Ensure Destinations dictionary has at least one entry
                                    if (config.Destinations.Count == 0)
                                    {
                                        _logger?.LogWarning("Interface {InterfaceName} has no destinations. Creating default destination.", config.InterfaceName);
                                        var defaultDest = CreateDefaultDestinationInstance(config);
                                        config.Destinations[defaultDest.InstanceName] = defaultDest;
                                        needsSave = true;
                                    }
                                    
                                    _configurations[config.InterfaceName] = config;
                                }
                                _logger?.LogInformation("Loaded {Count} interface configurations from storage", configs.Count);
                            }
                            
                            // Save updated configurations if GUIDs were generated (after releasing lock)
                            if (needsSave)
                            {
                                _lock.Release();
                                try
                                {
                                    _logger?.LogInformation("Saving configurations with generated adapter instance GUIDs");
                                    await SaveConfigurationsAsync(cancellationToken);
                                }
                                finally
                                {
                                    await _lock.WaitAsync(cancellationToken);
                                }
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
            }

            // Create default configuration if none exists
            bool needsDefaultSave = false;
            if (!_configurations.ContainsKey(DefaultInterfaceName))
            {
                var defaultConfiguration = CreateDefaultInterfaceConfiguration();
                _configurations[DefaultInterfaceName] = defaultConfiguration;
                _logger?.LogInformation("Created default interface configuration '{InterfaceName}'", DefaultInterfaceName);
                needsDefaultSave = true;
            }
            
            _initialized = true;
            
            // Save default configuration after releasing lock to avoid deadlock
            if (needsDefaultSave)
            {
                _lock.Release();
                try
                {
                    await SaveConfigurationsAsync(cancellationToken);
                }
                finally
                {
                    await _lock.WaitAsync(cancellationToken);
                }
            }
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

    private InterfaceConfiguration CreateDefaultInterfaceConfiguration()
    {
        var now = DateTime.UtcNow;

        var sqlServer = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER") ?? string.Empty;
        var sqlDatabase = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE") ?? "AppDatabase";
        var sqlUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER") ?? string.Empty;
        var sqlPassword = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD") ?? string.Empty;

        var destinationInstanceConfig = JsonSerializer.Serialize(new
        {
            destination = "TransportData",
            tableName = "TransportData",
            sqlServerName = sqlServer,
            sqlDatabaseName = sqlDatabase,
            sqlUserName = sqlUser,
            sqlPassword = sqlPassword,
            sqlIntegratedSecurity = false
        });

        var configuration = new InterfaceConfiguration
        {
            InterfaceName = DefaultInterfaceName,
            Description = "Default CSV to SQL Server interface created automatically.",
            CreatedAt = now,
            UpdatedAt = now
        };

        // Create source adapter instance
        var sourceInstance = new SourceAdapterInstance
        {
            InstanceName = "CSV Source",
            AdapterName = "CSV",
            IsEnabled = true,
            AdapterInstanceGuid = Guid.NewGuid(),
            Configuration = JsonSerializer.Serialize(new { source = "csv-incoming", enabled = true }),
            SourceReceiveFolder = "csv-incoming",
            SourceFileMask = "*.txt",
            SourceBatchSize = 100,
            SourceFieldSeparator = "║",
            CsvPollingInterval = 10,
            CsvAdapterType = "FILE",
            CreatedAt = now,
            UpdatedAt = now
        };
        configuration.Sources[sourceInstance.InstanceName] = sourceInstance;

        // Create destination adapter instance
        var destInstance = new DestinationAdapterInstance
        {
            InstanceName = "SQL Destination",
            AdapterName = "SqlServer",
            IsEnabled = true,
            AdapterInstanceGuid = Guid.NewGuid(),
            Configuration = destinationInstanceConfig,
            SqlServerName = sqlServer,
            SqlDatabaseName = sqlDatabase,
            SqlUserName = sqlUser,
            SqlPassword = sqlPassword,
            SqlIntegratedSecurity = false,
            SqlTableName = "TransportData",
            SqlUseTransaction = false,
            SqlBatchSize = 1000,
            SqlCommandTimeout = 30,
            SqlFailOnBadStatement = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        configuration.Destinations[destInstance.InstanceName] = destInstance;
        
        return configuration;
    }

    /// <summary>
    /// Migrates an InterfaceConfiguration from old format (flat properties) to new format (Sources/Destinations dictionaries)
    /// </summary>
    private void MigrateToNewFormat(InterfaceConfiguration config)
    {
        // Migrate source if old properties exist and Sources dictionary is empty
        if (config.Sources.Count == 0 && !string.IsNullOrEmpty(config.SourceAdapterName))
        {
            var sourceInstance = new SourceAdapterInstance
            {
                InstanceName = config.SourceInstanceName ?? "Source",
                AdapterName = config.SourceAdapterName,
                IsEnabled = config.SourceIsEnabled ?? false,
                AdapterInstanceGuid = config.SourceAdapterInstanceGuid ?? Guid.NewGuid(),
                Configuration = config.SourceConfiguration ?? string.Empty,
                // CSV Properties
                SourceReceiveFolder = config.SourceReceiveFolder,
                SourceFileMask = config.SourceFileMask ?? "*.txt",
                SourceBatchSize = config.SourceBatchSize ?? 100,
                SourceFieldSeparator = config.SourceFieldSeparator ?? "║",
                CsvData = config.CsvData,
                CsvAdapterType = config.CsvAdapterType ?? "FILE",
                CsvPollingInterval = config.CsvPollingInterval ?? 10,
                // SFTP Properties
                SftpHost = config.SftpHost,
                SftpPort = config.SftpPort ?? 22,
                SftpUsername = config.SftpUsername,
                SftpPassword = config.SftpPassword,
                SftpSshKey = config.SftpSshKey,
                SftpFolder = config.SftpFolder,
                SftpFileMask = config.SftpFileMask ?? "*.txt",
                SftpMaxConnectionPoolSize = config.SftpMaxConnectionPoolSize ?? 5,
                SftpFileBufferSize = config.SftpFileBufferSize ?? 8192,
                // SQL Properties (for SQL Source)
                SqlServerName = config.SqlServerName,
                SqlDatabaseName = config.SqlDatabaseName,
                SqlUserName = config.SqlUserName,
                SqlPassword = config.SqlPassword,
                SqlIntegratedSecurity = config.SqlIntegratedSecurity ?? false,
                SqlResourceGroup = config.SqlResourceGroup,
                SqlPollingStatement = config.SqlPollingStatement,
                SqlPollingInterval = config.SqlPollingInterval ?? 60,
                SqlTableName = config.SqlTableName,
                SqlUseTransaction = config.SqlUseTransaction ?? false,
                SqlBatchSize = config.SqlBatchSize ?? 1000,
                SqlCommandTimeout = config.SqlCommandTimeout ?? 30,
                SqlFailOnBadStatement = config.SqlFailOnBadStatement ?? false,
                CreatedAt = config.CreatedAt,
                UpdatedAt = config.UpdatedAt ?? DateTime.UtcNow
            };
            
            config.Sources[sourceInstance.InstanceName] = sourceInstance;
            _logger?.LogInformation("Migrated source adapter instance '{InstanceName}' for interface {InterfaceName}", 
                sourceInstance.InstanceName, config.InterfaceName);
        }

        // Migrate destinations from old DestinationAdapterInstances list or old properties
        if (config.Destinations.Count == 0)
        {
            // First, try to migrate from DestinationAdapterInstances list
            if (config.DestinationAdapterInstances != null && config.DestinationAdapterInstances.Count > 0)
            {
                foreach (var oldDest in config.DestinationAdapterInstances)
                {
                    var destInstance = new DestinationAdapterInstance
                    {
                        InstanceName = oldDest.InstanceName,
                        AdapterName = oldDest.AdapterName,
                        IsEnabled = oldDest.IsEnabled,
                        AdapterInstanceGuid = oldDest.AdapterInstanceGuid,
                        Configuration = oldDest.Configuration,
                        // Copy SQL properties from config if not already set in instance
                        SqlServerName = config.SqlServerName ?? oldDest.SqlServerName,
                        SqlDatabaseName = config.SqlDatabaseName ?? oldDest.SqlDatabaseName,
                        SqlUserName = config.SqlUserName ?? oldDest.SqlUserName,
                        SqlPassword = config.SqlPassword ?? oldDest.SqlPassword,
                        SqlIntegratedSecurity = config.SqlIntegratedSecurity ?? oldDest.SqlIntegratedSecurity,
                        SqlResourceGroup = config.SqlResourceGroup ?? oldDest.SqlResourceGroup,
                        SqlTableName = config.SqlTableName ?? oldDest.SqlTableName,
                        SqlUseTransaction = config.SqlUseTransaction ?? oldDest.SqlUseTransaction,
                        SqlBatchSize = config.SqlBatchSize ?? oldDest.SqlBatchSize,
                        SqlCommandTimeout = config.SqlCommandTimeout ?? oldDest.SqlCommandTimeout,
                        SqlFailOnBadStatement = config.SqlFailOnBadStatement ?? oldDest.SqlFailOnBadStatement,
                        // CSV Destination Properties
                        DestinationReceiveFolder = config.DestinationReceiveFolder,
                        DestinationFileMask = config.DestinationFileMask ?? "*.txt",
                        CreatedAt = oldDest.CreatedAt,
                        UpdatedAt = oldDest.UpdatedAt ?? DateTime.UtcNow
                    };
                    
                    config.Destinations[destInstance.InstanceName] = destInstance;
                    _logger?.LogInformation("Migrated destination adapter instance '{InstanceName}' for interface {InterfaceName}", 
                        destInstance.InstanceName, config.InterfaceName);
                }
            }
            // If no list exists, migrate from old flat properties
            else if (!string.IsNullOrEmpty(config.DestinationAdapterName))
            {
                var destInstance = new DestinationAdapterInstance
                {
                    InstanceName = config.DestinationInstanceName ?? "Destination",
                    AdapterName = config.DestinationAdapterName,
                    IsEnabled = config.DestinationIsEnabled ?? true,
                    AdapterInstanceGuid = config.DestinationAdapterInstanceGuid ?? Guid.NewGuid(),
                    Configuration = config.DestinationConfiguration ?? string.Empty,
                    // SQL Properties
                    SqlServerName = config.SqlServerName,
                    SqlDatabaseName = config.SqlDatabaseName,
                    SqlUserName = config.SqlUserName,
                    SqlPassword = config.SqlPassword,
                    SqlIntegratedSecurity = config.SqlIntegratedSecurity ?? false,
                    SqlResourceGroup = config.SqlResourceGroup,
                    SqlTableName = config.SqlTableName,
                    SqlUseTransaction = config.SqlUseTransaction ?? false,
                    SqlBatchSize = config.SqlBatchSize ?? 1000,
                    SqlCommandTimeout = config.SqlCommandTimeout ?? 30,
                    SqlFailOnBadStatement = config.SqlFailOnBadStatement ?? false,
                    // CSV Destination Properties
                    DestinationReceiveFolder = config.DestinationReceiveFolder,
                    DestinationFileMask = config.DestinationFileMask ?? "*.txt",
                    CreatedAt = config.CreatedAt,
                    UpdatedAt = config.UpdatedAt ?? DateTime.UtcNow
                };
                
                config.Destinations[destInstance.InstanceName] = destInstance;
                _logger?.LogInformation("Migrated destination adapter instance '{InstanceName}' for interface {InterfaceName}", 
                    destInstance.InstanceName, config.InterfaceName);
            }
        }
    }

    /// <summary>
    /// Creates a default source instance from old format properties (used during migration)
    /// </summary>
    private SourceAdapterInstance CreateDefaultSourceInstance(InterfaceConfiguration config)
    {
        return new SourceAdapterInstance
        {
            InstanceName = config.SourceInstanceName ?? "Source",
            AdapterName = config.SourceAdapterName ?? "CSV",
            IsEnabled = config.SourceIsEnabled ?? true,
            AdapterInstanceGuid = config.SourceAdapterInstanceGuid ?? Guid.NewGuid(),
            Configuration = config.SourceConfiguration ?? string.Empty,
            SourceReceiveFolder = config.SourceReceiveFolder ?? "csv-incoming",
            SourceFileMask = config.SourceFileMask ?? "*.txt",
            SourceBatchSize = config.SourceBatchSize ?? 100,
            SourceFieldSeparator = config.SourceFieldSeparator ?? "║",
            CsvData = config.CsvData,
            CsvAdapterType = config.CsvAdapterType ?? "FILE",
            CsvPollingInterval = config.CsvPollingInterval ?? 10,
            CreatedAt = config.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a default destination instance from old format properties (used during migration)
    /// </summary>
    private DestinationAdapterInstance CreateDefaultDestinationInstance(InterfaceConfiguration config)
    {
        return new DestinationAdapterInstance
        {
            InstanceName = config.DestinationInstanceName ?? "Destination",
            AdapterName = config.DestinationAdapterName ?? "SqlServer",
            IsEnabled = config.DestinationIsEnabled ?? true,
            AdapterInstanceGuid = config.DestinationAdapterInstanceGuid ?? Guid.NewGuid(),
            Configuration = config.DestinationConfiguration ?? string.Empty,
            SqlServerName = config.SqlServerName,
            SqlDatabaseName = config.SqlDatabaseName,
            SqlUserName = config.SqlUserName,
            SqlPassword = config.SqlPassword,
            SqlIntegratedSecurity = config.SqlIntegratedSecurity ?? false,
            SqlTableName = config.SqlTableName,
            SqlUseTransaction = config.SqlUseTransaction ?? false,
            SqlBatchSize = config.SqlBatchSize ?? 1000,
            SqlCommandTimeout = config.SqlCommandTimeout ?? 30,
            SqlFailOnBadStatement = config.SqlFailOnBadStatement ?? false,
            DestinationReceiveFolder = config.DestinationReceiveFolder,
            DestinationFileMask = config.DestinationFileMask ?? "*.txt",
            CreatedAt = config.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public async Task<List<InterfaceConfiguration>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
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
        await InitializeAsync(cancellationToken);
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
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var allConfigs = _configurations.Values.ToList();
            _logger?.LogInformation("DEBUG GetEnabledSourceConfigurationsAsync: Total configs in memory: {Count}", allConfigs.Count);
            
            // Filter configurations that have at least one enabled source adapter instance
            var enabledConfigs = _configurations.Values
                .Where(c => c.Sources.Values.Any(s => s.IsEnabled))
                .ToList();
            
            _logger?.LogInformation("DEBUG GetEnabledSourceConfigurationsAsync: Found {Count} enabled source configurations (filtered from {Total} total)", 
                enabledConfigs.Count, allConfigs.Count);
            
            // Debug logging
            foreach (var cfg in allConfigs)
            {
                var enabledSources = cfg.Sources.Values.Where(s => s.IsEnabled).ToList();
                _logger?.LogInformation("DEBUG GetEnabledSourceConfigurationsAsync: Config {InterfaceName} - Enabled sources: {Count}",
                    cfg.InterfaceName, enabledSources.Count);
                foreach (var source in enabledSources)
                {
                    _logger?.LogInformation("DEBUG GetEnabledSourceConfigurationsAsync:   Source '{InstanceName}': Adapter={AdapterName}, IsEnabled={IsEnabled}",
                        source.InstanceName, source.AdapterName, source.IsEnabled);
                }
            }
            
            return enabledConfigs;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<InterfaceConfiguration>> GetEnabledDestinationConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Filter configurations that have at least one enabled destination adapter instance
            return _configurations.Values
                .Where(c => c.Destinations.Values.Any(d => d.IsEnabled))
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveConfigurationAsync(InterfaceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
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
        await InitializeAsync(cancellationToken);
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

    public async Task SetSourceEnabledAsync(string interfaceName, bool enabled, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                // Enable/disable all source adapter instances
                foreach (var source in config.Sources.Values)
                {
                    source.IsEnabled = enabled;
                    source.UpdatedAt = DateTime.UtcNow;
                }
                // Also update legacy SourceIsEnabled property for backward compatibility
                config.SourceIsEnabled = enabled;
                config.UpdatedAt = DateTime.UtcNow;
            }
        }
        finally
        {
            _lock.Release();
        }

        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task SetDestinationEnabledAsync(string interfaceName, bool enabled, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                // Enable/disable all destination adapter instances
                foreach (var dest in config.Destinations.Values)
                {
                    dest.IsEnabled = enabled;
                    dest.UpdatedAt = DateTime.UtcNow;
                }
                config.UpdatedAt = DateTime.UtcNow;
            }
        }
        finally
        {
            _lock.Release();
        }

        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateInterfaceNameAsync(string oldInterfaceName, string newInterfaceName, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(oldInterfaceName, out var config))
            {
                _configurations.Remove(oldInterfaceName);
                config.InterfaceName = newInterfaceName;
                config.UpdatedAt = DateTime.UtcNow;
                _configurations[newInterfaceName] = config;
                _logger?.LogInformation("Interface name updated from '{OldName}' to '{NewName}'", oldInterfaceName, newInterfaceName);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating interface name", oldInterfaceName);
                throw new KeyNotFoundException($"Interface configuration '{oldInterfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateInstanceNameAsync(string interfaceName, string instanceType, string instanceName, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                if (instanceType.Equals("Source", StringComparison.OrdinalIgnoreCase))
                {
                    config.SourceInstanceName = instanceName;
                }
                else if (instanceType.Equals("Destination", StringComparison.OrdinalIgnoreCase))
                {
                    config.DestinationInstanceName = instanceName;
                }
                else
                {
                    throw new ArgumentException($"Invalid instance type: {instanceType}. Must be 'Source' or 'Destination'.", nameof(instanceType));
                }
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("Instance name for interface '{InterfaceName}' {InstanceType} updated to '{InstanceName}'", interfaceName, instanceType, instanceName);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating instance name", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateReceiveFolderAsync(string interfaceName, string receiveFolder, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                config.SourceReceiveFolder = receiveFolder;
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("Receive folder for interface '{InterfaceName}' updated to '{ReceiveFolder}'", interfaceName, receiveFolder);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating receive folder", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateFileMaskAsync(string interfaceName, string fileMask, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                config.SourceFileMask = string.IsNullOrWhiteSpace(fileMask) ? "*.txt" : fileMask.Trim();
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("File mask for interface '{InterfaceName}' updated to '{FileMask}'", interfaceName, config.SourceFileMask);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating file mask", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateBatchSizeAsync(string interfaceName, int batchSize, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                config.SourceBatchSize = batchSize > 0 ? batchSize : 100;
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("Batch size for interface '{InterfaceName}' updated to {BatchSize}", interfaceName, config.SourceBatchSize);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating batch size", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateSqlConnectionPropertiesAsync(
        string interfaceName,
        string? serverName,
        string? databaseName,
        string? userName,
        string? password,
        bool? integratedSecurity,
        string? resourceGroup,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                if (serverName != null) config.SqlServerName = serverName;
                if (databaseName != null) config.SqlDatabaseName = databaseName;
                if (userName != null) config.SqlUserName = userName;
                if (password != null) config.SqlPassword = password;
                if (integratedSecurity.HasValue) config.SqlIntegratedSecurity = integratedSecurity.Value;
                if (resourceGroup != null) config.SqlResourceGroup = resourceGroup;
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("SQL connection properties for interface '{InterfaceName}' updated", interfaceName);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating SQL connection properties", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateSqlPollingPropertiesAsync(
        string interfaceName,
        string? pollingStatement,
        int? pollingInterval,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                if (pollingStatement != null) config.SqlPollingStatement = pollingStatement;
                if (pollingInterval.HasValue) config.SqlPollingInterval = pollingInterval.Value > 0 ? pollingInterval.Value : 60;
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("SQL polling properties for interface '{InterfaceName}' updated: Statement={PollingStatement}, Interval={PollingInterval}", 
                    interfaceName, pollingStatement ?? "null", pollingInterval ?? 60);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating SQL polling properties", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateCsvPollingIntervalAsync(string interfaceName, int pollingInterval, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                var interval = pollingInterval > 0 ? pollingInterval : 10;
                config.CsvPollingInterval = interval;
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("CSV polling interval for interface '{InterfaceName}' updated to {PollingInterval} seconds", interfaceName, interval);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating CSV polling interval", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateFieldSeparatorAsync(string interfaceName, string fieldSeparator, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                config.SourceFieldSeparator = string.IsNullOrWhiteSpace(fieldSeparator) ? "║" : fieldSeparator.Trim();
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("Field separator for interface '{InterfaceName}' updated to '{FieldSeparator}'", interfaceName, config.SourceFieldSeparator);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating field separator", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateCsvDataAsync(string interfaceName, string? csvData, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                config.CsvData = csvData;
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("CsvData for interface '{InterfaceName}' updated. DataLength={DataLength}", 
                    interfaceName, csvData?.Length ?? 0);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating CsvData", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateDestinationReceiveFolderAsync(string interfaceName, string destinationReceiveFolder, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                config.DestinationReceiveFolder = string.IsNullOrWhiteSpace(destinationReceiveFolder) ? null : destinationReceiveFolder.Trim();
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("Destination receive folder for interface '{InterfaceName}' updated to '{DestinationReceiveFolder}'", interfaceName, config.DestinationReceiveFolder ?? "null");
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating destination receive folder", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task UpdateDestinationFileMaskAsync(string interfaceName, string destinationFileMask, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                config.DestinationFileMask = string.IsNullOrWhiteSpace(destinationFileMask) ? "*.txt" : destinationFileMask.Trim();
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("Destination file mask for interface '{InterfaceName}' updated to '{DestinationFileMask}'", interfaceName, config.DestinationFileMask);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating destination file mask", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }

    public async Task<List<DestinationAdapterInstance>> GetDestinationAdapterInstancesAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                if (config.DestinationAdapterInstances != null && config.DestinationAdapterInstances.Count > 0)
                {
                    return config.DestinationAdapterInstances.ToList();
                }
                
                // Backward compatibility: create instance from legacy properties
                if (!string.IsNullOrWhiteSpace(config.DestinationAdapterName))
                {
                    return new List<DestinationAdapterInstance>
                    {
                        new DestinationAdapterInstance
                        {
                            AdapterInstanceGuid = config.DestinationAdapterInstanceGuid ?? Guid.NewGuid(),
                            InstanceName = config.DestinationInstanceName ?? "Destination",
                            AdapterName = config.DestinationAdapterName ?? "SqlServer",
                            IsEnabled = config.DestinationIsEnabled ?? true,
                            Configuration = config.DestinationConfiguration ?? string.Empty,
                            CreatedAt = config.CreatedAt,
                            UpdatedAt = config.UpdatedAt
                        }
                    };
                }
                
                return new List<DestinationAdapterInstance>();
            }
            
            _logger?.LogWarning("Interface configuration '{InterfaceName}' not found", interfaceName);
            return new List<DestinationAdapterInstance>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DestinationAdapterInstance> AddDestinationAdapterInstanceAsync(
        string interfaceName,
        string adapterName,
        string instanceName,
        string configuration,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                if (config.DestinationAdapterInstances == null)
                {
                    config.DestinationAdapterInstances = new List<DestinationAdapterInstance>();
                }
                
                var newInstance = new DestinationAdapterInstance
                {
                    AdapterInstanceGuid = Guid.NewGuid(),
                    InstanceName = string.IsNullOrWhiteSpace(instanceName) ? $"{adapterName} Destination" : instanceName.Trim(),
                    AdapterName = adapterName,
                    IsEnabled = true,
                    Configuration = configuration ?? "{}",
                    CreatedAt = DateTime.UtcNow
                };
                
                config.DestinationAdapterInstances.Add(newInstance);
                config.UpdatedAt = DateTime.UtcNow;
                
                _logger?.LogInformation("Added destination adapter instance '{InstanceName}' ({AdapterName}) to interface '{InterfaceName}'", 
                    newInstance.InstanceName, adapterName, interfaceName);
                
                await SaveConfigurationsAsync(cancellationToken);
                return newInstance;
            }
            
            _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for adding destination adapter instance", interfaceName);
            throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveDestinationAdapterInstanceAsync(string interfaceName, Guid adapterInstanceGuid, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                if (config.DestinationAdapterInstances == null)
                {
                    config.DestinationAdapterInstances = new List<DestinationAdapterInstance>();
                }
                
                var instance = config.DestinationAdapterInstances.FirstOrDefault(i => i.AdapterInstanceGuid == adapterInstanceGuid);
                if (instance != null)
                {
                    config.DestinationAdapterInstances.Remove(instance);
                    config.UpdatedAt = DateTime.UtcNow;
                    _logger?.LogInformation("Removed destination adapter instance '{InstanceName}' ({AdapterInstanceGuid}) from interface '{InterfaceName}'", 
                        instance.InstanceName, adapterInstanceGuid, interfaceName);
                }
                else
                {
                    _logger?.LogWarning("Destination adapter instance '{AdapterInstanceGuid}' not found in interface '{InterfaceName}'", 
                        adapterInstanceGuid, interfaceName);
                }
                
                await SaveConfigurationsAsync(cancellationToken);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for removing destination adapter instance", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateDestinationAdapterInstanceAsync(
        string interfaceName,
        Guid adapterInstanceGuid,
        string? instanceName = null,
        bool? isEnabled = null,
        string? configuration = null,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                if (config.DestinationAdapterInstances == null)
                {
                    config.DestinationAdapterInstances = new List<DestinationAdapterInstance>();
                }
                
                var instance = config.DestinationAdapterInstances.FirstOrDefault(i => i.AdapterInstanceGuid == adapterInstanceGuid);
                if (instance != null)
                {
                    if (instanceName != null) instance.InstanceName = instanceName.Trim();
                    if (isEnabled.HasValue) instance.IsEnabled = isEnabled.Value;
                    if (configuration != null) instance.Configuration = configuration;
                    instance.UpdatedAt = DateTime.UtcNow;
                    config.UpdatedAt = DateTime.UtcNow;
                    
                    _logger?.LogInformation("Updated destination adapter instance '{InstanceName}' ({AdapterInstanceGuid}) in interface '{InterfaceName}'", 
                        instance.InstanceName, adapterInstanceGuid, interfaceName);
                }
                else
                {
                    _logger?.LogWarning("Destination adapter instance '{AdapterInstanceGuid}' not found in interface '{InterfaceName}'", 
                        adapterInstanceGuid, interfaceName);
                    throw new KeyNotFoundException($"Destination adapter instance '{adapterInstanceGuid}' not found.");
                }
                
                await SaveConfigurationsAsync(cancellationToken);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating destination adapter instance", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateSqlTransactionPropertiesAsync(
        string interfaceName,
        bool? useTransaction = null,
        int? batchSize = null,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                if (useTransaction.HasValue) config.SqlUseTransaction = useTransaction.Value;
                if (batchSize.HasValue) config.SqlBatchSize = batchSize.Value > 0 ? batchSize.Value : 1000;
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("SQL transaction properties for interface '{InterfaceName}' updated: UseTransaction={UseTransaction}, BatchSize={BatchSize}", 
                    interfaceName, config.SqlUseTransaction, config.SqlBatchSize);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating SQL transaction properties", interfaceName);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(cancellationToken);
    }
}

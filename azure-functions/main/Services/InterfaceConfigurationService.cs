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
    private const string ConfigFileNamePrefix = "interface-configurations";
    private const string ConfigContainerName = "function-config";
    private const string DefaultInterfaceName = "FromCsvToSqlServerExample";
    
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly ILogger<InterfaceConfigurationService>? _logger;
    // Session-based storage: Dictionary<sessionId, Dictionary<interfaceName, InterfaceConfiguration>>
    private readonly Dictionary<string, Dictionary<string, InterfaceConfiguration>> _sessionConfigurations = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public InterfaceConfigurationService(
        BlobServiceClient? blobServiceClient,
        ILogger<InterfaceConfigurationService>? logger = null)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task InitializeAsync(string? sessionId = null, CancellationToken cancellationToken = default)
    {
        sessionId ??= "default"; // For backward compatibility
        
        await EnsureSessionInitializedAsync(sessionId, cancellationToken);
    }
    
    private async Task EnsureSessionInitializedAsync(string sessionId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Check if session is already initialized
            if (_sessionConfigurations.ContainsKey(sessionId))
            {
                return;
            }

            // Initialize session dictionary
            var configurations = new Dictionary<string, InterfaceConfiguration>();
            _sessionConfigurations[sessionId] = configurations;

            if (_blobServiceClient == null)
            {
                _logger?.LogWarning("BlobServiceClient is null. Interface configurations will be in-memory only for session {SessionId}.", sessionId);
            }
            else
            {
                try
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(ConfigContainerName);
                    await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                    var configFileName = GetConfigFileName(sessionId);
                    var blobClient = containerClient.GetBlobClient(configFileName);
                    
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
                                    configurations[config.InterfaceName] = config;
                                }
                                _logger?.LogInformation("Loaded {Count} interface configurations from storage for session {SessionId}", configs.Count, sessionId);
                            }
                        }
                    }
                    else
                    {
                        _logger?.LogInformation("Interface configurations file not found for session {SessionId}. Starting with empty configuration.", sessionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error loading interface configurations from storage for session {SessionId}", sessionId);
                    // Continue with empty configuration
                }
            }

            // Create default configuration if none exists for this session
            if (!configurations.ContainsKey(DefaultInterfaceName))
            {
                var defaultConfiguration = CreateDefaultInterfaceConfiguration();
                configurations[DefaultInterfaceName] = defaultConfiguration;
                _logger?.LogInformation("Created default interface configuration '{InterfaceName}' for session {SessionId}", DefaultInterfaceName, sessionId);
                
                await SaveConfigurationsAsync(sessionId, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    private string GetConfigFileName(string sessionId)
    {
        return $"{ConfigFileNamePrefix}-{sessionId}.json";
    }

    public async Task UpdateCsvPollingIntervalAsync(string interfaceName, int pollingInterval, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        sessionId ??= "default";
        await EnsureSessionInitializedAsync(sessionId, cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var configurations = _sessionConfigurations[sessionId];
            if (configurations.TryGetValue(interfaceName, out var config))
            {
                var interval = pollingInterval > 0 ? pollingInterval : 10;
                config.CsvPollingInterval = interval;
                config.UpdatedAt = DateTime.UtcNow;
                _logger?.LogInformation("CSV polling interval for interface '{InterfaceName}' updated to {PollingInterval} seconds (session: {SessionId})", interfaceName, interval, sessionId);
            }
            else
            {
                _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating CSV polling interval (session: {SessionId})", interfaceName, sessionId);
                throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
            }
        }
        finally
        {
            _lock.Release();
        }
        await SaveConfigurationsAsync(sessionId, cancellationToken);
    }

    private async Task SaveConfigurationsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_blobServiceClient == null)
        {
            _logger?.LogWarning("BlobServiceClient is null. Cannot persist interface configurations for session {SessionId}.", sessionId);
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_sessionConfigurations.TryGetValue(sessionId, out var configurations))
            {
                _logger?.LogWarning("Session {SessionId} not found for saving configurations", sessionId);
                return;
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(ConfigContainerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var configFileName = GetConfigFileName(sessionId);
            var blobClient = containerClient.GetBlobClient(configFileName);
            var jsonContent = JsonSerializer.Serialize(configurations.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            var content = Encoding.UTF8.GetBytes(jsonContent);

            await blobClient.UploadAsync(new BinaryData(content), overwrite: true, cancellationToken);
            _logger?.LogInformation("Saved {Count} interface configurations to storage for session {SessionId}", configurations.Count, sessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving interface configurations to storage for session {SessionId}", sessionId);
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
            SourceAdapterName = "CSV",
            SourceConfiguration = JsonSerializer.Serialize(new { source = "csv-incoming", enabled = true }),
            DestinationAdapterName = "SqlServer",
            DestinationConfiguration = JsonSerializer.Serialize(new { destination = "TransportData", enabled = true }),
            SourceIsEnabled = true,
            DestinationIsEnabled = true,
            SourceInstanceName = "CSV Source",
            DestinationInstanceName = "SQL Destination",
            SourceAdapterInstanceGuid = Guid.NewGuid(),
            DestinationAdapterInstanceGuid = Guid.NewGuid(),
            SourceReceiveFolder = "csv-incoming",
            SourceFileMask = "*.txt",
            SourceBatchSize = 100,
            SourceFieldSeparator = "║",
            CsvPollingInterval = 10,
            CsvAdapterType = "RAW",
            CsvData = "Id║FirstName║LastName║Email║City\n1║Max║Mustermann║max@example.com║Berlin\n2║Anna║Schmidt║anna@example.com║München",
            DestinationFileMask = "*.txt",
            SqlServerName = sqlServer,
            SqlDatabaseName = sqlDatabase,
            SqlUserName = sqlUser,
            SqlPassword = sqlPassword,
            SqlIntegratedSecurity = false,
            SqlTableName = "TransportData",
            CreatedAt = now,
            UpdatedAt = now
        };

        configuration.DestinationAdapterInstances.Add(new DestinationAdapterInstance
        {
            AdapterInstanceGuid = Guid.NewGuid(),
            InstanceName = "SqlServer Adapter 1",
            AdapterName = "SqlServer",
            IsEnabled = true,
            Configuration = destinationInstanceConfig,
            CreatedAt = now,
            UpdatedAt = now
        });

        return configuration;
    }

    public async Task<List<InterfaceConfiguration>> GetAllConfigurationsAsync(string? sessionId = null, CancellationToken cancellationToken = default)
    {
        sessionId ??= "default";
        await EnsureSessionInitializedAsync(sessionId, cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var configurations = _sessionConfigurations[sessionId];
            return configurations.Values.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<InterfaceConfiguration?> GetConfigurationAsync(string interfaceName, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        sessionId ??= "default";
        await EnsureSessionInitializedAsync(sessionId, cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var configurations = _sessionConfigurations[sessionId];
            return configurations.TryGetValue(interfaceName, out var config) ? config : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<InterfaceConfiguration>> GetEnabledSourceConfigurationsAsync(string? sessionId = null, CancellationToken cancellationToken = default)
    {
        sessionId ??= "default";
        await EnsureSessionInitializedAsync(sessionId, cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var configurations = _sessionConfigurations[sessionId];
            return configurations.Values
                .Where(c => c.SourceIsEnabled)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<InterfaceConfiguration>> GetEnabledDestinationConfigurationsAsync(string? sessionId = null, CancellationToken cancellationToken = default)
    {
        sessionId ??= "default";
        await EnsureSessionInitializedAsync(sessionId, cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var configurations = _sessionConfigurations[sessionId];
            return configurations.Values
                .Where(c => c.DestinationIsEnabled)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveConfigurationAsync(InterfaceConfiguration configuration, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        sessionId ??= "default";
        await EnsureSessionInitializedAsync(sessionId, cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var configurations = _sessionConfigurations[sessionId];
            configuration.UpdatedAt = DateTime.UtcNow;
            configurations[configuration.InterfaceName] = configuration;
        }
        finally
        {
            _lock.Release();
        }

        await SaveConfigurationsAsync(sessionId, cancellationToken);
    }

    public async Task DeleteConfigurationAsync(string interfaceName, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        sessionId ??= "default";
        await EnsureSessionInitializedAsync(sessionId, cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var configurations = _sessionConfigurations[sessionId];
            configurations.Remove(interfaceName);
        }
        finally
        {
            _lock.Release();
        }

        await SaveConfigurationsAsync(sessionId, cancellationToken);
    }

    public async Task SetSourceEnabledAsync(string interfaceName, bool enabled, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        sessionId ??= "default";
        await EnsureSessionInitializedAsync(sessionId, cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var configurations = _sessionConfigurations[sessionId];
            if (configurations.TryGetValue(interfaceName, out var config))
            {
                config.SourceIsEnabled = enabled;
                config.UpdatedAt = DateTime.UtcNow;
            }
        }
        finally
        {
            _lock.Release();
        }

        await SaveConfigurationsAsync(sessionId, cancellationToken);
    }

    public async Task SetDestinationEnabledAsync(string interfaceName, bool enabled, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        sessionId ??= "default";
        await EnsureSessionInitializedAsync(sessionId, cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var configurations = _sessionConfigurations[sessionId];
            if (configurations.TryGetValue(interfaceName, out var config))
            {
                config.DestinationIsEnabled = enabled;
                config.UpdatedAt = DateTime.UtcNow;
            }
        }
        finally
        {
            _lock.Release();
        }

        await SaveConfigurationsAsync(sessionId, cancellationToken);
    }

       public async Task UpdateInterfaceNameAsync(string oldInterfaceName, string newInterfaceName, string? sessionId = null, CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(oldInterfaceName, out var config))
               {
                   configurations.Remove(oldInterfaceName);
                   config.InterfaceName = newInterfaceName;
                   config.UpdatedAt = DateTime.UtcNow;
                   configurations[newInterfaceName] = config;
                   _logger?.LogInformation("Interface name updated from '{OldName}' to '{NewName}' (session: {SessionId})", oldInterfaceName, newInterfaceName, sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating interface name (session: {SessionId})", oldInterfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{oldInterfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task UpdateInstanceNameAsync(string interfaceName, string instanceType, string instanceName, string? sessionId = null, CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
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
                   _logger?.LogInformation("Instance name for interface '{InterfaceName}' {InstanceType} updated to '{InstanceName}' (session: {SessionId})", interfaceName, instanceType, instanceName, sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating instance name (session: {SessionId})", interfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task UpdateReceiveFolderAsync(string interfaceName, string receiveFolder, string? sessionId = null, CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   config.SourceReceiveFolder = receiveFolder;
                   config.UpdatedAt = DateTime.UtcNow;
                   _logger?.LogInformation("Receive folder for interface '{InterfaceName}' updated to '{ReceiveFolder}' (session: {SessionId})", interfaceName, receiveFolder, sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating receive folder (session: {SessionId})", interfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task UpdateFileMaskAsync(string interfaceName, string fileMask, string? sessionId = null, CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   config.SourceFileMask = string.IsNullOrWhiteSpace(fileMask) ? "*.txt" : fileMask.Trim();
                   config.UpdatedAt = DateTime.UtcNow;
                   _logger?.LogInformation("File mask for interface '{InterfaceName}' updated to '{FileMask}' (session: {SessionId})", interfaceName, config.SourceFileMask, sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating file mask (session: {SessionId})", interfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task UpdateBatchSizeAsync(string interfaceName, int batchSize, string? sessionId = null, CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   config.SourceBatchSize = batchSize > 0 ? batchSize : 100; // Ensure positive value, default to 100
                   config.UpdatedAt = DateTime.UtcNow;
                   _logger?.LogInformation("Batch size for interface '{InterfaceName}' updated to {BatchSize} (session: {SessionId})", interfaceName, config.SourceBatchSize, sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating batch size (session: {SessionId})", interfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task UpdateSqlConnectionPropertiesAsync(
           string interfaceName,
           string? serverName,
           string? databaseName,
           string? userName,
           string? password,
           bool? integratedSecurity,
           string? resourceGroup,
           string? sessionId = null,
           CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   if (serverName != null) config.SqlServerName = serverName;
                   if (databaseName != null) config.SqlDatabaseName = databaseName;
                   if (userName != null) config.SqlUserName = userName;
                   if (password != null) config.SqlPassword = password;
                   if (integratedSecurity.HasValue) config.SqlIntegratedSecurity = integratedSecurity.Value;
                   if (resourceGroup != null) config.SqlResourceGroup = resourceGroup;
                   config.UpdatedAt = DateTime.UtcNow;
                   _logger?.LogInformation("SQL connection properties for interface '{InterfaceName}' updated (session: {SessionId})", interfaceName, sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating SQL connection properties (session: {SessionId})", interfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task UpdateSqlPollingPropertiesAsync(
           string interfaceName,
           string? pollingStatement,
           int? pollingInterval,
           string? sessionId = null,
           CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   if (pollingStatement != null) config.SqlPollingStatement = pollingStatement;
                   if (pollingInterval.HasValue) config.SqlPollingInterval = pollingInterval.Value > 0 ? pollingInterval.Value : 60;
                   config.UpdatedAt = DateTime.UtcNow;
                   _logger?.LogInformation("SQL polling properties for interface '{InterfaceName}' updated: Statement={PollingStatement}, Interval={PollingInterval} (session: {SessionId})", 
                       interfaceName, pollingStatement ?? "null", pollingInterval ?? 60, sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating SQL polling properties (session: {SessionId})", interfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task UpdateFieldSeparatorAsync(string interfaceName, string fieldSeparator, string? sessionId = null, CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   config.SourceFieldSeparator = string.IsNullOrWhiteSpace(fieldSeparator) ? "║" : fieldSeparator.Trim();
                   config.UpdatedAt = DateTime.UtcNow;
                   _logger?.LogInformation("Field separator for interface '{InterfaceName}' updated to '{FieldSeparator}' (session: {SessionId})", interfaceName, config.SourceFieldSeparator, sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating field separator (session: {SessionId})", interfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task UpdateCsvDataAsync(string interfaceName, string? csvData, string? sessionId = null, CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   config.CsvData = csvData;
                   config.UpdatedAt = DateTime.UtcNow;
                   _logger?.LogInformation("CsvData for interface '{InterfaceName}' updated. DataLength={DataLength} (session: {SessionId})", 
                       interfaceName, csvData?.Length ?? 0, sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating CsvData (session: {SessionId})", interfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task UpdateDestinationReceiveFolderAsync(string interfaceName, string destinationReceiveFolder, string? sessionId = null, CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   config.DestinationReceiveFolder = string.IsNullOrWhiteSpace(destinationReceiveFolder) ? null : destinationReceiveFolder.Trim();
                   config.UpdatedAt = DateTime.UtcNow;
                   _logger?.LogInformation("Destination receive folder for interface '{InterfaceName}' updated to '{DestinationReceiveFolder}' (session: {SessionId})", interfaceName, config.DestinationReceiveFolder ?? "null", sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating destination receive folder (session: {SessionId})", interfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task UpdateDestinationFileMaskAsync(string interfaceName, string destinationFileMask, string? sessionId = null, CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   config.DestinationFileMask = string.IsNullOrWhiteSpace(destinationFileMask) ? "*.txt" : destinationFileMask.Trim();
                   config.UpdatedAt = DateTime.UtcNow;
                   _logger?.LogInformation("Destination file mask for interface '{InterfaceName}' updated to '{DestinationFileMask}' (session: {SessionId})", interfaceName, config.DestinationFileMask, sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating destination file mask (session: {SessionId})", interfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task<List<DestinationAdapterInstance>> GetDestinationAdapterInstancesAsync(string interfaceName, string? sessionId = null, CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   // Return instances from the list, or create a default one from legacy properties for backward compatibility
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
                               AdapterInstanceGuid = config.DestinationAdapterInstanceGuid,
                               InstanceName = config.DestinationInstanceName,
                               AdapterName = config.DestinationAdapterName,
                               IsEnabled = config.DestinationIsEnabled,
                               Configuration = config.DestinationConfiguration,
                               CreatedAt = config.CreatedAt,
                               UpdatedAt = config.UpdatedAt
                           }
                       };
                   }
                   
                   return new List<DestinationAdapterInstance>();
               }
               
               _logger?.LogWarning("Interface configuration '{InterfaceName}' not found (session: {SessionId})", interfaceName, sessionId);
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
           string? sessionId = null,
           CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   // Initialize list if null
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
                   
                   _logger?.LogInformation("Added destination adapter instance '{InstanceName}' ({AdapterName}) to interface '{InterfaceName}' (session: {SessionId})", 
                       newInstance.InstanceName, adapterName, interfaceName, sessionId);
                   
                   await SaveConfigurationsAsync(sessionId, cancellationToken);
                   return newInstance;
               }
               
               _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for adding destination adapter instance (session: {SessionId})", interfaceName, sessionId);
               throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
           }
           finally
           {
               _lock.Release();
           }
       }

       public async Task RemoveDestinationAdapterInstanceAsync(string interfaceName, Guid adapterInstanceGuid, string? sessionId = null, CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
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
                       _logger?.LogInformation("Removed destination adapter instance '{InstanceName}' ({AdapterInstanceGuid}) from interface '{InterfaceName}' (session: {SessionId})", 
                           instance.InstanceName, adapterInstanceGuid, interfaceName, sessionId);
                   }
                   else
                   {
                       _logger?.LogWarning("Destination adapter instance '{AdapterInstanceGuid}' not found in interface '{InterfaceName}' (session: {SessionId})", 
                           adapterInstanceGuid, interfaceName, sessionId);
                   }
                   
                   await SaveConfigurationsAsync(sessionId, cancellationToken);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for removing destination adapter instance (session: {SessionId})", interfaceName, sessionId);
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
           string? sessionId = null,
           CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
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
                       
                       _logger?.LogInformation("Updated destination adapter instance '{InstanceName}' ({AdapterInstanceGuid}) in interface '{InterfaceName}' (session: {SessionId})", 
                           instance.InstanceName, adapterInstanceGuid, interfaceName, sessionId);
                   }
                   else
                   {
                       _logger?.LogWarning("Destination adapter instance '{AdapterInstanceGuid}' not found in interface '{InterfaceName}' (session: {SessionId})", 
                           adapterInstanceGuid, interfaceName, sessionId);
                       throw new KeyNotFoundException($"Destination adapter instance '{adapterInstanceGuid}' not found.");
                   }
                   
                   await SaveConfigurationsAsync(sessionId, cancellationToken);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating destination adapter instance (session: {SessionId})", interfaceName, sessionId);
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
           string? sessionId = null,
           CancellationToken cancellationToken = default)
       {
           sessionId ??= "default";
           await EnsureSessionInitializedAsync(sessionId, cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var configurations = _sessionConfigurations[sessionId];
               if (configurations.TryGetValue(interfaceName, out var config))
               {
                   if (useTransaction.HasValue) config.SqlUseTransaction = useTransaction.Value;
                   if (batchSize.HasValue) config.SqlBatchSize = batchSize.Value > 0 ? batchSize.Value : 1000;
                   config.UpdatedAt = DateTime.UtcNow;
                   _logger?.LogInformation("SQL transaction properties for interface '{InterfaceName}' updated: UseTransaction={UseTransaction}, BatchSize={BatchSize} (session: {SessionId})", 
                       interfaceName, config.SqlUseTransaction, config.SqlBatchSize, sessionId);
               }
               else
               {
                   _logger?.LogWarning("Interface configuration '{InterfaceName}' not found for updating SQL transaction properties (session: {SessionId})", interfaceName, sessionId);
                   throw new KeyNotFoundException($"Interface configuration '{interfaceName}' not found.");
               }
           }
           finally
           {
               _lock.Release();
           }
           await SaveConfigurationsAsync(sessionId, cancellationToken);
       }

       public async Task<List<InterfaceConfiguration>> GetAllEnabledSourceConfigurationsAcrossSessionsAsync(CancellationToken cancellationToken = default)
       {
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var allEnabledConfigs = new List<InterfaceConfiguration>();

               // Load all sessions if not already loaded
               await LoadAllSessionsAsync(cancellationToken);

               // Aggregate enabled source configurations from all sessions
               foreach (var sessionPair in _sessionConfigurations)
               {
                   var sessionId = sessionPair.Key;
                   var sessionConfigurations = sessionPair.Value;
                   var enabledInSession = sessionConfigurations.Values
                       .Where(c => c.SourceIsEnabled)
                       .Select(c => 
                       {
                           // Set SessionId so we can retrieve destination instances later
                           var configWithSession = c;
                           configWithSession.SessionId = sessionId;
                           return configWithSession;
                       })
                       .ToList();
                   allEnabledConfigs.AddRange(enabledInSession);
               }

               _logger?.LogInformation("Found {Count} enabled source configurations across {SessionCount} sessions", 
                   allEnabledConfigs.Count, _sessionConfigurations.Count);
               
               return allEnabledConfigs;
           }
           finally
           {
               _lock.Release();
           }
       }

       public async Task<List<InterfaceConfiguration>> GetAllEnabledDestinationConfigurationsAcrossSessionsAsync(CancellationToken cancellationToken = default)
       {
           await _lock.WaitAsync(cancellationToken);
           try
           {
               var allEnabledConfigs = new List<InterfaceConfiguration>();

               // Load all sessions if not already loaded
               await LoadAllSessionsAsync(cancellationToken);

               // Aggregate enabled destination configurations from all sessions
               foreach (var sessionPair in _sessionConfigurations)
               {
                   var sessionId = sessionPair.Key;
                   var sessionConfigurations = sessionPair.Value;
                   var enabledInSession = sessionConfigurations.Values
                       .Where(c => c.DestinationIsEnabled)
                       .Select(c => 
                       {
                           // Set SessionId so we can retrieve destination instances later
                           var configWithSession = c;
                           configWithSession.SessionId = sessionId;
                           return configWithSession;
                       })
                       .ToList();
                   allEnabledConfigs.AddRange(enabledInSession);
               }

               _logger?.LogInformation("Found {Count} enabled destination configurations across {SessionCount} sessions", 
                   allEnabledConfigs.Count, _sessionConfigurations.Count);
               
               return allEnabledConfigs;
           }
           finally
           {
               _lock.Release();
           }
       }

       public async Task<List<string>> GetAllActiveSessionIdsAsync(CancellationToken cancellationToken = default)
       {
           await _lock.WaitAsync(cancellationToken);
           try
           {
               // Load all sessions if not already loaded
               await LoadAllSessionsAsync(cancellationToken);

               var sessionIds = _sessionConfigurations.Keys.ToList();
               _logger?.LogInformation("Found {Count} active sessions", sessionIds.Count);
               
               return sessionIds;
           }
           finally
           {
               _lock.Release();
           }
       }

       /// <summary>
       /// Loads all session configuration files from blob storage into memory
       /// </summary>
       private async Task LoadAllSessionsAsync(CancellationToken cancellationToken)
       {
           if (_blobServiceClient == null)
           {
               _logger?.LogWarning("BlobServiceClient is null. Cannot load sessions from storage.");
               return;
           }

           try
           {
               var containerClient = _blobServiceClient.GetBlobContainerClient(ConfigContainerName);
               
               if (!await containerClient.ExistsAsync(cancellationToken))
               {
                   _logger?.LogInformation("Config container does not exist. No sessions to load.");
                   return;
               }

               // List all blobs matching the pattern "interface-configurations-*.json"
               var prefix = $"{ConfigFileNamePrefix}-";
               await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
               {
                   try
                   {
                       // Extract sessionId from blob name: "interface-configurations-{sessionId}.json"
                       var blobName = blobItem.Name;
                       if (!blobName.StartsWith(prefix) || !blobName.EndsWith(".json"))
                       {
                           continue;
                       }

                       var sessionId = blobName.Substring(prefix.Length, blobName.Length - prefix.Length - 5); // Remove prefix and ".json"
                       
                       // Skip if already loaded
                       if (_sessionConfigurations.ContainsKey(sessionId))
                       {
                           continue;
                       }

                       // Load configurations for this session
                       var configurations = new Dictionary<string, InterfaceConfiguration>();
                       var blobClient = containerClient.GetBlobClient(blobName);
                       
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
                                       configurations[config.InterfaceName] = config;
                                   }
                                   _logger?.LogInformation("Loaded {Count} configurations for session {SessionId}", configs.Count, sessionId);
                               }
                           }
                       }

                       _sessionConfigurations[sessionId] = configurations;
                   }
                   catch (Exception ex)
                   {
                       _logger?.LogError(ex, "Error loading session from blob {BlobName}", blobItem.Name);
                       // Continue with other sessions
                   }
               }
           }
           catch (Exception ex)
           {
               _logger?.LogError(ex, "Error loading all sessions from storage");
               // Continue - partial loading is better than nothing
           }
       }
   }


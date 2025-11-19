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
            return;

        bool createdDefaultConfiguration = false;
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

            if (!_configurations.ContainsKey(DefaultInterfaceName))
            {
                var defaultConfiguration = CreateDefaultInterfaceConfiguration();
                _configurations[DefaultInterfaceName] = defaultConfiguration;
                createdDefaultConfiguration = true;
                _logger?.LogInformation("Created default interface configuration '{InterfaceName}'", DefaultInterfaceName);
            }

            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }

        if (createdDefaultConfiguration)
        {
            await SaveConfigurationsAsync(cancellationToken);
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
                .Where(c => c.SourceIsEnabled)
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
                .Where(c => c.DestinationIsEnabled)
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

    public async Task SetSourceEnabledAsync(string interfaceName, bool enabled, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
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
        await EnsureInitializedAsync(cancellationToken);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_configurations.TryGetValue(interfaceName, out var config))
            {
                config.DestinationIsEnabled = enabled;
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
           await EnsureInitializedAsync(cancellationToken);
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
           await EnsureInitializedAsync(cancellationToken);
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
           await EnsureInitializedAsync(cancellationToken);
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
           await EnsureInitializedAsync(cancellationToken);
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
           await EnsureInitializedAsync(cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               if (_configurations.TryGetValue(interfaceName, out var config))
               {
                   config.SourceBatchSize = batchSize > 0 ? batchSize : 100; // Ensure positive value, default to 100
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
           await EnsureInitializedAsync(cancellationToken);
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
           await EnsureInitializedAsync(cancellationToken);
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

       public async Task UpdateFieldSeparatorAsync(string interfaceName, string fieldSeparator, CancellationToken cancellationToken = default)
       {
           await EnsureInitializedAsync(cancellationToken);
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
           await EnsureInitializedAsync(cancellationToken);
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
           await EnsureInitializedAsync(cancellationToken);
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
           await EnsureInitializedAsync(cancellationToken);
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
           await EnsureInitializedAsync(cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               if (_configurations.TryGetValue(interfaceName, out var config))
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
           await EnsureInitializedAsync(cancellationToken);
           await _lock.WaitAsync(cancellationToken);
           try
           {
               if (_configurations.TryGetValue(interfaceName, out var config))
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
           await EnsureInitializedAsync(cancellationToken);
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
           await EnsureInitializedAsync(cancellationToken);
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
           await EnsureInitializedAsync(cancellationToken);
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

       private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
       {
           if (!_initialized)
           {
               await InitializeAsync(cancellationToken);
           }
       }
   }


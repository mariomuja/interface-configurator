using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Core.Helpers;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Factory for creating adapter instances based on interface configuration
/// </summary>
public class AdapterFactory : IAdapterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdapterFactory>? _logger;

    public AdapterFactory(IServiceProvider serviceProvider, ILogger<AdapterFactory>? logger = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger;
    }

    public async Task<IAdapter> CreateSourceAdapterAsync(InterfaceConfiguration config, CancellationToken cancellationToken = default)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var adapterName = config.SourceAdapterName;
        var configJson = config.SourceConfiguration;

        try
        {
            var configDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson) 
                ?? new Dictionary<string, JsonElement>();

            return adapterName.ToUpperInvariant() switch
            {
                "CSV" => CreateCsvAdapter(config, configDict, isSource: true),
                "SQLSERVER" => CreateSqlServerAdapter(config, configDict, isSource: true),
                _ => throw new NotSupportedException($"Source adapter '{adapterName}' is not supported")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating source adapter '{AdapterName}' for interface '{InterfaceName}'", 
                adapterName, config.InterfaceName);
            throw;
        }
    }

    public async Task<IAdapter> CreateDestinationAdapterAsync(InterfaceConfiguration config, CancellationToken cancellationToken = default)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var adapterName = config.DestinationAdapterName;
        var configJson = config.DestinationConfiguration;

        try
        {
            var configDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson) 
                ?? new Dictionary<string, JsonElement>();

            return adapterName.ToUpperInvariant() switch
            {
                "CSV" => CreateCsvAdapter(config, configDict, isSource: false),
                "SQLSERVER" => CreateSqlServerAdapter(config, configDict, isSource: false),
                _ => throw new NotSupportedException($"Destination adapter '{adapterName}' is not supported")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating destination adapter '{AdapterName}' for interface '{InterfaceName}'", 
                adapterName, config.InterfaceName);
            throw;
        }
    }

    private CsvAdapter CreateCsvAdapter(InterfaceConfiguration config, Dictionary<string, JsonElement> configDict, bool isSource)
    {
        var csvProcessingService = _serviceProvider.GetRequiredService<ICsvProcessingService>();
        var adapterConfig = _serviceProvider.GetRequiredService<IAdapterConfigurationService>();
        var blobServiceClient = _serviceProvider.GetRequiredService<Azure.Storage.Blobs.BlobServiceClient>();
        var messageBoxService = _serviceProvider.GetService<IMessageBoxService>();
        var subscriptionService = _serviceProvider.GetService<IMessageSubscriptionService>();
        var logger = _serviceProvider.GetService<ILogger<CsvAdapter>>();

        // Get adapter instance GUID, receive folder, file mask, batch size, field separator, and destination properties
        Guid adapterInstanceGuid = isSource ? config.SourceAdapterInstanceGuid : config.DestinationAdapterInstanceGuid;
        string? receiveFolder = isSource ? config.SourceReceiveFolder : null; // Only Source adapters have receive folder
        string? fileMask = isSource ? config.SourceFileMask : null; // Only Source adapters have file mask
        int? batchSize = isSource ? config.SourceBatchSize : null; // Only Source adapters have batch size
        string? fieldSeparator = config.SourceFieldSeparator; // Used for both source and destination
        string? destinationReceiveFolder = isSource ? null : config.DestinationReceiveFolder; // Only Destination adapters have destination receive folder
        string? destinationFileMask = isSource ? null : config.DestinationFileMask; // Only Destination adapters have destination file mask
        
        // Get SFTP properties (only for source adapters)
        string? adapterType = isSource ? config.CsvAdapterType : null;
        string? sftpHost = isSource ? config.SftpHost : null;
        int? sftpPort = isSource ? config.SftpPort : null;
        string? sftpUsername = isSource ? config.SftpUsername : null;
        string? sftpPassword = isSource ? config.SftpPassword : null;
        string? sftpSshKey = isSource ? config.SftpSshKey : null;
        string? sftpFolder = isSource ? config.SftpFolder : null;
        string? sftpFileMask = isSource ? config.SftpFileMask : null;
        int? sftpMaxConnectionPoolSize = isSource ? config.SftpMaxConnectionPoolSize : null;
        int? sftpFileBufferSize = isSource ? config.SftpFileBufferSize : null;

        // Ensure adapter instance exists in MessageBox
        if (messageBoxService != null)
        {
            var instanceName = isSource ? config.SourceInstanceName : config.DestinationInstanceName;
            var adapterName = isSource ? config.SourceAdapterName : config.DestinationAdapterName;
            var isEnabled = isSource ? config.SourceIsEnabled : config.DestinationIsEnabled;
            
            // Fire and forget - don't block adapter creation
            _ = Task.Run(async () =>
            {
                try
                {
                    await messageBoxService.EnsureAdapterInstanceAsync(
                        adapterInstanceGuid,
                        config.InterfaceName,
                        instanceName,
                        adapterName,
                        isSource ? "Source" : "Destination",
                        isEnabled,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error ensuring adapter instance: AdapterInstanceGuid={AdapterInstanceGuid}", adapterInstanceGuid);
                }
            });
        }

        return new CsvAdapter(
            csvProcessingService,
            adapterConfig,
            blobServiceClient,
            messageBoxService,
            subscriptionService,
            config.InterfaceName,
            adapterInstanceGuid,
            receiveFolder,
            fileMask,
            batchSize,
            fieldSeparator,
            destinationReceiveFolder,
            destinationFileMask,
            adapterType,
            sftpHost,
            sftpPort,
            sftpUsername,
            sftpPassword,
            sftpSshKey,
            sftpFolder,
            sftpFileMask,
            sftpMaxConnectionPoolSize,
            sftpFileBufferSize,
            logger);
    }

    private SqlServerAdapter CreateSqlServerAdapter(InterfaceConfiguration config, Dictionary<string, JsonElement> configDict, bool isSource)
    {
        var defaultContext = _serviceProvider.GetService<ApplicationDbContext>();
        var dynamicTableService = _serviceProvider.GetRequiredService<IDynamicTableService>();
        var dataService = _serviceProvider.GetRequiredService<IDataService>();
        var messageBoxService = _serviceProvider.GetService<IMessageBoxService>();
        var subscriptionService = _serviceProvider.GetService<IMessageSubscriptionService>();
        var logger = _serviceProvider.GetService<ILogger<SqlServerAdapter>>();

        // Get adapter instance GUID
        Guid adapterInstanceGuid = isSource ? config.SourceAdapterInstanceGuid : config.DestinationAdapterInstanceGuid;

        // Build connection string from configuration if SQL Server properties are provided
        string? connectionString = null;
        if (!string.IsNullOrWhiteSpace(config.SqlServerName) && !string.IsNullOrWhiteSpace(config.SqlDatabaseName))
        {
            try
            {
                connectionString = SqlConnectionStringBuilder.BuildConnectionStringFromConfig(config);
                _logger?.LogInformation("Built connection string for SQL Server adapter: Server={ServerName}, Database={DatabaseName}", 
                    config.SqlServerName, config.SqlDatabaseName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to build connection string from configuration. Will use default context.");
            }
        }

        // Get source-specific properties (only for source adapters)
        string? pollingStatement = isSource ? config.SqlPollingStatement : null;
        int? pollingInterval = isSource ? config.SqlPollingInterval : null;
        
        // Get table name (used for both source and destination)
        string? tableName = config.SqlTableName;
        
        // Get general SQL Server adapter properties (used for both source and destination)
        bool? useTransaction = config.SqlUseTransaction;
        int? batchSize = config.SqlBatchSize;
        int? commandTimeout = config.SqlCommandTimeout;
        bool? failOnBadStatement = config.SqlFailOnBadStatement;
        var configService = _serviceProvider.GetService<IInterfaceConfigurationService>();

        // Ensure adapter instance exists in MessageBox
        if (messageBoxService != null)
        {
            var instanceName = isSource ? config.SourceInstanceName : config.DestinationInstanceName;
            var adapterName = isSource ? config.SourceAdapterName : config.DestinationAdapterName;
            var isEnabled = isSource ? config.SourceIsEnabled : config.DestinationIsEnabled;
            
            // Fire and forget - don't block adapter creation
            _ = Task.Run(async () =>
            {
                try
                {
                    await messageBoxService.EnsureAdapterInstanceAsync(
                        adapterInstanceGuid,
                        config.InterfaceName,
                        instanceName,
                        adapterName,
                        isSource ? "Source" : "Destination",
                        isEnabled,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error ensuring adapter instance: AdapterInstanceGuid={AdapterInstanceGuid}", adapterInstanceGuid);
                }
            });
        }

        var statisticsService = _serviceProvider.GetService<ProcessingStatisticsService>();
        
        return new SqlServerAdapter(
            defaultContext,
            dynamicTableService,
            dataService,
            messageBoxService,
            subscriptionService,
            config.InterfaceName,
            adapterInstanceGuid,
            connectionString,
            pollingStatement,
            pollingInterval,
            tableName,
            useTransaction,
            batchSize,
            commandTimeout,
            failOnBadStatement,
            configService,
            logger,
            statisticsService);
    }
}


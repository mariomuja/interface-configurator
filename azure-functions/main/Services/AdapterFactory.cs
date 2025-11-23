using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Core.Helpers;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;

#pragma warning disable CS0618 // Type or member is obsolete - Deprecated properties are used for backward compatibility

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

#pragma warning disable CS0618 // Type or member is obsolete - Used for backward compatibility
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
                "SAP" => CreateSapAdapter(config, configDict, isSource: true),
                "DYNAMICS365" => CreateDynamics365Adapter(config, configDict, isSource: true),
                "CRM" => CreateCrmAdapter(config, configDict, isSource: true),
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
                "SAP" => CreateSapAdapter(config, configDict, isSource: false),
                "DYNAMICS365" => CreateDynamics365Adapter(config, configDict, isSource: false),
                "CRM" => CreateCrmAdapter(config, configDict, isSource: false),
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
        // Handle both null and Guid.Empty cases - if GUID is null or Empty, generate a new one
        Guid adapterInstanceGuid = isSource 
            ? (config.SourceAdapterInstanceGuid.HasValue && config.SourceAdapterInstanceGuid.Value != Guid.Empty 
                ? config.SourceAdapterInstanceGuid.Value 
                : Guid.NewGuid())
            : (config.DestinationAdapterInstanceGuid.HasValue && config.DestinationAdapterInstanceGuid.Value != Guid.Empty
                ? config.DestinationAdapterInstanceGuid.Value
                : Guid.NewGuid());
        
        _logger?.LogInformation("DEBUG AdapterFactory: Creating CsvAdapter with InterfaceName={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}, IsSource={IsSource}",
            config.InterfaceName, adapterInstanceGuid, isSource);
        string? receiveFolder = isSource ? config.SourceReceiveFolder : null; // Only Source adapters have receive folder
        string? fileMask = isSource ? config.SourceFileMask : null; // Only Source adapters have file mask
        int? batchSize = isSource ? config.SourceBatchSize : null; // Only Source adapters have batch size
        string? fieldSeparator = config.SourceFieldSeparator; // Used for both source and destination
        string? destinationReceiveFolder = isSource ? null : config.DestinationReceiveFolder; // Only Destination adapters have destination receive folder
        string? destinationFileMask = isSource ? null : config.DestinationFileMask; // Only Destination adapters have destination file mask
        
        // Get SFTP/adapter properties
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

        if (!isSource && configDict != null)
        {
            string? TryGetString(string key)
            {
                if (configDict.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String)
                {
                    return element.GetString();
                }
                return null;
            }

            fieldSeparator = TryGetString("fieldSeparator") ?? fieldSeparator;
            destinationReceiveFolder = TryGetString("destinationReceiveFolder") ?? TryGetString("destination") ?? destinationReceiveFolder;
            destinationFileMask = TryGetString("destinationFileMask") ?? destinationFileMask;

            adapterType = TryGetString("csvAdapterType") ?? TryGetString("adapterType") ?? adapterType;

            if (adapterType != null && adapterType.Equals("SFTP", StringComparison.OrdinalIgnoreCase))
            {
                sftpHost = TryGetString("sftpHost") ?? sftpHost;
                if (configDict.TryGetValue("sftpPort", out var sftpPortElement) && sftpPortElement.ValueKind == JsonValueKind.Number && sftpPortElement.TryGetInt32(out var portValue))
                {
                    sftpPort = portValue;
                }
                sftpUsername = TryGetString("sftpUsername") ?? sftpUsername;
                sftpPassword = TryGetString("sftpPassword") ?? sftpPassword;
                sftpSshKey = TryGetString("sftpSshKey") ?? sftpSshKey;
                sftpFolder = TryGetString("sftpFolder") ?? sftpFolder;
                sftpFileMask = TryGetString("sftpFileMask") ?? sftpFileMask;
                if (configDict.TryGetValue("sftpMaxConnectionPoolSize", out var poolSizeElement) && poolSizeElement.ValueKind == JsonValueKind.Number && poolSizeElement.TryGetInt32(out var poolValue))
                {
                    sftpMaxConnectionPoolSize = poolValue;
                }
                if (configDict.TryGetValue("sftpFileBufferSize", out var bufferElement) && bufferElement.ValueKind == JsonValueKind.Number && bufferElement.TryGetInt32(out var bufferValue))
                {
                    sftpFileBufferSize = bufferValue;
                }
            }
        }

        // Ensure adapter instance exists in MessageBox
        if (messageBoxService != null)
        {
            var instanceName = isSource ? config.SourceInstanceName : config.DestinationInstanceName;
            var adapterName = isSource ? config.SourceAdapterName : config.DestinationAdapterName;
            var isEnabled = isSource ? (config.SourceIsEnabled ?? false) : (config.DestinationIsEnabled ?? false);
            
            // Fire and forget - don't block adapter creation
            _ = Task.Run(async () =>
            {
                try
                {
                    await messageBoxService.EnsureAdapterInstanceAsync(
                        adapterInstanceGuid,
                        config.InterfaceName,
                        instanceName ?? (isSource ? "Source" : "Destination"),
                        adapterName ?? (isSource ? "CSV" : "SqlServer"),
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

        // Create SftpAdapter if adapter type is SFTP
        SftpAdapter? sftpAdapter = null;
        if (adapterType != null && adapterType.Equals("SFTP", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(sftpHost) || string.IsNullOrWhiteSpace(sftpUsername))
            {
                throw new InvalidOperationException("SFTP Host and Username must be configured for SFTP adapter type.");
            }

            var sftpLogger = _serviceProvider.GetService<ILogger<SftpAdapter>>();
            var sftpAdapterRole = isSource ? "Source" : "Destination";
            sftpAdapter = new SftpAdapter(
                sftpHost!,
                sftpPort ?? 22,
                sftpUsername!,
                adapterRole: sftpAdapterRole,
                messageBoxService: messageBoxService,
                interfaceName: config.InterfaceName,
                adapterInstanceGuid: adapterInstanceGuid,
                password: sftpPassword,
                sshKey: sftpSshKey,
                folder: sftpFolder,
                fileMask: sftpFileMask,
                maxConnectionPoolSize: sftpMaxConnectionPoolSize,
                fileBufferSize: sftpFileBufferSize,
                batchSize: batchSize ?? 1000,
                logger: sftpLogger);
        }

        // Create FileAdapter if adapter type is FILE (or default)
        FileAdapter? fileAdapter = null;
        if (adapterType == null || adapterType.Equals("FILE", StringComparison.OrdinalIgnoreCase))
        {
            var fileLogger = _serviceProvider.GetService<ILogger<FileAdapter>>();
            var fileAdapterRole = isSource ? "Source" : "Destination";
            fileAdapter = new FileAdapter(
                blobServiceClient,
                adapterRole: fileAdapterRole,
                messageBoxService: messageBoxService,
                subscriptionService: subscriptionService,
                interfaceName: config.InterfaceName,
                adapterInstanceGuid: adapterInstanceGuid,
                receiveFolder: receiveFolder,
                fileMask: fileMask,
                destinationReceiveFolder: destinationReceiveFolder,
                destinationFileMask: destinationFileMask,
                batchSize: batchSize,
                logger: fileLogger);
        }

        // Update SftpAdapter with role and MessageBox service if needed
        if (sftpAdapter != null && isSource)
        {
            // SftpAdapter is read-only, so it can only be used as Source
            // Note: SftpAdapter constructor was already updated to accept these parameters
            // We need to recreate it with the proper parameters
            var sftpLogger = _serviceProvider.GetService<ILogger<SftpAdapter>>();
            sftpAdapter = new SftpAdapter(
                sftpHost!,
                sftpPort ?? 22,
                sftpUsername!,
                adapterRole: "Source",
                messageBoxService: messageBoxService,
                interfaceName: config.InterfaceName,
                adapterInstanceGuid: adapterInstanceGuid,
                password: sftpPassword,
                sshKey: sftpSshKey,
                folder: sftpFolder,
                fileMask: sftpFileMask,
                maxConnectionPoolSize: sftpMaxConnectionPoolSize,
                fileBufferSize: sftpFileBufferSize,
                batchSize: batchSize,
                logger: sftpLogger);
        }

        var adapterRole = isSource ? "Source" : "Destination";
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
            sftpAdapter,
            fileAdapter,
            adapterRole,
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
        Guid adapterInstanceGuid = isSource 
            ? (config.SourceAdapterInstanceGuid ?? Guid.NewGuid())
            : (config.DestinationAdapterInstanceGuid ?? Guid.NewGuid());

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
            var isEnabled = isSource ? (config.SourceIsEnabled ?? false) : (config.DestinationIsEnabled ?? false);
            
            // Fire and forget - don't block adapter creation
            _ = Task.Run(async () =>
            {
                try
                {
                    await messageBoxService.EnsureAdapterInstanceAsync(
                        adapterInstanceGuid,
                        config.InterfaceName,
                        instanceName ?? (isSource ? "Source" : "Destination"),
                        adapterName ?? (isSource ? "CSV" : "SqlServer"),
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
        var adapterRole = isSource ? "Source" : "Destination";
        
        // Get jq transformation properties (only for destination adapters)
        JQTransformationService? jqService = null;
        string? jqScriptFile = null;
        string? insertStatement = null;
        string? updateStatement = null;
        string? deleteStatement = null;
        
        if (!isSource)
        {
            // Get JQ service
            jqService = _serviceProvider.GetService<JQTransformationService>();
            
            // Try to get properties from DestinationAdapterInstance if available
            if (config.Destinations.TryGetValue(adapterInstanceGuid.ToString(), out var destInstance))
            {
                jqScriptFile = destInstance.JQScriptFile;
                insertStatement = destInstance.InsertStatement;
                updateStatement = destInstance.UpdateStatement;
                deleteStatement = destInstance.DeleteStatement;
            }
            
            // Also check configDict for backward compatibility
            if (configDict != null)
            {
                string? TryGetString(string key)
                {
                    if (configDict.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String)
                    {
                        return element.GetString();
                    }
                    return null;
                }
                
                jqScriptFile = TryGetString("jqScriptFile") ?? jqScriptFile;
                insertStatement = TryGetString("insertStatement") ?? insertStatement;
                updateStatement = TryGetString("updateStatement") ?? updateStatement;
                deleteStatement = TryGetString("deleteStatement") ?? deleteStatement;
            }
        }
        
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
            adapterRole,
            logger,
            statisticsService,
            insertStatement,
            updateStatement,
            deleteStatement,
            jqService,
            jqScriptFile);
    }

    private SapAdapter CreateSapAdapter(InterfaceConfiguration config, Dictionary<string, JsonElement> configDict, bool isSource)
    {
        var messageBoxService = _serviceProvider.GetService<IMessageBoxService>();
        var subscriptionService = _serviceProvider.GetService<IMessageSubscriptionService>();
        var logger = _serviceProvider.GetService<ILogger<SapAdapter>>();

        SourceAdapterInstance? sourceInstance = null;
        DestinationAdapterInstance? destInstance = null;
        Guid adapterInstanceGuid;

        if (isSource)
        {
            sourceInstance = config.Sources.Values.FirstOrDefault();
            adapterInstanceGuid = sourceInstance?.AdapterInstanceGuid ?? Guid.NewGuid();
        }
        else
        {
            destInstance = config.Destinations.Values.FirstOrDefault();
            adapterInstanceGuid = destInstance?.AdapterInstanceGuid ?? Guid.NewGuid();
        }

        // SAP Properties from instance
        string? sapApplicationServer = isSource ? sourceInstance?.SapApplicationServer : destInstance?.SapApplicationServer;
        string? sapSystemNumber = isSource ? sourceInstance?.SapSystemNumber : destInstance?.SapSystemNumber;
        string? sapClient = isSource ? sourceInstance?.SapClient : destInstance?.SapClient;
        string? sapUsername = isSource ? sourceInstance?.SapUsername : destInstance?.SapUsername;
        string? sapPassword = isSource ? sourceInstance?.SapPassword : destInstance?.SapPassword;
        string? sapLanguage = (isSource ? sourceInstance?.SapLanguage : destInstance?.SapLanguage) ?? "EN";
        string? sapIdocType = isSource ? sourceInstance?.SapIdocType : destInstance?.SapIdocType;
        string? sapIdocMessageType = isSource ? sourceInstance?.SapIdocMessageType : destInstance?.SapIdocMessageType;
        string? sapIdocFilter = isSource ? sourceInstance?.SapIdocFilter : null; // Only for source
        int sapPollingInterval = isSource ? (sourceInstance?.SapPollingInterval > 0 ? sourceInstance.SapPollingInterval : 60) : 60;
        int sapBatchSize = (isSource ? sourceInstance?.SapBatchSize : destInstance?.SapBatchSize) > 0 
            ? (isSource ? sourceInstance!.SapBatchSize : destInstance!.SapBatchSize) 
            : 100;
        int sapConnectionTimeout = (isSource ? sourceInstance?.SapConnectionTimeout : destInstance?.SapConnectionTimeout) > 0 
            ? (isSource ? sourceInstance!.SapConnectionTimeout : destInstance!.SapConnectionTimeout) 
            : 30;
        bool sapUseRfc = (isSource ? sourceInstance?.SapUseRfc : destInstance?.SapUseRfc) ?? true;
        string? sapRfcDestination = isSource ? sourceInstance?.SapRfcDestination : destInstance?.SapRfcDestination;
        
        string? sapReceiverPort = isSource ? null : destInstance?.SapReceiverPort; // Only for destination
        string? sapReceiverPartner = isSource ? null : destInstance?.SapReceiverPartner; // Only for destination

        var adapterRole = isSource ? "Source" : "Destination";

        return new SapAdapter(
            messageBoxService,
            subscriptionService,
            config.InterfaceName,
            adapterInstanceGuid,
            sapBatchSize,
            adapterRole,
            logger,
            sapApplicationServer,
            sapSystemNumber,
            sapClient,
            sapUsername,
            sapPassword,
            sapLanguage,
            sapIdocType,
            sapIdocMessageType,
            sapIdocFilter,
            sapPollingInterval,
            sapBatchSize,
            sapConnectionTimeout,
            sapUseRfc,
            sapRfcDestination,
            sapReceiverPort,
            sapReceiverPartner);
    }

    private Dynamics365Adapter CreateDynamics365Adapter(InterfaceConfiguration config, Dictionary<string, JsonElement> configDict, bool isSource)
    {
        var messageBoxService = _serviceProvider.GetService<IMessageBoxService>();
        var subscriptionService = _serviceProvider.GetService<IMessageSubscriptionService>();
        var logger = _serviceProvider.GetService<ILogger<Dynamics365Adapter>>();

        // Get adapter instance from Sources or Destinations dictionary
        var instance = isSource 
            ? config.Sources.Values.FirstOrDefault()
            : null;

        Guid adapterInstanceGuid = isSource 
            ? (instance?.AdapterInstanceGuid ?? Guid.NewGuid())
            : (config.Destinations.Values.FirstOrDefault()?.AdapterInstanceGuid ?? Guid.NewGuid());

        // Dynamics 365 Properties from instance
        string? tenantId = instance?.Dynamics365TenantId;
        string? clientId = instance?.Dynamics365ClientId;
        string? clientSecret = instance?.Dynamics365ClientSecret;
        string? instanceUrl = instance?.Dynamics365InstanceUrl;
        string? entityName = instance?.Dynamics365EntityName;
        string? odataFilter = isSource ? instance?.Dynamics365ODataFilter : null; // Only for source
        int pollingInterval = isSource ? (instance?.Dynamics365PollingInterval > 0 ? instance.Dynamics365PollingInterval : 60) : 60;
        int batchSize = instance?.Dynamics365BatchSize > 0 ? instance.Dynamics365BatchSize : 100;
        int pageSize = instance?.Dynamics365PageSize > 0 ? instance.Dynamics365PageSize : 50;
        
        bool useBatch = false;
        if (!isSource)
        {
            var destInstance = config.Destinations.Values.FirstOrDefault();
            useBatch = destInstance?.Dynamics365UseBatch ?? true;
        }

        var adapterRole = isSource ? "Source" : "Destination";

        return new Dynamics365Adapter(
            messageBoxService,
            subscriptionService,
            config.InterfaceName,
            adapterInstanceGuid,
            batchSize,
            adapterRole,
            logger,
            tenantId,
            clientId,
            clientSecret,
            instanceUrl,
            entityName,
            odataFilter,
            pollingInterval,
            batchSize,
            pageSize,
            useBatch);
    }

    private CrmAdapter CreateCrmAdapter(InterfaceConfiguration config, Dictionary<string, JsonElement> configDict, bool isSource)
    {
        var messageBoxService = _serviceProvider.GetService<IMessageBoxService>();
        var subscriptionService = _serviceProvider.GetService<IMessageSubscriptionService>();
        var logger = _serviceProvider.GetService<ILogger<CrmAdapter>>();

        // Get adapter instance from Sources or Destinations dictionary
        var instance = isSource 
            ? config.Sources.Values.FirstOrDefault()
            : null;

        Guid adapterInstanceGuid = isSource 
            ? (instance?.AdapterInstanceGuid ?? Guid.NewGuid())
            : (config.Destinations.Values.FirstOrDefault()?.AdapterInstanceGuid ?? Guid.NewGuid());

        // CRM Properties from instance
        string? organizationUrl = instance?.CrmOrganizationUrl;
        string? username = instance?.CrmUsername;
        string? password = instance?.CrmPassword;
        string? entityName = instance?.CrmEntityName;
        string? fetchXml = isSource ? instance?.CrmFetchXml : null; // Only for source
        int pollingInterval = isSource ? (instance?.CrmPollingInterval > 0 ? instance.CrmPollingInterval : 60) : 60;
        int batchSize = instance?.CrmBatchSize > 0 ? instance.CrmBatchSize : 100;
        
        bool useBatch = false;
        if (!isSource)
        {
            var destInstance = config.Destinations.Values.FirstOrDefault();
            useBatch = destInstance?.CrmUseBatch ?? true;
        }

        var adapterRole = isSource ? "Source" : "Destination";

        return new CrmAdapter(
            messageBoxService,
            subscriptionService,
            config.InterfaceName,
            adapterInstanceGuid,
            batchSize,
            adapterRole,
            logger,
            organizationUrl,
            username,
            password,
            entityName,
            fetchXml,
            pollingInterval,
            batchSize,
            useBatch);
    }
}

#pragma warning restore CS0618 // Type or member is obsolete


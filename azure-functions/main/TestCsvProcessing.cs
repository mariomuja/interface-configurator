using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Core.Models;
using System.Linq;
using Azure.Storage.Blobs;

#pragma warning disable CS0618 // Type or member is obsolete - Deprecated properties are used for backward compatibility

namespace InterfaceConfigurator.Main;

/// <summary>
/// Test endpoint to simulate CSV processing and check MessageBox write
/// </summary>
public class TestCsvProcessing
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IAdapterFactory _adapterFactory;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<TestCsvProcessing> _logger;

    public TestCsvProcessing(
        IInterfaceConfigurationService configService,
        IAdapterFactory adapterFactory,
        BlobServiceClient blobServiceClient,
        ILogger<TestCsvProcessing> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("TestCsvProcessing")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "test-csv-processing")] HttpRequestData req,
        FunctionContext context)
    {
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            _logger.LogInformation("=== TESTING CSV PROCESSING WITH MESSAGEBOX WRITE ===");
            
            // Force initialization
            await _configService.InitializeAsync(context.CancellationToken);
            
            // Get enabled CSV source configurations
            var enabledConfigs = await _configService.GetEnabledSourceConfigurationsAsync(context.CancellationToken);
            _logger.LogInformation("Found {Count} enabled source configurations", enabledConfigs.Count);
            
            // Filter configurations that have CSV source adapters
            var csvConfigs = enabledConfigs
                .Where(c => c.Sources.Values.Any(s => s.AdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase) && s.IsEnabled))
                .ToList();
            
            _logger.LogInformation("Found {Count} CSV configurations", csvConfigs.Count);

            if (csvConfigs.Count == 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(errorResponse);
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "No CSV configurations found" }));
                return errorResponse;
            }

            var results = new List<object>();

            foreach (var config in csvConfigs)
            {
                // Get all enabled CSV source instances for this interface
                var csvSources = config.Sources.Values
                    .Where(s => s.AdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase) && s.IsEnabled)
                    .ToList();
                
                foreach (var sourceInstance in csvSources)
                {
                    try
                    {
                        _logger.LogInformation("Testing CSV processing for interface {InterfaceName}, source instance '{InstanceName}' (GUID: {AdapterInstanceGuid})",
                            config.InterfaceName, sourceInstance.InstanceName, sourceInstance.AdapterInstanceGuid);
                        
                        // Verify configuration has required values
                        if (sourceInstance.AdapterInstanceGuid == Guid.Empty)
                        {
                            results.Add(new
                            {
                                interfaceName = config.InterfaceName,
                                instanceName = sourceInstance.InstanceName,
                                success = false,
                                error = "AdapterInstanceGuid is empty"
                            });
                            continue;
                        }
                        
                        if (string.IsNullOrWhiteSpace(config.InterfaceName))
                        {
                            results.Add(new
                            {
                                interfaceName = config.InterfaceName,
                                instanceName = sourceInstance.InstanceName,
                                success = false,
                                error = "InterfaceName is empty"
                            });
                            continue;
                        }
                        
                        // Create a temporary config with this source instance for the adapter factory
                        var tempConfig = CreateTempConfigForSource(config, sourceInstance);
                        
                        // Create adapter
                        var csvAdapter = await _adapterFactory.CreateSourceAdapterAsync(tempConfig, context.CancellationToken);
                        
                        if (csvAdapter is CsvAdapter csv)
                        {
                            // Try to process a test CSV file from blob storage
                            // First, check if there are any files in csv-incoming
                            var containerClient = _blobServiceClient.GetBlobContainerClient("csv-files");
                            var blobPath = "csv-incoming";
                            
                            var blobs = containerClient.GetBlobs(prefix: blobPath);
                            var csvFiles = blobs.Where(b => !b.Name.EndsWith(".folder-initialized") && b.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(b => b.Properties.CreatedOn)
                                .Take(1)
                                .ToList();
                            
                            if (csvFiles.Count == 0)
                            {
                                results.Add(new
                                {
                                    interfaceName = config.InterfaceName,
                                    instanceName = sourceInstance.InstanceName,
                                    success = false,
                                    error = "No CSV files found in csv-incoming folder",
                                    message = "Please upload a CSV file first"
                                });
                                continue;
                            }
                            
                            var testFile = csvFiles.First();
                            var sourcePath = testFile.Name;
                            
                            _logger.LogInformation("Processing CSV file: {SourcePath}", sourcePath);
                            
                            // Call ReadAsync which should write to MessageBox
                            var (headers, records) = await csv.ReadAsync(sourcePath, context.CancellationToken);
                            
                            results.Add(new
                            {
                                interfaceName = config.InterfaceName,
                                instanceName = sourceInstance.InstanceName,
                                adapterInstanceGuid = sourceInstance.AdapterInstanceGuid,
                                success = true,
                                sourcePath = sourcePath,
                                headerCount = headers.Count,
                                recordCount = records.Count,
                                message = $"Successfully processed CSV file. Check Application Insights logs for 'Successfully debatched and wrote' messages to verify MessageBox write."
                            });
                        }
                        else
                        {
                            results.Add(new
                            {
                                interfaceName = config.InterfaceName,
                                instanceName = sourceInstance.InstanceName,
                                success = false,
                                error = $"Adapter is not CsvAdapter type: {csvAdapter?.GetType().Name ?? "null"}"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error testing CSV processing for interface {InterfaceName}, source instance '{InstanceName}'",
                            config.InterfaceName, sourceInstance.InstanceName);
                        
                        results.Add(new
                        {
                            interfaceName = config.InterfaceName,
                            instanceName = sourceInstance.InstanceName,
                            success = false,
                            error = ex.Message,
                            stackTrace = ex.StackTrace
                        });
        }
    }
}

#pragma warning restore CS0618 // Type or member is obsolete

            var result = new
            {
                csvConfigurationsCount = csvConfigs.Count,
                testedInstancesCount = results.Count,
                results = results
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TestCsvProcessing");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message, stackTrace = ex.StackTrace }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Creates a temporary InterfaceConfiguration from a SourceAdapterInstance for use with AdapterFactory
    /// </summary>
    private InterfaceConfiguration CreateTempConfigForSource(InterfaceConfiguration originalConfig, SourceAdapterInstance sourceInstance)
    {
        var tempConfig = new InterfaceConfiguration
        {
            InterfaceName = originalConfig.InterfaceName,
            Description = originalConfig.Description,
            CreatedAt = originalConfig.CreatedAt,
            UpdatedAt = originalConfig.UpdatedAt,
            SourceAdapterName = sourceInstance.AdapterName,
            SourceConfiguration = sourceInstance.Configuration,
            SourceIsEnabled = sourceInstance.IsEnabled,
            SourceInstanceName = sourceInstance.InstanceName,
            SourceAdapterInstanceGuid = sourceInstance.AdapterInstanceGuid,
            SourceReceiveFolder = sourceInstance.SourceReceiveFolder,
            SourceFileMask = sourceInstance.SourceFileMask,
            SourceBatchSize = sourceInstance.SourceBatchSize,
            SourceFieldSeparator = sourceInstance.SourceFieldSeparator,
            CsvData = sourceInstance.CsvData,
            CsvAdapterType = sourceInstance.CsvAdapterType,
            CsvPollingInterval = sourceInstance.CsvPollingInterval,
            SftpHost = sourceInstance.SftpHost,
            SftpPort = sourceInstance.SftpPort,
            SftpUsername = sourceInstance.SftpUsername,
            SftpPassword = sourceInstance.SftpPassword,
            SftpSshKey = sourceInstance.SftpSshKey,
            SftpFolder = sourceInstance.SftpFolder,
            SftpFileMask = sourceInstance.SftpFileMask,
            SftpMaxConnectionPoolSize = sourceInstance.SftpMaxConnectionPoolSize,
            SftpFileBufferSize = sourceInstance.SftpFileBufferSize,
            SqlServerName = sourceInstance.SqlServerName,
            SqlDatabaseName = sourceInstance.SqlDatabaseName,
            SqlUserName = sourceInstance.SqlUserName,
            SqlPassword = sourceInstance.SqlPassword,
            SqlIntegratedSecurity = sourceInstance.SqlIntegratedSecurity,
            SqlResourceGroup = sourceInstance.SqlResourceGroup,
            SqlPollingStatement = sourceInstance.SqlPollingStatement,
            SqlPollingInterval = sourceInstance.SqlPollingInterval,
            SqlTableName = sourceInstance.SqlTableName,
            SqlUseTransaction = sourceInstance.SqlUseTransaction,
            SqlBatchSize = sourceInstance.SqlBatchSize,
            SqlCommandTimeout = sourceInstance.SqlCommandTimeout,
            SqlFailOnBadStatement = sourceInstance.SqlFailOnBadStatement,
            DestinationAdapterName = originalConfig.DestinationAdapterName,
            DestinationConfiguration = originalConfig.DestinationConfiguration,
            DestinationIsEnabled = originalConfig.DestinationIsEnabled,
            DestinationInstanceName = originalConfig.DestinationInstanceName,
            DestinationAdapterInstanceGuid = originalConfig.DestinationAdapterInstanceGuid,
            DestinationAdapterInstances = originalConfig.DestinationAdapterInstances
        };
        
        return tempConfig;
    }
}

#pragma warning restore CS0618 // Type or member is obsolete


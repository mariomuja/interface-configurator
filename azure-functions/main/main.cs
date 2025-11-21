using System.Linq;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Core.Helpers;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main;

public class MainFunction
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IAdapterFactory _adapterFactory;
    private readonly ILogger<MainFunction> _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public MainFunction(
        IInterfaceConfigurationService configService,
        IAdapterFactory adapterFactory,
        ILogger<MainFunction> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _logger = logger;
        
        // Initialize BlobServiceClient for moving blobs between containers
        // Use MainStorageConnection (main storage account where blobs are uploaded)
        // Fallback to AzureWebJobsStorage if MainStorageConnection is not set
        var connectionString = Environment.GetEnvironmentVariable("MainStorageConnection") 
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Storage connection string not found. Blob movement will not work.");
            _blobServiceClient = null!;
        }
        else
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
        }
    }

    [Function("Main")]
    public async Task Run(
        [BlobTrigger("csv-files/csv-incoming/{name}", Connection = "MainStorageConnection")] Stream blobStream,
        string name,
        FunctionContext context)
    {
        string? blobName = name;
        try
        {
            _logger.LogInformation("=== BLOB TRIGGER ACTIVATED ===");
            _logger.LogInformation("Blob trigger received for blob: {BlobName}", blobName);
            _logger.LogInformation("Blob stream length: {Length} bytes", blobStream?.Length ?? 0);
            _logger.LogInformation("MainStorageConnection configured: {IsConfigured}", 
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MainStorageConnection")));
            
            // Force initialization of configuration service before processing
            _logger.LogInformation("DEBUG: Forcing configuration service initialization...");
            await _configService.InitializeAsync(context.CancellationToken);
            _logger.LogInformation("DEBUG: Configuration service initialization completed");

            // Use CsvAdapter to read CSV from blob storage and write to MessageBox
            // The CsvAdapter.ReadAsync will automatically:
            // 1. Read CSV from blob storage
            // 2. Parse CSV content
            // 3. Debatch records (one record per message)
            // 4. Write each record to MessageBox as a separate message
            // The DestinationAdapterFunction (timer-triggered, runs every minute) will then:
            // 1. Read pending messages from MessageBox
            // 2. Write them to SQL Server TransportData table
            // 3. Mark messages as processed
            try
            {
                var sourcePath = $"csv-files/csv-incoming/{blobName}";
                
                // Validate CSV file before processing
                try
                {
                    var validationService = new CsvValidationService(_logger);
                    var containerClient = _blobServiceClient.GetBlobContainerClient("csv-files");
                    var blobClient = containerClient.GetBlobClient(sourcePath);
                    
                    if (await blobClient.ExistsAsync(context.CancellationToken))
                    {
                        var downloadResult = await blobClient.DownloadContentAsync(context.CancellationToken);
                        var csvContent = downloadResult.Value.Content.ToString();
                        var validationResult = validationService.ValidateCsv(csvContent);
                        
                        if (!validationResult.IsValid)
                        {
                            _logger.LogWarning("CSV validation failed for {BlobName}. Issues: {Issues}", 
                                blobName, string.Join("; ", validationResult.Issues));
                            
                            // Move to error folder if validation fails
                            await MoveBlobToFolderAsync("csv-files", "csv-incoming", "csv-error", blobName, 
                                $"Validation failed: {string.Join("; ", validationResult.Issues)}");
                            return; // Don't process invalid CSV
                        }
                        
                        _logger.LogInformation("CSV validation passed for {BlobName}. Encoding: {Encoding}, Delimiter: {Delimiter}, Columns: {ColumnCount}, Lines: {LineCount}",
                            blobName, validationResult.Encoding, validationResult.DetectedDelimiter, validationResult.ColumnCount, validationResult.LineCount);
                    }
                }
                catch (Exception validationEx)
                {
                    _logger.LogWarning(validationEx, "CSV validation error for {BlobName}, continuing with processing", blobName);
                    // Continue processing even if validation fails (non-blocking)
                }
                
                // Get all enabled CSV source configurations and process blob for each
                // This ensures messages are written with the correct interface name and adapter instance GUID
                
                // Debug: Get all configurations first to ensure they're loaded
                var allConfigs = await _configService.GetAllConfigurationsAsync(context.CancellationToken);
                _logger.LogInformation("DEBUG: Total configurations loaded: {TotalCount}", allConfigs.Count);
                foreach (var cfg in allConfigs)
                {
                    _logger.LogInformation("DEBUG: Config {InterfaceName}: SourceIsEnabled={SourceIsEnabled} (type: {Type}), SourceAdapterName={SourceAdapterName}, SourceAdapterInstanceGuid={Guid}",
                        cfg.InterfaceName, cfg.SourceIsEnabled, cfg.SourceIsEnabled.GetType().Name, cfg.SourceAdapterName, cfg.SourceAdapterInstanceGuid);
                }
                
                // Now get enabled configurations
                var enabledConfigs = await _configService.GetEnabledSourceConfigurationsAsync(context.CancellationToken);
                _logger.LogInformation("DEBUG: GetEnabledSourceConfigurationsAsync returned {Count} configurations", enabledConfigs.Count);
                foreach (var cfg in enabledConfigs)
                {
                    _logger.LogInformation("DEBUG: Enabled config {InterfaceName}: SourceIsEnabled={SourceIsEnabled}, SourceAdapterName={SourceAdapterName}",
                        cfg.InterfaceName, cfg.SourceIsEnabled, cfg.SourceAdapterName);
                }
                
                // Filter configurations that have CSV source adapters
                var csvConfigs = enabledConfigs
                    .Where(c => c.Sources.Values.Any(s => s.AdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase) && s.IsEnabled))
                    .ToList();
                
                _logger.LogInformation("Found {TotalConfigs} enabled configurations, {CsvConfigs} CSV configurations for blob {BlobName}",
                    enabledConfigs.Count, csvConfigs.Count, blobName);
                
                if (!csvConfigs.Any())
                {
                    _logger.LogWarning("No enabled CSV source configurations found. CSV blob {BlobName} will not be processed.", blobName);
                    await MoveBlobToFolderAsync("csv-files", "csv-incoming", "csv-error", blobName, "No enabled CSV source configurations found");
                    return;
                }
                
                // Process blob for each enabled CSV source configuration
                var processedCount = 0;
                var errorCount = 0;
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
                            _logger.LogInformation("Processing CSV blob {BlobName} for interface {InterfaceName} with source instance '{InstanceName}' (GUID: {AdapterInstanceGuid})",
                                blobName, config.InterfaceName, sourceInstance.InstanceName, sourceInstance.AdapterInstanceGuid);
                            
                            // Verify configuration has required values
                            if (sourceInstance.AdapterInstanceGuid == Guid.Empty)
                            {
                                _logger.LogError("AdapterInstanceGuid is empty for source instance '{InstanceName}' in interface {InterfaceName}. Skipping.", 
                                    sourceInstance.InstanceName, config.InterfaceName);
                                errorCount++;
                                continue;
                            }
                            
                            if (string.IsNullOrWhiteSpace(config.InterfaceName))
                            {
                                _logger.LogError("InterfaceName is empty for configuration. Skipping.");
                                errorCount++;
                                continue;
                            }
                            
                            // Create a temporary config with this source instance for the adapter factory
                            // The adapter factory expects InterfaceConfiguration, so we create a temporary one
                            var tempConfig = CreateTempConfigForSource(config, sourceInstance);
                            
                            _logger.LogInformation("DEBUG: Created tempConfig with InterfaceName={InterfaceName}, SourceAdapterInstanceGuid={AdapterInstanceGuid}",
                                tempConfig.InterfaceName, tempConfig.SourceAdapterInstanceGuid);
                            
                            // Create adapter with correct interface name and adapter instance GUID
                            var csvAdapter = await _adapterFactory.CreateSourceAdapterAsync(tempConfig, context.CancellationToken);
                        if (csvAdapter is CsvAdapter csv)
                        {
                            _logger.LogInformation("CsvAdapter created successfully. InterfaceName={InterfaceName}, AdapterInstanceGuid={AdapterInstanceGuid}. Calling ReadAsync to process blob and write to MessageBox...",
                                config.InterfaceName, sourceInstance.AdapterInstanceGuid);
                            var (headers, records) = await csv.ReadAsync(sourcePath, context.CancellationToken);
                            
                            _logger.LogInformation("Successfully processed CSV blob {BlobName} for interface {InterfaceName}: {HeaderCount} headers, {RecordCount} records. " +
                                "Messages should have been written to MessageBox (check logs for 'Successfully debatched and wrote' messages).",
                                blobName, config.InterfaceName, headers.Count, records.Count);
                            processedCount++;
                        }
                        else
                        {
                            _logger.LogWarning("Adapter is not CsvAdapter type: {AdapterType}", csvAdapter?.GetType().Name ?? "null");
                            errorCount++;
                        }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing CSV blob {BlobName} for interface {InterfaceName}, source instance '{InstanceName}': {ErrorMessage}",
                                blobName, config.InterfaceName, sourceInstance.InstanceName, ex.Message);
                            errorCount++;
                        }
                    }
                }
                
                _logger.LogInformation("Completed processing CSV blob {BlobName}: {ProcessedCount} succeeded, {ErrorCount} failed",
                    blobName, processedCount, errorCount);
                
                _logger.LogInformation("Completed processing CSV blob {BlobName} for {ConfigCount} interface configuration(s). Destination adapter will process messages within 1 minute.",
                    blobName, csvConfigs.Count);
            }
            catch (Exception ex)
            {
                // Log to Application Insights with full exception details including inner exceptions
                _logger.LogError(ex, "CSV processing failed for {BlobName}: {ErrorMessage}", blobName, ex.Message);
                
                // Also log formatted exception details for better visibility in Application Insights
                var exceptionDetails = ExceptionHelper.FormatException(ex, includeStackTrace: true);
                _logger.LogError("CSV processing failed - Full exception details for {BlobName}:\n{ExceptionDetails}", blobName, exceptionDetails);
                
                // Move blob to csv-error folder
                await MoveBlobToFolderAsync("csv-files", "csv-incoming", "csv-error", blobName, "Processing failed");
                
                throw;
            }
            
            // Move blob to csv-processed folder
            await MoveBlobToFolderAsync("csv-files", "csv-incoming", "csv-processed", blobName, "Processing completed successfully");
        }
        catch (Exception ex)
        {
            // Log to Application Insights with full exception details including inner exceptions
            _logger.LogError(ex, "Unexpected error processing blob from Queue Trigger");
            
            // Also log formatted exception details for better visibility in Application Insights
            var exceptionDetails = ExceptionHelper.FormatException(ex, includeStackTrace: true);
            _logger.LogError("Unexpected error - Full exception details:\n{ExceptionDetails}", exceptionDetails);
            
            // Move blob to error folder if we have the blob name
            if (!string.IsNullOrEmpty(blobName))
            {
                try
                {
                    var exceptionSummary = ExceptionHelper.GetExceptionSummary(ex);
                    await MoveBlobToFolderAsync("csv-files", "csv-incoming", "csv-error", blobName, $"Exception: {exceptionSummary}");
                }
                catch (Exception moveEx)
                {
                    // Log move exception with full details to Application Insights
                    _logger.LogError(moveEx, "Failed to move blob {BlobName} to error folder", blobName);
                    var moveExceptionDetails = ExceptionHelper.FormatException(moveEx, includeStackTrace: true);
                    _logger.LogError("Blob move failed - Full exception details:\n{ExceptionDetails}", moveExceptionDetails);
                }
            }
            
            throw;
        }
    }

    /// <summary>
    /// Creates a temporary InterfaceConfiguration from a SourceAdapterInstance for use with AdapterFactory
    /// This is a compatibility layer until AdapterFactory is updated to work directly with SourceAdapterInstance
    /// </summary>
    private InterfaceConfiguration CreateTempConfigForSource(InterfaceConfiguration originalConfig, SourceAdapterInstance sourceInstance)
    {
        // Create a temporary config with properties from the source instance
        // This allows the AdapterFactory to work with the existing interface
        var tempConfig = new InterfaceConfiguration
        {
            InterfaceName = originalConfig.InterfaceName,
            Description = originalConfig.Description,
            CreatedAt = originalConfig.CreatedAt,
            UpdatedAt = originalConfig.UpdatedAt,
            // Set deprecated properties from source instance for backward compatibility
            SourceAdapterName = sourceInstance.AdapterName,
            SourceConfiguration = sourceInstance.Configuration,
            SourceIsEnabled = sourceInstance.IsEnabled,
            SourceInstanceName = sourceInstance.InstanceName,
            SourceAdapterInstanceGuid = sourceInstance.AdapterInstanceGuid,
            // Copy all source properties
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
            SqlFailOnBadStatement = sourceInstance.SqlFailOnBadStatement
        };
        
        // Also copy Sources and Destinations dictionaries
        tempConfig.Sources = originalConfig.Sources;
        tempConfig.Destinations = originalConfig.Destinations;
        
        return tempConfig;
    }

    private async Task MoveBlobToFolderAsync(string containerName, string sourceFolder, string targetFolder, string blobName, string reason)
    {
        if (_blobServiceClient == null)
        {
            _logger.LogWarning("BlobServiceClient not initialized. Cannot move blob {BlobName} from {SourceFolder} to {TargetFolder}", 
                blobName, sourceFolder, targetFolder);
            return;
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync();
            
            // Build blob paths with folder prefixes
            var sourceBlobPath = $"{sourceFolder}/{blobName}";
            var targetBlobPath = $"{targetFolder}/{blobName}";
            
            var sourceBlobClient = containerClient.GetBlobClient(sourceBlobPath);
            var targetBlobClient = containerClient.GetBlobClient(targetBlobPath);
            
            // Check if source blob exists
            if (!await sourceBlobClient.ExistsAsync())
            {
                _logger.LogWarning("Source blob {BlobName} does not exist at {SourcePath}", blobName, sourceBlobPath);
                return;
            }
            
            // Check if target blob already exists (idempotency check)
            if (await targetBlobClient.ExistsAsync())
            {
                _logger.LogInformation("Target blob {BlobName} already exists at {TargetPath}, skipping move (idempotent)", blobName, targetBlobPath);
                // Delete source blob since target already exists
                await sourceBlobClient.DeleteIfExistsAsync();
                return;
            }
            
            // Copy blob to target folder
            await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
            
            // Wait for copy to complete
            var properties = await targetBlobClient.GetPropertiesAsync();
            while (properties.Value.CopyStatus == Azure.Storage.Blobs.Models.CopyStatus.Pending)
            {
                await Task.Delay(100);
                properties = await targetBlobClient.GetPropertiesAsync();
            }
            
            if (properties.Value.CopyStatus == Azure.Storage.Blobs.Models.CopyStatus.Success)
            {
                // Delete source blob after successful copy
                await sourceBlobClient.DeleteIfExistsAsync();
                _logger.LogInformation("Moved blob {BlobName} from {SourceFolder} to {TargetFolder}. Reason: {Reason}", 
                    blobName, sourceFolder, targetFolder, reason);
                
                // Clean up old files in target folder (keep only 10 most recent) for csv-processed and csv-error folders
                if (targetFolder == "csv-processed" || targetFolder == "csv-error")
                {
                    await CleanupOldFilesAsync(containerName, targetFolder, maxFiles: 10);
                }
            }
            else
            {
                _logger.LogError("Failed to copy blob {BlobName} from {SourceFolder} to {TargetFolder}. Copy status: {CopyStatus}", 
                    blobName, sourceFolder, targetFolder, properties.Value.CopyStatus);
            }
        }
        catch (Exception ex)
        {
            // Log to Application Insights with full exception details including inner exceptions
            _logger.LogError(ex, "Error moving blob {BlobName} from {SourceFolder} to {TargetFolder}", 
                blobName, sourceFolder, targetFolder);
            
            // Also log formatted exception details for better visibility in Application Insights
            var exceptionDetails = ExceptionHelper.FormatException(ex, includeStackTrace: true);
            _logger.LogError("Blob move error - Full exception details for {BlobName}:\n{ExceptionDetails}", blobName, exceptionDetails);
            
            // Don't throw - blob movement failure shouldn't fail the function
        }
    }

    /// <summary>
    /// Cleans up old files in a blob folder, keeping only the most recent N files
    /// Excludes placeholder files like .folder-initialized
    /// </summary>
    private async Task CleanupOldFilesAsync(string containerName, string folderPath, int maxFiles)
    {
        if (_blobServiceClient == null)
        {
            _logger.LogWarning("BlobServiceClient not initialized. Cannot clean up old files in {FolderPath}", folderPath);
            return;
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            // Ensure folder path ends with /
            if (!folderPath.EndsWith("/"))
                folderPath += "/";
            
            // List all blobs in the folder
            var blobs = new List<(string Name, DateTimeOffset CreatedOn)>();
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: folderPath))
            {
                // Skip placeholder files
                if (blobItem.Name.EndsWith(".folder-initialized", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                // Get blob properties to get creation time
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                var properties = await blobClient.GetPropertiesAsync();
                
                blobs.Add((blobItem.Name, properties.Value.CreatedOn));
            }
            
            // Sort by creation time (newest first)
            var sortedBlobs = blobs.OrderByDescending(b => b.CreatedOn).ToList();
            
            // If we have more than maxFiles, delete the older ones
            if (sortedBlobs.Count > maxFiles)
            {
                var filesToDelete = sortedBlobs.Skip(maxFiles).ToList();
                _logger.LogInformation("Cleaning up {DeleteCount} old files from {FolderPath} (keeping {KeepCount} most recent)", 
                    filesToDelete.Count, folderPath, maxFiles);
                
                foreach (var (blobName, _) in filesToDelete)
                {
                    try
                    {
                        var blobClient = containerClient.GetBlobClient(blobName);
                        await blobClient.DeleteIfExistsAsync();
                        _logger.LogDebug("Deleted old file: {BlobName}", blobName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error deleting old file {BlobName} from {FolderPath}", blobName, folderPath);
                        // Continue with other files even if one fails
                    }
                }
                
                _logger.LogInformation("Successfully cleaned up {DeleteCount} old files from {FolderPath}", filesToDelete.Count, folderPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old files in folder {FolderPath}", folderPath);
            // Don't throw - cleanup failure shouldn't fail the main operation
        }
    }
}


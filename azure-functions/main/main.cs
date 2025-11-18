using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Core.Helpers;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main;

public class MainFunction
{
    private readonly CsvAdapter _csvAdapter;
    private readonly ILogger<MainFunction> _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public MainFunction(
        CsvAdapter csvAdapter,
        ILogger<MainFunction> logger)
    {
        _csvAdapter = csvAdapter ?? throw new ArgumentNullException(nameof(csvAdapter));
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
            _logger.LogInformation("Blob trigger received for blob: {BlobName}", blobName);

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
                
                var (headers, records) = await _csvAdapter.ReadAsync(sourcePath, context.CancellationToken);
                
                _logger.LogInformation("Successfully processed CSV blob {BlobName}: {HeaderCount} headers, {RecordCount} records written to MessageBox. Destination adapter will process messages within 1 minute.",
                    blobName, headers.Count, records.Count);
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
}


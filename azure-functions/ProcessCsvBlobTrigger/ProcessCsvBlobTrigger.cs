using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Helpers;
using ProcessCsvBlobTrigger.Core.Interfaces;

namespace ProcessCsvBlobTrigger;

public class ProcessCsvBlobTriggerFunction
{
    private readonly ICsvProcessor _csvProcessor;
    private readonly ILogger<ProcessCsvBlobTriggerFunction> _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public ProcessCsvBlobTriggerFunction(
        ICsvProcessor csvProcessor,
        ILogger<ProcessCsvBlobTriggerFunction> logger)
    {
        _csvProcessor = csvProcessor;
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

    [Function("ProcessCsvBlobTrigger")]
    public async Task Run(
        [BlobTrigger("csv-files/csv-incoming/{name}", Connection = "MainStorageConnection")] Stream blobStream,
        string name,
        FunctionContext context)
    {
        string? blobName = name;
        try
        {
            _logger.LogInformation("Blob trigger received for blob: {BlobName}", blobName);

            // Read blob content from stream
            byte[] blobContent;
            using (var memoryStream = new MemoryStream())
            {
                await blobStream.CopyToAsync(memoryStream);
                blobContent = memoryStream.ToArray();
            }

            var blobSize = blobContent.Length;
            _logger.LogInformation("Blob trigger processed blob: {BlobName} ({BlobSize} bytes) from csv-incoming folder", blobName, blobSize);

            var result = await _csvProcessor.ProcessCsvAsync(blobContent, blobName);

            if (!result.Success)
            {
                var exception = result.Exception ?? new Exception(result.ErrorMessage ?? "Unknown error occurred");
                
                // Log to Application Insights with full exception details including inner exceptions
                _logger.LogError(exception, "CSV processing failed for {BlobName}: {ErrorMessage}", blobName, result.ErrorMessage);
                
                // Also log formatted exception details for better visibility in Application Insights
                var exceptionDetails = ExceptionHelper.FormatException(exception, includeStackTrace: true);
                _logger.LogError("CSV processing failed - Full exception details for {BlobName}:\n{ExceptionDetails}", blobName, exceptionDetails);
                
                // Move blob to csv-error folder
                await MoveBlobToFolderAsync("csv-files", "csv-incoming", "csv-error", blobName, "Processing failed");
                
                throw exception;
            }

            _logger.LogInformation("Successfully processed {RecordCount} records from {BlobName} in {ChunkCount} chunks",
                result.RecordsProcessed, blobName, result.ChunksProcessed);
            
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


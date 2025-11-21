using System.Net;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class DeleteBlobFile
{
    private readonly ILogger<DeleteBlobFile> _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public DeleteBlobFile(ILogger<DeleteBlobFile> logger)
    {
        _logger = logger;
        
        // Initialize BlobServiceClient
        var connectionString = Environment.GetEnvironmentVariable("MainStorageConnection") 
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Storage connection string not found. Blob deletion will not work.");
            _blobServiceClient = null!;
        }
        else
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
        }
    }

    [Function("DeleteBlobFile")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", "options", Route = "DeleteBlobFile")] HttpRequestData req,
        FunctionContext executionContext)
    {
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var containerName = queryParams["containerName"] ?? "csv-files";
            var blobPath = queryParams["blobPath"];

            if (string.IsNullOrWhiteSpace(blobPath))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                    "blobPath query parameter is required");
            }

            if (_blobServiceClient == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                    "Blob storage connection not configured");
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            if (!await containerClient.ExistsAsync(executionContext.CancellationToken))
            {
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, 
                    $"Container '{containerName}' does not exist");
            }

            var blobClient = containerClient.GetBlobClient(blobPath);
            
            if (!await blobClient.ExistsAsync(executionContext.CancellationToken))
            {
                return await CreateErrorResponse(req, HttpStatusCode.NotFound, 
                    $"Blob '{blobPath}' does not exist");
            }

            await blobClient.DeleteAsync(cancellationToken: executionContext.CancellationToken);

            _logger.LogInformation("Successfully deleted blob: {BlobPath} from container {ContainerName}", 
                blobPath, containerName);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new 
            { 
                success = true, 
                message = $"Blob '{blobPath}' deleted successfully" 
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting blob file");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    private async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var errorResponse = req.CreateResponse(statusCode);
        errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
        CorsHelper.AddCorsHeaders(errorResponse);
        await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = message }));
        return errorResponse;
    }
}

















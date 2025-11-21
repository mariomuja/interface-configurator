using System.Net;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class GetBlobContainerFolders
{
    private readonly ILogger<GetBlobContainerFolders> _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public GetBlobContainerFolders(ILogger<GetBlobContainerFolders> logger)
    {
        _logger = logger;
        
        // Initialize BlobServiceClient
        var connectionString = Environment.GetEnvironmentVariable("MainStorageConnection") 
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Storage connection string not found. Blob container listing will not work.");
            _blobServiceClient = null!;
        }
        else
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
        }
    }

    [Function("GetBlobContainerFolders")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "GetBlobContainerFolders")] HttpRequestData req,
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
            var folderPrefix = queryParams["folderPrefix"] ?? "";

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

            // List all blobs with the specified prefix
            var folders = new Dictionary<string, List<BlobItemInfo>>();
            var foldersSet = new HashSet<string>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(
                prefix: folderPrefix,
                cancellationToken: executionContext.CancellationToken))
            {
                var blobPath = blobItem.Name;
                var pathParts = blobPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                
                if (pathParts.Length > 0)
                {
                    var folderPath = string.Join("/", pathParts.Take(pathParts.Length - 1));
                    var fileName = pathParts[pathParts.Length - 1];
                    
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        folderPath = "/"; // Root folder
                    }
                    else
                    {
                        folderPath = "/" + folderPath;
                    }

                    if (!folders.ContainsKey(folderPath))
                    {
                        folders[folderPath] = new List<BlobItemInfo>();
                        foldersSet.Add(folderPath);
                    }

                    folders[folderPath].Add(new BlobItemInfo
                    {
                        Name = fileName,
                        FullPath = blobPath,
                        Size = blobItem.Properties.ContentLength ?? 0,
                        LastModified = blobItem.Properties.LastModified?.DateTime ?? DateTime.MinValue,
                        ContentType = blobItem.Properties.ContentType ?? "application/octet-stream"
                    });
                }
            }

            // Build folder structure
            var folderStructure = new List<FolderInfo>();
            foreach (var folder in folders.OrderBy(f => f.Key))
            {
                folderStructure.Add(new FolderInfo
                {
                    Path = folder.Key,
                    Files = folder.Value.OrderBy(f => f.Name).ToList()
                });
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(folderStructure, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blob container folders");
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

    private class FolderInfo
    {
        public string Path { get; set; } = string.Empty;
        public List<BlobItemInfo> Files { get; set; } = new List<BlobItemInfo>();
    }

    private class BlobItemInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string ContentType { get; set; } = string.Empty;
    }
}

















using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Helpers;
using Azure.Storage.Blobs;

namespace InterfaceConfigurator.Main;

/// <summary>
/// Test endpoint to upload a test CSV file to csv-incoming folder
/// </summary>
public class UploadTestCsv
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<UploadTestCsv> _logger;

    public UploadTestCsv(
        BlobServiceClient blobServiceClient,
        ILogger<UploadTestCsv> logger)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UploadTestCsv")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "options", Route = "upload-test-csv")] HttpRequestData req,
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
            _logger.LogInformation("=== UPLOADING TEST CSV FILE ===");
            
            // Create test CSV content
            var csvContent = "Name║Age║City\nJohn Doe║30║New York\nJane Smith║25║Los Angeles\nBob Johnson║35║Chicago\nAlice Williams║28║Houston\nCharlie Brown║32║Phoenix";
            
            // Get container client
            var containerName = "csv-files";
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync(cancellationToken: context.CancellationToken);
            
            // Create blob path with timestamp
            var now = DateTime.UtcNow;
            var fileName = $"test-{now:yyyyMMddHHmmss}.csv";
            var blobPath = $"csv-incoming/{fileName}";
            
            // Upload CSV file
            var blobClient = containerClient.GetBlobClient(blobPath);
            var content = Encoding.UTF8.GetBytes(csvContent);
            
            await blobClient.UploadAsync(
                new BinaryData(content),
                new Azure.Storage.Blobs.Models.BlobUploadOptions
                {
                    HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
                    {
                        ContentType = "text/csv"
                    }
                },
                context.CancellationToken);

            _logger.LogInformation("Successfully uploaded test CSV file: {FileName}", fileName);

            var result = new
            {
                success = true,
                fileName = fileName,
                blobPath = blobPath,
                recordCount = 5,
                message = "Test CSV file uploaded successfully. Blob trigger should process it automatically."
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading test CSV file");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message, stackTrace = ex.StackTrace }));
            return errorResponse;
        }
    }
}


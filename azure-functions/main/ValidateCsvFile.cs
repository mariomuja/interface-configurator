using System.Net;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Services;

namespace ProcessCsvBlobTrigger;

/// <summary>
/// HTTP endpoint to validate CSV file before processing
/// </summary>
public class ValidateCsvFileFunction
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<ValidateCsvFileFunction> _logger;

    public ValidateCsvFileFunction(
        BlobServiceClient blobServiceClient,
        ILogger<ValidateCsvFileFunction> logger)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("ValidateCsvFile")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ValidateCsvFile")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            // Parse query parameters using System.Web.HttpUtility (like other endpoints)
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var blobPath = queryParams["blobPath"];
            var expectedDelimiter = queryParams["delimiter"];

            if (string.IsNullOrWhiteSpace(blobPath))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "blobPath parameter is required" }));
                return errorResponse;
            }

            // Download CSV file from blob storage
            var containerClient = _blobServiceClient.GetBlobContainerClient("csv-files");
            var blobClient = containerClient.GetBlobClient(blobPath);

            if (!await blobClient.ExistsAsync(context.CancellationToken))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = $"Blob not found: {blobPath}" }));
                return errorResponse;
            }

            var downloadResult = await blobClient.DownloadContentAsync(context.CancellationToken);
            var csvContent = downloadResult.Value.Content.ToString();

            // Validate CSV
            var validationService = new ProcessCsvBlobTrigger.Services.CsvValidationService(_logger);
            var validationResult = validationService.ValidateCsv(csvContent, expectedDelimiter);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                blobPath = blobPath,
                isValid = validationResult.IsValid,
                issues = validationResult.Issues,
                encoding = validationResult.Encoding,
                detectedDelimiter = validationResult.DetectedDelimiter,
                lineCount = validationResult.LineCount,
                columnCount = validationResult.ColumnCount,
                hasHeader = validationResult.HasHeader,
                hasBom = validationResult.HasBom
            }, new JsonSerializerOptions { WriteIndented = true }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating CSV file");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


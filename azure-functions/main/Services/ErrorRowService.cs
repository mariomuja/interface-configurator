using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for saving failed rows to error folder
/// </summary>
public class ErrorRowService : IErrorRowService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<ErrorRowService>? _logger;

    public ErrorRowService(BlobServiceClient blobServiceClient, ILogger<ErrorRowService>? logger = null)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger;
    }

    public async Task SaveFailedRowAsync(
        string originalBlobName,
        Dictionary<string, string> row,
        RowProcessingResult result,
        int rowNumber,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient("csv-files");
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            // Generate error file name: {original}_row{number}_error_{timestamp}.csv
            var originalFileName = Path.GetFileNameWithoutExtension(originalBlobName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var errorFileName = $"{originalFileName}_row{rowNumber}_error_{timestamp}.csv";

            // Create CSV content for single row
            var headers = row.Keys.ToList();
            var csvHeader = string.Join(",", headers.Select(h => EscapeCsvValue(h)));
            var csvRow = string.Join(",", headers.Select(h => EscapeCsvValue(row.GetValueOrDefault(h, string.Empty))));
            var csvContent = csvHeader + "\n" + csvRow;

            // Save CSV file
            var csvBlobClient = containerClient.GetBlobClient($"csv-error/{errorFileName}");
            await csvBlobClient.UploadAsync(
                new MemoryStream(Encoding.UTF8.GetBytes(csvContent)),
                overwrite: true,
                cancellationToken: cancellationToken);

            // Save error metadata as JSON
            var errorMetadata = new
            {
                originalFile = originalBlobName,
                rowNumber = rowNumber,
                error = result.ErrorMessage,
                errorTime = DateTime.UtcNow,
                rowData = row,
                exceptionType = result.Exception?.GetType().Name,
                exceptionMessage = result.Exception?.Message
            };

            var metadataFileName = errorFileName.Replace(".csv", ".error.json");
            var metadataBlobClient = containerClient.GetBlobClient($"csv-error/{metadataFileName}");
            var metadataJson = JsonSerializer.Serialize(errorMetadata, new JsonSerializerOptions { WriteIndented = true });
            await metadataBlobClient.UploadAsync(
                new MemoryStream(Encoding.UTF8.GetBytes(metadataJson)),
                overwrite: true,
                cancellationToken: cancellationToken);

            _logger?.LogInformation("Saved failed row {RowNumber} to error folder: {ErrorFileName}", rowNumber, errorFileName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save error row {RowNumber} to error folder", rowNumber);
            throw;
        }
    }

    private string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        // If value contains comma, quote, or newline, wrap in quotes and escape quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}







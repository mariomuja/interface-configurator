// TEMPORARILY DISABLED - Testing with minimal function
/*
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Processors;

namespace ProcessCsvBlobTrigger;

public class ProcessCsvBlobTriggerFunction
{
    private readonly ICsvProcessor _csvProcessor;
    private readonly ILogger<ProcessCsvBlobTriggerFunction> _logger;

    public ProcessCsvBlobTriggerFunction(
        ICsvProcessor csvProcessor,
        ILogger<ProcessCsvBlobTriggerFunction> logger)
    {
        _csvProcessor = csvProcessor;
        _logger = logger;
    }

    [Function("ProcessCsvBlobTrigger")]
    public async Task Run(
        [BlobTrigger("csv-uploads/{name}", Connection = "AzureWebJobsStorage")] byte[] blobContent,
        string name,
        FunctionContext context)
    {
        var blobSize = blobContent.Length;
        _logger.LogInformation("Blob trigger function processed blob: {BlobName} ({BlobSize} bytes)", name, blobSize);

        var result = await _csvProcessor.ProcessCsvAsync(blobContent, name);

        if (!result.Success)
        {
            _logger.LogError("CSV processing failed for {BlobName}: {ErrorMessage}", name, result.ErrorMessage);
            throw result.Exception ?? new Exception(result.ErrorMessage ?? "Unknown error occurred");
        }

        _logger.LogInformation("Successfully processed {RecordCount} records from {BlobName} in {ChunkCount} chunks",
            result.RecordsProcessed, name, result.ChunksProcessed);
    }
}
*/


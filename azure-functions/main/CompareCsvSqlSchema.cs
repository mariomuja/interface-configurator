using System.Net;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Adapters;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Services;

namespace ProcessCsvBlobTrigger;

/// <summary>
/// HTTP endpoint to compare CSV schema with SQL table schema
/// </summary>
public class CompareCsvSqlSchemaFunction
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IAdapterFactory _adapterFactory;
    private readonly ILogger<CompareCsvSqlSchemaFunction> _logger;

    public CompareCsvSqlSchemaFunction(
        BlobServiceClient blobServiceClient,
        IAdapterFactory adapterFactory,
        ILogger<CompareCsvSqlSchemaFunction> logger)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("CompareCsvSqlSchema")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "CompareCsvSqlSchema")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            // Parse query parameters
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query);
            query.TryGetValue("csvBlobPath", out var csvBlobPathValues);
            query.TryGetValue("tableName", out var tableNameValues);
            query.TryGetValue("interfaceName", out var interfaceNameValues);
            
            var csvBlobPath = csvBlobPathValues.FirstOrDefault();
            var sqlTableName = tableNameValues.FirstOrDefault() ?? "TransportData";
            var interfaceName = interfaceNameValues.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(csvBlobPath))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "csvBlobPath parameter is required" }));
                return errorResponse;
            }

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "interfaceName parameter is required" }));
                return errorResponse;
            }

            // Get CSV schema
            var csvAdapter = await _adapterFactory.CreateAdapterAsync("CSV", interfaceName, "Source", Guid.Empty, context.CancellationToken);
            if (csvAdapter == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "CSV adapter not found" }));
                return errorResponse;
            }

            var csvSchema = await csvAdapter.GetSchemaAsync(csvBlobPath, context.CancellationToken);

            // Get SQL schema
            var sqlAdapter = await _adapterFactory.CreateAdapterAsync("SqlServer", interfaceName, "Destination", Guid.Empty, context.CancellationToken);
            if (sqlAdapter == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "SQL Server adapter not found" }));
                return errorResponse;
            }

            var sqlSchema = await sqlAdapter.GetSchemaAsync(sqlTableName, context.CancellationToken);

            // Compare schemas
            var csvColumns = csvSchema.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var sqlColumns = sqlSchema.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingInSql = csvColumns.Except(sqlColumns, StringComparer.OrdinalIgnoreCase).ToList();
            var missingInCsv = sqlColumns.Except(csvColumns, StringComparer.OrdinalIgnoreCase).ToList();
            var commonColumns = csvColumns.Intersect(sqlColumns, StringComparer.OrdinalIgnoreCase).ToList();

            // Check for type mismatches
            var typeMismatches = new List<object>();
            foreach (var column in commonColumns)
            {
                var csvType = csvSchema[column];
                var sqlType = sqlSchema[column];
                
                if (csvType.DataType != sqlType.DataType)
                {
                    typeMismatches.Add(new
                    {
                        columnName = column,
                        csvType = csvType.DataType.ToString(),
                        sqlType = sqlType.DataType.ToString(),
                        csvSqlTypeDefinition = csvType.SqlTypeDefinition,
                        sqlSqlTypeDefinition = sqlType.SqlTypeDefinition
                    });
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                csvBlobPath = csvBlobPath,
                sqlTableName = sqlTableName,
                csvColumnCount = csvSchema.Count,
                sqlColumnCount = sqlSchema.Count,
                commonColumnCount = commonColumns.Count,
                missingInSql = missingInSql,
                missingInCsv = missingInCsv,
                typeMismatches = typeMismatches,
                isCompatible = missingInSql.Count == 0 && typeMismatches.Count == 0,
                csvColumns = csvSchema.Keys.ToList(),
                sqlColumns = sqlSchema.Keys.ToList()
            }, new JsonSerializerOptions { WriteIndented = true }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing CSV and SQL schemas");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


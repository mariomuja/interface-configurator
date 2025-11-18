using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Adapters;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Services;

namespace ProcessCsvBlobTrigger;

/// <summary>
/// HTTP endpoint to get SQL table schema
/// </summary>
public class GetSqlTableSchemaFunction
{
    private readonly IAdapterFactory _adapterFactory;
    private readonly ILogger<GetSqlTableSchemaFunction> _logger;

    public GetSqlTableSchemaFunction(
        IAdapterFactory adapterFactory,
        ILogger<GetSqlTableSchemaFunction> logger)
    {
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetSqlTableSchema")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetSqlTableSchema")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            // Parse query parameters
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query);
            query.TryGetValue("tableName", out var tableNameValues);
            query.TryGetValue("interfaceName", out var interfaceNameValues);
            
            var tableName = tableNameValues.FirstOrDefault() ?? "TransportData";
            var interfaceName = interfaceNameValues.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "interfaceName parameter is required" }));
                return errorResponse;
            }

            // Get SQL Server adapter
            var adapter = await _adapterFactory.CreateAdapterAsync("SqlServer", interfaceName, "Destination", Guid.Empty, context.CancellationToken);
            
            if (adapter == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "SQL Server adapter not found" }));
                return errorResponse;
            }

            // Get schema
            var schema = await adapter.GetSchemaAsync(tableName, context.CancellationToken);

            // Convert to response format
            var schemaResponse = schema.Select(kvp => new
            {
                columnName = kvp.Key,
                dataType = kvp.Value.DataType.ToString(),
                sqlTypeDefinition = kvp.Value.SqlTypeDefinition,
                precision = kvp.Value.Precision,
                scale = kvp.Value.Scale,
                isNullable = true // CSV columns are always nullable
            }).ToList();

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                tableName = tableName,
                columns = schemaResponse,
                columnCount = schemaResponse.Count
            }, new JsonSerializerOptions { WriteIndented = true }));
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SQL table schema");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


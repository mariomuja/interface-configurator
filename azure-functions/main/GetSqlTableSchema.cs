using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Services;

namespace ProcessCsvBlobTrigger;

/// <summary>
/// HTTP endpoint to get SQL table schema
/// </summary>
public class GetSqlTableSchemaFunction
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IAdapterFactory _adapterFactory;
    private readonly ILogger<GetSqlTableSchemaFunction> _logger;

    public GetSqlTableSchemaFunction(
        IInterfaceConfigurationService configService,
        IAdapterFactory adapterFactory,
        ILogger<GetSqlTableSchemaFunction> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
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
            // Parse query parameters using System.Web.HttpUtility (like other endpoints)
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var tableName = queryParams["tableName"] ?? "TransportData";
            var interfaceName = queryParams["interfaceName"];

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "interfaceName parameter is required" }));
                return errorResponse;
            }

            // Get interface configuration
            var config = await _configService.GetConfigurationAsync(interfaceName, context.CancellationToken);
            if (config == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.NotFound);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = $"Interface '{interfaceName}' not found" }));
                return errorResponse;
            }

            // Get SQL Server adapter
            var adapter = await _adapterFactory.CreateDestinationAdapterAsync(config, context.CancellationToken);
            
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


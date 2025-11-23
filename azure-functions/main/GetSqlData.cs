using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to get SQL data from TransportData table
/// </summary>
public class GetSqlDataFunction
{
    private readonly ILogger<GetSqlDataFunction> _logger;

    public GetSqlDataFunction(ILogger<GetSqlDataFunction> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetSqlData")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "sql-data")] HttpRequestData req,
        FunctionContext context)
    {
        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            // Parse query parameters manually (System.Web.HttpUtility is not available in .NET 8)
            var query = req.Url.Query;
            var queryParams = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(query) && query.StartsWith("?"))
            {
                var pairs = query.Substring(1).Split('&');
                foreach (var pair in pairs)
                {
                    var parts = pair.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        queryParams[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
                    }
                }
            }
            var tableName = queryParams.ContainsKey("tableName") ? queryParams["tableName"] : "TransportData";
            var limit = queryParams.ContainsKey("limit") && int.TryParse(queryParams["limit"], out var limitValue) ? limitValue : 100;

            // Get SQL connection string from environment variables
            var sqlServer = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER");
            var sqlDatabase = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE") ?? "app-database";
            var sqlUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
            var sqlPassword = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");

            if (string.IsNullOrEmpty(sqlServer) || string.IsNullOrEmpty(sqlUser) || string.IsNullOrEmpty(sqlPassword))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(errorResponse);
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "SQL connection configuration is missing" }));
                return errorResponse;
            }

            var connectionString = $"Server=tcp:{sqlServer},1433;Initial Catalog={sqlDatabase};Persist Security Info=False;User ID={sqlUser};Password={sqlPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

            var records = new List<Dictionary<string, object?>>();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(context.CancellationToken);

                // Get column names first
                var columnQuery = $"SELECT TOP 0 * FROM [{tableName}]";
                using (var columnCmd = new SqlCommand(columnQuery, connection))
                using (var columnReader = await columnCmd.ExecuteReaderAsync(context.CancellationToken))
                {
                    var columns = new List<string>();
                    for (int i = 0; i < columnReader.FieldCount; i++)
                    {
                        columns.Add(columnReader.GetName(i));
                    }

                    // Now get data
                    var dataQuery = $"SELECT TOP {limit} * FROM [{tableName}] ORDER BY (SELECT NULL)";
                    using (var dataCmd = new SqlCommand(dataQuery, connection))
                    using (var dataReader = await dataCmd.ExecuteReaderAsync(context.CancellationToken))
                    {
                        while (await dataReader.ReadAsync(context.CancellationToken))
                        {
                            var record = new Dictionary<string, object?>();
                            foreach (var column in columns)
                            {
                                var value = dataReader[column];
                                record[column] = value == DBNull.Value ? null : value;
                            }
                            records.Add(record);
                        }
                    }
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(records, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SQL data");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


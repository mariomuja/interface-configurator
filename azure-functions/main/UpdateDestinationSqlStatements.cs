using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateDestinationSqlStatements
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateDestinationSqlStatements> _logger;

    public UpdateDestinationSqlStatements(
        IInterfaceConfigurationService configService,
        ILogger<UpdateDestinationSqlStatements> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateDestinationSqlStatements")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "UpdateDestinationSqlStatements")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateDestinationSqlStatements function triggered");

        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<UpdateDestinationSqlStatementsRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName) || string.IsNullOrWhiteSpace(request.AdapterInstanceGuid))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName and AdapterInstanceGuid are required" }));
                return badRequestResponse;
            }

            await _configService.UpdateDestinationSqlStatementsAsync(
                request.InterfaceName,
                Guid.Parse(request.AdapterInstanceGuid),
                request.InsertStatement ?? string.Empty,
                request.UpdateStatement ?? string.Empty,
                request.DeleteStatement ?? string.Empty,
                executionContext.CancellationToken);

            _logger.LogInformation("SQL Statements for destination adapter '{AdapterInstanceGuid}' in interface '{InterfaceName}' updated", 
                request.AdapterInstanceGuid, request.InterfaceName);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"SQL Statements for destination adapter '{request.AdapterInstanceGuid}' in interface '{request.InterfaceName}' updated",
                interfaceName = request.InterfaceName,
                adapterInstanceGuid = request.AdapterInstanceGuid,
                insertStatement = request.InsertStatement,
                updateStatement = request.UpdateStatement,
                deleteStatement = request.DeleteStatement
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SQL Statements");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateDestinationSqlStatementsRequest
    {
        public string? InterfaceName { get; set; }
        public string? AdapterInstanceGuid { get; set; }
        public string? InsertStatement { get; set; }
        public string? UpdateStatement { get; set; }
        public string? DeleteStatement { get; set; }
    }
}



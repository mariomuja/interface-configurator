using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateSqlConnectionProperties
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<UpdateSqlConnectionProperties> _logger;

    public UpdateSqlConnectionProperties(
        IInterfaceConfigurationService configService,
        ILogger<UpdateSqlConnectionProperties> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateSqlConnectionProperties")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "options", Route = "UpdateSqlConnectionProperties")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateSqlConnectionProperties function triggered");

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
            var request = JsonSerializer.Deserialize<UpdateSqlConnectionPropertiesRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            await _configService.UpdateSqlConnectionPropertiesAsync(
                request.InterfaceName,
                request.ServerName,
                request.DatabaseName,
                request.UserName,
                request.Password,
                request.IntegratedSecurity,
                request.ResourceGroup,
                executionContext.CancellationToken);

            _logger.LogInformation("SQL connection properties for interface '{InterfaceName}' updated", request.InterfaceName);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"SQL connection properties for interface '{request.InterfaceName}' updated successfully",
                interfaceName = request.InterfaceName
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SQL connection properties");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateSqlConnectionPropertiesRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string? ServerName { get; set; }
        public string? DatabaseName { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public bool? IntegratedSecurity { get; set; }
        public string? ResourceGroup { get; set; }
    }
}





using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;

namespace ProcessCsvBlobTrigger;

public class CreateInterfaceConfiguration
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<CreateInterfaceConfiguration> _logger;

    public CreateInterfaceConfiguration(
        IInterfaceConfigurationService configService,
        ILogger<CreateInterfaceConfiguration> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("CreateInterfaceConfiguration")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("CreateInterfaceConfiguration function triggered");

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CreateInterfaceConfigRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            // Check if configuration already exists
            var existing = await _configService.GetConfigurationAsync(request.InterfaceName, executionContext.CancellationToken);
            if (existing != null)
            {
                _logger.LogInformation("Interface configuration '{InterfaceName}' already exists, returning existing configuration", request.InterfaceName);
                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonSerializer.Serialize(existing));
                return response;
            }

            // Create new configuration
            var config = new InterfaceConfiguration
            {
                InterfaceName = request.InterfaceName,
                SourceAdapterName = request.SourceAdapterName ?? "CSV",
                SourceConfiguration = request.SourceConfiguration ?? JsonSerializer.Serialize(new { source = "csv-files/csv-incoming", enabled = true }),
                DestinationAdapterName = request.DestinationAdapterName ?? "SqlServer",
                DestinationConfiguration = request.DestinationConfiguration ?? JsonSerializer.Serialize(new { destination = "TransportData", enabled = true }),
                IsEnabled = true,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow
            };

            await _configService.SaveConfigurationAsync(config, executionContext.CancellationToken);

            _logger.LogInformation("Created interface configuration: {InterfaceName}", request.InterfaceName);

            var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            successResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await successResponse.WriteStringAsync(JsonSerializer.Serialize(config));
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating interface configuration");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class CreateInterfaceConfigRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string? SourceAdapterName { get; set; }
        public string? SourceConfiguration { get; set; }
        public string? DestinationAdapterName { get; set; }
        public string? DestinationConfiguration { get; set; }
        public string? Description { get; set; }
    }
}


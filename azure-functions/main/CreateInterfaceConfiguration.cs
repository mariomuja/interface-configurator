using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class CreateInterfaceConfiguration
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IAdapterFactory _adapterFactory;
    private readonly ILogger<CreateInterfaceConfiguration> _logger;

    public CreateInterfaceConfiguration(
        IInterfaceConfigurationService configService,
        IAdapterFactory adapterFactory,
        ILogger<CreateInterfaceConfiguration> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("CreateInterfaceConfiguration")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "CreateInterfaceConfiguration")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("CreateInterfaceConfiguration function triggered");

        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(System.Net.HttpStatusCode.NoContent); // Use 204 No Content for OPTIONS
            // Set CORS headers FIRST before any other headers
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<CreateInterfaceConfigRequest>(requestBody);

            if (request == null)
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                CorsHelper.AddCorsHeaders(badRequestResponse);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "Request body is required" }));
                return badRequestResponse;
            }

            // Generate interface name automatically if not provided: {SourceAlias} → {DestinationAlias}
            string interfaceName = request.InterfaceName;
            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                var sourceAdapterName = request.SourceAdapterName ?? "CSV";
                var destinationAdapterName = request.DestinationAdapterName ?? "SqlServer";
                
                // Get adapter aliases by creating temporary adapters
                var tempConfig = new InterfaceConfiguration
                {
                    InterfaceName = "temp"
                };
                
                // Create temporary source instance
                var tempSourceInstance = new SourceAdapterInstance
                {
                    InstanceName = "temp",
                    AdapterName = sourceAdapterName,
                    IsEnabled = true,
                    AdapterInstanceGuid = Guid.NewGuid(),
                    Configuration = request.SourceConfiguration ?? JsonSerializer.Serialize(new { source = "" })
                };
                tempConfig.Sources["temp"] = tempSourceInstance;
                
                // Create temporary destination instance
                var tempDestInstance = new DestinationAdapterInstance
                {
                    InstanceName = "temp",
                    AdapterName = destinationAdapterName,
                    IsEnabled = true,
                    AdapterInstanceGuid = Guid.NewGuid(),
                    Configuration = request.DestinationConfiguration ?? JsonSerializer.Serialize(new { destination = "" })
                };
                tempConfig.Destinations["temp"] = tempDestInstance;
                
                try
                {
                    var sourceAdapter = await _adapterFactory.CreateSourceAdapterAsync(tempConfig, executionContext.CancellationToken);
                    var destAdapter = await _adapterFactory.CreateDestinationAdapterAsync(tempConfig, executionContext.CancellationToken);
                    interfaceName = $"{sourceAdapter.AdapterAlias} → {destAdapter.AdapterAlias}";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not generate interface name from adapter aliases, using default");
                    interfaceName = $"{sourceAdapterName} → {destinationAdapterName}";
                }
            }

            // Check if configuration already exists
            var existing = await _configService.GetConfigurationAsync(interfaceName, executionContext.CancellationToken);
            if (existing != null)
            {
                _logger.LogInformation("Interface configuration '{InterfaceName}' already exists, returning existing configuration", request.InterfaceName);
                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                CorsHelper.AddCorsHeaders(response);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonSerializer.Serialize(existing));
                return response;
            }

            // Create new configuration with new structure
            var config = new InterfaceConfiguration
            {
                InterfaceName = interfaceName,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Create source instance
            var sourceInstanceName = request.SourceInstanceName ?? "Source";
            var sourceInstance = new SourceAdapterInstance
            {
                InstanceName = sourceInstanceName,
                AdapterName = request.SourceAdapterName ?? "CSV",
                IsEnabled = request.SourceIsEnabled ?? true,
                AdapterInstanceGuid = Guid.NewGuid(),
                Configuration = request.SourceConfiguration ?? JsonSerializer.Serialize(new { source = "csv-files/csv-incoming" }),
                SourceFieldSeparator = "║",
                CsvPollingInterval = 10,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            config.Sources[sourceInstanceName] = sourceInstance;

            // Create destination instance
            var destInstanceName = request.DestinationInstanceName ?? "Destination";
            var destInstance = new DestinationAdapterInstance
            {
                InstanceName = destInstanceName,
                AdapterName = request.DestinationAdapterName ?? "SqlServer",
                IsEnabled = request.DestinationIsEnabled ?? true,
                AdapterInstanceGuid = Guid.NewGuid(),
                Configuration = request.DestinationConfiguration ?? JsonSerializer.Serialize(new { destination = "TransportData" }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            config.Destinations[destInstanceName] = destInstance;

            await _configService.SaveConfigurationAsync(config, executionContext.CancellationToken);

            _logger.LogInformation("Created interface configuration: {InterfaceName}", request.InterfaceName);

            var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(successResponse);
            successResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await successResponse.WriteStringAsync(JsonSerializer.Serialize(config));
            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating interface configuration");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            CorsHelper.AddCorsHeaders(errorResponse);
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
        public bool? SourceIsEnabled { get; set; }
        public bool? DestinationIsEnabled { get; set; }
        public string? SourceInstanceName { get; set; }
        public string? DestinationInstanceName { get; set; }
        public string? Description { get; set; }
    }
}


using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class UpdateCsvData
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly IAdapterFactory _adapterFactory;
    private readonly ILogger<UpdateCsvData> _logger;

    public UpdateCsvData(
        IInterfaceConfigurationService configService,
        IAdapterFactory adapterFactory,
        ILogger<UpdateCsvData> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("UpdateCsvData")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", "options", Route = "UpdateCsvData")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("UpdateCsvData function triggered");

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
            var request = JsonSerializer.Deserialize<UpdateCsvDataRequest>(requestBody);

            if (request == null || string.IsNullOrWhiteSpace(request.InterfaceName))
            {
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                badRequestResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
                CorsHelper.AddCorsHeaders(badRequestResponse);
                await badRequestResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = "InterfaceName is required" }));
                return badRequestResponse;
            }

            // Update CsvData in configuration
            await _configService.UpdateCsvDataAsync(
                request.InterfaceName,
                request.CsvData,
                executionContext.CancellationToken);

            // If CsvData is provided, process it by creating adapter instance and triggering debatching
            if (!string.IsNullOrWhiteSpace(request.CsvData))
            {
                try
                {
                    var config = await _configService.GetConfigurationAsync(request.InterfaceName, executionContext.CancellationToken);
                    if (config != null && config.SourceAdapterName.Equals("CSV", StringComparison.OrdinalIgnoreCase))
                    {
                        // Create adapter instance to process CsvData
                        var adapter = await _adapterFactory.CreateSourceAdapterAsync(config, executionContext.CancellationToken);

                        if (adapter is Adapters.CsvAdapter csvAdapter)
                        {
                            // Set CsvData property which will trigger processing
                            csvAdapter.CsvData = request.CsvData;
                            _logger.LogInformation("CsvData set on adapter instance, processing triggered");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not process CsvData immediately, but it has been saved to configuration");
                    // Don't fail the request - CsvData is saved and can be processed later
                }
            }

            _logger.LogInformation("CsvData for interface '{InterfaceName}' updated. DataLength={DataLength}", 
                request.InterfaceName, request.CsvData?.Length ?? 0);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(response);
            await response.WriteStringAsync(JsonSerializer.Serialize(new { 
                message = $"CsvData for interface '{request.InterfaceName}' updated successfully",
                interfaceName = request.InterfaceName,
                dataLength = request.CsvData?.Length ?? 0
            }));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating CsvData");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            CorsHelper.AddCorsHeaders(errorResponse);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }

    private class UpdateCsvDataRequest
    {
        public string InterfaceName { get; set; } = string.Empty;
        public string? CsvData { get; set; }
    }
}


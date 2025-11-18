using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to get processing statistics
/// </summary>
public class GetProcessingStatisticsFunction
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<GetProcessingStatisticsFunction> _logger;

    public GetProcessingStatisticsFunction(
        MessageBoxDbContext context,
        ILogger<GetProcessingStatisticsFunction> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetProcessingStatistics")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GetProcessingStatistics")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            // Parse query parameters using System.Web.HttpUtility (like other endpoints)
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var interfaceName = queryParams["interfaceName"];
            var startDateStr = queryParams["startDate"];
            var endDateStr = queryParams["endDate"];

            DateTime? startDate = null;
            DateTime? endDate = null;

            if (!string.IsNullOrEmpty(startDateStr) && DateTime.TryParse(startDateStr, out var sd))
            {
                startDate = sd;
            }

            if (!string.IsNullOrEmpty(endDateStr) && DateTime.TryParse(endDateStr, out var ed))
            {
                endDate = ed;
            }

            var statisticsService = new InterfaceConfigurator.Main.Services.ProcessingStatisticsService(_context, _logger);

            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                // Return recent statistics for all interfaces
                var recentStats = await statisticsService.GetRecentStatisticsAsync(limit: 100, cancellationToken: context.CancellationToken);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonSerializer.Serialize(recentStats, new JsonSerializerOptions { WriteIndented = true }));
                return response;
            }
            else
            {
                // Return summary statistics for specific interface
                var summary = await statisticsService.GetStatisticsAsync(interfaceName, startDate, endDate, context.CancellationToken);
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
                return response;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processing statistics");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new { error = ex.Message }));
            return errorResponse;
        }
    }
}


using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Data;

namespace ProcessCsvBlobTrigger;

/// <summary>
/// Health check endpoint for monitoring Function App and dependencies
/// Returns 200 OK if healthy, 503 Service Unavailable if unhealthy
/// </summary>
public class HealthCheck
{
    private readonly ApplicationDbContext? _applicationContext;
    private readonly MessageBoxDbContext? _messageBoxContext;
    private readonly ILogger<HealthCheck> _logger;

    public HealthCheck(
        ApplicationDbContext? applicationContext,
        MessageBoxDbContext? messageBoxContext,
        ILogger<HealthCheck> logger)
    {
        _applicationContext = applicationContext;
        _messageBoxContext = messageBoxContext;
        _logger = logger;
    }

    [Function("HealthCheck")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Health check requested");

        var healthStatus = new HealthStatusResult
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Checks = new List<HealthCheckItem>()
        };

        var overallHealthy = true;

        // Check Application Database
        if (_applicationContext != null)
        {
            var dbCheck = await CheckDatabaseAsync(_applicationContext, "ApplicationDatabase");
            healthStatus.Checks.Add(dbCheck);
            if (dbCheck.Status != "Healthy")
            {
                overallHealthy = false;
            }
        }
        else
        {
            healthStatus.Checks.Add(new HealthCheckItem
            {
                Name = "ApplicationDatabase",
                Status = "Unhealthy",
                Message = "ApplicationDbContext not configured"
            });
            overallHealthy = false;
        }

        // Check MessageBox Database
        if (_messageBoxContext != null)
        {
            var messageBoxCheck = await CheckDatabaseAsync(_messageBoxContext, "MessageBoxDatabase");
            healthStatus.Checks.Add(messageBoxCheck);
            if (messageBoxCheck.Status != "Healthy")
            {
                overallHealthy = false;
            }
        }
        else
        {
            healthStatus.Checks.Add(new HealthCheckItem
            {
                Name = "MessageBoxDatabase",
                Status = "Unhealthy",
                Message = "MessageBoxDbContext not configured"
            });
            overallHealthy = false;
        }

        // Check Storage Account (if configured)
        var storageConnectionString = Environment.GetEnvironmentVariable("MainStorageConnection") 
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (!string.IsNullOrEmpty(storageConnectionString))
        {
            healthStatus.Checks.Add(new HealthCheckItem
            {
                Name = "StorageAccount",
                Status = "Healthy",
                Message = "Storage connection string configured"
            });
        }
        else
        {
            healthStatus.Checks.Add(new HealthCheckItem
            {
                Name = "StorageAccount",
                Status = "Degraded",
                Message = "Storage connection string not configured"
            });
        }

        // Update overall status
        healthStatus.Status = overallHealthy ? "Healthy" : "Unhealthy";

        var response = req.CreateResponse(overallHealthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");

        var jsonResponse = JsonSerializer.Serialize(healthStatus, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await response.WriteStringAsync(jsonResponse);

        return response;
    }

    private async Task<HealthCheckItem> CheckDatabaseAsync(DbContext context, string databaseName)
    {
        try
        {
            var canConnect = await context.Database.CanConnectAsync();
            
            if (canConnect)
            {
                // Test query with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await context.Database.ExecuteSqlRawAsync("SELECT 1", cts.Token);
                
                return new HealthCheckItem
                {
                    Name = databaseName,
                    Status = "Healthy",
                    Message = "Database connection successful"
                };
            }
            
            return new HealthCheckItem
            {
                Name = databaseName,
                Status = "Unhealthy",
                Message = "Cannot connect to database"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for {DatabaseName}", databaseName);
            return new HealthCheckItem
            {
                Name = databaseName,
                Status = "Unhealthy",
                Message = $"Health check failed: {ex.Message}"
            };
        }
    }

    private class HealthStatusResult
    {
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public List<HealthCheckItem> Checks { get; set; } = new();
    }

    private class HealthCheckItem
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}


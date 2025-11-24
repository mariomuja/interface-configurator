using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Helpers;
using Azure.Messaging.ServiceBus.Administration;

namespace InterfaceConfigurator.Main;

/// <summary>
/// Health check endpoint for monitoring Function App and dependencies
/// Returns 200 OK if healthy, 503 Service Unavailable if unhealthy
/// </summary>
public class HealthCheck
{
    private readonly ApplicationDbContext? _applicationContext;
    private readonly InterfaceConfigDbContext? _interfaceConfigContext;
    private readonly ILogger<HealthCheck> _logger;
    private readonly MetricsService? _metricsService;

    public HealthCheck(
        ApplicationDbContext? applicationContext,
        InterfaceConfigDbContext? interfaceConfigContext,
        ILogger<HealthCheck> logger,
        MetricsService? metricsService = null)
    {
        _applicationContext = applicationContext;
        _interfaceConfigContext = interfaceConfigContext;
        _logger = logger;
        _metricsService = metricsService;
    }

    [Function("HealthCheck")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "health")] HttpRequestData req,
        FunctionContext context)
    {
        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

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
            _metricsService?.TrackHealthCheck("ApplicationDatabase", dbCheck.Status == "Healthy", dbCheck.Message);
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

        // Check InterfaceConfigDb Database (formerly MessageBox)
        if (_interfaceConfigContext != null)
        {
            var interfaceConfigCheck = await CheckDatabaseAsync(_interfaceConfigContext, "InterfaceConfigDatabase");
            healthStatus.Checks.Add(interfaceConfigCheck);
            _metricsService?.TrackHealthCheck("InterfaceConfigDatabase", interfaceConfigCheck.Status == "Healthy", interfaceConfigCheck.Message);
            if (interfaceConfigCheck.Status != "Healthy")
            {
                overallHealthy = false;
            }
        }
        else
        {
            healthStatus.Checks.Add(new HealthCheckItem
            {
                Name = "InterfaceConfigDatabase",
                Status = "Unhealthy",
                Message = "InterfaceConfigDbContext not configured"
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

        // Check Service Bus (if configured)
        var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString")
            ?? Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(serviceBusConnectionString))
        {
            try
            {
                var serviceBusCheck = await CheckServiceBusAsync(serviceBusConnectionString);
                healthStatus.Checks.Add(serviceBusCheck);
                if (serviceBusCheck.Status != "Healthy")
                {
                    overallHealthy = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Service Bus health");
                healthStatus.Checks.Add(new HealthCheckItem
                {
                    Name = "ServiceBus",
                    Status = "Unhealthy",
                    Message = $"Service Bus check failed: {ex.Message}"
                });
                overallHealthy = false;
            }
        }
        else
        {
            healthStatus.Checks.Add(new HealthCheckItem
            {
                Name = "ServiceBus",
                Status = "Degraded",
                Message = "Service Bus connection string not configured"
            });
        }

        // Check Container Apps (sample check - checks if Container App Service is available)
        try
        {
            var containerAppCheck = await CheckContainerAppsAsync();
            healthStatus.Checks.Add(containerAppCheck);
            if (containerAppCheck.Status == "Unhealthy")
            {
                overallHealthy = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Container Apps health");
            healthStatus.Checks.Add(new HealthCheckItem
            {
                Name = "ContainerApps",
                Status = "Degraded",
                Message = $"Container Apps check failed: {ex.Message}"
            });
        }

        // Update overall status
        healthStatus.Status = overallHealthy ? "Healthy" : "Unhealthy";

        var response = req.CreateResponse(overallHealthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        CorsHelper.AddCorsHeaders(response);

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

    private async Task<HealthCheckItem> CheckServiceBusAsync(string connectionString)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var adminClient = new Azure.Messaging.ServiceBus.Administration.ServiceBusAdministrationClient(connectionString);
            
            // Try to get namespace properties as a connectivity test
            var namespaceProperties = await adminClient.GetNamespacePropertiesAsync(cts.Token);
            
            return new HealthCheckItem
            {
                Name = "ServiceBus",
                Status = "Healthy",
                Message = $"Service Bus namespace '{namespaceProperties.Value.Name}' is accessible"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckItem
            {
                Name = "ServiceBus",
                Status = "Unhealthy",
                Message = $"Service Bus connectivity check failed: {ex.Message}"
            };
        }
    }

    private async Task<HealthCheckItem> CheckContainerAppsAsync()
    {
        try
        {
            // Check if Container App Service can be initialized
            // This is a lightweight check - actual container app status is checked via GetContainerAppStatus endpoint
            var resourceGroupName = Environment.GetEnvironmentVariable("ResourceGroupName") ?? "rg-interface-configurator";
            var containerAppEnvironmentName = Environment.GetEnvironmentVariable("ContainerAppEnvironmentName") ?? "cae-adapter-instances";
            
            // Check if Azure credentials are available
            var hasCredentials = Azure.Identity.DefaultAzureCredential.TryGetDefaultAzureCredential(out _);
            
            if (hasCredentials)
            {
                return new HealthCheckItem
                {
                    Name = "ContainerApps",
                    Status = "Healthy",
                    Message = $"Container App Service is available. Environment: {containerAppEnvironmentName}"
                };
            }
            else
            {
                return new HealthCheckItem
                {
                    Name = "ContainerApps",
                    Status = "Degraded",
                    Message = "Azure credentials not available for Container App management"
                };
            }
        }
        catch (Exception ex)
        {
            return new HealthCheckItem
            {
                Name = "ContainerApps",
                Status = "Degraded",
                Message = $"Container Apps check failed: {ex.Message}"
            };
        }
    }

    private class HealthCheckItem
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}


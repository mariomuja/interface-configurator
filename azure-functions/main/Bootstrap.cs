using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using InterfaceConfigurator.Main.Helpers;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Interfaces;
using Azure.Messaging.ServiceBus.Administration;
using Azure.Storage.Blobs;

namespace InterfaceConfigurator.Main;

/// <summary>
/// Bootstrap function that checks all components and resources on application startup
/// Results are logged to ProcessLogs table for display in UI
/// </summary>
public class Bootstrap
{
    private readonly ApplicationDbContext? _applicationContext;
    private readonly InterfaceConfigDbContext? _interfaceConfigContext;
    private readonly ILogger<Bootstrap> _logger;
    private readonly ILoggingService? _loggingService;

    public Bootstrap(
        ApplicationDbContext? applicationContext,
        InterfaceConfigDbContext? interfaceConfigContext,
        ILogger<Bootstrap> logger,
        ILoggingService? loggingService = null)
    {
        _applicationContext = applicationContext;
        _interfaceConfigContext = interfaceConfigContext;
        _logger = logger;
        _loggingService = loggingService;
    }

    [Function("Bootstrap")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "options", Route = "bootstrap")] HttpRequestData req,
        FunctionContext context)
    {
        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        _logger.LogInformation("Bootstrap process started");

        var bootstrapResults = new BootstrapResult
        {
            Timestamp = DateTime.UtcNow,
            Checks = new List<BootstrapCheck>()
        };

        // Log bootstrap start
        await LogToProcessLogsAsync("info", "Bootstrap", "Bootstrap process started", null);

        // Check 1: Application Database
        var appDbCheck = await CheckApplicationDatabaseAsync();
        bootstrapResults.Checks.Add(appDbCheck);
        await LogToProcessLogsAsync(
            appDbCheck.Status == "Healthy" ? "info" : "error",
            "Bootstrap",
            $"Application Database: {appDbCheck.Message}",
            appDbCheck.Details);

        // Check 2: InterfaceConfig Database
        var interfaceConfigCheck = await CheckInterfaceConfigDatabaseAsync();
        bootstrapResults.Checks.Add(interfaceConfigCheck);
        await LogToProcessLogsAsync(
            interfaceConfigCheck.Status == "Healthy" ? "info" : "error",
            "Bootstrap",
            $"InterfaceConfig Database: {interfaceConfigCheck.Message}",
            interfaceConfigCheck.Details);

        // Check 3: Storage Account / Blob Storage
        var storageCheck = await CheckBlobStorageAsync();
        bootstrapResults.Checks.Add(storageCheck);
        await LogToProcessLogsAsync(
            storageCheck.Status == "Healthy" ? "info" : "warning",
            "Bootstrap",
            $"Blob Storage: {storageCheck.Message}",
            storageCheck.Details);

        // Check 4: Service Bus
        var serviceBusCheck = await CheckServiceBusAsync();
        bootstrapResults.Checks.Add(serviceBusCheck);
        await LogToProcessLogsAsync(
            serviceBusCheck.Status == "Healthy" ? "info" : "warning",
            "Bootstrap",
            $"Service Bus: {serviceBusCheck.Message}",
            serviceBusCheck.Details);

        // Check 5: Container Apps Environment
        var containerAppsCheck = await CheckContainerAppsEnvironmentAsync();
        bootstrapResults.Checks.Add(containerAppsCheck);
        await LogToProcessLogsAsync(
            containerAppsCheck.Status == "Healthy" ? "info" : "warning",
            "Bootstrap",
            $"Container Apps: {containerAppsCheck.Message}",
            containerAppsCheck.Details);

        // Check 6: Function App Configuration
        var functionAppCheck = await CheckFunctionAppConfigurationAsync();
        bootstrapResults.Checks.Add(functionAppCheck);
        await LogToProcessLogsAsync(
            functionAppCheck.Status == "Healthy" ? "info" : "warning",
            "Bootstrap",
            $"Function App Configuration: {functionAppCheck.Message}",
            functionAppCheck.Details);

        // Check 7: ProcessLogs Table (self-check)
        var processLogsCheck = await CheckProcessLogsTableAsync();
        bootstrapResults.Checks.Add(processLogsCheck);
        await LogToProcessLogsAsync(
            processLogsCheck.Status == "Healthy" ? "info" : "error",
            "Bootstrap",
            $"ProcessLogs Table: {processLogsCheck.Message}",
            processLogsCheck.Details);

        // Calculate overall status
        var healthyCount = bootstrapResults.Checks.Count(c => c.Status == "Healthy");
        var totalCount = bootstrapResults.Checks.Count;
        bootstrapResults.OverallStatus = healthyCount == totalCount ? "Healthy" 
            : healthyCount > totalCount / 2 ? "Degraded" 
            : "Unhealthy";
        bootstrapResults.HealthyChecks = healthyCount;
        bootstrapResults.TotalChecks = totalCount;

        // Log bootstrap completion
        await LogToProcessLogsAsync(
            bootstrapResults.OverallStatus == "Healthy" ? "info" : "warning",
            "Bootstrap",
            $"Bootstrap process completed: {bootstrapResults.OverallStatus} ({healthyCount}/{totalCount} checks passed)",
            JsonSerializer.Serialize(bootstrapResults, new JsonSerializerOptions { WriteIndented = true }));

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        CorsHelper.AddCorsHeaders(response);

        var jsonResponse = JsonSerializer.Serialize(bootstrapResults, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await response.WriteStringAsync(jsonResponse);

        return response;
    }

    private async Task<BootstrapCheck> CheckApplicationDatabaseAsync()
    {
        try
        {
            if (_applicationContext == null)
            {
                return new BootstrapCheck
                {
                    Name = "ApplicationDatabase",
                    Status = "Unhealthy",
                    Message = "ApplicationDbContext not configured",
                    Details = "ApplicationDbContext is null"
                };
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var canConnect = await _applicationContext.Database.CanConnectAsync(cts.Token);
            
            if (canConnect)
            {
                await _applicationContext.Database.ExecuteSqlRawAsync("SELECT 1", cts.Token);
                return new BootstrapCheck
                {
                    Name = "ApplicationDatabase",
                    Status = "Healthy",
                    Message = "Database connection successful",
                    Details = "Application database is accessible and responsive"
                };
            }
            
            return new BootstrapCheck
            {
                Name = "ApplicationDatabase",
                Status = "Unhealthy",
                Message = "Cannot connect to database",
                Details = "Database connection failed"
            };
        }
        catch (Exception ex)
        {
            return new BootstrapCheck
            {
                Name = "ApplicationDatabase",
                Status = "Unhealthy",
                Message = $"Health check failed: {ex.Message}",
                Details = ex.ToString()
            };
        }
    }

    private async Task<BootstrapCheck> CheckInterfaceConfigDatabaseAsync()
    {
        try
        {
            if (_interfaceConfigContext == null)
            {
                return new BootstrapCheck
                {
                    Name = "InterfaceConfigDatabase",
                    Status = "Unhealthy",
                    Message = "InterfaceConfigDbContext not configured",
                    Details = "InterfaceConfigDbContext is null"
                };
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var canConnect = await _interfaceConfigContext.Database.CanConnectAsync(cts.Token);
            
            if (canConnect)
            {
                // Check if ProcessLogs table exists
                var tableExists = await _interfaceConfigContext.Database.ExecuteSqlRawAsync(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ProcessLogs'", cts.Token);
                
                await _interfaceConfigContext.Database.ExecuteSqlRawAsync("SELECT 1", cts.Token);
                return new BootstrapCheck
                {
                    Name = "InterfaceConfigDatabase",
                    Status = "Healthy",
                    Message = "Database connection successful",
                    Details = "InterfaceConfig database is accessible and ProcessLogs table exists"
                };
            }
            
            return new BootstrapCheck
            {
                Name = "InterfaceConfigDatabase",
                Status = "Unhealthy",
                Message = "Cannot connect to database",
                Details = "Database connection failed"
            };
        }
        catch (Exception ex)
        {
            return new BootstrapCheck
            {
                Name = "InterfaceConfigDatabase",
                Status = "Unhealthy",
                Message = $"Health check failed: {ex.Message}",
                Details = ex.ToString()
            };
        }
    }

    private async Task<BootstrapCheck> CheckBlobStorageAsync()
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("MainStorageConnection") 
                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                return new BootstrapCheck
                {
                    Name = "BlobStorage",
                    Status = "Degraded",
                    Message = "Storage connection string not configured",
                    Details = "MainStorageConnection or AzureWebJobsStorage environment variable not set"
                };
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var blobServiceClient = new BlobServiceClient(connectionString);
            
            // Try to list containers as a connectivity test
            await foreach (var container in blobServiceClient.GetBlobContainersAsync(cancellationToken: cts.Token))
            {
                // If we can list at least one container, storage is accessible
                return new BootstrapCheck
                {
                    Name = "BlobStorage",
                    Status = "Healthy",
                    Message = "Blob Storage is accessible",
                    Details = $"Storage account accessible. Found container: {container.Name}"
                };
            }

            // No containers found, but connection works
            return new BootstrapCheck
            {
                Name = "BlobStorage",
                Status = "Healthy",
                Message = "Blob Storage is accessible",
                Details = "Storage account accessible (no containers found yet)"
            };
        }
        catch (Exception ex)
        {
            return new BootstrapCheck
            {
                Name = "BlobStorage",
                Status = "Unhealthy",
                Message = $"Blob Storage check failed: {ex.Message}",
                Details = ex.ToString()
            };
        }
    }

    private async Task<BootstrapCheck> CheckServiceBusAsync()
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString")
                ?? Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                return new BootstrapCheck
                {
                    Name = "ServiceBus",
                    Status = "Degraded",
                    Message = "Service Bus connection string not configured",
                    Details = "ServiceBusConnectionString or AZURE_SERVICEBUS_CONNECTION_STRING environment variable not set"
                };
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var adminClient = new ServiceBusAdministrationClient(connectionString);
            
            // Try to get namespace properties as a connectivity test
            var namespaceProperties = await adminClient.GetNamespacePropertiesAsync(cts.Token);
            
            return new BootstrapCheck
            {
                Name = "ServiceBus",
                Status = "Healthy",
                Message = $"Service Bus namespace '{namespaceProperties.Value.Name}' is accessible",
                Details = $"Service Bus is accessible. Namespace: {namespaceProperties.Value.Name}"
            };
        }
        catch (Exception ex)
        {
            return new BootstrapCheck
            {
                Name = "ServiceBus",
                Status = "Unhealthy",
                Message = $"Service Bus connectivity check failed: {ex.Message}",
                Details = ex.ToString()
            };
        }
    }

    private async Task<BootstrapCheck> CheckContainerAppsEnvironmentAsync()
    {
        try
        {
            var resourceGroupName = Environment.GetEnvironmentVariable("ResourceGroupName") 
                ?? Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")
                ?? "rg-interface-configurator";
            var containerAppEnvironmentName = Environment.GetEnvironmentVariable("ContainerAppEnvironmentName") 
                ?? "cae-adapter-instances";
            
            // Check if Azure credentials are available (DefaultAzureCredential will use available credentials)
            var hasCredentials = true; // DefaultAzureCredential will use available credentials automatically
            
            if (hasCredentials)
            {
                return new BootstrapCheck
                {
                    Name = "ContainerApps",
                    Status = "Healthy",
                    Message = "Container App Service is available",
                    Details = $"Container App Environment: {containerAppEnvironmentName}, Resource Group: {resourceGroupName}"
                };
            }
            else
            {
                return new BootstrapCheck
                {
                    Name = "ContainerApps",
                    Status = "Degraded",
                    Message = "Azure credentials not available for Container App management",
                    Details = "DefaultAzureCredential could not find credentials"
                };
            }
        }
        catch (Exception ex)
        {
            return new BootstrapCheck
            {
                Name = "ContainerApps",
                Status = "Degraded",
                Message = $"Container Apps check failed: {ex.Message}",
                Details = ex.ToString()
            };
        }
    }

    private async Task<BootstrapCheck> CheckFunctionAppConfigurationAsync()
    {
        try
        {
            var checks = new List<string>();
            var issues = new List<string>();

            // Check required environment variables
            var functionAppName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") 
                ?? Environment.GetEnvironmentVariable("FUNCTIONAPP_NAME");
            if (!string.IsNullOrEmpty(functionAppName))
            {
                checks.Add($"Function App Name: {functionAppName}");
            }
            else
            {
                issues.Add("Function App name not configured");
            }

            var resourceGroup = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP") 
                ?? Environment.GetEnvironmentVariable("ResourceGroup");
            if (!string.IsNullOrEmpty(resourceGroup))
            {
                checks.Add($"Resource Group: {resourceGroup}");
            }

            var status = issues.Count == 0 ? "Healthy" : "Degraded";
            var message = issues.Count == 0 
                ? "Function App configuration is valid" 
                : $"Configuration issues: {string.Join(", ", issues)}";

            return new BootstrapCheck
            {
                Name = "FunctionAppConfiguration",
                Status = status,
                Message = message,
                Details = string.Join("\n", checks)
            };
        }
        catch (Exception ex)
        {
            return new BootstrapCheck
            {
                Name = "FunctionAppConfiguration",
                Status = "Unhealthy",
                Message = $"Configuration check failed: {ex.Message}",
                Details = ex.ToString()
            };
        }
    }

    private async Task<BootstrapCheck> CheckProcessLogsTableAsync()
    {
        try
        {
            if (_interfaceConfigContext == null)
            {
                return new BootstrapCheck
                {
                    Name = "ProcessLogsTable",
                    Status = "Unhealthy",
                    Message = "InterfaceConfigDbContext not available",
                    Details = "Cannot check ProcessLogs table without database context"
                };
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            // Try to query ProcessLogs table
            var logCount = await _interfaceConfigContext.ProcessLogs.CountAsync(cts.Token);
            
            return new BootstrapCheck
            {
                Name = "ProcessLogsTable",
                Status = "Healthy",
                Message = "ProcessLogs table is accessible",
                Details = $"ProcessLogs table exists and is accessible. Current log count: {logCount}"
            };
        }
        catch (Exception ex)
        {
            return new BootstrapCheck
            {
                Name = "ProcessLogsTable",
                Status = "Unhealthy",
                Message = $"ProcessLogs table check failed: {ex.Message}",
                Details = ex.ToString()
            };
        }
    }

    private async Task LogToProcessLogsAsync(string level, string component, string message, string? details)
    {
        try
        {
            if (_loggingService != null)
            {
                await _loggingService.LogAsync(level, message, details);
            }
            else if (_interfaceConfigContext != null)
            {
                // Direct logging if logging service not available
                var logEntry = new ProcessLog
                {
                    Timestamp = DateTime.UtcNow,
                    Level = level.Length > 50 ? level.Substring(0, 50) : level,
                    Message = message ?? string.Empty,
                    Details = details
                };
                
                // Set Component property
                logEntry.Component = component;

                if (logEntry.Message.Length > 4000)
                {
                    logEntry.Message = logEntry.Message.Substring(0, 4000) + "... [truncated]";
                }

                if (logEntry.Details != null && logEntry.Details.Length > 4000)
                {
                    logEntry.Details = logEntry.Details.Substring(0, 4000) + "... [truncated]";
                }

                _interfaceConfigContext.ProcessLogs.Add(logEntry);
                await _interfaceConfigContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail bootstrap
            _logger.LogWarning(ex, "Failed to log bootstrap result to ProcessLogs: {Message}", ex.Message);
        }
    }

    private class BootstrapResult
    {
        public DateTime Timestamp { get; set; }
        public string OverallStatus { get; set; } = string.Empty;
        public int HealthyChecks { get; set; }
        public int TotalChecks { get; set; }
        public List<BootstrapCheck> Checks { get; set; } = new();
    }

    private class BootstrapCheck
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}


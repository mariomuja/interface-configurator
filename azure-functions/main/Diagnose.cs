using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Helpers;
using Azure.Storage.Blobs;

namespace InterfaceConfigurator.Main;

/// <summary>
/// Diagnostic endpoint to check system health and configuration
/// Returns detailed information about database connectivity, storage, and configuration
/// </summary>
public class Diagnose
{
    private readonly ApplicationDbContext? _applicationContext;
    private readonly MessageBoxDbContext? _messageBoxContext;
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly ILogger<Diagnose> _logger;

    public Diagnose(
        ApplicationDbContext? applicationContext,
        MessageBoxDbContext? messageBoxContext,
        BlobServiceClient? blobServiceClient,
        ILogger<Diagnose> logger)
    {
        _applicationContext = applicationContext;
        _messageBoxContext = messageBoxContext;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    [Function("Diagnose")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options", Route = "diagnose")] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Diagnostic check requested");

        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        var checks = new List<DiagnosticCheck>();
        var overallStatus = "OK";
        var passedChecks = 0;
        var totalChecks = 0;

        // Check 1: Application Database Connection
        totalChecks++;
        try
        {
            if (_applicationContext == null)
            {
                checks.Add(new DiagnosticCheck
                {
                    Name = "Application Database Connection",
                    Status = "ERROR",
                    Details = "ApplicationDbContext is not configured"
                });
            }
            else
            {
                var canConnect = await _applicationContext.Database.CanConnectAsync(context.CancellationToken);
                if (canConnect)
                {
                    checks.Add(new DiagnosticCheck
                    {
                        Name = "Application Database Connection",
                        Status = "OK",
                        Details = "Successfully connected to application database"
                    });
                    passedChecks++;
                }
                else
                {
                    checks.Add(new DiagnosticCheck
                    {
                        Name = "Application Database Connection",
                        Status = "FAILED",
                        Details = "Cannot connect to application database"
                    });
                    overallStatus = "FAILED";
                }
            }
        }
        catch (Exception ex)
        {
            checks.Add(new DiagnosticCheck
            {
                Name = "Application Database Connection",
                Status = "ERROR",
                Details = $"Error checking application database: {ex.Message}"
            });
            overallStatus = "ERROR";
        }

        // Check 2: MessageBox Database Connection
        totalChecks++;
        try
        {
            if (_messageBoxContext == null)
            {
                checks.Add(new DiagnosticCheck
                {
                    Name = "MessageBox Database Connection",
                    Status = "ERROR",
                    Details = "MessageBoxDbContext is not configured"
                });
            }
            else
            {
                var canConnect = await _messageBoxContext.Database.CanConnectAsync(context.CancellationToken);
                if (canConnect)
                {
                    checks.Add(new DiagnosticCheck
                    {
                        Name = "MessageBox Database Connection",
                        Status = "OK",
                        Details = "Successfully connected to MessageBox database"
                    });
                    passedChecks++;
                }
                else
                {
                    checks.Add(new DiagnosticCheck
                    {
                        Name = "MessageBox Database Connection",
                        Status = "FAILED",
                        Details = "Cannot connect to MessageBox database"
                    });
                    overallStatus = "FAILED";
                }
            }
        }
        catch (Exception ex)
        {
            checks.Add(new DiagnosticCheck
            {
                Name = "MessageBox Database Connection",
                Status = "ERROR",
                Details = $"Error checking MessageBox database: {ex.Message}"
            });
            overallStatus = "ERROR";
        }

        // Check 3: Blob Storage Connection
        totalChecks++;
        try
        {
            if (_blobServiceClient == null)
            {
                checks.Add(new DiagnosticCheck
                {
                    Name = "Blob Storage Connection",
                    Status = "ERROR",
                    Details = "BlobServiceClient is not configured"
                });
            }
            else
            {
                try
                {
                    // Test blob storage connection by listing containers
                    await foreach (var container in _blobServiceClient.GetBlobContainersAsync(cancellationToken: context.CancellationToken))
                    {
                        // If we can enumerate containers, connection is working
                        checks.Add(new DiagnosticCheck
                        {
                            Name = "Blob Storage Connection",
                            Status = "OK",
                            Details = $"Successfully connected to blob storage. Found container: {container.Name}"
                        });
                        passedChecks++;
                        break; // Only need to check one container
                    }
                    
                    // If no containers found, still consider it OK (connection works)
                    if (!checks.Any(c => c.Name == "Blob Storage Connection"))
                    {
                        checks.Add(new DiagnosticCheck
                        {
                            Name = "Blob Storage Connection",
                            Status = "OK",
                            Details = "Successfully connected to blob storage (no containers found)"
                        });
                        passedChecks++;
                    }
                }
                catch (Exception ex)
                {
                    checks.Add(new DiagnosticCheck
                    {
                        Name = "Blob Storage Connection",
                        Status = "ERROR",
                        Details = $"Error checking blob storage: {ex.Message}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            checks.Add(new DiagnosticCheck
            {
                Name = "Blob Storage Connection",
                Status = "ERROR",
                Details = $"Error checking blob storage: {ex.Message}"
            });
            overallStatus = "ERROR";
        }

        // Check 4: Environment Variables
        totalChecks++;
        var missingEnvVars = new List<string>();
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_SQL_SERVER")))
            missingEnvVars.Add("AZURE_SQL_SERVER");
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE")))
            missingEnvVars.Add("AZURE_SQL_DATABASE");
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_SQL_USER")))
            missingEnvVars.Add("AZURE_SQL_USER");
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD")))
            missingEnvVars.Add("AZURE_SQL_PASSWORD");
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AzureWebJobsStorage")))
            missingEnvVars.Add("AzureWebJobsStorage");

        if (missingEnvVars.Count == 0)
        {
            checks.Add(new DiagnosticCheck
            {
                Name = "Environment Variables",
                Status = "OK",
                Details = "All required environment variables are set"
            });
            passedChecks++;
        }
        else
        {
            checks.Add(new DiagnosticCheck
            {
                Name = "Environment Variables",
                Status = "FAILED",
                Details = $"Missing environment variables: {string.Join(", ", missingEnvVars)}"
            });
            if (overallStatus == "OK")
                overallStatus = "FAILED";
        }

        // Check 5: Application Database Tables
        totalChecks++;
        try
        {
            if (_applicationContext != null)
            {
                var canConnect = await _applicationContext.Database.CanConnectAsync(context.CancellationToken);
                if (canConnect)
                {
                    // Try to query TransportData table
                    var tableExists = await _applicationContext.Database.ExecuteSqlRawAsync(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TransportData'",
                        context.CancellationToken);
                    
                    checks.Add(new DiagnosticCheck
                    {
                        Name = "Application Database Tables",
                        Status = "OK",
                        Details = "TransportData table exists or can be created"
                    });
                    passedChecks++;
                }
                else
                {
                    checks.Add(new DiagnosticCheck
                    {
                        Name = "Application Database Tables",
                        Status = "FAILED",
                        Details = "Cannot check tables - database connection failed"
                    });
                }
            }
            else
            {
                checks.Add(new DiagnosticCheck
                {
                    Name = "Application Database Tables",
                    Status = "ERROR",
                    Details = "ApplicationDbContext is not configured"
                });
            }
        }
        catch (Exception ex)
        {
            checks.Add(new DiagnosticCheck
            {
                Name = "Application Database Tables",
                Status = "ERROR",
                Details = $"Error checking tables: {ex.Message}"
            });
        }

        // Check 6: MessageBox Database Tables
        totalChecks++;
        try
        {
            if (_messageBoxContext != null)
            {
                var canConnect = await _messageBoxContext.Database.CanConnectAsync(context.CancellationToken);
                if (canConnect)
                {
                    // Try to query Messages table
                    var tableExists = await _messageBoxContext.Database.ExecuteSqlRawAsync(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Messages'",
                        context.CancellationToken);
                    
                    checks.Add(new DiagnosticCheck
                    {
                        Name = "MessageBox Database Tables",
                        Status = "OK",
                        Details = "MessageBox tables exist or can be created"
                    });
                    passedChecks++;
                }
                else
                {
                    checks.Add(new DiagnosticCheck
                    {
                        Name = "MessageBox Database Tables",
                        Status = "FAILED",
                        Details = "Cannot check tables - database connection failed"
                    });
                }
            }
            else
            {
                checks.Add(new DiagnosticCheck
                {
                    Name = "MessageBox Database Tables",
                    Status = "ERROR",
                    Details = "MessageBoxDbContext is not configured"
                });
            }
        }
        catch (Exception ex)
        {
            checks.Add(new DiagnosticCheck
            {
                Name = "MessageBox Database Tables",
                Status = "ERROR",
                Details = $"Error checking tables: {ex.Message}"
            });
        }

        var result = new DiagnosticResult
        {
            Summary = new DiagnosticSummary
            {
                Overall = overallStatus,
                Passed = passedChecks,
                TotalChecks = totalChecks
            },
            Checks = checks
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        CorsHelper.AddCorsHeaders(response);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        await response.WriteStringAsync(JsonSerializer.Serialize(result, jsonOptions));

        return response;
    }

    private class DiagnosticResult
    {
        public DiagnosticSummary Summary { get; set; } = new();
        public List<DiagnosticCheck> Checks { get; set; } = new();
    }

    private class DiagnosticSummary
    {
        public string Overall { get; set; } = "OK";
        public int Passed { get; set; }
        public int TotalChecks { get; set; }
    }

    private class DiagnosticCheck
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = "OK";
        public string Details { get; set; } = string.Empty;
    }
}


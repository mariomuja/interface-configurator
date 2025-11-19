using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Hosted service that ensures Application database and tables are created automatically on startup
/// This initializes the TransportData table in the main application database (app-database)
/// </summary>
public class ApplicationDatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApplicationDatabaseInitializer> _logger;

    public ApplicationDatabaseInitializer(
        IServiceProvider serviceProvider,
        ILogger<ApplicationDatabaseInitializer> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing Application database and tables...");

            using var scope = _serviceProvider.CreateScope();
            var applicationContext = scope.ServiceProvider.GetService<ApplicationDbContext>();

            if (applicationContext == null)
            {
                _logger.LogWarning("ApplicationDbContext is not available. Skipping database initialization.");
                return;
            }

            // Add timeout to prevent hanging on connection issues
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Ensure database exists and tables are created
            // EnsureCreatedAsync will create the database if it doesn't exist (if permissions allow)
            // and will create all tables defined in the DbContext if they don't exist
            var created = await applicationContext.Database.EnsureCreatedAsync(linkedCts.Token);

            if (created)
            {
                _logger.LogInformation("Application database and tables created successfully. Tables: TransportData");
            }
            else
            {
                _logger.LogInformation("Application database and tables already exist. Tables: TransportData");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Application database initialization timed out after 30 seconds. Database may be unreachable or firewall rules may need to be updated.");
            // Don't throw - allow function app to start even if database initialization fails
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Handle SQL-specific errors (e.g., database doesn't exist, permission issues)
            if (sqlEx.Number == 4060) // Cannot open database
            {
                _logger.LogError(sqlEx, "Application database does not exist. Please ensure the database is created via Terraform before deploying the Function App.");
            }
            else if (sqlEx.Number == 18456) // Login failed
            {
                _logger.LogError(sqlEx, "Failed to connect to Application database. Please check SQL credentials.");
            }
            else
            {
                _logger.LogError(sqlEx, "SQL error initializing Application database: {ErrorNumber} - {Message}", sqlEx.Number, sqlEx.Message);
            }
            // Don't throw - allow function app to start even if database initialization fails
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Application database. Tables may need to be created manually. Error: {Message}", ex.Message);
            // Don't throw - allow function app to start even if database initialization fails
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Nothing to clean up
        return Task.CompletedTask;
    }
}


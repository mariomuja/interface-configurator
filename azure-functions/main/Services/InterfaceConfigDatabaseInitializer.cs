using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Hosted service that ensures InterfaceConfigDb database and tables are created automatically on startup
/// Formerly MessageBoxDatabaseInitializer - database renamed from MessageBox to InterfaceConfigDb
/// </summary>
public class InterfaceConfigDatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InterfaceConfigDatabaseInitializer> _logger;

    public InterfaceConfigDatabaseInitializer(
        IServiceProvider serviceProvider,
        ILogger<InterfaceConfigDatabaseInitializer> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing InterfaceConfigDb database and tables...");

            using var scope = _serviceProvider.CreateScope();
            var interfaceConfigContext = scope.ServiceProvider.GetService<InterfaceConfigDbContext>();

            if (interfaceConfigContext == null)
            {
                _logger.LogWarning("InterfaceConfigDbContext is not available. Skipping database initialization.");
                return;
            }

            // Add timeout to prevent hanging on connection issues
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Ensure database exists and tables are created
            // EnsureCreatedAsync will create the database if it doesn't exist (if permissions allow)
            // and will create all tables defined in the DbContext if they don't exist
            var created = await interfaceConfigContext.Database.EnsureCreatedAsync(linkedCts.Token);

            if (created)
            {
                _logger.LogInformation("InterfaceConfigDb database and tables created successfully. Tables: AdapterInstances, ProcessLogs, ProcessingStatistics, Features, Users, Interfaces");
            }
            else
            {
                _logger.LogInformation("InterfaceConfigDb database and tables already exist. Tables: AdapterInstances, ProcessLogs, ProcessingStatistics, Features, Users, Interfaces");
                
                // Ensure ProcessingStatistics table exists (in case it was added after initial creation)
                // Note: Use the create-processing-statistics-table.sql script for full schema including new columns
                // This is a minimal check - the SQL script handles column additions for existing tables
                try
                {
                    var tableExists = await interfaceConfigContext.Database.ExecuteSqlRawAsync(
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessingStatistics') CREATE TABLE ProcessingStatistics (Id INT IDENTITY(1,1) PRIMARY KEY, InterfaceName NVARCHAR(200) NOT NULL, RowsProcessed INT NOT NULL, RowsSucceeded INT NOT NULL, RowsFailed INT NOT NULL, ProcessingDurationMs BIGINT NOT NULL, ProcessingStartTime DATETIME2 NOT NULL, ProcessingEndTime DATETIME2 NOT NULL, SourceFile NVARCHAR(500) NULL);",
                        cancellationToken);
                    _logger.LogInformation("Verified ProcessingStatistics table exists. Run create-processing-statistics-table.sql to add new statistics columns.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not verify/create ProcessingStatistics table. It may need to be created manually using create-processing-statistics-table.sql.");
                }
            }

            // Initialize default users
            try
            {
                var authService = scope.ServiceProvider.GetService<InterfaceConfigurator.Main.Services.AuthService>();
                if (authService != null)
                {
                    await authService.InitializeDefaultUsersAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize default users. They may already exist.");
            }

            // Note: Features are not auto-initialized on startup
            // They should be created via InitializeFeatures API endpoint or manually
            // This prevents duplicate features on every restart
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("InterfaceConfigDb database initialization timed out after 30 seconds. Database may be unreachable or firewall rules may need to be updated.");
            // Don't throw - allow function app to start even if database initialization fails
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Handle SQL-specific errors (e.g., database doesn't exist, permission issues)
            if (sqlEx.Number == 4060) // Cannot open database
            {
                _logger.LogError(sqlEx, "InterfaceConfigDb database does not exist. Please ensure the database is created via Terraform before deploying the Function App.");
            }
            else if (sqlEx.Number == 18456) // Login failed
            {
                _logger.LogError(sqlEx, "Failed to connect to InterfaceConfigDb database. Please check SQL credentials.");
            }
            else
            {
                _logger.LogError(sqlEx, "SQL error initializing InterfaceConfigDb database: {ErrorNumber} - {Message}", sqlEx.Number, sqlEx.Message);
            }
            // Don't throw - allow function app to start even if database initialization fails
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing InterfaceConfigDb database. Tables may need to be created manually. Error: {Message}", ex.Message);
            // Don't throw - allow function app to start even if database initialization fails
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Nothing to clean up
        return Task.CompletedTask;
    }
}


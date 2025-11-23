using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Hosted service that ensures MessageBox database and tables are created automatically on startup
/// </summary>
public class MessageBoxDatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageBoxDatabaseInitializer> _logger;

    public MessageBoxDatabaseInitializer(
        IServiceProvider serviceProvider,
        ILogger<MessageBoxDatabaseInitializer> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing MessageBox database and tables...");

            using var scope = _serviceProvider.CreateScope();
            var messageBoxContext = scope.ServiceProvider.GetService<MessageBoxDbContext>();

            if (messageBoxContext == null)
            {
                _logger.LogWarning("MessageBoxDbContext is not available. Skipping database initialization.");
                return;
            }

            // Add timeout to prevent hanging on connection issues
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // Ensure database exists and tables are created
            // EnsureCreatedAsync will create the database if it doesn't exist (if permissions allow)
            // and will create all tables defined in the DbContext if they don't exist
            var created = await messageBoxContext.Database.EnsureCreatedAsync(linkedCts.Token);

            if (created)
            {
                _logger.LogInformation("MessageBox database and tables created successfully. Tables: Messages, MessageSubscriptions, AdapterInstances, ProcessLogs, ProcessingStatistics, Features, Users, InterfaceConfigurations, SourceAdapterInstances, DestinationAdapterInstances");
            }
            else
            {
                _logger.LogInformation("MessageBox database and tables already exist. Tables: Messages, MessageSubscriptions, AdapterInstances, ProcessLogs, ProcessingStatistics, Features, Users, InterfaceConfigurations, SourceAdapterInstances, DestinationAdapterInstances");
                
                // Check if Messages table has required columns (AdapterInstanceGuid, MessageHash, etc.)
                try
                {
                    var hasAdapterInstanceGuid = await messageBoxContext.Database.ExecuteSqlRawAsync(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Messages' AND COLUMN_NAME = 'AdapterInstanceGuid'",
                        cancellationToken);
                    
                    if (hasAdapterInstanceGuid == 0)
                    {
                        _logger.LogError("CRITICAL: Messages table is missing required column 'AdapterInstanceGuid'. " +
                            "The table structure does not match the model. " +
                            "Please run update-messagebox-database.sql to add missing columns. " +
                            "Messages cannot be written until the table structure is fixed.");
                    }
                    else
                    {
                        _logger.LogInformation("Verified Messages table has required columns (AdapterInstanceGuid exists)");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not verify Messages table structure. It may need to be updated manually.");
                }
                
                // Ensure ProcessingStatistics table exists (in case it was added after initial creation)
                try
                {
                    var tableExists = await messageBoxContext.Database.ExecuteSqlRawAsync(
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessingStatistics') CREATE TABLE ProcessingStatistics (Id INT IDENTITY(1,1) PRIMARY KEY, InterfaceName NVARCHAR(200) NOT NULL, RowsProcessed INT NOT NULL, RowsSucceeded INT NOT NULL, RowsFailed INT NOT NULL, ProcessingDurationMs BIGINT NOT NULL, ProcessingStartTime DATETIME2 NOT NULL, ProcessingEndTime DATETIME2 NOT NULL, SourceFile NVARCHAR(500) NULL);",
                        cancellationToken);
                    _logger.LogInformation("Verified ProcessingStatistics table exists");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not verify/create ProcessingStatistics table. It may need to be created manually.");
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
            _logger.LogWarning("MessageBox database initialization timed out after 30 seconds. Database may be unreachable or firewall rules may need to be updated.");
            // Don't throw - allow function app to start even if database initialization fails
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            // Handle SQL-specific errors (e.g., database doesn't exist, permission issues)
            if (sqlEx.Number == 4060) // Cannot open database
            {
                _logger.LogError(sqlEx, "MessageBox database does not exist. Please ensure the database is created via Terraform before deploying the Function App.");
            }
            else if (sqlEx.Number == 18456) // Login failed
            {
                _logger.LogError(sqlEx, "Failed to connect to MessageBox database. Please check SQL credentials.");
            }
            else
            {
                _logger.LogError(sqlEx, "SQL error initializing MessageBox database: {ErrorNumber} - {Message}", sqlEx.Number, sqlEx.Message);
            }
            // Don't throw - allow function app to start even if database initialization fails
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing MessageBox database. Tables may need to be created manually. Error: {Message}", ex.Message);
            // Don't throw - allow function app to start even if database initialization fails
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Nothing to clean up
        return Task.CompletedTask;
    }
}


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

            // Ensure database exists and tables are created
            // EnsureCreatedAsync will create the database if it doesn't exist (if permissions allow)
            // and will create all tables defined in the DbContext if they don't exist
            var created = await messageBoxContext.Database.EnsureCreatedAsync(cancellationToken);

            if (created)
            {
                _logger.LogInformation("MessageBox database and tables created successfully. Tables: Messages, MessageSubscriptions, AdapterInstances, ProcessLogs");
            }
            else
            {
                _logger.LogInformation("MessageBox database and tables already exist. Tables: Messages, MessageSubscriptions, AdapterInstances, ProcessLogs");
            }
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


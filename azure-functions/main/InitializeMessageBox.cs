using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Data;

namespace InterfaceConfigurator.Main;

/// <summary>
/// HTTP endpoint to manually initialize MessageBox database and tables
/// </summary>
public class InitializeMessageBoxFunction
{
    private readonly MessageBoxDbContext _context;
    private readonly ILogger<InitializeMessageBoxFunction> _logger;

    public InitializeMessageBoxFunction(
        MessageBoxDbContext context,
        ILogger<InitializeMessageBoxFunction> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("InitializeMessageBox")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "InitializeMessageBox")] HttpRequestData req,
        FunctionContext context)
    {
        try
        {
            _logger.LogInformation("Initializing MessageBox database and tables...");

            // Ensure database exists and tables are created
            var created = await _context.Database.EnsureCreatedAsync(context.CancellationToken);

            // If tables already exist, add missing columns to Messages table
            if (!created)
            {
                await AddMissingColumnsToMessagesTable(context.CancellationToken);
            }

            var message = created
                ? "MessageBox database and tables created successfully. Tables: Messages, MessageSubscriptions, AdapterInstances, ProcessLogs"
                : "MessageBox database and tables already exist. Tables: Messages, MessageSubscriptions, AdapterInstances, ProcessLogs";

            _logger.LogInformation(message);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.WriteString(System.Text.Json.JsonSerializer.Serialize(new { 
                success = true, 
                message = message,
                created = created
            }));

            return response;
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            string errorMessage;
            if (sqlEx.Number == 4060) // Cannot open database
            {
                errorMessage = "MessageBox database does not exist. Please ensure the database is created via Terraform before initializing tables.";
            }
            else if (sqlEx.Number == 18456) // Login failed
            {
                errorMessage = "Failed to connect to MessageBox database. Please check SQL credentials.";
            }
            else
            {
                errorMessage = $"SQL error initializing MessageBox database: {sqlEx.Number} - {sqlEx.Message}";
            }

            _logger.LogError(sqlEx, errorMessage);
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            errorResponse.WriteString(System.Text.Json.JsonSerializer.Serialize(new { 
                success = false, 
                error = errorMessage,
                sqlErrorNumber = sqlEx.Number
            }));
            return errorResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing MessageBox database");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            errorResponse.WriteString(System.Text.Json.JsonSerializer.Serialize(new { 
                success = false, 
                error = ex.Message 
            }));
            return errorResponse;
        }
    }

    private async Task AddMissingColumnsToMessagesTable(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Checking for missing columns in Messages table...");

            var sqlCommands = new[]
            {
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Messages') AND name = 'AdapterInstanceGuid') ALTER TABLE Messages ADD AdapterInstanceGuid UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Messages') AND name = 'RetryCount') ALTER TABLE Messages ADD RetryCount INT NOT NULL DEFAULT 0;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Messages') AND name = 'MaxRetries') ALTER TABLE Messages ADD MaxRetries INT NOT NULL DEFAULT 3;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Messages') AND name = 'LastRetryTime') ALTER TABLE Messages ADD LastRetryTime DATETIME2 NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Messages') AND name = 'InProgressUntil') ALTER TABLE Messages ADD InProgressUntil DATETIME2 NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Messages') AND name = 'DeadLetter') ALTER TABLE Messages ADD DeadLetter BIT NOT NULL DEFAULT 0;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'Messages') AND name = 'MessageHash') ALTER TABLE Messages ADD MessageHash NVARCHAR(64) NULL;"
            };

            foreach (var sql in sqlCommands)
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error executing SQL command: {Sql}", sql);
                }
            }

            // Create index on AdapterInstanceGuid if it doesn't exist
            try
            {
                await _context.Database.ExecuteSqlRawAsync(
                    "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'Messages') AND name = 'IX_Messages_AdapterInstanceGuid') CREATE INDEX IX_Messages_AdapterInstanceGuid ON Messages(AdapterInstanceGuid);",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error creating index on AdapterInstanceGuid");
            }

            _logger.LogInformation("Missing columns check completed for Messages table");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error adding missing columns to Messages table");
        }
    }
}





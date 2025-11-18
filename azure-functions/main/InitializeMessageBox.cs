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
}





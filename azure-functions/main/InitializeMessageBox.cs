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
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "InitializeMessageBox")] HttpRequestData req,
        FunctionContext context)
    {
        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(HttpStatusCode.OK);
            InterfaceConfigurator.Main.Helpers.CorsHelper.AddCorsHeaders(optionsResponse);
            return optionsResponse;
        }

        try
        {
            _logger.LogInformation("Initializing MessageBox database and tables...");

            // Ensure database exists and tables are created
            var created = await _context.Database.EnsureCreatedAsync(context.CancellationToken);

            // If tables already exist, add missing columns to Messages table
            if (!created)
            {
                await AddMissingColumnsToMessagesTable(context.CancellationToken);
                
                // Ensure InterfaceConfigurations and related tables exist
                await EnsureInterfaceConfigurationTablesExist(context.CancellationToken);
            }

            var message = created
                ? "MessageBox database and tables created successfully. Tables: Messages, MessageSubscriptions, AdapterInstances, ProcessLogs"
                : "MessageBox database and tables already exist. Tables: Messages, MessageSubscriptions, AdapterInstances, ProcessLogs";

            _logger.LogInformation(message);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            InterfaceConfigurator.Main.Helpers.CorsHelper.AddCorsHeaders(response);
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
            InterfaceConfigurator.Main.Helpers.CorsHelper.AddCorsHeaders(errorResponse);
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
            InterfaceConfigurator.Main.Helpers.CorsHelper.AddCorsHeaders(errorResponse);
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

    private async Task EnsureInterfaceConfigurationTablesExist(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Checking for InterfaceConfiguration tables...");

            var sqlCommands = new[]
            {
                // Create InterfaceConfigurations table if it doesn't exist
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InterfaceConfigurations')
                  CREATE TABLE InterfaceConfigurations (
                      InterfaceName NVARCHAR(200) NOT NULL PRIMARY KEY,
                      Description NVARCHAR(500) NULL,
                      CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                      UpdatedAt DATETIME2 NULL
                  );
                  CREATE INDEX IX_InterfaceConfigurations_CreatedAt ON InterfaceConfigurations(CreatedAt);",
                
                // Create SourceAdapterInstances table if it doesn't exist (with all columns)
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SourceAdapterInstances')
                  CREATE TABLE SourceAdapterInstances (
                      AdapterInstanceGuid UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                      InstanceName NVARCHAR(200) NOT NULL,
                      AdapterName NVARCHAR(100) NOT NULL,
                      IsEnabled BIT NOT NULL DEFAULT 1,
                      Configuration NVARCHAR(1000) NULL,
                      SourceReceiveFolder NVARCHAR(500) NULL,
                      SourceFileMask NVARCHAR(100) NULL,
                      SourceBatchSize INT NOT NULL DEFAULT 100,
                      SourceFieldSeparator NVARCHAR(10) NULL,
                      CsvData NVARCHAR(MAX) NULL,
                      CsvAdapterType NVARCHAR(20) NULL,
                      CsvPollingInterval INT NOT NULL DEFAULT 10,
                      SftpHost NVARCHAR(500) NULL,
                      SftpPort INT NOT NULL DEFAULT 22,
                      SftpUsername NVARCHAR(200) NULL,
                      SftpPassword NVARCHAR(500) NULL,
                      SftpSshKey NVARCHAR(5000) NULL,
                      SftpFolder NVARCHAR(500) NULL,
                      SftpFileMask NVARCHAR(100) NULL,
                      SftpMaxConnectionPoolSize INT NOT NULL DEFAULT 5,
                      SftpFileBufferSize INT NOT NULL DEFAULT 8192,
                      SqlServerName NVARCHAR(500) NULL,
                      SqlDatabaseName NVARCHAR(200) NULL,
                      SqlUserName NVARCHAR(200) NULL,
                      SqlPassword NVARCHAR(500) NULL,
                      SqlIntegratedSecurity BIT NOT NULL DEFAULT 0,
                      SqlResourceGroup NVARCHAR(200) NULL,
                      SqlPollingStatement NVARCHAR(2000) NULL,
                      SqlPollingInterval INT NOT NULL DEFAULT 60,
                      SqlTableName NVARCHAR(200) NULL,
                      SqlUseTransaction BIT NOT NULL DEFAULT 0,
                      SqlBatchSize INT NOT NULL DEFAULT 1000,
                      SqlCommandTimeout INT NOT NULL DEFAULT 30,
                      SqlFailOnBadStatement BIT NOT NULL DEFAULT 0,
                      SapApplicationServer NVARCHAR(500) NULL,
                      SapSystemNumber NVARCHAR(200) NULL,
                      SapClient NVARCHAR(200) NULL,
                      SapUsername NVARCHAR(200) NULL,
                      SapPassword NVARCHAR(500) NULL,
                      SapLanguage NVARCHAR(200) NULL,
                      SapIdocType NVARCHAR(500) NULL,
                      SapIdocMessageType NVARCHAR(500) NULL,
                      SapIdocFilter NVARCHAR(2000) NULL,
                      SapPollingInterval INT NOT NULL DEFAULT 60,
                      SapBatchSize INT NOT NULL DEFAULT 100,
                      SapConnectionTimeout INT NOT NULL DEFAULT 30,
                      SapUseRfc BIT NOT NULL DEFAULT 1,
                      SapRfcDestination NVARCHAR(500) NULL,
                      SapRfcFunctionModule NVARCHAR(500) NULL,
                      SapRfcParameters NVARCHAR(2000) NULL,
                      SapODataServiceUrl NVARCHAR(500) NULL,
                      SapRestApiEndpoint NVARCHAR(500) NULL,
                      SapUseOData BIT NOT NULL DEFAULT 0,
                      SapUseRestApi BIT NOT NULL DEFAULT 0,
                      Dynamics365TenantId NVARCHAR(500) NULL,
                      Dynamics365ClientId NVARCHAR(500) NULL,
                      Dynamics365ClientSecret NVARCHAR(500) NULL,
                      Dynamics365InstanceUrl NVARCHAR(500) NULL,
                      Dynamics365EntityName NVARCHAR(200) NULL,
                      Dynamics365ODataFilter NVARCHAR(2000) NULL,
                      Dynamics365PollingInterval INT NOT NULL DEFAULT 60,
                      Dynamics365BatchSize INT NOT NULL DEFAULT 100,
                      Dynamics365PageSize INT NOT NULL DEFAULT 50,
                      CrmOrganizationUrl NVARCHAR(500) NULL,
                      CrmUsername NVARCHAR(200) NULL,
                      CrmPassword NVARCHAR(500) NULL,
                      CrmEntityName NVARCHAR(200) NULL,
                      CrmFetchXml NVARCHAR(2000) NULL,
                      CrmPollingInterval INT NOT NULL DEFAULT 60,
                      CrmBatchSize INT NOT NULL DEFAULT 100,
                      CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                      UpdatedAt DATETIME2 NULL
                  );
                  CREATE UNIQUE INDEX IX_SourceAdapterInstances_AdapterInstanceGuid ON SourceAdapterInstances(AdapterInstanceGuid);
                  CREATE INDEX IX_SourceAdapterInstances_InstanceName ON SourceAdapterInstances(InstanceName);
                  CREATE INDEX IX_SourceAdapterInstances_AdapterName ON SourceAdapterInstances(AdapterName);",
                
                // Create DestinationAdapterInstances table if it doesn't exist (with all columns)
                @"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DestinationAdapterInstances')
                  CREATE TABLE DestinationAdapterInstances (
                      AdapterInstanceGuid UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                      InstanceName NVARCHAR(200) NOT NULL,
                      AdapterName NVARCHAR(100) NOT NULL,
                      IsEnabled BIT NOT NULL DEFAULT 1,
                      Configuration NVARCHAR(1000) NOT NULL,
                      DestinationReceiveFolder NVARCHAR(500) NULL,
                      DestinationFileMask NVARCHAR(100) NULL,
                      SqlServerName NVARCHAR(500) NULL,
                      SqlDatabaseName NVARCHAR(200) NULL,
                      SqlUserName NVARCHAR(200) NULL,
                      SqlPassword NVARCHAR(500) NULL,
                      SqlIntegratedSecurity BIT NOT NULL DEFAULT 0,
                      SqlResourceGroup NVARCHAR(200) NULL,
                      SqlTableName NVARCHAR(200) NULL,
                      SqlUseTransaction BIT NOT NULL DEFAULT 0,
                      SqlBatchSize INT NOT NULL DEFAULT 1000,
                      SqlCommandTimeout INT NOT NULL DEFAULT 30,
                      SqlFailOnBadStatement BIT NOT NULL DEFAULT 0,
                      SapApplicationServer NVARCHAR(500) NULL,
                      SapSystemNumber NVARCHAR(200) NULL,
                      SapClient NVARCHAR(200) NULL,
                      SapUsername NVARCHAR(200) NULL,
                      SapPassword NVARCHAR(500) NULL,
                      SapLanguage NVARCHAR(200) NULL,
                      SapIdocType NVARCHAR(500) NULL,
                      SapIdocMessageType NVARCHAR(500) NULL,
                      SapReceiverPort NVARCHAR(500) NULL,
                      SapReceiverPartner NVARCHAR(500) NULL,
                      SapConnectionTimeout INT NOT NULL DEFAULT 30,
                      SapUseRfc BIT NOT NULL DEFAULT 1,
                      SapRfcDestination NVARCHAR(500) NULL,
                      SapRfcFunctionModule NVARCHAR(500) NULL,
                      SapRfcParameters NVARCHAR(2000) NULL,
                      SapODataServiceUrl NVARCHAR(500) NULL,
                      SapRestApiEndpoint NVARCHAR(500) NULL,
                      SapUseOData BIT NOT NULL DEFAULT 0,
                      SapUseRestApi BIT NOT NULL DEFAULT 0,
                      SapBatchSize INT NOT NULL DEFAULT 100,
                      Dynamics365TenantId NVARCHAR(500) NULL,
                      Dynamics365ClientId NVARCHAR(500) NULL,
                      Dynamics365ClientSecret NVARCHAR(500) NULL,
                      Dynamics365InstanceUrl NVARCHAR(500) NULL,
                      Dynamics365EntityName NVARCHAR(200) NULL,
                      Dynamics365BatchSize INT NOT NULL DEFAULT 100,
                      Dynamics365UseBatch BIT NOT NULL DEFAULT 1,
                      CrmOrganizationUrl NVARCHAR(500) NULL,
                      CrmUsername NVARCHAR(200) NULL,
                      CrmPassword NVARCHAR(500) NULL,
                      CrmEntityName NVARCHAR(200) NULL,
                      CrmBatchSize INT NOT NULL DEFAULT 100,
                      CrmUseBatch BIT NOT NULL DEFAULT 1,
                      JQScriptFile NVARCHAR(1000) NULL,
                      SourceAdapterSubscription UNIQUEIDENTIFIER NULL,
                      InsertStatement NVARCHAR(5000) NULL,
                      UpdateStatement NVARCHAR(5000) NULL,
                      DeleteStatement NVARCHAR(5000) NULL,
                      CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                      UpdatedAt DATETIME2 NULL
                  );
                  CREATE UNIQUE INDEX IX_DestinationAdapterInstances_AdapterInstanceGuid ON DestinationAdapterInstances(AdapterInstanceGuid);
                  CREATE INDEX IX_DestinationAdapterInstances_InstanceName ON DestinationAdapterInstances(InstanceName);
                  CREATE INDEX IX_DestinationAdapterInstances_AdapterName ON DestinationAdapterInstances(AdapterName);"
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

            // Add missing columns to existing tables if they don't exist
            await AddMissingColumnsToSourceAdapterInstances(cancellationToken);
            await AddMissingColumnsToDestinationAdapterInstances(cancellationToken);

            _logger.LogInformation("InterfaceConfiguration tables check completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error ensuring InterfaceConfiguration tables exist");
        }
    }

    private async Task AddMissingColumnsToSourceAdapterInstances(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Checking for missing columns in SourceAdapterInstances table...");

            var sqlCommands = new[]
            {
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SourceReceiveFolder') ALTER TABLE SourceAdapterInstances ADD SourceReceiveFolder NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SourceFileMask') ALTER TABLE SourceAdapterInstances ADD SourceFileMask NVARCHAR(100) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SourceBatchSize') ALTER TABLE SourceAdapterInstances ADD SourceBatchSize INT NOT NULL DEFAULT 100;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SourceFieldSeparator') ALTER TABLE SourceAdapterInstances ADD SourceFieldSeparator NVARCHAR(10) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'CsvData') ALTER TABLE SourceAdapterInstances ADD CsvData NVARCHAR(MAX) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'CsvAdapterType') ALTER TABLE SourceAdapterInstances ADD CsvAdapterType NVARCHAR(20) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'CsvPollingInterval') ALTER TABLE SourceAdapterInstances ADD CsvPollingInterval INT NOT NULL DEFAULT 10;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SftpHost') ALTER TABLE SourceAdapterInstances ADD SftpHost NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SftpPort') ALTER TABLE SourceAdapterInstances ADD SftpPort INT NOT NULL DEFAULT 22;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SftpUsername') ALTER TABLE SourceAdapterInstances ADD SftpUsername NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SftpPassword') ALTER TABLE SourceAdapterInstances ADD SftpPassword NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SftpSshKey') ALTER TABLE SourceAdapterInstances ADD SftpSshKey NVARCHAR(5000) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SftpFolder') ALTER TABLE SourceAdapterInstances ADD SftpFolder NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SftpFileMask') ALTER TABLE SourceAdapterInstances ADD SftpFileMask NVARCHAR(100) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SftpMaxConnectionPoolSize') ALTER TABLE SourceAdapterInstances ADD SftpMaxConnectionPoolSize INT NOT NULL DEFAULT 5;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SftpFileBufferSize') ALTER TABLE SourceAdapterInstances ADD SftpFileBufferSize INT NOT NULL DEFAULT 8192;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlServerName') ALTER TABLE SourceAdapterInstances ADD SqlServerName NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlDatabaseName') ALTER TABLE SourceAdapterInstances ADD SqlDatabaseName NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlUserName') ALTER TABLE SourceAdapterInstances ADD SqlUserName NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlPassword') ALTER TABLE SourceAdapterInstances ADD SqlPassword NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlIntegratedSecurity') ALTER TABLE SourceAdapterInstances ADD SqlIntegratedSecurity BIT NOT NULL DEFAULT 0;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlResourceGroup') ALTER TABLE SourceAdapterInstances ADD SqlResourceGroup NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlPollingStatement') ALTER TABLE SourceAdapterInstances ADD SqlPollingStatement NVARCHAR(2000) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlPollingInterval') ALTER TABLE SourceAdapterInstances ADD SqlPollingInterval INT NOT NULL DEFAULT 60;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlTableName') ALTER TABLE SourceAdapterInstances ADD SqlTableName NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlUseTransaction') ALTER TABLE SourceAdapterInstances ADD SqlUseTransaction BIT NOT NULL DEFAULT 0;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlBatchSize') ALTER TABLE SourceAdapterInstances ADD SqlBatchSize INT NOT NULL DEFAULT 1000;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlCommandTimeout') ALTER TABLE SourceAdapterInstances ADD SqlCommandTimeout INT NOT NULL DEFAULT 30;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SqlFailOnBadStatement') ALTER TABLE SourceAdapterInstances ADD SqlFailOnBadStatement BIT NOT NULL DEFAULT 0;",
                // SAP Adapter Properties
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapApplicationServer') ALTER TABLE SourceAdapterInstances ADD SapApplicationServer NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapSystemNumber') ALTER TABLE SourceAdapterInstances ADD SapSystemNumber NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapClient') ALTER TABLE SourceAdapterInstances ADD SapClient NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapUsername') ALTER TABLE SourceAdapterInstances ADD SapUsername NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapPassword') ALTER TABLE SourceAdapterInstances ADD SapPassword NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapLanguage') ALTER TABLE SourceAdapterInstances ADD SapLanguage NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapIdocType') ALTER TABLE SourceAdapterInstances ADD SapIdocType NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapIdocMessageType') ALTER TABLE SourceAdapterInstances ADD SapIdocMessageType NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapIdocFilter') ALTER TABLE SourceAdapterInstances ADD SapIdocFilter NVARCHAR(2000) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapPollingInterval') ALTER TABLE SourceAdapterInstances ADD SapPollingInterval INT NOT NULL DEFAULT 60;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapBatchSize') ALTER TABLE SourceAdapterInstances ADD SapBatchSize INT NOT NULL DEFAULT 100;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapConnectionTimeout') ALTER TABLE SourceAdapterInstances ADD SapConnectionTimeout INT NOT NULL DEFAULT 30;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapUseRfc') ALTER TABLE SourceAdapterInstances ADD SapUseRfc BIT NOT NULL DEFAULT 1;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapRfcDestination') ALTER TABLE SourceAdapterInstances ADD SapRfcDestination NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapRfcFunctionModule') ALTER TABLE SourceAdapterInstances ADD SapRfcFunctionModule NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapRfcParameters') ALTER TABLE SourceAdapterInstances ADD SapRfcParameters NVARCHAR(2000) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapODataServiceUrl') ALTER TABLE SourceAdapterInstances ADD SapODataServiceUrl NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapRestApiEndpoint') ALTER TABLE SourceAdapterInstances ADD SapRestApiEndpoint NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapUseOData') ALTER TABLE SourceAdapterInstances ADD SapUseOData BIT NOT NULL DEFAULT 0;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'SapUseRestApi') ALTER TABLE SourceAdapterInstances ADD SapUseRestApi BIT NOT NULL DEFAULT 0;",
                // Dynamics 365 Adapter Properties
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'Dynamics365TenantId') ALTER TABLE SourceAdapterInstances ADD Dynamics365TenantId NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'Dynamics365ClientId') ALTER TABLE SourceAdapterInstances ADD Dynamics365ClientId NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'Dynamics365ClientSecret') ALTER TABLE SourceAdapterInstances ADD Dynamics365ClientSecret NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'Dynamics365InstanceUrl') ALTER TABLE SourceAdapterInstances ADD Dynamics365InstanceUrl NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'Dynamics365EntityName') ALTER TABLE SourceAdapterInstances ADD Dynamics365EntityName NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'Dynamics365ODataFilter') ALTER TABLE SourceAdapterInstances ADD Dynamics365ODataFilter NVARCHAR(2000) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'Dynamics365PollingInterval') ALTER TABLE SourceAdapterInstances ADD Dynamics365PollingInterval INT NOT NULL DEFAULT 60;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'Dynamics365BatchSize') ALTER TABLE SourceAdapterInstances ADD Dynamics365BatchSize INT NOT NULL DEFAULT 100;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'Dynamics365PageSize') ALTER TABLE SourceAdapterInstances ADD Dynamics365PageSize INT NOT NULL DEFAULT 50;",
                // CRM Adapter Properties
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'CrmOrganizationUrl') ALTER TABLE SourceAdapterInstances ADD CrmOrganizationUrl NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'CrmUsername') ALTER TABLE SourceAdapterInstances ADD CrmUsername NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'CrmPassword') ALTER TABLE SourceAdapterInstances ADD CrmPassword NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'CrmEntityName') ALTER TABLE SourceAdapterInstances ADD CrmEntityName NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'CrmFetchXml') ALTER TABLE SourceAdapterInstances ADD CrmFetchXml NVARCHAR(2000) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'CrmPollingInterval') ALTER TABLE SourceAdapterInstances ADD CrmPollingInterval INT NOT NULL DEFAULT 60;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'SourceAdapterInstances') AND name = 'CrmBatchSize') ALTER TABLE SourceAdapterInstances ADD CrmBatchSize INT NOT NULL DEFAULT 100;"
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

            _logger.LogInformation("Missing columns check completed for SourceAdapterInstances table");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error adding missing columns to SourceAdapterInstances table");
        }
    }

    private async Task AddMissingColumnsToDestinationAdapterInstances(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Checking for missing columns in DestinationAdapterInstances table...");

            var sqlCommands = new[]
            {
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'DestinationReceiveFolder') ALTER TABLE DestinationAdapterInstances ADD DestinationReceiveFolder NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'DestinationFileMask') ALTER TABLE DestinationAdapterInstances ADD DestinationFileMask NVARCHAR(100) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SqlServerName') ALTER TABLE DestinationAdapterInstances ADD SqlServerName NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SqlDatabaseName') ALTER TABLE DestinationAdapterInstances ADD SqlDatabaseName NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SqlUserName') ALTER TABLE DestinationAdapterInstances ADD SqlUserName NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SqlPassword') ALTER TABLE DestinationAdapterInstances ADD SqlPassword NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SqlIntegratedSecurity') ALTER TABLE DestinationAdapterInstances ADD SqlIntegratedSecurity BIT NOT NULL DEFAULT 0;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SqlResourceGroup') ALTER TABLE DestinationAdapterInstances ADD SqlResourceGroup NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SqlTableName') ALTER TABLE DestinationAdapterInstances ADD SqlTableName NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SqlUseTransaction') ALTER TABLE DestinationAdapterInstances ADD SqlUseTransaction BIT NOT NULL DEFAULT 0;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SqlBatchSize') ALTER TABLE DestinationAdapterInstances ADD SqlBatchSize INT NOT NULL DEFAULT 1000;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SqlCommandTimeout') ALTER TABLE DestinationAdapterInstances ADD SqlCommandTimeout INT NOT NULL DEFAULT 30;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SqlFailOnBadStatement') ALTER TABLE DestinationAdapterInstances ADD SqlFailOnBadStatement BIT NOT NULL DEFAULT 0;",
                // SAP Adapter Properties
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapApplicationServer') ALTER TABLE DestinationAdapterInstances ADD SapApplicationServer NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapSystemNumber') ALTER TABLE DestinationAdapterInstances ADD SapSystemNumber NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapClient') ALTER TABLE DestinationAdapterInstances ADD SapClient NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapUsername') ALTER TABLE DestinationAdapterInstances ADD SapUsername NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapPassword') ALTER TABLE DestinationAdapterInstances ADD SapPassword NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapLanguage') ALTER TABLE DestinationAdapterInstances ADD SapLanguage NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapIdocType') ALTER TABLE DestinationAdapterInstances ADD SapIdocType NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapIdocMessageType') ALTER TABLE DestinationAdapterInstances ADD SapIdocMessageType NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapReceiverPort') ALTER TABLE DestinationAdapterInstances ADD SapReceiverPort NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapReceiverPartner') ALTER TABLE DestinationAdapterInstances ADD SapReceiverPartner NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapConnectionTimeout') ALTER TABLE DestinationAdapterInstances ADD SapConnectionTimeout INT NOT NULL DEFAULT 30;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapUseRfc') ALTER TABLE DestinationAdapterInstances ADD SapUseRfc BIT NOT NULL DEFAULT 1;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapRfcDestination') ALTER TABLE DestinationAdapterInstances ADD SapRfcDestination NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapRfcFunctionModule') ALTER TABLE DestinationAdapterInstances ADD SapRfcFunctionModule NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapRfcParameters') ALTER TABLE DestinationAdapterInstances ADD SapRfcParameters NVARCHAR(2000) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapODataServiceUrl') ALTER TABLE DestinationAdapterInstances ADD SapODataServiceUrl NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapRestApiEndpoint') ALTER TABLE DestinationAdapterInstances ADD SapRestApiEndpoint NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapUseOData') ALTER TABLE DestinationAdapterInstances ADD SapUseOData BIT NOT NULL DEFAULT 0;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapUseRestApi') ALTER TABLE DestinationAdapterInstances ADD SapUseRestApi BIT NOT NULL DEFAULT 0;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SapBatchSize') ALTER TABLE DestinationAdapterInstances ADD SapBatchSize INT NOT NULL DEFAULT 100;",
                // Dynamics 365 Adapter Properties
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'Dynamics365TenantId') ALTER TABLE DestinationAdapterInstances ADD Dynamics365TenantId NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'Dynamics365ClientId') ALTER TABLE DestinationAdapterInstances ADD Dynamics365ClientId NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'Dynamics365ClientSecret') ALTER TABLE DestinationAdapterInstances ADD Dynamics365ClientSecret NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'Dynamics365InstanceUrl') ALTER TABLE DestinationAdapterInstances ADD Dynamics365InstanceUrl NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'Dynamics365EntityName') ALTER TABLE DestinationAdapterInstances ADD Dynamics365EntityName NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'Dynamics365BatchSize') ALTER TABLE DestinationAdapterInstances ADD Dynamics365BatchSize INT NOT NULL DEFAULT 100;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'Dynamics365UseBatch') ALTER TABLE DestinationAdapterInstances ADD Dynamics365UseBatch BIT NOT NULL DEFAULT 1;",
                // CRM Adapter Properties
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'CrmOrganizationUrl') ALTER TABLE DestinationAdapterInstances ADD CrmOrganizationUrl NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'CrmUsername') ALTER TABLE DestinationAdapterInstances ADD CrmUsername NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'CrmPassword') ALTER TABLE DestinationAdapterInstances ADD CrmPassword NVARCHAR(500) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'CrmEntityName') ALTER TABLE DestinationAdapterInstances ADD CrmEntityName NVARCHAR(200) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'CrmBatchSize') ALTER TABLE DestinationAdapterInstances ADD CrmBatchSize INT NOT NULL DEFAULT 100;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'CrmUseBatch') ALTER TABLE DestinationAdapterInstances ADD CrmUseBatch BIT NOT NULL DEFAULT 1;",
                // JQ Transformation and Subscription Properties
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'JQScriptFile') ALTER TABLE DestinationAdapterInstances ADD JQScriptFile NVARCHAR(1000) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'SourceAdapterSubscription') ALTER TABLE DestinationAdapterInstances ADD SourceAdapterSubscription UNIQUEIDENTIFIER NULL;",
                // SQL Server Custom Statement Properties
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'InsertStatement') ALTER TABLE DestinationAdapterInstances ADD InsertStatement NVARCHAR(5000) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'UpdateStatement') ALTER TABLE DestinationAdapterInstances ADD UpdateStatement NVARCHAR(5000) NULL;",
                "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'DestinationAdapterInstances') AND name = 'DeleteStatement') ALTER TABLE DestinationAdapterInstances ADD DeleteStatement NVARCHAR(5000) NULL;"
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

            _logger.LogInformation("Missing columns check completed for DestinationAdapterInstances table");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error adding missing columns to DestinationAdapterInstances table");
        }
    }
}





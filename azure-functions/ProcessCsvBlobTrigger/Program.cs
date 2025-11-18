using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Adapters;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Services;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        try
        {
            // Get connection string from environment variables
            var sqlServer = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER");
            var sqlDatabase = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE");
            var sqlUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
            var sqlPassword = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");
            
            if (string.IsNullOrEmpty(sqlServer) || string.IsNullOrEmpty(sqlDatabase) || 
                string.IsNullOrEmpty(sqlUser) || string.IsNullOrEmpty(sqlPassword))
            {
                Console.WriteLine("WARNING: SQL connection environment variables not set. Functions may not work correctly.");
                // Don't throw - allow function app to start even if DB config is missing
            }
            else
            {
                // Main application database connection (app-database)
                // This database contains TransportData table and other application tables
                var connectionString = $"Server=tcp:{sqlServer},1433;Initial Catalog={sqlDatabase};Persist Security Info=False;User ID={sqlUser};Password={sqlPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
                
                // MessageBox database connection (separate database for staging/messaging)
                // This database contains Messages, MessageSubscriptions, and ProcessLogs tables ONLY
                // TransportData table is NOT created here - it belongs to the main application database
                var messageBoxConnectionString = $"Server=tcp:{sqlServer},1433;Initial Catalog=MessageBox;Persist Security Info=False;User ID={sqlUser};Password={sqlPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
                
                // Register DbContext for main application database (app-database)
                // SqlServerAdapter uses this context to create/write to TransportData table
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(connectionString));
                
                // Register MessageBoxDbContext for MessageBox database (separate database)
                // Contains Messages, MessageSubscriptions, ProcessLogs - NOT TransportData
                services.AddDbContext<MessageBoxDbContext>(options =>
                    options.UseSqlServer(messageBoxConnectionString));
                
                // Ensure MessageBox database and tables are created automatically on startup
                services.AddHostedService<MessageBoxDatabaseInitializer>();
            }
            
            // Register Adapter Configuration Service (must be registered before CsvProcessingService)
            // Uses JSON file storage in Blob Storage with in-memory cache
            services.AddSingleton<IAdapterConfigurationService>(sp =>
            {
                var blobServiceClient = sp.GetService<Azure.Storage.Blobs.BlobServiceClient>();
                var logger = sp.GetService<ILogger<ProcessCsvBlobTrigger.Services.AdapterConfigurationService>>();
                var service = new ProcessCsvBlobTrigger.Services.AdapterConfigurationService(blobServiceClient, logger);
                // Initialize on startup (fire and forget)
                _ = Task.Run(async () => await service.InitializeAsync(), CancellationToken.None);
                return service;
            });
            
            // Register Interface Configuration Service
            services.AddSingleton<IInterfaceConfigurationService>(sp =>
            {
                var blobServiceClient = sp.GetService<Azure.Storage.Blobs.BlobServiceClient>();
                var logger = sp.GetService<ILogger<ProcessCsvBlobTrigger.Services.InterfaceConfigurationService>>();
                var service = new ProcessCsvBlobTrigger.Services.InterfaceConfigurationService(blobServiceClient, logger);
                // Initialize on startup (fire and forget)
                _ = Task.Run(async () => await service.InitializeAsync(), CancellationToken.None);
                return service;
            });
            
            // Register Adapter Factory
            services.AddScoped<IAdapterFactory, ProcessCsvBlobTrigger.Services.AdapterFactory>();
            
            // Register Core Services
            services.AddScoped<ICsvProcessingService, CsvProcessingService>();
            
            // Register Event Queue (in-memory)
            services.AddSingleton<IEventQueue, InMemoryEventQueue>();
            
            // Register Message Subscription Service
            services.AddScoped<IMessageSubscriptionService>(sp =>
            {
                var messageBoxContext = sp.GetService<MessageBoxDbContext>();
                var logger = sp.GetService<ILogger<MessageSubscriptionService>>();
                if (messageBoxContext == null)
                {
                    return null!;
                }
                return new MessageSubscriptionService(messageBoxContext, logger);
            });
            
            // Register MessageBox Service
            services.AddScoped<IMessageBoxService>(sp =>
            {
                var messageBoxContext = sp.GetService<MessageBoxDbContext>();
                var eventQueue = sp.GetService<IEventQueue>();
                var subscriptionService = sp.GetService<IMessageSubscriptionService>();
                var logger = sp.GetService<ILogger<MessageBoxService>>();
                if (messageBoxContext == null)
                {
                    // Fallback to in-memory logging if MessageBox is not available
                    return null!;
                }
                return new MessageBoxService(messageBoxContext, eventQueue, subscriptionService, logger);
            });
            
            // Register SQL Server Logging Service (uses MessageBox database)
            services.AddScoped<ILoggingService>(sp =>
            {
                var messageBoxContext = sp.GetService<MessageBoxDbContext>();
                var logger = sp.GetService<ILogger<SqlServerLoggingService>>();
                if (messageBoxContext == null)
                {
                    // Fallback to in-memory logging if MessageBox is not available
                    var inMemoryLogger = sp.GetService<ILogger<InMemoryLoggingService>>();
                    return new InMemoryLoggingService(inMemoryLogger);
                }
                return new SqlServerLoggingService(messageBoxContext, logger);
            });
            
            // Keep InMemoryLoggingService for backward compatibility (used by HTTP endpoints)
            services.AddSingleton<IInMemoryLoggingService, InMemoryLoggingService>();
            
            // Register Adapter Services (these handle null DbContext gracefully)
            services.AddScoped<IDataService, DataServiceAdapter>();
            services.AddScoped<IDynamicTableService, DynamicTableService>();
            
            // Register Error Row Service (requires BlobServiceClient)
            var storageConnectionString = Environment.GetEnvironmentVariable("MainStorageConnection") 
                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            Azure.Storage.Blobs.BlobServiceClient? blobServiceClient = null;
            if (!string.IsNullOrEmpty(storageConnectionString))
            {
                blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(storageConnectionString);
                services.AddSingleton(blobServiceClient);
                services.AddScoped<IErrorRowService, ErrorRowService>();
            }
            else
            {
                Console.WriteLine("WARNING: Storage connection string not set. Error row service may not work.");
            }
            
            // Register Adapters
            // CSV Adapter (for source) - register as named service
            services.AddScoped<CsvAdapter>(sp =>
            {
                var csvProcessingService = sp.GetRequiredService<ICsvProcessingService>();
                var adapterConfig = sp.GetRequiredService<IAdapterConfigurationService>();
                var messageBoxService = sp.GetService<IMessageBoxService>();
                var subscriptionService = sp.GetService<IMessageSubscriptionService>();
                var logger = sp.GetService<ILogger<CsvAdapter>>();
                if (blobServiceClient == null)
                {
                    throw new InvalidOperationException("BlobServiceClient is required for CsvAdapter");
                }
                return new CsvAdapter(csvProcessingService, adapterConfig, blobServiceClient, messageBoxService, subscriptionService, "FromCsvToSqlServerExample", null, null, null, null, null, null, null, logger);
            });
            
            // SQL Server Adapter (for destination) - register as named service
            services.AddScoped<SqlServerAdapter>(sp =>
            {
                var context = sp.GetService<ApplicationDbContext>();
                var dynamicTableService = sp.GetRequiredService<IDynamicTableService>();
                var dataService = sp.GetRequiredService<IDataService>();
                var messageBoxService = sp.GetService<IMessageBoxService>();
                var subscriptionService = sp.GetService<IMessageSubscriptionService>();
                var logger = sp.GetService<ILogger<SqlServerAdapter>>();
                if (context == null)
                {
                    throw new InvalidOperationException("ApplicationDbContext is required for SqlServerAdapter");
                }
                return new SqlServerAdapter(context, dynamicTableService, dataService, messageBoxService, subscriptionService, "FromCsvToSqlServerExample", null, null, null, null, null, null, null, null, null, logger);
            });
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR during service configuration: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            // Don't rethrow - allow function app to start
        }
    })
    .Build();

host.Run();

using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker => worker.UseMiddleware<InterfaceConfigurator.Main.Middleware.CorsMiddleware>())
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
                // Enhanced with retry policy and connection pooling for resilience
                var connectionString = $"Server=tcp:{sqlServer},1433;Initial Catalog={sqlDatabase};Persist Security Info=False;User ID={sqlUser};Password={sqlPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Lifetime=0;";
                
                // MessageBox database connection (separate database for staging/messaging)
                // This database contains Messages, MessageSubscriptions, and ProcessLogs tables ONLY
                // TransportData table is NOT created here - it belongs to the main application database
                var messageBoxConnectionString = $"Server=tcp:{sqlServer},1433;Initial Catalog=MessageBox;Persist Security Info=False;User ID={sqlUser};Password={sqlPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Lifetime=0;";
                
                // Register DbContext for main application database (app-database)
                // SqlServerAdapter uses this context to create/write to TransportData table
                // Enhanced with retry-on-failure for transient errors
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        // Enable retry on transient failures (network issues, timeouts, etc.)
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null); // Use default transient error numbers
                        sqlOptions.CommandTimeout(60); // Increase timeout to accommodate retries
                    }));
                
                // Register MessageBoxDbContext for MessageBox database (separate database)
                // Contains Messages, MessageSubscriptions, ProcessLogs - NOT TransportData
                // Enhanced with retry-on-failure for transient errors
                services.AddDbContext<MessageBoxDbContext>(options =>
                    options.UseSqlServer(messageBoxConnectionString, sqlOptions =>
                    {
                        // Enable retry on transient failures
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                        sqlOptions.CommandTimeout(60);
                    }));
                
                // Ensure both databases and tables are created automatically on startup
                services.AddHostedService<MessageBoxDatabaseInitializer>();
                services.AddHostedService<ApplicationDatabaseInitializer>();
            }
            
            // Register Adapter Configuration Service (must be registered before CsvProcessingService)
            // Uses JSON file storage in Blob Storage with in-memory cache
            services.AddSingleton<IAdapterConfigurationService>(sp =>
            {
                var blobServiceClient = sp.GetService<Azure.Storage.Blobs.BlobServiceClient>();
                var logger = sp.GetService<ILogger<AdapterConfigurationService>>();
                var service = new AdapterConfigurationService(blobServiceClient, logger);
                // Initialize on startup (fire and forget) with timeout
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        await service.InitializeAsync(cts.Token);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to initialize adapter configuration service on startup. Will retry on first use.");
                    }
                }, CancellationToken.None);
                return service;
            });
            
            // Register Interface Configuration Service
            services.AddSingleton<IInterfaceConfigurationService>(sp =>
            {
                var blobServiceClient = sp.GetService<Azure.Storage.Blobs.BlobServiceClient>();
                var logger = sp.GetService<ILogger<InterfaceConfigurator.Main.Services.InterfaceConfigurationService>>();
                var service = new InterfaceConfigurator.Main.Services.InterfaceConfigurationService(blobServiceClient, logger);
                // Initialize on startup (fire and forget) with timeout
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        await service.InitializeAsync(cts.Token);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to initialize interface configuration service on startup. Will retry on first use.");
                    }
                }, CancellationToken.None);
                return service;
            });
            
            // Register Adapter Factory
            services.AddScoped<IAdapterFactory, InterfaceConfigurator.Main.Services.AdapterFactory>();
            
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
            
            // Register Metrics Service for Application Insights tracking
            // TelemetryClient is automatically injected by Azure Functions when Application Insights is configured
            services.AddSingleton<MetricsService>(sp =>
            {
                // Try to get TelemetryClient from Application Insights (if available)
                // Using object type to avoid dependency on Application Insights package
                object? telemetryClient = null;
                try
                {
                    // Application Insights SDK is automatically configured in Azure Functions
                    // TelemetryClient can be accessed via dependency injection if Application Insights package is added
                    // MetricsService uses reflection to call TelemetryClient methods if available
                    // For now, we'll use null and MetricsService will handle gracefully
                }
                catch
                {
                    // TelemetryClient not available - MetricsService will work without it
                }
                var logger = sp.GetService<ILogger<MetricsService>>();
                return new MetricsService(telemetryClient, logger);
            });
            
            // Register Dead Letter Monitor (only if MessageBox is available)
            services.AddScoped<DeadLetterMonitor>(sp =>
            {
                var messageBoxService = sp.GetService<IMessageBoxService>();
                var logger = sp.GetService<ILogger<DeadLetterMonitor>>();
                if (messageBoxService == null)
                {
                    // Return a no-op implementation if MessageBox is not available
                    return new DeadLetterMonitor(null!, logger);
                }
                return new DeadLetterMonitor(messageBoxService, logger);
            });
            
            // Register Processing Statistics Service (only if MessageBox is available)
            services.AddScoped<ProcessingStatisticsService>(sp =>
            {
                var messageBoxContext = sp.GetService<MessageBoxDbContext>();
                var logger = sp.GetService<ILogger<ProcessingStatisticsService>>();
                if (messageBoxContext == null)
                {
                    // Return a no-op implementation if MessageBox is not available
                    // Note: This might cause issues if the service is actually used, but allows startup
                    logger?.LogWarning("MessageBoxDbContext is not available. ProcessingStatisticsService may not work correctly.");
                    return null!; // Will need to handle null checks where this is used
                }
                return new ProcessingStatisticsService(messageBoxContext, logger);
            });
            
            // Register CSV Validation Service
            services.AddSingleton<CsvValidationService>(sp =>
            {
                var logger = sp.GetService<ILogger<CsvValidationService>>();
                return new CsvValidationService(logger);
            });
            
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
            
            // Register Adapters (only if required dependencies are available)
            // CSV Adapter (for source) - register as named service
            // Note: This will only be instantiated when actually used, not on startup
            services.AddScoped<CsvAdapter>(sp =>
            {
                var csvProcessingService = sp.GetRequiredService<ICsvProcessingService>();
                var adapterConfig = sp.GetRequiredService<IAdapterConfigurationService>();
                var messageBoxService = sp.GetService<IMessageBoxService>();
                var subscriptionService = sp.GetService<IMessageSubscriptionService>();
                var logger = sp.GetService<ILogger<CsvAdapter>>();
                var blobClient = sp.GetService<Azure.Storage.Blobs.BlobServiceClient>();
                if (blobClient == null)
                {
                    logger?.LogError("BlobServiceClient is required for CsvAdapter but is not available. Please configure MainStorageConnection or AzureWebJobsStorage.");
                    throw new InvalidOperationException("BlobServiceClient is required for CsvAdapter. Please configure MainStorageConnection or AzureWebJobsStorage in local.settings.json");
                }
                // Create FileAdapter for default FILE adapter type
                var fileLogger = sp.GetService<ILogger<FileAdapter>>();
                var fileAdapter = new FileAdapter(
                    blobServiceClient,
                    adapterRole: "Source",
                    messageBoxService: messageBoxService,
                    subscriptionService: subscriptionService,
                    interfaceName: "FromCsvToSqlServerExample",
                    adapterInstanceGuid: null,
                    receiveFolder: null,
                    fileMask: null,
                    destinationReceiveFolder: null,
                    destinationFileMask: null,
                    batchSize: null,
                    logger: fileLogger);

                return new CsvAdapter(
                    csvProcessingService: csvProcessingService, 
                    adapterConfig: adapterConfig, 
                    blobServiceClient: blobClient, 
                    messageBoxService: messageBoxService, 
                    subscriptionService: subscriptionService, 
                    interfaceName: "FromCsvToSqlServerExample", 
                    adapterInstanceGuid: null,
                    receiveFolder: null,
                    fileMask: null,
                    batchSize: null,
                    fieldSeparator: null,
                    destinationReceiveFolder: null,
                    destinationFileMask: null,
                    adapterType: null,
                    sftpAdapter: null, // SftpAdapter will be created by AdapterFactory when needed
                    fileAdapter: fileAdapter,
                    adapterRole: "Source",
                    logger: logger);
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
                    logger?.LogWarning("ApplicationDbContext is not available. SqlServerAdapter may not work correctly.");
                    // Still allow registration - will fail when actually used
                    throw new InvalidOperationException("ApplicationDbContext is required for SqlServerAdapter");
                }
                return new SqlServerAdapter(context, dynamicTableService, dataService, messageBoxService, subscriptionService, "FromCsvToSqlServerExample", null, null, null, null, null, null, null, null, null, null, adapterRole: "Destination", logger: logger, statisticsService: null);
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

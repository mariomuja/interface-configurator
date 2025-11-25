using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using InterfaceConfigurator.Adapters;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Core.Factories;
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
                
                // InterfaceConfigDb database connection (formerly MessageBox)
                // This database contains InterfaceConfigurations, AdapterInstances, and ProcessLogs tables
                // TransportData table is NOT created here - it belongs to the main application database
                // Note: Messaging is now handled via Azure Service Bus, not this database
                var interfaceConfigConnectionString = $"Server=tcp:{sqlServer},1433;Initial Catalog=InterfaceConfigDb;Persist Security Info=False;User ID={sqlUser};Password={sqlPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Lifetime=0;";
                
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
                
                // Register InterfaceConfigDbContext for InterfaceConfigDb database (formerly MessageBox)
                // Contains InterfaceConfigurations, AdapterInstances, ProcessLogs - NOT TransportData
                // Enhanced with retry-on-failure for transient errors
                services.AddDbContext<InterfaceConfigDbContext>(options =>
                    options.UseSqlServer(interfaceConfigConnectionString, sqlOptions =>
                    {
                        // Enable retry on transient failures
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                        sqlOptions.CommandTimeout(60);
                    }));
                
                // Ensure both databases and tables are created automatically on startup
                services.AddHostedService<InterfaceConfigDatabaseInitializer>();
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
            
            // Register Interface Configuration Service (now using InterfaceConfigDbContext instead of Blob Storage)
            services.AddScoped<IInterfaceConfigurationService>(sp =>
            {
                var interfaceConfigContext = sp.GetService<InterfaceConfigDbContext>();
                var logger = sp.GetService<ILogger<InterfaceConfigurator.Main.Services.InterfaceConfigurationServiceV2>>();
                if (interfaceConfigContext == null)
                {
                    // Fallback to Blob Storage if InterfaceConfigDb is not available
                    var blobServiceClient = sp.GetService<Azure.Storage.Blobs.BlobServiceClient>();
                    var fallbackLogger = sp.GetService<ILogger<InterfaceConfigurator.Main.Services.InterfaceConfigurationService>>();
                    return new InterfaceConfigurator.Main.Services.InterfaceConfigurationService(blobServiceClient, fallbackLogger, sp);
                }
                return new InterfaceConfigurator.Main.Services.InterfaceConfigurationServiceV2(interfaceConfigContext, logger, sp);
            });
            
            // Register Interface Config Service (replaces IMessageBoxService for adapter instance management)
            services.AddScoped<IInterfaceConfigService>(sp =>
            {
                var interfaceConfigContext = sp.GetService<InterfaceConfigDbContext>();
                var logger = sp.GetService<ILogger<InterfaceConfigService>>();
                if (interfaceConfigContext == null)
                {
                    return null!;
                }
                return new InterfaceConfigService(interfaceConfigContext, logger);
            });
            
            // Register Service Bus Lock Tracking Service
            services.AddScoped<IServiceBusLockTrackingService>(sp =>
            {
                var interfaceConfigContext = sp.GetService<InterfaceConfigDbContext>();
                var logger = sp.GetService<ILogger<ServiceBusLockTrackingService>>();
                if (interfaceConfigContext == null)
                {
                    return null!;
                }
                return new ServiceBusLockTrackingService(interfaceConfigContext, logger);
            });
            
            // Register Service Bus Receiver Cache for efficient lock renewal
            services.AddSingleton<IServiceBusReceiverCache>(sp =>
            {
                var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString")
                    ?? Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING") ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(serviceBusConnectionString))
                {
                    throw new InvalidOperationException("Service Bus connection string is required for receiver cache");
                }
                
                var serviceBusClient = new Azure.Messaging.ServiceBus.ServiceBusClient(serviceBusConnectionString);
                var logger = sp.GetService<ILogger<ServiceBusReceiverCache>>();
                return new ServiceBusReceiverCache(serviceBusClient, logger);
            });
            
            // Register Service Bus Lock Renewal Background Service
            services.AddHostedService<ServiceBusLockRenewalService>();
            
            // Register Service Bus Dead Letter Monitoring Background Service
            services.AddHostedService<ServiceBusDeadLetterMonitoringService>();
            
            // Register Configuration Validation Service
            services.AddSingleton<IConfigurationValidationService, ConfigurationValidationService>();
            
            // Register Retry Policy
            services.AddSingleton<IRetryPolicy>(sp =>
            {
                var logger = sp.GetService<ILogger<ExponentialBackoffRetryPolicy>>();
                return new ExponentialBackoffRetryPolicy(
                    maxRetryAttempts: 3,
                    baseDelay: TimeSpan.FromSeconds(1),
                    maxDelay: TimeSpan.FromSeconds(30),
                    logger: logger);
            });
            
            // Register Rate Limiter
            services.AddSingleton<IRateLimiter>(sp =>
            {
                var logger = sp.GetService<ILogger<TokenBucketRateLimiter>>();
                var config = new RateLimitConfig
                {
                    MaxRequests = 100,
                    TimeWindow = TimeSpan.FromMinutes(1),
                    Identifier = "default"
                };
                return new TokenBucketRateLimiter(config, logger);
            });
            
            // Register Batch Processing Service
            services.AddSingleton<BatchProcessingService>(sp =>
            {
                var logger = sp.GetService<ILogger<BatchProcessingService>>();
                return new BatchProcessingService(
                    batchSize: 100,
                    batchTimeout: TimeSpan.FromSeconds(5),
                    logger: logger);
            });
            
            // Register Cached Configuration Service
            services.AddSingleton<CachedConfigurationService>(sp =>
            {
                var memoryCache = sp.GetRequiredService<IMemoryCache>();
                var logger = sp.GetService<ILogger<CachedConfigurationService>>();
                return new CachedConfigurationService(
                    memoryCache,
                    defaultCacheExpiration: TimeSpan.FromMinutes(15),
                    logger: logger);
            });
            
            // Register Memory Cache if not already registered
            if (!services.Any(s => s.ServiceType == typeof(IMemoryCache)))
            {
                services.AddMemoryCache();
            }
            
            // Register Adapter Factory
            services.AddScoped<IAdapterFactory, InterfaceConfigurator.Main.Services.AdapterFactory>();
            
            // Register Core Services
            services.AddScoped<ICsvProcessingService, CsvProcessingService>();
            
            // Register Event Queue (in-memory)
            services.AddSingleton<IEventQueue, InMemoryEventQueue>();
            
            // Register Service Bus Service (primary message communication)
            services.AddScoped<IServiceBusService>(sp =>
            {
                var connectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString")
                    ?? Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING");
                var logger = sp.GetService<ILogger<ServiceBusService>>();
                
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    logger?.LogWarning("Service Bus connection string not configured. Service Bus functionality will be disabled.");
                    return null!;
                }
                
                var serviceProvider = sp.GetService<IServiceProvider>();
                return new ServiceBusService(connectionString, logger, serviceProvider);
            });
            
            // Register Service Bus Subscription Service (manages subscriptions for destination adapters)
            services.AddScoped<IServiceBusSubscriptionService>(sp =>
            {
                var connectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString")
                    ?? Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING");
                var logger = sp.GetService<ILogger<ServiceBusSubscriptionService>>();
                
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    logger?.LogWarning("Service Bus connection string not configured. Subscription management will be disabled.");
                    return null!;
                }
                
                return new ServiceBusSubscriptionService(connectionString, logger);
            });
            
            // Register SQL Server Logging Service (uses InterfaceConfigDb database)
            // Note: ILoggingService is now registered via FeatureFactory above
            // This registration is removed - the factory handles it
            
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
            
            // Register Processing Statistics Service (uses InterfaceConfigDb database)
            services.AddScoped<ProcessingStatisticsService>(sp =>
            {
                var interfaceConfigContext = sp.GetService<InterfaceConfigDbContext>();
                var logger = sp.GetService<ILogger<ProcessingStatisticsService>>();
                if (interfaceConfigContext == null)
                {
                    // Return a no-op implementation if InterfaceConfigDb is not available
                    // Note: This might cause issues if the service is actually used, but allows startup
                    logger?.LogWarning("InterfaceConfigDbContext is not available. ProcessingStatisticsService may not work correctly.");
                    return null!; // Will need to handle null checks where this is used
                }
                return new ProcessingStatisticsService(interfaceConfigContext, logger);
            });
            
            // Register CSV Validation Service
            services.AddSingleton<CsvValidationService>(sp =>
            {
                var logger = sp.GetService<ILogger<CsvValidationService>>();
                return new CsvValidationService(logger);
            });
            
            // Register JQ Transformation Service
            services.AddScoped<JQTransformationService>(sp =>
            {
                var logger = sp.GetService<ILogger<JQTransformationService>>();
                return new JQTransformationService(logger);
            });
            
            // Register Adapter Services (these handle null DbContext gracefully)
            // Note: IDataService is now registered via FeatureFactory below
            // This registration is removed - the factory handles it
            services.AddScoped<IDynamicTableService, DynamicTableService>();
            
            // Register AI Error Analysis and Auto-Fix Services
            services.AddScoped<ErrorAnalysisService>();
            services.AddScoped<AutoFixService>();
            services.AddScoped<AutoTestService>();
            
            // Register Feature Management and Authentication Services
            services.AddScoped<FeatureService>();
            services.AddScoped<AuthService>();
            
            // Register Container App Service for dynamic container app management
            services.AddScoped<IContainerAppService>(sp =>
            {
                var logger = sp.GetService<ILogger<ContainerAppService>>();
                var configuration = sp.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
                return new ContainerAppService(logger!, configuration!);
            });
            
            // Register Feature Factory Infrastructure
            services.AddMemoryCache(); // Required for FeatureRegistry caching
            services.AddScoped<IFeatureRegistry, FeatureRegistry>();
            
            // Register Feature Factories for Services
            // Feature #5: Enhanced DataService (example - adjust feature number as needed)
            services.AddFeatureFactory<IDataService, DataServiceAdapter, DataServiceAdapterV2>(featureNumber: 5);
            
            // Feature #6: Enhanced LoggingService (example - adjust feature number as needed)
            services.AddScoped<ILoggingService>(sp =>
            {
                var featureRegistry = sp.GetRequiredService<IFeatureRegistry>();
                var interfaceConfigContext = sp.GetService<InterfaceConfigDbContext>();
                var logger = sp.GetService<ILogger<SqlServerLoggingService>>();
                var loggerV2 = sp.GetService<ILogger<SqlServerLoggingServiceV2>>();
                
                if (interfaceConfigContext == null)
                {
                    var inMemoryLogger = sp.GetService<ILogger<InMemoryLoggingService>>();
                    return new InMemoryLoggingService(inMemoryLogger);
                }
                
                // Check if feature is enabled (synchronous check with cache)
                var isEnabled = featureRegistry.IsFeatureEnabledAsync(6).GetAwaiter().GetResult();
                
                if (isEnabled)
                {
                    return new SqlServerLoggingServiceV2(interfaceConfigContext, loggerV2);
                }
                else
                {
                    return new SqlServerLoggingService(interfaceConfigContext, logger);
                }
            });
            
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
                var serviceBusService = sp.GetService<IServiceBusService>();
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
                    blobClient,
                    serviceBusService: serviceBusService,
                    adapterRole: "Source",
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
                    serviceBusService: serviceBusService,
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
                var serviceBusService = sp.GetService<IServiceBusService>();
                var logger = sp.GetService<ILogger<SqlServerAdapter>>();
                if (context == null)
                {
                    logger?.LogWarning("ApplicationDbContext is not available. SqlServerAdapter may not work correctly.");
                    // Still allow registration - will fail when actually used
                    throw new InvalidOperationException("ApplicationDbContext is required for SqlServerAdapter");
                }
                return new SqlServerAdapter(context, dynamicTableService, dataService, serviceBusService, "FromCsvToSqlServerExample", null, null, null, null, null, null, null, null, null, null, adapterRole: "Destination", logger: logger, statisticsService: null);
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

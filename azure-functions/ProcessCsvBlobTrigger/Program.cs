using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Processors;
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
                var connectionString = $"Server=tcp:{sqlServer},1433;Initial Catalog={sqlDatabase};Persist Security Info=False;User ID={sqlUser};Password={sqlPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
                
                // Register DbContext
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(connectionString));
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
            
            // Register Core Services
            services.AddScoped<ICsvProcessingService, CsvProcessingService>();
            
            // Register In-Memory Logging Service (no SQL dependency)
            services.AddSingleton<IInMemoryLoggingService, InMemoryLoggingService>();
            services.AddSingleton<ILoggingService>(sp => sp.GetRequiredService<IInMemoryLoggingService>());
            
            // Register Adapter Services (these handle null DbContext gracefully)
            services.AddScoped<IDataService, DataServiceAdapter>();
            services.AddScoped<IDynamicTableService, DynamicTableService>();
            
            // Register Error Row Service (requires BlobServiceClient)
            var storageConnectionString = Environment.GetEnvironmentVariable("MainStorageConnection") 
                ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            if (!string.IsNullOrEmpty(storageConnectionString))
            {
                var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(storageConnectionString);
                services.AddSingleton(blobServiceClient);
                services.AddScoped<IErrorRowService, ErrorRowService>();
            }
            else
            {
                Console.WriteLine("WARNING: Storage connection string not set. Error row service may not work.");
            }
            
            // Register Processor - using new row-by-row processor
            services.AddScoped<ICsvProcessor, ProcessCsvBlobTrigger.Core.Processors.CsvProcessor>();
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

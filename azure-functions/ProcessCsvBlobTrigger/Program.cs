using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            
            // Register Core Services
            services.AddScoped<ICsvProcessingService, CsvProcessingService>();
            
            // Register Adapter Services (these handle null DbContext gracefully)
            services.AddScoped<ILoggingService, LoggingServiceAdapter>();
            services.AddScoped<IDataService, DataServiceAdapter>();
            
            // Register Processor
            services.AddScoped<ICsvProcessor, CsvProcessor>();
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

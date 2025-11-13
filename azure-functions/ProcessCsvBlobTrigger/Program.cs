using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Processors;
using ProcessCsvBlobTrigger.Core.Services;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Services;

try
{
    var host = new HostBuilder()
        .ConfigureFunctionsWorkerDefaults()
        .ConfigureServices(services =>
        {
            // Configure Entity Framework Core
            var sqlServer = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER");
            var sqlDatabase = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE");
            var sqlUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
            var sqlPassword = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");

            if (string.IsNullOrEmpty(sqlServer) || string.IsNullOrEmpty(sqlDatabase) || 
                string.IsNullOrEmpty(sqlUser) || string.IsNullOrEmpty(sqlPassword))
            {
                throw new InvalidOperationException(
                    "Missing required SQL connection settings. " +
                    $"Server: {sqlServer}, Database: {sqlDatabase}, User: {sqlUser}, Password: {(string.IsNullOrEmpty(sqlPassword) ? "MISSING" : "SET")}");
            }

            var connectionString = $"Server={sqlServer};" +
                                  $"Database={sqlDatabase};" +
                                  $"User Id={sqlUser};" +
                                  $"Password={sqlPassword};" +
                                  "Encrypt=True;TrustServerCertificate=False;";

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            // Register Core services
            services.AddScoped<ProcessCsvBlobTrigger.Core.Interfaces.ICsvProcessingService, ProcessCsvBlobTrigger.Core.Services.CsvProcessingService>();
            services.AddScoped<ICsvProcessor, CsvProcessor>();

            // Register adapters (bridge between Core and Function App)
            // LoggingServiceAdapter requires both ApplicationDbContext and ILogger
            services.AddScoped<ILoggingService>(sp => 
                new LoggingServiceAdapter(
                    sp.GetRequiredService<ApplicationDbContext>(),
                    sp.GetService<ILogger<LoggingServiceAdapter>>()));
            services.AddScoped<IDataService>(sp =>
                new DataServiceAdapter(
                    sp.GetRequiredService<ApplicationDbContext>(),
                    sp.GetService<ILoggingService>(),
                    sp.GetService<ILogger<DataServiceAdapter>>()));

            // Register Function App services (for backward compatibility if needed)
            services.AddScoped<LoggingService>();
        })
        .Build();

    host.Run();
}
catch (Exception ex)
{
    // Log error to console so it appears in Azure logs
    Console.Error.WriteLine($"Fatal error starting Function App: {ex.Message}");
    Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
    if (ex.InnerException != null)
    {
        Console.Error.WriteLine($"Inner exception: {ex.InnerException.Message}");
    }
    throw;
}

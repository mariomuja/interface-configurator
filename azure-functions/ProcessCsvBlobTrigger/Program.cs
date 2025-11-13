using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

            // Register services
            services.AddScoped<CsvProcessingService>();
            services.AddScoped<DataService>();
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


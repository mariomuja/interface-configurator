using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Configure Entity Framework Core
        var connectionString = $"Server={Environment.GetEnvironmentVariable("AZURE_SQL_SERVER")};" +
                              $"Database={Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE")};" +
                              $"User Id={Environment.GetEnvironmentVariable("AZURE_SQL_USER")};" +
                              $"Password={Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD")};" +
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


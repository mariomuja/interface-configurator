using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Helpers;

namespace InterfaceConfigurator.Main;

public class StartTransport
{
    private readonly IInterfaceConfigurationService _configService;
    private readonly ILogger<StartTransport> _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public StartTransport(
        IInterfaceConfigurationService configService,
        ILogger<StartTransport> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Initialize BlobServiceClient
        var connectionString = Environment.GetEnvironmentVariable("MainStorageConnection") 
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Storage connection string not found. Blob upload will not work.");
            _blobServiceClient = null!;
        }
        else
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
        }
    }

    [Function("StartTransport")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options", Route = "start-transport")] HttpRequestData req,
        FunctionContext executionContext)
    {
        _logger.LogInformation("StartTransport function triggered");

        // Handle CORS preflight requests
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var optionsResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            // Set CORS headers explicitly
            optionsResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            optionsResponse.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, PATCH");
            optionsResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
            optionsResponse.Headers.Add("Access-Control-Max-Age", "3600");
            return optionsResponse;
        }

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var request = JsonSerializer.Deserialize<StartTransportRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var interfaceName = string.IsNullOrWhiteSpace(request?.InterfaceName)
                ? "FromCsvToSqlServerExample"
                : request.InterfaceName!.Trim();

            // Ensure interface configuration exists
            await EnsureInterfaceConfigurationAsync(interfaceName, executionContext.CancellationToken);

            // Get CSV content from request or generate sample data
            string csvContent;
            if (!string.IsNullOrWhiteSpace(request?.CsvContent))
            {
                csvContent = request.CsvContent;
                _logger.LogInformation("Using CSV content from request body");
            }
            else
            {
                csvContent = GenerateSampleCsvData();
                _logger.LogInformation("Using generated sample CSV data");
            }

            // Upload CSV to blob storage
            var fileName = await UploadCsvToBlobStorageAsync(csvContent, executionContext.CancellationToken);

            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            // Set CORS headers explicitly
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, PATCH");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
            response.Headers.Add("Access-Control-Max-Age", "3600");

            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                message = "CSV file uploaded and transport pipeline triggered. The source adapter will process the data and forward it via MessageBox to all enabled destination adapters.",
                fileId = fileName
            }));

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting transport");
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json; charset=utf-8");
            // Set CORS headers explicitly
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            errorResponse.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS, PATCH");
            errorResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
            errorResponse.Headers.Add("Access-Control-Max-Age", "3600");
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new
            {
                error = "Failed to start transport",
                details = ex.Message
            }));
            return errorResponse;
        }
    }

    private async Task EnsureInterfaceConfigurationAsync(string interfaceName, CancellationToken cancellationToken)
    {
        var config = await _configService.GetConfigurationAsync(interfaceName, cancellationToken);
        if (config != null)
        {
            _logger.LogInformation("Interface configuration '{InterfaceName}' already exists", interfaceName);
            return;
        }

        _logger.LogInformation("Creating interface configuration '{InterfaceName}'", interfaceName);
        
        // Create default configuration
        var newConfig = new InterfaceConfigurator.Main.Core.Models.InterfaceConfiguration
        {
            InterfaceName = interfaceName,
            SourceAdapterName = "CSV",
            SourceConfiguration = JsonSerializer.Serialize(new { source = "csv-files/csv-incoming", enabled = true }),
            DestinationAdapterName = "SqlServer",
            DestinationConfiguration = JsonSerializer.Serialize(new { destination = "TransportData", enabled = true }),
            SourceIsEnabled = false,
            DestinationIsEnabled = false,
            SourceInstanceName = "Source",
            DestinationInstanceName = "Destination",
            SourceAdapterInstanceGuid = Guid.NewGuid(),
            DestinationAdapterInstanceGuid = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        await _configService.SaveConfigurationAsync(newConfig, cancellationToken);
        _logger.LogInformation("Interface configuration '{InterfaceName}' created", interfaceName);
    }

    private async Task<string> UploadCsvToBlobStorageAsync(string csvContent, CancellationToken cancellationToken)
    {
        if (_blobServiceClient == null)
        {
            throw new InvalidOperationException("BlobServiceClient is not initialized. Check storage connection string configuration.");
        }

        const string containerName = "csv-files";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Ensure container exists
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // Generate filename with timestamp
        var now = DateTime.UtcNow;
        var fileName = $"transport-{now:yyyy}_{now:MM}_{now:dd}_{now:HH}_{now:mm}_{now:ss}_{now:fff}.csv";
        var blobPath = $"csv-incoming/{fileName}";

        var blobClient = containerClient.GetBlobClient(blobPath);
        var csvBytes = Encoding.UTF8.GetBytes(csvContent);
        
        await blobClient.UploadAsync(
            new MemoryStream(csvBytes),
            new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
                {
                    ContentType = "text/csv"
                }
            },
            cancellationToken);

        _logger.LogInformation("CSV file uploaded to blob storage: {BlobPath}", blobPath);
        return fileName;
    }

    private string GenerateSampleCsvData()
    {
        var fieldSeparator = Environment.GetEnvironmentVariable("CsvFieldSeparator") ?? "║";
        var headers = new[] { "id", "name", "email", "age", "city", "salary" };
        var rows = new List<string> { string.Join(fieldSeparator, headers) };

        var names = new[] { "Max Mustermann", "Anna Schmidt", "Peter Müller", "Lisa Weber", "Thomas Fischer" };
        var cities = new[] { "Berlin", "München", "Hamburg", "Köln", "Frankfurt" };
        var random = new Random();

        for (int i = 1; i <= 50; i++)
        {
            var name = names[random.Next(names.Length)];
            var city = cities[random.Next(cities.Length)];
            var age = random.Next(20, 60);
            var salary = random.Next(30000, 80000);
            var email = $"user{i}@example.com";

            rows.Add($"{i}{fieldSeparator}{name} {i}{fieldSeparator}{email}{fieldSeparator}{age}{fieldSeparator}{city}{fieldSeparator}{salary}");
        }

        return string.Join("\n", rows);
    }

    private class StartTransportRequest
    {
        [JsonPropertyName("interfaceName")]
        public string? InterfaceName { get; set; }

        [JsonPropertyName("csvContent")]
        public string? CsvContent { get; set; }
    }
}


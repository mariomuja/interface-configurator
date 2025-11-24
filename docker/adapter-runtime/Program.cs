using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdapterRuntime;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        
        // Configure logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();
        
        // Get configuration from environment variables
        var adapterInstanceGuid = Environment.GetEnvironmentVariable("ADAPTER_INSTANCE_GUID") ?? "";
        var adapterName = Environment.GetEnvironmentVariable("ADAPTER_NAME") ?? "";
        var adapterType = Environment.GetEnvironmentVariable("ADAPTER_TYPE") ?? "";
        var interfaceName = Environment.GetEnvironmentVariable("INTERFACE_NAME") ?? "";
        var instanceName = Environment.GetEnvironmentVariable("INSTANCE_NAME") ?? "";
        var blobConnectionString = Environment.GetEnvironmentVariable("BLOB_CONNECTION_STRING") ?? "";
        var blobContainerName = Environment.GetEnvironmentVariable("BLOB_CONTAINER_NAME") ?? "";
        var adapterConfigPath = Environment.GetEnvironmentVariable("ADAPTER_CONFIG_PATH") ?? "adapter-config.json";
        var serviceBusConnectionString = Environment.GetEnvironmentVariable("AZURE_SERVICEBUS_CONNECTION_STRING") ?? "";
        
        var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Adapter Runtime starting...");
        logger.LogInformation("Adapter Instance GUID: {Guid}", adapterInstanceGuid);
        logger.LogInformation("Adapter Name: {Name}", adapterName);
        logger.LogInformation("Adapter Type: {Type}", adapterType);
        logger.LogInformation("Interface Name: {Interface}", interfaceName);
        logger.LogInformation("Instance Name: {Instance}", instanceName);
        
        // Load adapter configuration from blob storage
        if (!string.IsNullOrWhiteSpace(blobConnectionString) && !string.IsNullOrWhiteSpace(blobContainerName))
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(blobConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
                var blobClient = containerClient.GetBlobClient(adapterConfigPath);
                
                if (await blobClient.ExistsAsync())
                {
                    var response = await blobClient.DownloadContentAsync();
                    var configJson = response.Value.Content.ToString();
                    logger.LogInformation("Loaded adapter configuration from {Path}", adapterConfigPath);
                    logger.LogDebug("Configuration: {Config}", configJson);
                }
                else
                {
                    logger.LogWarning("Adapter configuration file not found: {Path}", adapterConfigPath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error loading adapter configuration from blob storage");
            }
        }
        else
        {
            logger.LogWarning("Blob storage connection string or container name not provided");
        }
        
        // Register adapter service based on adapter name
        // This would need to be implemented based on the adapter type
        // For now, just log that we're ready
        logger.LogInformation("Adapter Runtime ready. Waiting for configuration updates...");
        
        var host = builder.Build();
        host.Run();
    }
}


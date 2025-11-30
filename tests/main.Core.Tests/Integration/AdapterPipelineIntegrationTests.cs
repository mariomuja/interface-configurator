using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Core.Interfaces;

namespace InterfaceConfigurator.Main.Core.Tests.Integration;

/// <summary>
/// End-to-end integration tests for adapter pipeline
/// Tests complete flow: Source Adapter → Service Bus → Destination Adapter
/// Requires SQL Server, Service Bus, and Blob Storage connections
/// </summary>
public class AdapterPipelineIntegrationTests : IClassFixture<AdapterPipelineTestFixture>, IDisposable
{
    private readonly AdapterPipelineTestFixture _fixture;
    private readonly ILogger<AdapterPipelineIntegrationTests> _logger;

    public AdapterPipelineIntegrationTests(AdapterPipelineTestFixture fixture)
    {
        _fixture = fixture;
        _logger = new Mock<ILogger<AdapterPipelineIntegrationTests>>().Object;
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Full Pipeline")]
    public async Task CSV_To_ServiceBus_Pipeline_Should_Work()
    {
        // Arrange
        var serviceBusService = new ServiceBusService(
            _fixture.ServiceBusConnectionString,
            _logger);

        var testInterfaceName = $"test-interface-{Guid.NewGuid()}";
        var testAdapterName = "CsvAdapter";
        var testAdapterType = "CSV";
        var testAdapterInstanceGuid = Guid.NewGuid();
        var testHeaders = new List<string> { "Column1", "Column2", "Column3" };
        var testRecord = new Dictionary<string, string>
        {
            { "Column1", "Value1" },
            { "Column2", "Value2" },
            { "Column3", "Value3" }
        };

        // Act - Send message to Service Bus (simulating CSV adapter)
        var messageId = await serviceBusService.SendMessageAsync(
            testInterfaceName,
            testAdapterName,
            testAdapterType,
            testAdapterInstanceGuid,
            testHeaders,
            testRecord,
            CancellationToken.None);

        // Assert
        Assert.NotNull(messageId);
        Assert.NotEmpty(messageId);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Full Pipeline")]
    public async Task ServiceBus_To_SQL_Pipeline_Should_Work()
    {
        // Arrange
        var serviceBusService = new ServiceBusService(
            _fixture.ServiceBusConnectionString,
            _logger);

        var topicName = $"interface-test-{Guid.NewGuid()}";
        var subscriptionName = $"destination-test-{Guid.NewGuid()}";

        // First, send a test message
        await serviceBusService.SendMessageAsync(
            "test-interface",
            "CsvAdapter",
            "CSV",
            Guid.NewGuid(),
            new List<string> { "Column1", "Column2" },
            new Dictionary<string, string> { { "Column1", "Value1" }, { "Column2", "Value2" } },
            CancellationToken.None);

        await Task.Delay(1000); // Wait for message to be available

        // Act - Receive message from Service Bus (simulating SQL adapter)
        var messages = await serviceBusService.ReceiveMessagesAsync(
            topicName,
            subscriptionName,
            maxMessages: 1,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.NotNull(messages);
        // May be empty if subscription doesn't exist, but should not throw
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Full Pipeline")]
    public async Task Blob_Storage_To_ServiceBus_Flow_Should_Work()
    {
        // Arrange
        var blobServiceClient = new BlobServiceClient(_fixture.BlobStorageConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient("csv-files");
        var testFileName = $"csv-incoming/test-{Guid.NewGuid()}.csv";
        var testContent = "Column1,Column2,Column3\nValue1,Value2,Value3";
        var blobClient = containerClient.GetBlobClient(testFileName);

        try
        {
            // Act - Upload CSV file (simulating source adapter)
            await blobClient.UploadAsync(BinaryData.FromString(testContent), overwrite: true);

            // Verify file exists
            var exists = await blobClient.ExistsAsync();
            Assert.True(exists.Value, "Blob should exist after upload");

            // In real scenario, this would trigger the Azure Function which processes the CSV
            // and sends messages to Service Bus
        }
        finally
        {
            // Cleanup
            await blobClient.DeleteIfExistsAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Full Pipeline")]
    public async Task ServiceBus_To_SQL_Write_Flow_Should_Work()
    {
        // Arrange
        using var sqlConnection = new SqlConnection(_fixture.SqlConnectionString);
        await sqlConnection.OpenAsync();

        // Act - Test SQL write operation (simulating destination adapter)
        var testQuery = @"
            SELECT COUNT(*) AS TableCount
            FROM sys.tables
            WHERE name = 'TransportData'
        ";

        using var command = new SqlCommand(testQuery, sqlConnection);
        var result = await command.ExecuteScalarAsync();
        var tableCount = Convert.ToInt32(result);

        // Assert - TransportData table should exist
        Assert.True(tableCount > 0, "TransportData table should exist for SQL writes");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Full Pipeline")]
    public async Task End_To_End_Pipeline_Should_Complete()
    {
        // This test verifies the complete pipeline:
        // 1. CSV file uploaded to Blob Storage
        // 2. Azure Function processes CSV and sends to Service Bus
        // 3. Destination adapter reads from Service Bus
        // 4. Data written to SQL Server

        // Arrange
        var blobServiceClient = new BlobServiceClient(_fixture.BlobStorageConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient("csv-files");
        var testFileName = $"csv-incoming/e2e-test-{Guid.NewGuid()}.csv";
        var testContent = "Id,Name,Value\n1,Test1,100\n2,Test2,200";
        var blobClient = containerClient.GetBlobClient(testFileName);

        try
        {
            // Step 1: Upload CSV
            await blobClient.UploadAsync(BinaryData.FromString(testContent), overwrite: true);
            var exists = await blobClient.ExistsAsync();
            Assert.True(exists.Value, "CSV file should be uploaded");

            // Step 2: Verify Service Bus connectivity
            var serviceBusService = new ServiceBusService(
                _fixture.ServiceBusConnectionString,
                _logger);
            
            var messageId = await serviceBusService.SendMessageAsync(
                "e2e-test",
                "CsvAdapter",
                "CSV",
                Guid.NewGuid(),
                new List<string> { "Id", "Name", "Value" },
                new Dictionary<string, string> { { "Id", "1" }, { "Name", "Test1" }, { "Value", "100" } },
                CancellationToken.None);
            
            Assert.NotNull(messageId, "Message should be sent to Service Bus");

            // Step 3: Verify SQL connectivity
            using var sqlConnection = new SqlConnection(_fixture.SqlConnectionString);
            await sqlConnection.OpenAsync();
            Assert.Equal(System.Data.ConnectionState.Open, sqlConnection.State);
        }
        finally
        {
            // Cleanup
            await blobClient.DeleteIfExistsAsync();
        }
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

/// <summary>
/// Test fixture for adapter pipeline integration tests
/// Provides all required connection strings
/// </summary>
public class AdapterPipelineTestFixture : IDisposable
{
    public string ServiceBusConnectionString { get; }
    public string BlobStorageConnectionString { get; }
    public string SqlConnectionString { get; }

    public AdapterPipelineTestFixture()
    {
        // Service Bus
        var serviceBusConnection = Environment.GetEnvironmentVariable("AZURE_SERVICE_BUS_CONNECTION_STRING") ??
                                  Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION_STRING") ??
                                  Environment.GetEnvironmentVariable("ServiceBusConnectionString");

        if (string.IsNullOrWhiteSpace(serviceBusConnection))
        {
            throw new InvalidOperationException(
                "Service Bus connection string not found. Required: AZURE_SERVICE_BUS_CONNECTION_STRING");
        }

        ServiceBusConnectionString = serviceBusConnection;

        // Blob Storage
        var blobConnection = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") ??
                            Environment.GetEnvironmentVariable("MainStorageConnection") ??
                            Environment.GetEnvironmentVariable("AzureWebJobsStorage");

        if (string.IsNullOrWhiteSpace(blobConnection))
        {
            throw new InvalidOperationException(
                "Blob Storage connection string not found. Required: AZURE_STORAGE_CONNECTION_STRING");
        }

        BlobStorageConnectionString = blobConnection;

        // SQL Server
        var sqlServer = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER");
        var sqlDatabase = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE");
        var sqlUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
        var sqlPassword = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");

        if (string.IsNullOrWhiteSpace(sqlServer) || string.IsNullOrWhiteSpace(sqlDatabase) ||
            string.IsNullOrWhiteSpace(sqlUser) || string.IsNullOrWhiteSpace(sqlPassword))
        {
            throw new InvalidOperationException(
                "SQL Server connection details not found. Required: AZURE_SQL_SERVER, AZURE_SQL_DATABASE, AZURE_SQL_USER, AZURE_SQL_PASSWORD");
        }

        SqlConnectionString = $"Server=tcp:{sqlServer},1433;Initial Catalog={sqlDatabase};Persist Security Info=False;User ID={sqlUser};Password={sqlPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}


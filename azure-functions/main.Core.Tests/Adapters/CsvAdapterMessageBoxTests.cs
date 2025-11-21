using Azure.Storage.Blobs;
using Azure.Storage.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using Xunit;
using System.Linq;
using System.Reflection;

namespace InterfaceConfigurator.Main.Core.Tests.Adapters;

/// <summary>
/// Unit tests for CsvAdapter MessageBox integration
/// Tests detailed communication with MessageBox when adapter is used as Source or Destination
/// </summary>
public class CsvAdapterMessageBoxTests : IDisposable
{
    private readonly MessageBoxDbContext _context;
    private readonly IMessageBoxService _messageBoxService;
    private readonly Mock<ICsvProcessingService> _mockCsvProcessingService;
    private readonly Mock<IAdapterConfigurationService> _mockAdapterConfig;
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<ILogger<CsvAdapter>> _mockLogger;
    private const string InterfaceName = "FromCsvToSqlServerExample";

    public CsvAdapterMessageBoxTests()
    {
        // Use in-memory database for testing
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<MessageBoxDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MessageBoxDbContext(options);
        var logger = new Mock<ILogger<MessageBoxService>>();
        var mockEventQueue = new Mock<IEventQueue>();
        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _messageBoxService = new MessageBoxService(_context, mockEventQueue.Object, mockSubscriptionService.Object, logger.Object);
        _mockCsvProcessingService = new Mock<ICsvProcessingService>();
        _mockAdapterConfig = new Mock<IAdapterConfigurationService>();
        _mockBlobServiceClient = new Mock<BlobServiceClient>();
        _mockLogger = new Mock<ILogger<CsvAdapter>>();

        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CsvAdapter_ReadAsync_AsSource_ShouldWriteToMessageBox()
    {
        // Arrange
        var headers = new List<string> { "Name", "Age", "City" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" }, { "Age", "30" }, { "City", "New York" } },
            new() { { "Name", "Jane" }, { "Age", "25" }, { "City", "London" } }
        };

        // Mock blob client chain - simplified approach
        // Since we're testing MessageBox integration and mocking CsvProcessingService,
        // we just need to ensure blob operations don't throw
        var mockBlobClient = new Mock<Azure.Storage.Blobs.BlobClient>();
        var mockContainerClient = new Mock<Azure.Storage.Blobs.BlobContainerClient>();
        
        // Mock ExistsAsync to return true
        var mockExistsResponse = new Mock<Azure.Response<bool>>();
        mockExistsResponse.Setup(r => r.Value).Returns(true);
        mockBlobClient.Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse.Object);
        
        // Mock DownloadContentAsync - create a real BlobDownloadResult instance
        var csvContent = "Name║Age║City\nJohn║30║New York\nJane║25║London";
        var binaryData = BinaryData.FromString(csvContent);
        // Create real BlobDownloadResult using reflection since constructor might be internal
        var blobDownloadResult = CreateBlobDownloadResult(binaryData);
        
        var mockDownloadResponse = new Mock<Azure.Response<Azure.Storage.Blobs.Models.BlobDownloadResult>>();
        mockDownloadResponse.Setup(r => r.Value).Returns((Azure.Storage.Blobs.Models.BlobDownloadResult)blobDownloadResult);
        mockBlobClient.Setup(x => x.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDownloadResponse.Object);
        
        mockContainerClient.Setup(x => x.GetBlobClient(It.IsAny<string>())).Returns(mockBlobClient.Object);
        _mockBlobServiceClient.Setup(x => x.GetBlobContainerClient(It.IsAny<string>())).Returns(mockContainerClient.Object);

        _mockCsvProcessingService
            .Setup(x => x.ParseCsvWithHeadersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((headers, records));

        _mockAdapterConfig
            .Setup(x => x.GetCsvFieldSeparatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("║");

        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        var adapterInstanceGuid = Guid.NewGuid();
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            mockSubscriptionService.Object,
            InterfaceName,
            adapterInstanceGuid,
            null, // receiveFolder
            null, // fileMask (defaults to "*.txt")
            null, // batchSize (defaults to 1000)
            null, // fieldSeparator (defaults to "║")
            null, // destinationReceiveFolder
            null, // destinationFileMask (defaults to "*.txt")
            null, // adapterType
            null, // sftpHost
            null, // sftpPort
            null, // sftpUsername
            null, // sftpPassword
            null, // sftpSshKey
            null, // sftpFolder
            null, // sftpFileMask
            null, // sftpMaxConnectionPoolSize
            null, // sftpFileBufferSize
            _mockLogger.Object);

        // Act
        var (readHeaders, readRecords) = await adapter.ReadAsync("csv-files/csv-incoming/test.csv");

        // Assert
        Assert.Equal(headers.Count, readHeaders.Count);
        Assert.Equal(records.Count, readRecords.Count);

        // Verify messages were written to MessageBox (debatching: 2 records = 2 messages)
        var messages = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending");
        Assert.Equal(2, messages.Count); // Debatched into 2 messages
        
        var message = messages[0];
        Assert.Equal("CSV", message.AdapterName);
        Assert.Equal("Source", message.AdapterType);
        Assert.Equal(InterfaceName, message.InterfaceName);
        Assert.Equal(adapterInstanceGuid, message.AdapterInstanceGuid);
        Assert.Equal("Pending", message.Status);

        // Verify data integrity - each message contains a single record
        var (messageHeaders, messageRecord) = _messageBoxService.ExtractDataFromMessage(message);
        Assert.Equal(headers.Count, messageHeaders.Count);
        Assert.NotNull(messageRecord);
        Assert.Equal("John", messageRecord["Name"]);
        Assert.Equal("30", messageRecord["Age"]);
    }

    [Fact]
    public async Task CsvAdapter_WriteAsync_AsDestination_ShouldReadFromMessageBox()
    {
        // Arrange
        var headers = new List<string> { "Name", "Age" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" }, { "Age", "30" } },
            new() { { "Name", "Jane" }, { "Age", "25" } }
        };

        // Create messages in MessageBox (simulating Source adapter - debatching)
        var sourceAdapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            InterfaceName, "SqlServer", "Source", sourceAdapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        _mockAdapterConfig
            .Setup(x => x.GetCsvFieldSeparatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("║");

        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        var adapterInstanceGuid = Guid.NewGuid();
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            mockSubscriptionService.Object,
            InterfaceName,
            adapterInstanceGuid,
            null, // receiveFolder
            null, // fileMask (defaults to "*.txt")
            null, // batchSize (defaults to 1000)
            null, // fieldSeparator (defaults to "║")
            null, // destinationReceiveFolder
            null, // destinationFileMask (defaults to "*.txt")
            null, // adapterType
            null, // sftpHost
            null, // sftpPort
            null, // sftpUsername
            null, // sftpPassword
            null, // sftpSshKey
            null, // sftpFolder
            null, // sftpFileMask
            null, // sftpMaxConnectionPoolSize
            null, // sftpFileBufferSize
            _mockLogger.Object);

        // Act
        await adapter.WriteAsync("csv-files/csv-outgoing/output.csv", headers, records);

        // Assert
        // Verify message was read and marked as processed
        var processedMessage = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.NotNull(processedMessage);
        Assert.Equal("Processed", processedMessage.Status);
        Assert.NotNull(processedMessage.datetime_processed);
        Assert.Contains("Written to CSV destination", processedMessage.ProcessingDetails ?? "");
    }

    [Fact]
    public async Task CsvAdapter_WriteAsync_AsDestination_WithNoPendingMessages_ShouldHandleGracefully()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" } }
        };

        _mockAdapterConfig
            .Setup(x => x.GetCsvFieldSeparatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("║");

        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        var adapterInstanceGuid = Guid.NewGuid();
        var adapter = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            mockSubscriptionService.Object,
            InterfaceName,
            adapterInstanceGuid,
            null, // receiveFolder
            null, // fileMask (defaults to "*.txt")
            null, // batchSize (defaults to 1000)
            null, // fieldSeparator (defaults to "║")
            null, // destinationReceiveFolder
            null, // destinationFileMask (defaults to "*.txt")
            null, // adapterType
            null, // sftpHost
            null, // sftpPort
            null, // sftpUsername
            null, // sftpPassword
            null, // sftpSshKey
            null, // sftpFolder
            null, // sftpFileMask
            null, // sftpMaxConnectionPoolSize
            null, // sftpFileBufferSize
            _mockLogger.Object);

        // Act - No messages in MessageBox
        await adapter.WriteAsync("csv-files/csv-outgoing/output.csv", headers, records);

        // Assert - Should not throw, should use provided headers/records
        // (The adapter falls back to using provided data if no MessageBox message found)
        Assert.True(true); // Test passes if no exception thrown
    }

    [Fact]
    public async Task CsvAdapter_ReadAsync_AsSource_WithMultipleInterfaces_ShouldIsolateMessages()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" } }
        };

        _mockCsvProcessingService
            .Setup(x => x.ParseCsvWithHeadersAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((headers, records));

        _mockAdapterConfig
            .Setup(x => x.GetCsvFieldSeparatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("║");

        var adapterInstanceGuid1 = Guid.NewGuid();
        var adapterInstanceGuid2 = Guid.NewGuid();
        var mockSubscriptionService1 = new Mock<IMessageSubscriptionService>();
        var adapter1 = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            mockSubscriptionService1.Object,
            "Interface1",
            adapterInstanceGuid1,
            null, // receiveFolder
            null, // fileMask (defaults to "*.txt")
            null, // batchSize (defaults to 1000)
            null, // fieldSeparator (defaults to "║")
            null, // destinationReceiveFolder
            null, // destinationFileMask (defaults to "*.txt")
            null, // adapterType
            null, // sftpHost
            null, // sftpPort
            null, // sftpUsername
            null, // sftpPassword
            null, // sftpSshKey
            null, // sftpFolder
            null, // sftpFileMask
            null, // sftpMaxConnectionPoolSize
            null, // sftpFileBufferSize
            _mockLogger.Object);

        var mockSubscriptionService2 = new Mock<IMessageSubscriptionService>();
        var adapter2 = new CsvAdapter(
            _mockCsvProcessingService.Object,
            _mockAdapterConfig.Object,
            _mockBlobServiceClient.Object,
            _messageBoxService,
            mockSubscriptionService2.Object,
            "Interface2",
            adapterInstanceGuid2,
            null, // receiveFolder
            null, // fileMask (defaults to "*.txt")
            null, // batchSize (defaults to 1000)
            null, // fieldSeparator (defaults to "║")
            null, // destinationReceiveFolder
            null, // destinationFileMask (defaults to "*.txt")
            null, // adapterType
            null, // sftpHost
            null, // sftpPort
            null, // sftpUsername
            null, // sftpPassword
            null, // sftpSshKey
            null, // sftpFolder
            null, // sftpFileMask
            null, // sftpMaxConnectionPoolSize
            null, // sftpFileBufferSize
            _mockLogger.Object);

        // Act
        await adapter1.ReadAsync("csv-files/csv-incoming/test1.csv");
        await adapter2.ReadAsync("csv-files/csv-incoming/test2.csv");

        // Assert
        var interface1Messages = await _messageBoxService.ReadMessagesAsync("Interface1");
        var interface2Messages = await _messageBoxService.ReadMessagesAsync("Interface2");
        
        Assert.Single(interface1Messages);
        Assert.Single(interface2Messages);
        Assert.Equal("Interface1", interface1Messages[0].InterfaceName);
        Assert.Equal("Interface2", interface2Messages[0].InterfaceName);
    }

    private static Azure.Storage.Blobs.Models.BlobDownloadResult CreateBlobDownloadResult(BinaryData content)
    {
        var resultType = typeof(Azure.Storage.Blobs.Models.BlobDownloadResult);
        var constructors = resultType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
        var targetConstructor = constructors.FirstOrDefault(c => c.GetParameters().Any(p => p.ParameterType == typeof(BinaryData)))
            ?? constructors.FirstOrDefault()
            ?? throw new InvalidOperationException("BlobDownloadResult constructor not found.");

        var parameters = targetConstructor.GetParameters();
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];

            if (parameter.ParameterType == typeof(BinaryData))
            {
                args[i] = content;
            }
            else if (parameter.ParameterType.IsValueType)
            {
                args[i] = Activator.CreateInstance(parameter.ParameterType);
            }
            else
            {
                args[i] = null;
            }
        }

        var instance = (Azure.Storage.Blobs.Models.BlobDownloadResult)targetConstructor.Invoke(args);

        var contentProperty = resultType.GetProperty("Content", BindingFlags.Instance | BindingFlags.Public);
        if (contentProperty != null && contentProperty.CanWrite)
        {
            contentProperty.SetValue(instance, content);
        }
        else
        {
            var backingField = resultType.GetField("<Content>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            backingField?.SetValue(instance, content);
        }

        return instance;
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

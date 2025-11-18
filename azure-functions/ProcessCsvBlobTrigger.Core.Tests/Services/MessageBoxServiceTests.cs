using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Models;
using ProcessCsvBlobTrigger.Services;
using Xunit;

namespace ProcessCsvBlobTrigger.Core.Tests.Services;

/// <summary>
/// Unit tests for MessageBoxService
/// Tests reading and writing messages to MessageBox database
/// </summary>
public class MessageBoxServiceTests : IDisposable
{
    private readonly MessageBoxDbContext _context;
    private readonly IMessageBoxService _messageBoxService;
    private readonly Mock<ILogger<MessageBoxService>> _mockLogger;

    public MessageBoxServiceTests()
    {
        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<MessageBoxDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MessageBoxDbContext(options);
        _mockLogger = new Mock<ILogger<MessageBoxService>>();
        var mockEventQueue = new Mock<IEventQueue>();
        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _messageBoxService = new MessageBoxService(_context, mockEventQueue.Object, mockSubscriptionService.Object, _mockLogger.Object);

        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task WriteMessagesAsync_ShouldCreateMessagesInMessageBox()
    {
        // Arrange
        var interfaceName = "FromCsvToSqlServerExample";
        var adapterName = "CSV";
        var adapterType = "Source";
        var headers = new List<string> { "Name", "Age", "City" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" }, { "Age", "30" }, { "City", "New York" } },
            new() { { "Name", "Jane" }, { "Age", "25" }, { "City", "London" } }
        };

        // Act - Debatches into 2 separate messages
        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            interfaceName, adapterName, adapterType, adapterInstanceGuid, headers, records);

        // Assert
        Assert.Equal(2, messageIds.Count);
        Assert.All(messageIds, id => Assert.NotEqual(Guid.Empty, id));

        var message = await _context.Messages.FindAsync(messageIds[0]);
        Assert.NotNull(message);
        Assert.Equal(interfaceName, message.InterfaceName);
        Assert.Equal(adapterName, message.AdapterName);
        Assert.Equal(adapterType, message.AdapterType);
        Assert.Equal(adapterInstanceGuid, message.AdapterInstanceGuid);
        Assert.Equal("Pending", message.Status);
        Assert.NotNull(message.MessageData);
        Assert.Contains("headers", message.MessageData);
        Assert.Contains("record", message.MessageData); // Single record per message
    }

    [Fact]
    public async Task ReadMessagesAsync_ShouldReturnMessagesForInterface()
    {
        // Arrange
        var interfaceName = "FromCsvToSqlServerExample";
        var headers = new List<string> { "Name", "Age" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" }, { "Age", "30" } }
        };

        // Create multiple messages (debatching creates one message per record)
        var adapterInstanceGuid1 = Guid.NewGuid();
        var adapterInstanceGuid2 = Guid.NewGuid();
        var messageIds1 = await _messageBoxService.WriteMessagesAsync(
            interfaceName, "CSV", "Source", adapterInstanceGuid1, headers, records);
        var messageIds2 = await _messageBoxService.WriteMessagesAsync(
            interfaceName, "SqlServer", "Destination", adapterInstanceGuid2, headers, records);
        var messageId1 = messageIds1[0];
        var messageId2 = messageIds2[0];

        // Act
        var messages = await _messageBoxService.ReadMessagesAsync(interfaceName);

        // Assert - Should have 2 messages (one from each WriteMessagesAsync call)
        Assert.NotNull(messages);
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.MessageId == messageId1);
        Assert.Contains(messages, m => m.MessageId == messageId2);
    }

    [Fact]
    public async Task ReadMessagesAsync_WithStatusFilter_ShouldReturnFilteredMessages()
    {
        // Arrange
        var interfaceName = "FromCsvToSqlServerExample";
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" } }
        };

        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds1 = await _messageBoxService.WriteMessagesAsync(
            interfaceName, "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId1 = messageIds1[0];
        
        // Mark one message as processed
        await _messageBoxService.MarkMessageAsProcessedAsync(messageId1);

        var messageIds2 = await _messageBoxService.WriteMessagesAsync(
            interfaceName, "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId2 = messageIds2[0];

        // Act
        var pendingMessages = await _messageBoxService.ReadMessagesAsync(interfaceName, "Pending");
        var processedMessages = await _messageBoxService.ReadMessagesAsync(interfaceName, "Processed");

        // Assert
        Assert.Single(pendingMessages);
        Assert.Equal(messageId2, pendingMessages[0].MessageId);
        
        Assert.Single(processedMessages);
        Assert.Equal(messageId1, processedMessages[0].MessageId);
    }

    [Fact]
    public async Task ReadMessageAsync_ShouldReturnSpecificMessage()
    {
        // Arrange
        var interfaceName = "FromCsvToSqlServerExample";
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" } }
        };

        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            interfaceName, "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        // Act
        var message = await _messageBoxService.ReadMessageAsync(messageId);

        // Assert
        Assert.NotNull(message);
        Assert.Equal(messageId, message.MessageId);
        Assert.Equal(interfaceName, message.InterfaceName);
    }

    [Fact]
    public async Task MarkMessageAsProcessedAsync_ShouldUpdateMessageStatus()
    {
        // Arrange
        var interfaceName = "FromCsvToSqlServerExample";
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" } }
        };

        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            interfaceName, "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        // Act
        await _messageBoxService.MarkMessageAsProcessedAsync(messageId, "Processing completed");

        // Assert
        var message = await _context.Messages.FindAsync(messageId);
        Assert.NotNull(message);
        Assert.Equal("Processed", message.Status);
        Assert.NotNull(message.datetime_processed);
        Assert.Equal("Processing completed", message.ProcessingDetails);
    }

    [Fact]
    public async Task MarkMessageAsErrorAsync_ShouldUpdateMessageStatus()
    {
        // Arrange
        var interfaceName = "FromCsvToSqlServerExample";
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" } }
        };

        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            interfaceName, "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        // Act
        await _messageBoxService.MarkMessageAsErrorAsync(messageId, "Test error message");

        // Assert
        var message = await _context.Messages.FindAsync(messageId);
        Assert.NotNull(message);
        Assert.Equal("Error", message.Status);
        Assert.NotNull(message.datetime_processed);
        Assert.Equal("Test error message", message.ErrorMessage);
    }

    [Fact]
    public void ExtractDataFromMessage_ShouldReturnHeadersAndRecords()
    {
        // Arrange
        var message = new MessageBoxMessage
        {
            MessageId = Guid.NewGuid(),
            InterfaceName = "FromCsvToSqlServerExample",
            AdapterName = "CSV",
            AdapterType = "Source",
            MessageData = "{\"headers\":[\"Name\",\"Age\"],\"record\":{\"Name\":\"John\",\"Age\":\"30\"}}",
            Status = "Pending",
            datetime_created = DateTime.UtcNow
        };

        // Act
        var (extractedHeaders, extractedRecord) = _messageBoxService.ExtractDataFromMessage(message);

        // Assert
        Assert.Equal(2, extractedHeaders.Count);
        Assert.Equal("Name", extractedHeaders[0]);
        Assert.Equal("Age", extractedHeaders[1]);
        
        Assert.NotNull(extractedRecord);
        Assert.Equal("John", extractedRecord["Name"]);
        Assert.Equal("30", extractedRecord["Age"]);
    }

    [Fact]
    public async Task CsvAdapter_AsSource_ShouldWriteToMessageBox()
    {
        // This test verifies that CsvAdapter writes to MessageBox when used as Source
        // Note: This is an integration test that would require mocking BlobServiceClient
        // For now, we test the MessageBoxService directly
        
        // Arrange
        var interfaceName = "FromCsvToSqlServerExample";
        var headers = new List<string> { "Name", "Age" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" }, { "Age", "30" } }
        };

        // Act - Simulate CsvAdapter writing to MessageBox
        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            interfaceName, "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        // Assert
        var message = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.NotNull(message);
        Assert.Equal("CSV", message.AdapterName);
        Assert.Equal("Source", message.AdapterType);
    }

    [Fact]
    public async Task SqlServerAdapter_AsDestination_ShouldReadFromMessageBox()
    {
        // This test verifies that SqlServerAdapter reads from MessageBox when used as Destination
        // Note: This is an integration test that would require mocking ApplicationDbContext
        // For now, we test the MessageBoxService directly
        
        // Arrange
        var interfaceName = "FromCsvToSqlServerExample";
        var headers = new List<string> { "Name", "Age" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John" }, { "Age", "30" } }
        };

        // Create a message in MessageBox (simulating CsvAdapter as Source)
        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            interfaceName, "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        // Act - Simulate SqlServerAdapter reading from MessageBox
        var messages = await _messageBoxService.ReadMessagesAsync(interfaceName, "Pending");
        Assert.NotEmpty(messages);
        
        var message = messages.OrderBy(m => m.datetime_created).First(); // Oldest first
        var (readHeaders, readRecord) = _messageBoxService.ExtractDataFromMessage(message);

        // Assert
        Assert.Equal(headers.Count, readHeaders.Count);
        Assert.NotNull(readRecord);
        Assert.Equal("John", readRecord["Name"]);
        Assert.Equal("30", readRecord["Age"]);

        // Mark as processed (simulating successful write to SQL Server)
        await _messageBoxService.MarkMessageAsProcessedAsync(messageId, "Written to SQL Server");
        
        var processedMessage = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.NotNull(processedMessage);
        Assert.Equal("Processed", processedMessage.Status);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}


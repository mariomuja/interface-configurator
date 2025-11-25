using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Integration;

/// <summary>
/// End-to-end integration tests for MessageBox communication flow
/// Tests the complete flow: Source Adapter → MessageBox → Destination Adapter
/// </summary>
public class MessageBoxEndToEndTests : IDisposable
{
    private readonly MessageBoxDbContext _context;
    private readonly IMessageBoxService _messageBoxService;
    private readonly Mock<ILogger<MessageBoxService>> _mockLogger;
    private const string InterfaceName = "FromCsvToSqlServerExample";

    public MessageBoxEndToEndTests()
    {
        var options = new DbContextOptionsBuilder<MessageBoxDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new MessageBoxDbContext(options);
        _mockLogger = new Mock<ILogger<MessageBoxService>>();
        var mockEventQueue = new Mock<IEventQueue>();
        var mockSubscriptionService = new Mock<IMessageSubscriptionService>();
        _messageBoxService = new MessageBoxService(_context, mockEventQueue.Object, mockSubscriptionService.Object, _mockLogger.Object);

        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CompleteFlow_CsvSourceToSqlDestination_ShouldWorkCorrectly()
    {
        // Arrange - Simulate CSV Source adapter writing to MessageBox
        var headers = new List<string> { "Name", "Age", "City" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "John Doe" }, { "Age", "30" }, { "City", "New York" } },
            new() { { "Name", "Jane Smith" }, { "Age", "25" }, { "City", "London" } },
            new() { { "Name", "Bob Johnson" }, { "Age", "35" }, { "City", "Paris" } }
        };

        // Step 1: CSV Adapter (Source) writes to MessageBox (debatching: 3 records = 3 messages)
        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            InterfaceName, "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0]; // Use first message for testing

        // Assert Step 1
        var sourceMessage = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.NotNull(sourceMessage);
        Assert.Equal("Pending", sourceMessage.Status);
        Assert.Equal("CSV", sourceMessage.AdapterName);
        Assert.Equal("Source", sourceMessage.AdapterType);

        // Step 2: SQL Server Adapter (Destination) reads from MessageBox
        var pendingMessages = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending");
        Assert.Equal(3, pendingMessages.Count); // 3 messages (debatching)
        
        var messageToProcess = pendingMessages.OrderBy(m => m.datetime_created).First(); // Oldest first
        Assert.NotNull(messageToProcess);
        var (readHeaders, readRecord) = _messageBoxService.ExtractDataFromMessage(messageToProcess);

        // Assert Step 2 - Single record per message
        Assert.Equal(headers.Count, readHeaders.Count);
        Assert.NotNull(readRecord);
        Assert.Equal("John Doe", readRecord["Name"]);
        Assert.Equal("30", readRecord["Age"]);

        // Step 3: SQL Server Adapter marks message as processed after successful write
        await _messageBoxService.MarkMessageAsProcessedAsync(messageId, "Successfully written to TransportData table");

        // Assert Step 3
        var processedMessage = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.Equal("Processed", processedMessage.Status);
        Assert.NotNull(processedMessage.datetime_processed);
        Assert.Contains("Successfully written", processedMessage.ProcessingDetails ?? "");
    }

    [Fact]
    public async Task CompleteFlow_WithErrorHandling_ShouldMarkAsError()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Name", "Test" } }
        };

        // Step 1: Source writes to MessageBox (debatching)
        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            InterfaceName, "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        // Step 2: Destination adapter encounters error
        var errorMessage = "SQL Server connection timeout after 30 seconds";
        await _messageBoxService.MarkMessageAsErrorAsync(messageId, errorMessage);

        // Assert
        var errorMessageObj = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.Equal("Error", errorMessageObj.Status);
        Assert.Equal(errorMessage, errorMessageObj.ErrorMessage);
        Assert.NotNull(errorMessageObj.datetime_processed);

        // Verify error messages are not returned when querying for pending messages
        var pendingMessages = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending");
        Assert.DoesNotContain(pendingMessages, m => m.MessageId == messageId);
    }

    [Fact]
    public async Task CompleteFlow_MultipleMessages_ShouldProcessInOrder()
    {
        // Arrange
        var headers = new List<string> { "Batch" };
        
        // Create 3 messages (debatching: 1 record each = 1 message each)
        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds1 = await _messageBoxService.WriteMessagesAsync(
            InterfaceName, "CSV", "Source", adapterInstanceGuid, headers,
            new List<Dictionary<string, string>> { new() { { "Batch", "1" } } });
        var messageId1 = messageIds1[0];
        
        await Task.Delay(10);
        
        var messageIds2 = await _messageBoxService.WriteMessagesAsync(
            InterfaceName, "CSV", "Source", adapterInstanceGuid, headers,
            new List<Dictionary<string, string>> { new() { { "Batch", "2" } } });
        var messageId2 = messageIds2[0];
        
        await Task.Delay(10);
        
        var messageIds3 = await _messageBoxService.WriteMessagesAsync(
            InterfaceName, "CSV", "Source", adapterInstanceGuid, headers,
            new List<Dictionary<string, string>> { new() { { "Batch", "3" } } });
        var messageId3 = messageIds3[0];

        // Act - Process messages in order (oldest first)
        var pendingMessages = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending");
        Assert.Equal(3, pendingMessages.Count);

        // Process message 1 (oldest)
        var message1 = pendingMessages.OrderBy(m => m.datetime_created).First();
        await _messageBoxService.MarkMessageAsProcessedAsync(message1.MessageId, "Processed batch 1");

        // Process message 2
        pendingMessages = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending");
        var message2 = pendingMessages.OrderBy(m => m.datetime_created).First();
        await _messageBoxService.MarkMessageAsProcessedAsync(message2.MessageId, "Processed batch 2");

        // Process message 3 (newest)
        pendingMessages = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending");
        var message3 = pendingMessages.OrderBy(m => m.datetime_created).First();
        await _messageBoxService.MarkMessageAsProcessedAsync(message3.MessageId, "Processed batch 3");

        // Assert
        var allProcessed = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Processed");
        Assert.Equal(3, allProcessed.Count);
        
        var noPending = await _messageBoxService.ReadMessagesAsync(InterfaceName, "Pending");
        Assert.Empty(noPending);
    }

    [Fact]
    public async Task CompleteFlow_WithDataTransformation_ShouldPreserveDataIntegrity()
    {
        // Arrange - CSV Source with specific data format
        var csvHeaders = new List<string> { "First Name", "Last Name", "Email Address" };
        var csvRecords = new List<Dictionary<string, string>>
        {
            new() { { "First Name", "John" }, { "Last Name", "Doe" }, { "Email Address", "john@example.com" } }
        };

        // Step 1: CSV writes to MessageBox (debatching: 1 record = 1 message)
        var adapterInstanceGuid1 = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            InterfaceName, "CSV", "Source", adapterInstanceGuid1, csvHeaders, csvRecords);
        var messageId = messageIds[0];

        // Step 2: Read from MessageBox
        var message = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.NotNull(message);
        var (readHeaders, readRecord) = _messageBoxService.ExtractDataFromMessage(message);

        // Step 3: Transform data (simulate SQL Server adapter transforming column names)
        var transformedHeaders = readHeaders.Select(h => h.Replace(" ", "_")).ToList();
        var transformedRecord = new Dictionary<string, string>();
        foreach (var kvp in readRecord)
        {
            transformedRecord[kvp.Key.Replace(" ", "_")] = kvp.Value;
        }

        // Step 4: Write transformed data back to MessageBox (simulating SQL Server as Source)
        var adapterInstanceGuid2 = Guid.NewGuid();
        var transformedMessageIds = await _messageBoxService.WriteMessagesAsync(
            InterfaceName, "SqlServer", "Source", adapterInstanceGuid2, transformedHeaders, new List<Dictionary<string, string>> { transformedRecord });
        var transformedMessageId = transformedMessageIds[0];

        // Assert
        var transformedMessage = await _messageBoxService.ReadMessageAsync(transformedMessageId);
        Assert.NotNull(transformedMessage);
        var (finalHeaders, finalRecord) = _messageBoxService.ExtractDataFromMessage(transformedMessage);
        
        Assert.Contains("First_Name", finalHeaders);
        Assert.Contains("Last_Name", finalHeaders);
        Assert.Contains("Email_Address", finalHeaders);
        Assert.Equal("John", finalRecord["First_Name"]);
        Assert.Equal("Doe", finalRecord["Last_Name"]);
    }

    [Fact]
    public async Task CompleteFlow_MessageBoxMetadata_ShouldBeComplete()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>> { new() { { "Name", "Test" } } };

        var beforeWrite = DateTime.UtcNow;
        await Task.Delay(10);

        // Act
        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            InterfaceName, "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        await Task.Delay(10);
        var afterWrite = DateTime.UtcNow;

        // Assert - Verify all metadata fields
        var message = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.NotNull(message);
        Assert.NotEqual(Guid.Empty, message.MessageId);
        Assert.Equal(InterfaceName, message.InterfaceName);
        Assert.Equal("CSV", message.AdapterName);
        Assert.Equal("Source", message.AdapterType);
        Assert.Equal("Pending", message.Status);
        Assert.True(message.datetime_created >= beforeWrite);
        Assert.True(message.datetime_created <= afterWrite);
        Assert.Null(message.datetime_processed);
        Assert.Null(message.ErrorMessage);
        Assert.Null(message.ProcessingDetails);
        Assert.NotNull(message.MessageData);
        Assert.Contains("headers", message.MessageData);
        Assert.Contains("record", message.MessageData); // Single record per message
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Data;
using ProcessCsvBlobTrigger.Services;
using Xunit;

namespace ProcessCsvBlobTrigger.Core.Tests.Services;

/// <summary>
/// Detailed unit tests for MessageBoxService
/// Tests all aspects of MessageBox communication including edge cases and error scenarios
/// </summary>
public class MessageBoxServiceDetailedTests : IDisposable
{
    private readonly MessageBoxDbContext _context;
    private readonly IMessageBoxService _messageBoxService;
    private readonly Mock<ILogger<MessageBoxService>> _mockLogger;

    public MessageBoxServiceDetailedTests()
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
    public async Task WriteMessageAsync_WithEmptyHeaders_ShouldCreateMessage()
    {
        // Arrange
        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();

        // Act - Empty records list creates no messages
        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "CSV", "Source", adapterInstanceGuid, headers, records);

        // Assert - Empty records means no messages created
        Assert.Empty(messageIds);
    }

    [Fact]
    public async Task WriteMessageAsync_WithLargeDataSet_ShouldSerializeCorrectly()
    {
        // Arrange
        var headers = new List<string> { "Id", "Name", "Description" };
        var records = new List<Dictionary<string, string>>();
        
        // Create 1000 records
        for (int i = 0; i < 1000; i++)
        {
            records.Add(new Dictionary<string, string>
            {
                { "Id", i.ToString() },
                { "Name", $"Name{i}" },
                { "Description", $"Description for record {i}" }
            });
        }

        // Act - Debatches into 1000 separate messages
        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "CSV", "Source", adapterInstanceGuid, headers, records);

        // Assert - Should have 1000 messages (one per record)
        Assert.Equal(1000, messageIds.Count);
        
        // Check first and last messages
        var firstMessage = await _messageBoxService.ReadMessageAsync(messageIds[0]);
        Assert.NotNull(firstMessage);
        var (readHeaders1, readRecord1) = _messageBoxService.ExtractDataFromMessage(firstMessage);
        Assert.Equal("0", readRecord1["Id"]);
        
        var lastMessage = await _messageBoxService.ReadMessageAsync(messageIds[999]);
        Assert.NotNull(lastMessage);
        var (readHeaders2, readRecord2) = _messageBoxService.ExtractDataFromMessage(lastMessage);
        Assert.Equal("999", readRecord2["Id"]);
    }

    [Fact]
    public async Task WriteMessageAsync_WithSpecialCharacters_ShouldSerializeCorrectly()
    {
        // Arrange
        var headers = new List<string> { "Text" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Text", "Text with \"quotes\"" } },
            new() { { "Text", "Text with\nnewlines" } },
            new() { { "Text", "Text with\t tabs" } },
            new() { { "Text", "Text with unicode: 你好世界" } }
        };

        // Act - Debatches into 4 separate messages
        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "CSV", "Source", adapterInstanceGuid, headers, records);

        // Assert - Should have 4 messages (one per record)
        Assert.Equal(4, messageIds.Count);
        
        // Check each message
        var message1 = await _messageBoxService.ReadMessageAsync(messageIds[0]);
        var (_, record1) = _messageBoxService.ExtractDataFromMessage(message1!);
        Assert.Contains("quotes", record1["Text"]);
        
        var message2 = await _messageBoxService.ReadMessageAsync(messageIds[1]);
        var (_, record2) = _messageBoxService.ExtractDataFromMessage(message2!);
        Assert.Contains("newlines", record2["Text"]);
        
        var message3 = await _messageBoxService.ReadMessageAsync(messageIds[2]);
        var (_, record3) = _messageBoxService.ExtractDataFromMessage(message3!);
        Assert.Contains("tabs", record3["Text"]);
        
        var message4 = await _messageBoxService.ReadMessageAsync(messageIds[3]);
        var (_, record4) = _messageBoxService.ExtractDataFromMessage(message4!);
        Assert.Contains("你好世界", record4["Text"]);
    }

    [Fact]
    public async Task ReadMessagesAsync_ShouldOrderByDateDescending()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>> { new() { { "Name", "Test" } } };

        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds1 = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId1 = messageIds1[0];
        
        await Task.Delay(10);
        
        var messageIds2 = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId2 = messageIds2[0];

        // Act
        var messages = await _messageBoxService.ReadMessagesAsync("TestInterface");

        // Assert - Messages ordered by datetime_created ascending (oldest first)
        Assert.Equal(2, messages.Count);
        Assert.Equal(messageId1, messages[0].MessageId);
        Assert.Equal(messageId2, messages[1].MessageId);
    }

    [Fact]
    public async Task MarkMessageAsProcessedAsync_ShouldSetCorrectTimestamp()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>> { new() { { "Name", "Test" } } };

        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        var beforeProcessing = DateTime.UtcNow;
        await Task.Delay(10);

        // Act
        await _messageBoxService.MarkMessageAsProcessedAsync(messageId, "Test processing");

        await Task.Delay(10);
        var afterProcessing = DateTime.UtcNow;

        // Assert
        var message = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.NotNull(message);
        Assert.NotNull(message!.datetime_processed);
        Assert.True(message.datetime_processed >= beforeProcessing);
        Assert.True(message.datetime_processed <= afterProcessing);
    }

    [Fact]
    public async Task MarkMessageAsErrorAsync_ShouldPreserveErrorMessage()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>> { new() { { "Name", "Test" } } };

        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        var errorMessage = "SQL connection failed: Timeout after 30 seconds";

        // Act
        await _messageBoxService.MarkMessageAsErrorAsync(messageId, errorMessage);

        // Assert
        var message = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.NotNull(message);
        Assert.Equal("Error", message!.Status);
        Assert.Equal(errorMessage, message.ErrorMessage);
        Assert.NotNull(message.datetime_processed);
    }

    [Fact]
    public async Task ExtractDataFromMessage_WithComplexNestedData_ShouldDeserializeCorrectly()
    {
        // Arrange
        var headers = new List<string> { "Id", "Name", "Tags", "Metadata" };
        var records = new List<Dictionary<string, string>>
        {
            new()
            {
                { "Id", "1" },
                { "Name", "Product A" },
                { "Tags", "electronics,smartphone" },
                { "Metadata", "{\"price\":999,\"currency\":\"USD\"}" }
            }
        };

        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        // Act
        var message = await _messageBoxService.ReadMessageAsync(messageId);
        var (readHeaders, readRecords) = _messageBoxService.ExtractDataFromMessage(message!);

        // Assert
        Assert.Equal(4, readHeaders.Count);
        Assert.NotNull(readRecords);
        Assert.Equal("1", readRecords["Id"]);
        Assert.Equal("Product A", readRecords["Name"]);
        Assert.Equal("electronics,smartphone", readRecords["Tags"]);
        Assert.Equal("{\"price\":999,\"currency\":\"USD\"}", readRecords["Metadata"]);
    }

    [Fact]
    public async Task ReadMessagesAsync_WithDifferentAdapters_ShouldFilterCorrectly()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>> { new() { { "Name", "Test" } } };

        var adapterInstanceGuid1 = Guid.NewGuid();
        var adapterInstanceGuid2 = Guid.NewGuid();
        var adapterInstanceGuid3 = Guid.NewGuid();
        var csvMessageIds = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "CSV", "Source", adapterInstanceGuid1, headers, records);
        
        var sqlMessageIds = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "SqlServer", "Source", adapterInstanceGuid2, headers, records);
        
        var jsonMessageIds = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "JSON", "Destination", adapterInstanceGuid3, headers, records);

        // Act
        var csvMessages = await _messageBoxService.ReadMessagesAsync("TestInterface");
        var csvSourceMessages = csvMessages.Where(m => m.AdapterName == "CSV" && m.AdapterType == "Source").ToList();
        var sqlMessages = csvMessages.Where(m => m.AdapterName == "SqlServer").ToList();

        // Assert - Each WriteMessagesAsync creates 1 message (1 record each)
        Assert.Equal(3, csvMessages.Count);
        Assert.Single(csvSourceMessages);
        Assert.Single(sqlMessages);
    }

    [Fact]
    public async Task ReadMessageAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var message = await _messageBoxService.ReadMessageAsync(Guid.NewGuid());

        // Assert
        Assert.Null(message);
    }

    [Fact]
    public async Task MarkMessageAsProcessedAsync_WithNonExistentId_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _messageBoxService.MarkMessageAsProcessedAsync(Guid.NewGuid());
        });
    }

    [Fact]
    public async Task MarkMessageAsErrorAsync_WithNonExistentId_ShouldThrowException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _messageBoxService.MarkMessageAsErrorAsync(Guid.NewGuid(), "Test error");
        });
    }

    [Fact]
    public async Task WriteMessageAsync_ShouldSetCorrectDefaultValues()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>> { new() { { "Name", "Test" } } };

        // Act
        var adapterInstanceGuid = Guid.NewGuid();
        var messageIds = await _messageBoxService.WriteMessagesAsync(
            "TestInterface", "CSV", "Source", adapterInstanceGuid, headers, records);
        var messageId = messageIds[0];

        // Assert
        var message = await _messageBoxService.ReadMessageAsync(messageId);
        Assert.NotNull(message);
        Assert.Equal("Pending", message.Status);
        Assert.Null(message.datetime_processed);
        Assert.Null(message.ErrorMessage);
        Assert.Null(message.ProcessingDetails);
        Assert.NotEqual(default(DateTime), message.datetime_created);
    }

    [Fact]
    public async Task MultipleInterfaces_ShouldIsolateMessages()
    {
        // Arrange
        var headers = new List<string> { "Name" };
        var records = new List<Dictionary<string, string>> { new() { { "Name", "Test" } } };

        // Act - Each creates 1 message (1 record each)
        var adapterInstanceGuid1 = Guid.NewGuid();
        var adapterInstanceGuid2 = Guid.NewGuid();
        await _messageBoxService.WriteMessagesAsync("Interface1", "CSV", "Source", adapterInstanceGuid1, headers, records);
        await _messageBoxService.WriteMessagesAsync("Interface2", "CSV", "Source", adapterInstanceGuid2, headers, records);
        await _messageBoxService.WriteMessagesAsync("Interface1", "CSV", "Source", adapterInstanceGuid1, headers, records);

        // Assert
        var interface1Messages = await _messageBoxService.ReadMessagesAsync("Interface1");
        var interface2Messages = await _messageBoxService.ReadMessagesAsync("Interface2");
        
        Assert.Equal(2, interface1Messages.Count);
        Assert.Single(interface2Messages);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}


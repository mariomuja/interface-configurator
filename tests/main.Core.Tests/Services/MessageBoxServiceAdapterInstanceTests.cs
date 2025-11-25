using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

public class MessageBoxServiceAdapterInstanceTests
{
    private MessageBoxDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<MessageBoxDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new MessageBoxDbContext(options);
    }

    [Fact]
    public async Task EnsureAdapterInstanceAsync_CreatesNewInstance_WhenNotExists()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var logger = new Mock<ILogger<MessageBoxService>>();
        var service = new MessageBoxService(context, null, null, logger.Object);

        var adapterInstanceGuid = Guid.NewGuid();
        var interfaceName = "TestInterface";
        var instanceName = "TestInstance";
        var adapterName = "CSV";
        var adapterType = "Source";
        var isEnabled = true;

        // Act
        await service.EnsureAdapterInstanceAsync(
            adapterInstanceGuid,
            interfaceName,
            instanceName,
            adapterName,
            adapterType,
            isEnabled);

        // Assert
        var instance = await context.AdapterInstances
            .FirstOrDefaultAsync(a => a.AdapterInstanceGuid == adapterInstanceGuid);
        
        Assert.NotNull(instance);
        Assert.Equal(adapterInstanceGuid, instance.AdapterInstanceGuid);
        Assert.Equal(interfaceName, instance.InterfaceName);
        Assert.Equal(instanceName, instance.InstanceName);
        Assert.Equal(adapterName, instance.AdapterName);
        Assert.Equal(adapterType, instance.AdapterType);
        Assert.Equal(isEnabled, instance.IsEnabled);
        Assert.NotEqual(default(DateTime), instance.datetime_created);
    }

    [Fact]
    public async Task EnsureAdapterInstanceAsync_UpdatesExistingInstance_WhenExists()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var logger = new Mock<ILogger<MessageBoxService>>();
        var service = new MessageBoxService(context, null, null, logger.Object);

        var adapterInstanceGuid = Guid.NewGuid();
        var existingInstance = new AdapterInstance
        {
            AdapterInstanceGuid = adapterInstanceGuid,
            InterfaceName = "OldInterface",
            InstanceName = "OldInstance",
            AdapterName = "OldAdapter",
            AdapterType = "OldType",
            IsEnabled = false,
            datetime_created = DateTime.UtcNow.AddDays(-1)
        };
        context.AdapterInstances.Add(existingInstance);
        await context.SaveChangesAsync();

        // Act
        await service.EnsureAdapterInstanceAsync(
            adapterInstanceGuid,
            "NewInterface",
            "NewInstance",
            "NewAdapter",
            "NewType",
            true);

        // Assert
        var instance = await context.AdapterInstances
            .FirstOrDefaultAsync(a => a.AdapterInstanceGuid == adapterInstanceGuid);
        
        Assert.NotNull(instance);
        Assert.Equal("NewInterface", instance.InterfaceName);
        Assert.Equal("NewInstance", instance.InstanceName);
        Assert.Equal("NewAdapter", instance.AdapterName);
        Assert.Equal("NewType", instance.AdapterType);
        Assert.True(instance.IsEnabled);
        Assert.NotNull(instance.datetime_updated);
    }

    [Fact]
    public async Task WriteSingleRecordMessageAsync_IncludesAdapterInstanceGuid()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var logger = new Mock<ILogger<MessageBoxService>>();
        var service = new MessageBoxService(context, null, null, logger.Object);

        var adapterInstanceGuid = Guid.NewGuid();
        var interfaceName = "TestInterface";
        var adapterName = "CSV";
        var adapterType = "Source";
        var headers = new List<string> { "Column1", "Column2" };
        var record = new Dictionary<string, string> { { "Column1", "Value1" }, { "Column2", "Value2" } };

        // Act
        var messageId = await service.WriteSingleRecordMessageAsync(
            interfaceName,
            adapterName,
            adapterType,
            adapterInstanceGuid,
            headers,
            record);

        // Assert
        var message = await context.Messages
            .FirstOrDefaultAsync(m => m.MessageId == messageId);
        
        Assert.NotNull(message);
        Assert.Equal(adapterInstanceGuid, message.AdapterInstanceGuid);
        Assert.Equal(interfaceName, message.InterfaceName);
        Assert.Equal(adapterName, message.AdapterName);
        Assert.Equal(adapterType, message.AdapterType);
    }

    [Fact]
    public async Task WriteMessagesAsync_IncludesAdapterInstanceGuid_ForAllMessages()
    {
        // Arrange
        var context = CreateInMemoryDbContext();
        var logger = new Mock<ILogger<MessageBoxService>>();
        var service = new MessageBoxService(context, null, null, logger.Object);

        var adapterInstanceGuid = Guid.NewGuid();
        var interfaceName = "TestInterface";
        var adapterName = "CSV";
        var adapterType = "Source";
        var headers = new List<string> { "Column1", "Column2" };
        var records = new List<Dictionary<string, string>>
        {
            new() { { "Column1", "Value1" }, { "Column2", "Value2" } },
            new() { { "Column1", "Value3" }, { "Column2", "Value4" } }
        };

        // Act
        var messageIds = await service.WriteMessagesAsync(
            interfaceName,
            adapterName,
            adapterType,
            adapterInstanceGuid,
            headers,
            records);

        // Assert
        Assert.Equal(2, messageIds.Count);
        
        var messages = await context.Messages
            .Where(m => messageIds.Contains(m.MessageId))
            .ToListAsync();
        
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Equal(adapterInstanceGuid, m.AdapterInstanceGuid));
    }
}


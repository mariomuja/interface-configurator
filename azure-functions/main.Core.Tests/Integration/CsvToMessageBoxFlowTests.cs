using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using InterfaceConfigurator.Main.Adapters;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Services;
using InterfaceConfigurator.Main.Data;
using InterfaceConfigurator.Main.Services;
using Azure.Storage.Blobs;

namespace InterfaceConfigurator.Main.Core.Tests.Integration;

/// <summary>
/// Integration test that verifies a CSV source adapter debatches records into the MessageBox database.
/// </summary>
public class CsvToMessageBoxFlowTests : IDisposable
{
    private readonly MessageBoxDbContext _messageBoxContext;
    private readonly IMessageBoxService _messageBoxService;
    private readonly Mock<IMessageSubscriptionService> _subscriptionServiceMock;
    private readonly Mock<IAdapterConfigurationService> _adapterConfigMock;
    private readonly ICsvProcessingService _csvProcessingService;
    private readonly Mock<BlobServiceClient> _blobServiceClientMock;
    private readonly Guid _adapterInstanceGuid = Guid.NewGuid();
    private readonly string _interfaceName = "FromCsvToSqlServerExample";

    public CsvToMessageBoxFlowTests()
    {
        var messageBoxOptions = new DbContextOptionsBuilder<MessageBoxDbContext>()
            .UseInMemoryDatabase(databaseName: $"MessageBoxFlow_{Guid.NewGuid()}")
            .Options;

        _messageBoxContext = new MessageBoxDbContext(messageBoxOptions);
        _messageBoxContext.Database.EnsureCreated();

        var queueMock = new Mock<IEventQueue>();
        queueMock.Setup(q => q.EnqueueMessageEventAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _subscriptionServiceMock = new Mock<IMessageSubscriptionService>();

        var messageBoxLogger = new Mock<ILogger<MessageBoxService>>();
        _messageBoxService = new MessageBoxService(_messageBoxContext, queueMock.Object, _subscriptionServiceMock.Object, messageBoxLogger.Object);

        _adapterConfigMock = new Mock<IAdapterConfigurationService>();
        _adapterConfigMock.Setup(a => a.GetCsvFieldSeparatorAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("║");

        _csvProcessingService = new CsvProcessingService(_adapterConfigMock.Object, new Mock<ILogger<CsvProcessingService>>().Object);
        _blobServiceClientMock = new Mock<BlobServiceClient>();
    }

    [Fact]
    public async Task CsvAdapter_FileType_ShouldWriteMessagesToMessageBox()
    {
        // Arrange
        var logger = new Mock<ILogger<CsvAdapter>>();

        var adapter = new CsvAdapter(
            _csvProcessingService,
            _adapterConfigMock.Object,
            _blobServiceClientMock.Object,
            _messageBoxService,
            _subscriptionServiceMock.Object,
            _interfaceName,
            _adapterInstanceGuid,
            receiveFolder: null,
            fileMask: "*.csv",
            batchSize: 100,
            fieldSeparator: "║",
            destinationReceiveFolder: null,
            destinationFileMask: "*.csv",
            adapterType: "FILE",
            sftpHost: null,
            sftpPort: null,
            sftpUsername: null,
            sftpPassword: null,
            sftpSshKey: null,
            sftpFolder: null,
            sftpFileMask: null,
            sftpMaxConnectionPoolSize: null,
            sftpFileBufferSize: null,
            logger: logger.Object);

        var csvContent = "Id║Name║City\n1║Alice║Berlin\n2║Bob║Munich";

        // Act - assign CSV data via reflection to avoid concurrent DbContext usage from property setter
        var csvDataField = typeof(CsvAdapter).GetField("_csvData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        csvDataField!.SetValue(adapter, csvContent);
        await adapter.ProcessCsvDataAsync();

        // Assert
        var messages = await _messageBoxContext.Messages.ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.All(messages, m =>
        {
            Assert.Equal(_interfaceName, m.InterfaceName);
            Assert.Equal(_adapterInstanceGuid, m.AdapterInstanceGuid);
            Assert.Equal("Source", m.AdapterType);
            Assert.Equal("CSV", m.AdapterName);
        });
    }

    public void Dispose()
    {
        _messageBoxContext?.Dispose();
    }
}


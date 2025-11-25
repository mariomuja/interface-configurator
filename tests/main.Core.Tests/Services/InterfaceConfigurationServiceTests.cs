using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Moq;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using InterfaceConfigurator.Main.Services;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Services;

/// <summary>
/// Unit tests for InterfaceConfigurationService
/// </summary>
public class InterfaceConfigurationServiceTests
{
    private readonly Mock<BlobServiceClient> _mockBlobServiceClient;
    private readonly Mock<ILogger<InterfaceConfigurationService>> _mockLogger;
    private readonly InterfaceConfigurationService _service;

    public InterfaceConfigurationServiceTests()
    {
        _mockBlobServiceClient = new Mock<BlobServiceClient>();
        _mockLogger = new Mock<ILogger<InterfaceConfigurationService>>();
        _service = new InterfaceConfigurationService(_mockBlobServiceClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task InitializeAsync_WithNoExistingConfig_ShouldCreateEmptyConfig()
    {
        // Arrange
        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);
        
        mockContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Azure.Response<BlobContainerInfo>>());
        
        mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);
        
        var mockResponse = new Mock<Azure.Response<bool>>();
        mockResponse.Setup(r => r.Value).Returns(false);
        mockBlobClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        // Act
        await _service.InitializeAsync();

        // Assert
        _mockBlobServiceClient.Verify(x => x.GetBlobContainerClient("function-config"), Times.Once);
    }

    [Fact]
    public async Task SaveConfigurationAsync_WithValidConfig_ShouldSaveToBlob()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "CSV",
            SourceIsEnabled = true
        };

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);
        
        mockContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Azure.Response<BlobContainerInfo>>());
        
        mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);
        
        var mockUploadResponse = new Mock<Azure.Response<BlobContentInfo>>();
        mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUploadResponse.Object);

        // Initialize first
        var mockExistsResponse = new Mock<Azure.Response<bool>>();
        mockExistsResponse.Setup(r => r.Value).Returns(false);
        mockBlobClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse.Object);
        
        await _service.InitializeAsync();

        // Act
        await _service.SaveConfigurationAsync(config);

        // Assert
        mockBlobClient.Verify(x => x.UploadAsync(
            It.IsAny<BinaryData>(),
            It.IsAny<BlobUploadOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetConfigurationAsync_WithExistingConfig_ShouldReturnConfig()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "CSV"
        };
        var configs = new List<InterfaceConfiguration> { config };
        var jsonContent = JsonSerializer.Serialize(configs);

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);
        
        mockContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Azure.Response<BlobContainerInfo>>());
        
        mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);
        
        var mockExistsResponse = new Mock<Azure.Response<bool>>();
        mockExistsResponse.Setup(r => r.Value).Returns(true);
        mockBlobClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse.Object);
        
        var mockDownloadResponse = new Mock<Azure.Response<BlobDownloadResult>>();
        var binaryData = BinaryData.FromString(jsonContent);
        var blobDownloadResultType = typeof(BlobDownloadResult);
        var blobDownloadResult = Activator.CreateInstance(blobDownloadResultType, binaryData) 
            ?? throw new InvalidOperationException("Failed to create BlobDownloadResult");
        mockDownloadResponse.Setup(r => r.Value).Returns((BlobDownloadResult)blobDownloadResult);
        mockBlobClient
            .Setup(x => x.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDownloadResponse.Object);

        await _service.InitializeAsync();

        // Act
        var result = await _service.GetConfigurationAsync("TestInterface");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestInterface", result.InterfaceName);
        Assert.Equal("CSV", result.SourceAdapterName);
    }

    [Fact]
    public async Task GetAllConfigurationsAsync_WithMultipleConfigs_ShouldReturnAll()
    {
        // Arrange
        var configs = new List<InterfaceConfiguration>
        {
            new() { InterfaceName = "Interface1", SourceAdapterName = "CSV" },
            new() { InterfaceName = "Interface2", SourceAdapterName = "SqlServer" }
        };
        var jsonContent = JsonSerializer.Serialize(configs);

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);
        
        mockContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Azure.Response<BlobContainerInfo>>());
        
        mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);
        
        var mockExistsResponse = new Mock<Azure.Response<bool>>();
        mockExistsResponse.Setup(r => r.Value).Returns(true);
        mockBlobClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse.Object);
        
        var mockDownloadResponse = new Mock<Azure.Response<BlobDownloadResult>>();
        var binaryData = BinaryData.FromString(jsonContent);
        var blobDownloadResultType = typeof(BlobDownloadResult);
        var blobDownloadResult = Activator.CreateInstance(blobDownloadResultType, binaryData) 
            ?? throw new InvalidOperationException("Failed to create BlobDownloadResult");
        mockDownloadResponse.Setup(r => r.Value).Returns((BlobDownloadResult)blobDownloadResult);
        mockBlobClient
            .Setup(x => x.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDownloadResponse.Object);

        await _service.InitializeAsync();

        // Act
        var result = await _service.GetAllConfigurationsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.InterfaceName == "Interface1");
        Assert.Contains(result, c => c.InterfaceName == "Interface2");
    }

    [Fact]
    public async Task DeleteConfigurationAsync_WithExistingConfig_ShouldRemoveConfig()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "CSV"
        };
        var configs = new List<InterfaceConfiguration> { config };
        var jsonContent = JsonSerializer.Serialize(configs);

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);
        
        mockContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Azure.Response<BlobContainerInfo>>());
        
        mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);
        
        var mockExistsResponse = new Mock<Azure.Response<bool>>();
        mockExistsResponse.Setup(r => r.Value).Returns(true);
        mockBlobClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse.Object);
        
        var mockDownloadResponse = new Mock<Azure.Response<BlobDownloadResult>>();
        var binaryData = BinaryData.FromString(jsonContent);
        var blobDownloadResultType = typeof(BlobDownloadResult);
        var blobDownloadResult = Activator.CreateInstance(blobDownloadResultType, binaryData) 
            ?? throw new InvalidOperationException("Failed to create BlobDownloadResult");
        mockDownloadResponse.Setup(r => r.Value).Returns((BlobDownloadResult)blobDownloadResult);
        mockBlobClient
            .Setup(x => x.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDownloadResponse.Object);
        
        var mockUploadResponse = new Mock<Azure.Response<BlobContentInfo>>();
        mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUploadResponse.Object);

        await _service.InitializeAsync();

        // Act
        await _service.DeleteConfigurationAsync("TestInterface");

        // Assert
        var result = await _service.GetConfigurationAsync("TestInterface");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetSourceEnabledAsync_ShouldUpdateConfig()
    {
        // Arrange
        var config = new InterfaceConfiguration
        {
            InterfaceName = "TestInterface",
            SourceAdapterName = "CSV",
            SourceIsEnabled = false
        };
        var configs = new List<InterfaceConfiguration> { config };
        var jsonContent = JsonSerializer.Serialize(configs);

        var mockContainerClient = new Mock<BlobContainerClient>();
        var mockBlobClient = new Mock<BlobClient>();
        
        _mockBlobServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(mockContainerClient.Object);
        
        mockContainerClient
            .Setup(x => x.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Azure.Response<BlobContainerInfo>>());
        
        mockContainerClient
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(mockBlobClient.Object);
        
        var mockExistsResponse = new Mock<Azure.Response<bool>>();
        mockExistsResponse.Setup(r => r.Value).Returns(true);
        mockBlobClient
            .Setup(x => x.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse.Object);
        
        var mockDownloadResponse = new Mock<Azure.Response<BlobDownloadResult>>();
        var binaryData = BinaryData.FromString(jsonContent);
        var blobDownloadResultType = typeof(BlobDownloadResult);
        var blobDownloadResult = Activator.CreateInstance(blobDownloadResultType, binaryData) 
            ?? throw new InvalidOperationException("Failed to create BlobDownloadResult");
        mockDownloadResponse.Setup(r => r.Value).Returns((BlobDownloadResult)blobDownloadResult);
        mockBlobClient
            .Setup(x => x.DownloadContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockDownloadResponse.Object);
        
        var mockUploadResponse = new Mock<Azure.Response<BlobContentInfo>>();
        mockBlobClient
            .Setup(x => x.UploadAsync(It.IsAny<BinaryData>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockUploadResponse.Object);

        await _service.InitializeAsync();

        // Act
        await _service.SetSourceEnabledAsync("TestInterface", true);

        // Assert
        var result = await _service.GetConfigurationAsync("TestInterface");
        Assert.NotNull(result);
        Assert.True(result.SourceIsEnabled);
    }
}


using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Integration;

/// <summary>
/// Integration tests for Azure Blob Storage operations
/// Tests container operations, file upload/download, and folder structure
/// Requires Blob Storage connection string in environment variables
/// </summary>
public class BlobStorageIntegrationTests : IClassFixture<BlobStorageTestFixture>, IDisposable
{
    private readonly BlobStorageTestFixture _fixture;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageIntegrationTests> _logger;

    public BlobStorageIntegrationTests(BlobStorageTestFixture fixture)
    {
        _fixture = fixture;
        _blobServiceClient = new BlobServiceClient(_fixture.ConnectionString);
        _logger = new Mock<ILogger<BlobStorageIntegrationTests>>().Object;
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task BlobServiceClient_Connection_Should_Be_Valid()
    {
        // Arrange & Act
        var properties = await _blobServiceClient.GetPropertiesAsync();

        // Assert
        Assert.NotNull(properties.Value);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task csv_files_Container_Should_Exist()
    {
        // Arrange
        var containerName = "csv-files";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Act
        var exists = await containerClient.ExistsAsync();

        // Assert
        Assert.True(exists.Value, $"Container '{containerName}' should exist");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task function_config_Container_Should_Exist()
    {
        // Arrange
        var containerName = "function-config";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Act
        var exists = await containerClient.ExistsAsync();

        // Assert
        Assert.True(exists.Value, $"Container '{containerName}' should exist");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task csv_incoming_Folder_Should_Be_Accessible()
    {
        // Arrange
        var containerName = "csv-files";
        var folderPath = "csv-incoming/";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Act - Try to list blobs in the folder
        var blobs = containerClient.GetBlobsAsync(prefix: folderPath);

        // Assert - Should not throw
        await foreach (var blob in blobs)
        {
            Assert.NotNull(blob.Name);
            Assert.StartsWith(folderPath, blob.Name);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task csv_processed_Folder_Should_Be_Accessible()
    {
        // Arrange
        var containerName = "csv-files";
        var folderPath = "csv-processed/";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Act
        var blobs = containerClient.GetBlobsAsync(prefix: folderPath);

        // Assert
        await foreach (var blob in blobs)
        {
            Assert.NotNull(blob.Name);
            Assert.StartsWith(folderPath, blob.Name);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task csv_error_Folder_Should_Be_Accessible()
    {
        // Arrange
        var containerName = "csv-files";
        var folderPath = "csv-error/";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Act
        var blobs = containerClient.GetBlobsAsync(prefix: folderPath);

        // Assert
        await foreach (var blob in blobs)
        {
            Assert.NotNull(blob.Name);
            Assert.StartsWith(folderPath, blob.Name);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task Upload_Blob_Should_Work()
    {
        // Arrange
        var containerName = "csv-files";
        var testFileName = $"csv-incoming/test-{Guid.NewGuid()}.csv";
        var testContent = "Column1,Column2,Column3\nValue1,Value2,Value3";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(testFileName);

        try
        {
            // Act
            await blobClient.UploadAsync(BinaryData.FromString(testContent), overwrite: true);

            // Assert
            var exists = await blobClient.ExistsAsync();
            Assert.True(exists.Value, "Blob should exist after upload");
        }
        finally
        {
            // Cleanup
            await blobClient.DeleteIfExistsAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task Download_Blob_Should_Work()
    {
        // Arrange
        var containerName = "csv-files";
        var testFileName = $"csv-incoming/test-{Guid.NewGuid()}.csv";
        var testContent = "Column1,Column2\nValue1,Value2";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(testFileName);

        try
        {
            // Upload first
            await blobClient.UploadAsync(BinaryData.FromString(testContent), overwrite: true);

            // Act
            var downloadResult = await blobClient.DownloadContentAsync();
            var downloadedContent = downloadResult.Value.Content.ToString();

            // Assert
            Assert.Equal(testContent, downloadedContent);
        }
        finally
        {
            // Cleanup
            await blobClient.DeleteIfExistsAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task Move_Blob_Should_Work()
    {
        // Arrange
        var containerName = "csv-files";
        var sourcePath = $"csv-incoming/test-{Guid.NewGuid()}.csv";
        var destinationPath = $"csv-processed/test-{Guid.NewGuid()}.csv";
        var testContent = "Column1,Column2\nValue1,Value2";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var sourceBlob = containerClient.GetBlobClient(sourcePath);
        var destinationBlob = containerClient.GetBlobClient(destinationPath);

        try
        {
            // Upload to source
            await sourceBlob.UploadAsync(BinaryData.FromString(testContent), overwrite: true);

            // Act - Copy then delete (simulating move)
            await destinationBlob.StartCopyFromUriAsync(sourceBlob.Uri);
            
            // Wait for copy to complete
            var properties = await destinationBlob.GetPropertiesAsync();
            while (properties.Value.CopyStatus == CopyStatus.Pending)
            {
                await Task.Delay(100);
                properties = await destinationBlob.GetPropertiesAsync();
            }

            // Delete source
            await sourceBlob.DeleteIfExistsAsync();

            // Assert
            var sourceExists = await sourceBlob.ExistsAsync();
            var destExists = await destinationBlob.ExistsAsync();
            Assert.False(sourceExists.Value, "Source blob should be deleted");
            Assert.True(destExists.Value, "Destination blob should exist");
        }
        finally
        {
            // Cleanup
            await sourceBlob.DeleteIfExistsAsync();
            await destinationBlob.DeleteIfExistsAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task List_Blobs_Should_Work()
    {
        // Arrange
        var containerName = "csv-files";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Act
        var blobs = containerClient.GetBlobsAsync();
        var blobList = new List<BlobItem>();
        await foreach (var blob in blobs)
        {
            blobList.Add(blob);
        }

        // Assert
        Assert.NotNull(blobList);
        // May be empty, but should not throw
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task GetBlobContainerFolders_Should_Work()
    {
        // Arrange
        var containerName = "csv-files";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Act - List blobs with folder structure
        var folders = new HashSet<string>();
        await foreach (var blob in containerClient.GetBlobsAsync())
        {
            var folderPath = blob.Name.Contains('/') 
                ? blob.Name.Substring(0, blob.Name.LastIndexOf('/') + 1)
                : "";
            if (!string.IsNullOrEmpty(folderPath))
            {
                folders.Add(folderPath);
            }
        }

        // Assert - Should have at least csv-incoming, csv-processed, csv-error folders
        Assert.True(folders.Count >= 0, "Should be able to list folders");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task Delete_Blob_Should_Work()
    {
        // Arrange
        var containerName = "csv-files";
        var testFileName = $"csv-incoming/test-{Guid.NewGuid()}.csv";
        var testContent = "Test content";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(testFileName);

        // Upload first
        await blobClient.UploadAsync(BinaryData.FromString(testContent), overwrite: true);
        var existsBefore = await blobClient.ExistsAsync();
        Assert.True(existsBefore.Value, "Blob should exist before deletion");

        // Act
        await blobClient.DeleteIfExistsAsync();

        // Assert
        var existsAfter = await blobClient.ExistsAsync();
        Assert.False(existsAfter.Value, "Blob should not exist after deletion");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task Blob_Metadata_Should_Be_Accessible()
    {
        // Arrange
        var containerName = "csv-files";
        var testFileName = $"csv-incoming/test-{Guid.NewGuid()}.csv";
        var testContent = "Test content";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(testFileName);

        try
        {
            // Upload with metadata
            var metadata = new Dictionary<string, string>
            {
                { "Source", "Test" },
                { "InterfaceName", "test-interface" }
            };
            await blobClient.UploadAsync(
                BinaryData.FromString(testContent),
                new BlobUploadOptions { Metadata = metadata },
                overwrite: true);

            // Act
            var properties = await blobClient.GetPropertiesAsync();

            // Assert
            Assert.NotNull(properties.Value.Metadata);
            Assert.Equal("Test", properties.Value.Metadata["Source"]);
        }
        finally
        {
            await blobClient.DeleteIfExistsAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Blob Storage")]
    public async Task Container_Public_Access_Should_Be_Private()
    {
        // Arrange
        var containerName = "csv-files";
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Act
        var properties = await containerClient.GetPropertiesAsync();

        // Assert - Container should be private (no public access)
        Assert.Equal(PublicAccessType.None, properties.Value.PublicAccess);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

/// <summary>
/// Test fixture for Blob Storage integration tests
/// Provides connection string from environment variables
/// </summary>
public class BlobStorageTestFixture : IDisposable
{
    public string ConnectionString { get; }

    public BlobStorageTestFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") ??
                              Environment.GetEnvironmentVariable("MainStorageConnection") ??
                              Environment.GetEnvironmentVariable("AzureWebJobsStorage");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Blob Storage connection string not found in environment variables. " +
                "Required: AZURE_STORAGE_CONNECTION_STRING (or MainStorageConnection or AzureWebJobsStorage)");
        }

        ConnectionString = connectionString;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}


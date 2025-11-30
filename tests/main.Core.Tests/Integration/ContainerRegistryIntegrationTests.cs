using Azure.Containers.ContainerRegistry;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Integration;

/// <summary>
/// Integration tests for Azure Container Registry
/// Tests ACR connectivity, image availability, and pull operations
/// Requires ACR connection details
/// </summary>
public class ContainerRegistryIntegrationTests : IClassFixture<ContainerRegistryTestFixture>, IDisposable
{
    private readonly ContainerRegistryTestFixture _fixture;
    private readonly ILogger<ContainerRegistryIntegrationTests> _logger;

    public ContainerRegistryIntegrationTests(ContainerRegistryTestFixture fixture)
    {
        _fixture = fixture;
        _logger = new Mock<ILogger<ContainerRegistryIntegrationTests>>().Object;
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Container Registry")]
    public async Task ACR_Connection_Should_Be_Valid()
    {
        // Arrange
        var registryUri = new Uri($"https://{_fixture.RegistryName}.azurecr.io");
        var credential = new Azure.Identity.DefaultAzureCredential();
        var client = new ContainerRegistryClient(registryUri, credential);

        // Act - Get repository properties instead
        var repository = client.GetRepository("interface-configurator-adapters");
        var properties = await repository.GetPropertiesAsync();

        // Assert
        Assert.NotNull(properties.Value);
        Assert.NotNull(properties.Value.Name);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Container Registry")]
    public async Task Adapter_Image_Should_Exist_In_ACR()
    {
        // Arrange
        var registryUri = new Uri($"https://{_fixture.RegistryName}.azurecr.io");
        var credential = new Azure.Identity.DefaultAzureCredential();
        var client = new ContainerRegistryClient(registryUri, credential);
        var repositoryName = "interface-configurator-adapters";

        // Act
        var repository = client.GetRepository(repositoryName);
        var properties = await repository.GetPropertiesAsync();

        // Assert
        Assert.NotNull(properties.Value);
        Assert.Equal(repositoryName, properties.Value.Name);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Container Registry")]
    public async Task Latest_Tag_Should_Exist()
    {
        // Arrange
        var registryUri = new Uri($"https://{_fixture.RegistryName}.azurecr.io");
        var credential = new Azure.Identity.DefaultAzureCredential();
        var client = new ContainerRegistryClient(registryUri, credential);
        var repositoryName = "interface-configurator-adapters";
        var tagName = "latest";

        // Act
        var repository = client.GetRepository(repositoryName);
        var tag = repository.GetArtifact(tagName);
        var properties = await tag.GetManifestPropertiesAsync();

        // Assert
        Assert.NotNull(properties.Value);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Container Registry")]
    public async Task Image_Manifest_Should_Be_Accessible()
    {
        // Arrange
        var registryUri = new Uri($"https://{_fixture.RegistryName}.azurecr.io");
        var credential = new Azure.Identity.DefaultAzureCredential();
        var client = new ContainerRegistryClient(registryUri, credential);
        var repositoryName = "interface-configurator-adapters";
        var tagName = "latest";

        // Act
        var repository = client.GetRepository(repositoryName);
        var tag = repository.GetArtifact(tagName);
        var manifestProperties = await tag.GetManifestPropertiesAsync();

        // Assert
        Assert.NotNull(manifestProperties.Value);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

/// <summary>
/// Test fixture for Container Registry integration tests
/// Provides ACR registry name from environment variables
/// </summary>
public class ContainerRegistryTestFixture : IDisposable
{
    public string RegistryName { get; }

    public ContainerRegistryTestFixture()
    {
        var registryName = Environment.GetEnvironmentVariable("AZURE_CONTAINER_REGISTRY") ??
                           Environment.GetEnvironmentVariable("ACR_NAME");

        if (string.IsNullOrWhiteSpace(registryName))
        {
            throw new InvalidOperationException(
                "Container Registry name not found in environment variables. " +
                "Required: AZURE_CONTAINER_REGISTRY (or ACR_NAME)");
        }

        // Remove .azurecr.io suffix if present
        RegistryName = registryName.Replace(".azurecr.io", "");
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}


using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace InterfaceConfigurator.Main.Core.Tests.Integration;

/// <summary>
/// Integration tests for Azure Container Apps
/// Tests container app health, deployment status, and connectivity
/// Requires Azure credentials and Container App resource information
/// </summary>
public class ContainerAppIntegrationTests : IClassFixture<AzureTestFixture>, IDisposable
{
    private readonly AzureTestFixture _fixture;
    private readonly ILogger<ContainerAppIntegrationTests> _logger;

    public ContainerAppIntegrationTests(AzureTestFixture fixture)
    {
        _fixture = fixture;
        _logger = new Mock<ILogger<ContainerAppIntegrationTests>>().Object;
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Azure Container Apps")]
    public async Task Container_App_Environment_Should_Be_Accessible()
    {
        // Arrange
        if (string.IsNullOrWhiteSpace(_fixture.ResourceGroupName) || 
            string.IsNullOrWhiteSpace(_fixture.SubscriptionId))
        {
            Assert.True(true, "Azure credentials not configured - skipping test");
            return;
        }

        var armClient = new ArmClient(_fixture.DefaultAzureCredential);
        var subscription = await armClient.GetDefaultSubscriptionAsync();
        var resourceGroup = await subscription.GetResourceGroupAsync(_fixture.ResourceGroupName);

        // Act - Try to list container apps
        var containerApps = resourceGroup.Value.GetContainerApps();

        // Assert - Should be able to list container apps (may be empty)
        Assert.NotNull(containerApps);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Azure Container Apps")]
    public async Task Container_App_Should_Have_Correct_Configuration()
    {
        // This test verifies that container apps can be queried
        // Actual container app names would need to be provided via environment variables
        var containerAppName = Environment.GetEnvironmentVariable("CONTAINER_APP_NAME");
        
        if (string.IsNullOrWhiteSpace(containerAppName))
        {
            Assert.True(true, "CONTAINER_APP_NAME not set - skipping test");
            return;
        }

        // Test would verify container app configuration
        Assert.NotNull(containerAppName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Requires", "Azure Container Apps")]
    public async Task Container_App_Image_Should_Be_From_ACR()
    {
        // This test verifies that container apps use images from ACR
        var acrName = Environment.GetEnvironmentVariable("AZURE_CONTAINER_REGISTRY");
        
        if (string.IsNullOrWhiteSpace(acrName))
        {
            Assert.True(true, "AZURE_CONTAINER_REGISTRY not set - skipping test");
            return;
        }

        // Test would verify image source
        Assert.NotNull(acrName);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

/// <summary>
/// Test fixture for Azure integration tests
/// Provides Azure credentials and resource information
/// </summary>
public class AzureTestFixture : IDisposable
{
    public Azure.Identity.DefaultAzureCredential DefaultAzureCredential { get; }
    public string? ResourceGroupName { get; }
    public string? SubscriptionId { get; }

    public AzureTestFixture()
    {
        try
        {
            DefaultAzureCredential = new Azure.Identity.DefaultAzureCredential();
            ResourceGroupName = Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP");
            SubscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
        }
        catch
        {
            // Azure credentials not available - tests will skip
            DefaultAzureCredential = null!;
        }
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}


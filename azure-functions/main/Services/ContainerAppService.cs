using System.Text.Json;
using System.Linq;
using Azure.Core;
using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using System.Collections.Concurrent;

namespace InterfaceConfigurator.Main.Services;

/// <summary>
/// Service for managing Azure Container Apps dynamically
/// Creates isolated container apps for each adapter instance
/// </summary>
public class ContainerAppService : IContainerAppService
{
    private readonly ILogger<ContainerAppService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ArmClient? _armClient;
    private readonly string _resourceGroupName;
    private readonly string _location;
    private readonly string _containerAppEnvironmentName;
    private readonly string _registryServer;
    private readonly string _registryUsername;
    private readonly string _registryPassword;
    private readonly string _serviceBusConnectionString;
    
    // OPTIMIZATION: Caching for environment (created once, reused)
    private static ContainerAppManagedEnvironmentResource? _cachedEnvironment;
    private static readonly SemaphoreSlim _environmentLock = new SemaphoreSlim(1, 1);
    
    // OPTIMIZATION: Option to use shared storage account instead of per-instance storage
    private readonly bool _useSharedStorageAccount;
    private readonly string? _sharedStorageAccountName;
    private static StorageAccountResource? _sharedStorageAccount;
    private static readonly SemaphoreSlim _sharedStorageLock = new SemaphoreSlim(1, 1);

    public ContainerAppService(
        ILogger<ContainerAppService> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        _resourceGroupName = _configuration["ResourceGroupName"] ?? "rg-interface-configurator";
        _location = _configuration["Location"] ?? "Central US";
        _containerAppEnvironmentName = _configuration["ContainerAppEnvironmentName"] ?? "cae-adapter-instances";
        _registryServer = _configuration["ContainerRegistryServer"] ?? "";
        _registryUsername = _configuration["ContainerRegistryUsername"] ?? "";
        _registryPassword = _configuration["ContainerRegistryPassword"] ?? "";
        _serviceBusConnectionString = _configuration["ServiceBusConnectionString"] 
            ?? _configuration["AZURE_SERVICEBUS_CONNECTION_STRING"] ?? "";
        
        // OPTIMIZATION: Option to use shared storage account (faster, but less isolation)
        _useSharedStorageAccount = _configuration.GetValue<bool>("UseSharedStorageAccount", false);
        _sharedStorageAccountName = _configuration["SharedStorageAccountName"] ?? "st-adapter-instances";

        // Initialize ARM client with managed identity or service principal
        try
        {
            var credential = new DefaultAzureCredential();
            _armClient = new ArmClient(credential);
            _logger.LogInformation("ContainerAppService initialized with DefaultAzureCredential");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize ARM client. Container app management will be disabled.");
            _armClient = null;
        }
    }

    public async Task<ContainerAppInfo> CreateContainerAppAsync(
        Guid adapterInstanceGuid,
        string adapterName,
        string adapterType,
        string interfaceName,
        string instanceName,
        object adapterConfiguration,
        CancellationToken cancellationToken = default)
    {
        if (_armClient == null)
        {
            throw new InvalidOperationException("ARM client not initialized. Cannot create container app.");
        }

        try
        {
            // IMPORTANT: Each adapter instance gets its own isolated container app
            // Container app name is derived from adapterInstanceGuid: ca-{guid}
            // This ensures complete process isolation between adapter instances
            var containerAppName = GetContainerAppNamePrivate(adapterInstanceGuid);
            
            _logger.LogInformation(
                "Creating isolated container app for adapter instance: Guid={Guid}, ContainerApp={ContainerAppName}, Adapter={Adapter}, Type={Type}, Interface={Interface}, InstanceName={InstanceName}",
                adapterInstanceGuid, containerAppName, adapterName, adapterType, interfaceName, instanceName);

            var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken);
            var resourceGroup = await subscription.GetResourceGroupAsync(_resourceGroupName, cancellationToken);

            if (resourceGroup.Value == null)
            {
                throw new InvalidOperationException($"Resource group '{_resourceGroupName}' not found");
            }

            // OPTIMIZATION: Run environment and storage creation in parallel
            var environmentTask = GetOrCreateContainerAppEnvironmentAsync(
                resourceGroup.Value, cancellationToken);
            
            var blobStorageTask = _useSharedStorageAccount
                ? GetOrCreateSharedBlobStorageAsync(resourceGroup.Value, adapterInstanceGuid, cancellationToken)
                : CreateBlobStorageForInstanceAsync(
                    adapterInstanceGuid, resourceGroup.Value, cancellationToken);

            // Wait for both to complete in parallel
            await Task.WhenAll(environmentTask, blobStorageTask);
            
            var environment = await environmentTask;
            var blobStorageInfo = await blobStorageTask;

            // Store adapter configuration in blob storage
            await StoreAdapterConfigurationAsync(
                blobStorageInfo.ConnectionString,
                blobStorageInfo.ContainerName,
                adapterConfiguration,
                cancellationToken);

            // Container app name already set above - each instance gets unique name based on GUID
            var containerAppData = new ContainerAppData(_location)
            {
                ManagedEnvironmentId = environment.Id,
                Configuration = new ContainerAppConfiguration
                {
                    Ingress = new ContainerAppIngressConfiguration
                    {
                        External = false,
                        TargetPort = 8080,
                        Transport = "http"
                    },
                    Registries =
                    {
                        new ContainerAppRegistryCredentials
                        {
                            Server = _registryServer,
                            Username = _registryUsername,
                            PasswordSecretRef = "registry-password"
                        }
                    }
                },
                Template = new ContainerAppTemplate
                {
                    Containers =
                    {
                        new ContainerAppContainer
                        {
                            Name = containerAppName,
                            Image = GetAdapterImage(adapterName),
                            Env =
                            {
                                new ContainerAppEnvironmentVariable { Name = "ADAPTER_INSTANCE_GUID", Value = adapterInstanceGuid.ToString() },
                                new ContainerAppEnvironmentVariable { Name = "ADAPTER_NAME", Value = adapterName },
                                new ContainerAppEnvironmentVariable { Name = "ADAPTER_TYPE", Value = adapterType },
                                new ContainerAppEnvironmentVariable { Name = "INTERFACE_NAME", Value = interfaceName },
                                new ContainerAppEnvironmentVariable { Name = "INSTANCE_NAME", Value = instanceName },
                                new ContainerAppEnvironmentVariable { Name = "BLOB_CONNECTION_STRING", SecretRef = "blob-connection-string" },
                                new ContainerAppEnvironmentVariable { Name = "BLOB_CONTAINER_NAME", Value = blobStorageInfo.ContainerName },
                                new ContainerAppEnvironmentVariable { Name = "ADAPTER_CONFIG_PATH", Value = "adapter-config.json" },
                                new ContainerAppEnvironmentVariable { Name = "AZURE_SERVICEBUS_CONNECTION_STRING", SecretRef = "servicebus-connection-string" }
                            },
                            Resources = new AppContainerResources
                            {
                                Cpu = 0.25,
                                Memory = "0.5Gi"
                            }
                        }
                    },
                    Scale = new ContainerAppScale
                    {
                        MinReplicas = 1,
                        MaxReplicas = 1
                    }
                }
            };

            // Get container app collection from resource group (not from environment)
            var containerAppCollection = resourceGroup.Value.GetContainerApps();
            
            // Check if container app already exists (should not happen, but handle gracefully)
            try
            {
                var existingContainerApp = await containerAppCollection.GetAsync(containerAppName, cancellationToken: cancellationToken);
                if (existingContainerApp.Value != null)
                {
                    _logger.LogWarning(
                        "Container app already exists for adapter instance {Guid}: ContainerApp={ContainerAppName}. Updating instead of creating.",
                        adapterInstanceGuid, containerAppName);
                }
            }
            catch (Azure.RequestFailedException)
            {
                // Container app doesn't exist yet - this is expected
            }
            
            var containerAppOperation = await containerAppCollection.CreateOrUpdateAsync(
                Azure.WaitUntil.Started,
                containerAppName,
                containerAppData,
                cancellationToken);

            _logger.LogInformation(
                "Container app creation/update initiated (async): Name={Name}, Guid={Guid}, Adapter={Adapter}, Type={Type}, Interface={Interface}",
                containerAppName, adapterInstanceGuid, adapterName, adapterType, interfaceName);

            // OPTIMIZATION: Start background task to verify creation (non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken); // Wait a bit before checking
                    var status = await GetContainerAppStatusAsync(adapterInstanceGuid, cancellationToken);
                    _logger.LogInformation(
                        "Container app status check (background): Name={Name}, Status={Status}",
                        containerAppName, status.Status);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background status check failed for {Name}", containerAppName);
                }
            }, cancellationToken);

            return new ContainerAppInfo
            {
                ContainerAppName = containerAppName,
                ContainerAppUrl = $"https://{containerAppName}.{environment.Data.DefaultDomain}",
                BlobStorageConnectionString = blobStorageInfo.ConnectionString,
                BlobContainerName = blobStorageInfo.ContainerName,
                Status = "Creating",
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error creating container app for adapter instance {Guid}",
                adapterInstanceGuid);
            throw;
        }
    }

    public async Task DeleteContainerAppAsync(
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default)
    {
        if (_armClient == null)
        {
            _logger.LogWarning("ARM client not initialized. Cannot delete container app.");
            return;
        }

        try
        {
            var containerAppName = GetContainerAppNamePrivate(adapterInstanceGuid);
            var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken);
            var resourceGroup = await subscription.GetResourceGroupAsync(_resourceGroupName, cancellationToken);

            if (resourceGroup.Value == null)
            {
                _logger.LogWarning("Resource group '{ResourceGroup}' not found", _resourceGroupName);
                return;
            }

            var environment = await GetOrCreateContainerAppEnvironmentAsync(
                resourceGroup.Value, cancellationToken);

            var containerAppCollection = resourceGroup.Value.GetContainerApps();
            try
            {
                var containerApp = await containerAppCollection.GetAsync(containerAppName, cancellationToken: cancellationToken);
                if (containerApp.Value != null)
                {
                    await containerApp.Value.DeleteAsync(Azure.WaitUntil.Started, cancellationToken);
                    _logger.LogInformation("Container app deletion initiated: {Name}", containerAppName);
                }
            }
            catch (Azure.RequestFailedException)
            {
                // Container app doesn't exist - this is fine
                _logger.LogInformation("Container app not found: {Name}", containerAppName);
            }

            // Also delete blob storage
            await DeleteBlobStorageForInstanceAsync(adapterInstanceGuid, resourceGroup.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting container app for adapter instance {Guid}", adapterInstanceGuid);
            throw;
        }
    }

    public async Task<ContainerAppStatus> GetContainerAppStatusAsync(
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default)
    {
        if (_armClient == null)
        {
            return new ContainerAppStatus
            {
                Exists = false,
                Status = "Unknown",
                ErrorMessage = "ARM client not initialized"
            };
        }

        try
        {
            var containerAppName = GetContainerAppNamePrivate(adapterInstanceGuid);
            var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken);
            var resourceGroup = await subscription.GetResourceGroupAsync(_resourceGroupName, cancellationToken);

            if (resourceGroup.Value == null)
            {
                return new ContainerAppStatus
                {
                    Exists = false,
                    Status = "NotFound",
                    ErrorMessage = $"Resource group '{_resourceGroupName}' not found"
                };
            }

            var environment = await GetOrCreateContainerAppEnvironmentAsync(
                resourceGroup.Value, cancellationToken);

            var containerAppCollection = resourceGroup.Value.GetContainerApps();
            ContainerAppResource? containerApp = null;
            try
            {
                var containerAppResponse = await containerAppCollection.GetAsync(containerAppName, cancellationToken: cancellationToken);
                containerApp = containerAppResponse.Value;
            }
            catch (Azure.RequestFailedException)
            {
                // Container app doesn't exist
            }
            
            if (containerApp == null)
            {
                return new ContainerAppStatus
                {
                    Exists = false,
                    Status = "NotFound",
                    LastChecked = DateTime.UtcNow
                };
            }

            return new ContainerAppStatus
            {
                Exists = true,
                Status = containerApp.Data.ProvisioningState?.ToString() ?? "Unknown",
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting container app status for {Guid}", adapterInstanceGuid);
            return new ContainerAppStatus
            {
                Exists = false,
                Status = "Error",
                ErrorMessage = ex.Message,
                LastChecked = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> ContainerAppExistsAsync(
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default)
    {
        var status = await GetContainerAppStatusAsync(adapterInstanceGuid, cancellationToken);
        return status.Exists;
    }

    public string GetContainerAppName(Guid adapterInstanceGuid)
    {
        // Container app names must be lowercase, alphanumeric, and hyphens only
        // Max 32 characters
        var guidStr = adapterInstanceGuid.ToString("N");
        return $"ca-{guidStr.Substring(0, Math.Min(24, guidStr.Length))}";
    }

    private string GetContainerAppNamePrivate(Guid adapterInstanceGuid)
    {
        return GetContainerAppName(adapterInstanceGuid);
    }

    private string GetAdapterImage(string adapterName)
    {
        // Map adapter name to Docker image
        return adapterName.ToLowerInvariant() switch
        {
            "csv" => $"{_registryServer}/csv-adapter:latest",
            "sqlserver" => $"{_registryServer}/sqlserver-adapter:latest",
            "file" => $"{_registryServer}/file-adapter:latest",
            "sftp" => $"{_registryServer}/sftp-adapter:latest",
            "sap" => $"{_registryServer}/sap-adapter:latest",
            "dynamics365" => $"{_registryServer}/dynamics365-adapter:latest",
            "crm" => $"{_registryServer}/crm-adapter:latest",
            _ => $"{_registryServer}/generic-adapter:latest"
        };
    }

    /// <summary>
    /// OPTIMIZED: Get or create environment with caching (only created once)
    /// Uses WaitUntil.Started for faster return
    /// </summary>
    private async Task<ContainerAppManagedEnvironmentResource> GetOrCreateContainerAppEnvironmentAsync(
        ResourceGroupResource resourceGroup,
        CancellationToken cancellationToken)
    {
        // OPTIMIZATION: Check cache first
        if (_cachedEnvironment != null)
        {
            try
            {
                // Verify environment still exists
                await _cachedEnvironment.GetAsync(cancellationToken: cancellationToken);
                return _cachedEnvironment;
            }
            catch
            {
                // Environment was deleted, clear cache
                _cachedEnvironment = null;
            }
        }

        await _environmentLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cachedEnvironment != null)
            {
                return _cachedEnvironment;
            }

            var environmentCollection = resourceGroup.GetContainerAppManagedEnvironments();
            try
            {
                var environment = await environmentCollection.GetAsync(_containerAppEnvironmentName, cancellationToken: cancellationToken);
                if (environment.Value != null)
                {
                    _cachedEnvironment = environment.Value;
                    return _cachedEnvironment;
                }
            }
            catch (Azure.RequestFailedException)
            {
                // Environment doesn't exist - create it
            }

            // OPTIMIZATION: Create environment if it doesn't exist
            // Log Analytics is disabled - using Azure's built-in logging instead
            var environmentData = new ContainerAppManagedEnvironmentData(_location)
            {
                // AppLogsConfiguration omitted - using Azure's default logging
            };

            // OPTIMIZATION: Use WaitUntil.Started instead of Completed for faster return
            // Environment creation can take 2-5 minutes, but we don't need to wait
            var environmentOperation = await environmentCollection.CreateOrUpdateAsync(
                Azure.WaitUntil.Started, // Changed from Completed to Started
                _containerAppEnvironmentName,
                environmentData,
                cancellationToken);

            // Get the environment resource (may still be provisioning)
            var environmentResponse = await environmentCollection.GetAsync(_containerAppEnvironmentName, cancellationToken: cancellationToken);
            _cachedEnvironment = environmentResponse.Value;
            return _cachedEnvironment;
        }
        finally
        {
            _environmentLock.Release();
        }
    }

    /// <summary>
    /// OPTIMIZED: Create blob storage asynchronously (WaitUntil.Started instead of Completed)
    /// </summary>
    private async Task<(string ConnectionString, string ContainerName)> CreateBlobStorageForInstanceAsync(
        Guid adapterInstanceGuid,
        ResourceGroupResource resourceGroup,
        CancellationToken cancellationToken)
    {
        var storageAccountName = $"st{adapterInstanceGuid.ToString("N").Substring(0, 20)}";
        var containerName = $"adapter-{adapterInstanceGuid.ToString("N").Substring(0, 8)}";

        // OPTIMIZATION: Check if storage account already exists
        var storageAccountCollection = resourceGroup.GetStorageAccounts();
        try
        {
            var existingStorage = await storageAccountCollection.GetAsync(storageAccountName, cancellationToken: cancellationToken);
            if (existingStorage.Value != null)
            {
                var existingKeyValue = await TryGetStorageAccountKeyAsync(existingStorage.Value, cancellationToken);
                if (!string.IsNullOrEmpty(existingKeyValue))
                {
                    var existingConnectionString = BuildBlobConnectionString(storageAccountName, existingKeyValue);
                    await EnsureContainerExistsAsync(existingConnectionString, containerName, cancellationToken);
                    return (existingConnectionString, containerName);
                }
            }
        }
        catch (Azure.RequestFailedException)
        {
            // Storage account doesn't exist - create it
        }

        var storageAccountData = new StorageAccountCreateOrUpdateContent(
            new StorageSku(StorageSkuName.StandardLrs),
            StorageKind.StorageV2,
            new Azure.Core.AzureLocation(_location));

        // OPTIMIZATION: Use WaitUntil.Started instead of Completed
        // Storage account creation can take 30-60 seconds, but we can start using it before it's fully ready
        var storageAccountOperation = await storageAccountCollection.CreateOrUpdateAsync(
            Azure.WaitUntil.Started, // Changed from Completed to Started
            storageAccountName,
            storageAccountData,
            cancellationToken);

        // Wait a short time for storage account to be ready for key retrieval
        // This is a compromise - we wait a bit but not the full creation time
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        // Get the storage account resource
        var storageAccount = await storageAccountCollection.GetAsync(storageAccountName, cancellationToken: cancellationToken);
        
        // Retry getting keys (storage account might still be provisioning)
        string? keyValue = null;
        int retries = 0;
        while (string.IsNullOrEmpty(keyValue) && retries < 10)
        {
            try
            {
                keyValue = await TryGetStorageAccountKeyAsync(storageAccount.Value, cancellationToken);
            }
            catch
            {
                // Keys not ready yet, wait and retry
            }

            if (string.IsNullOrEmpty(keyValue))
            {
                retries++;
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        if (string.IsNullOrEmpty(keyValue))
        {
            throw new InvalidOperationException($"Failed to retrieve storage account keys for {storageAccountName} after {retries} retries");
        }

        var connectionString = BuildBlobConnectionString(storageAccountName, keyValue);

        // Create container
        await EnsureContainerExistsAsync(connectionString, containerName, cancellationToken);

        return (connectionString, containerName);
    }

    /// <summary>
    /// OPTIMIZATION: Use shared storage account (much faster - no storage account creation needed)
    /// </summary>
    private async Task<(string ConnectionString, string ContainerName)> GetOrCreateSharedBlobStorageAsync(
        ResourceGroupResource resourceGroup,
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken)
    {
        if (_sharedStorageAccountName == null)
        {
            throw new InvalidOperationException("Shared storage account name not configured");
        }

        // Check cache first
        if (_sharedStorageAccount != null)
        {
            try
            {
                await _sharedStorageAccount.GetAsync(cancellationToken: cancellationToken);
            }
            catch
            {
                _sharedStorageAccount = null;
            }
        }

        await _sharedStorageLock.WaitAsync(cancellationToken);
        try
        {
            if (_sharedStorageAccount != null)
            {
                var keyValue = await TryGetStorageAccountKeyAsync(_sharedStorageAccount, cancellationToken);
                if (!string.IsNullOrEmpty(keyValue))
                {
                    var connectionString = BuildBlobConnectionString(_sharedStorageAccountName!, keyValue);
                    var containerName = $"adapter-{adapterInstanceGuid.ToString("N").Substring(0, 8)}";
                    await EnsureContainerExistsAsync(connectionString, containerName, cancellationToken);
                    return (connectionString, containerName);
                }
            }

            var storageAccountCollection = resourceGroup.GetStorageAccounts();
            try
            {
                var storageAccount = await storageAccountCollection.GetAsync(_sharedStorageAccountName, cancellationToken: cancellationToken);
                _sharedStorageAccount = storageAccount.Value;
            }
            catch (Azure.RequestFailedException)
            {
                // Create shared storage account (only once)
                var storageAccountData = new StorageAccountCreateOrUpdateContent(
                    new StorageSku(StorageSkuName.StandardLrs),
                    StorageKind.StorageV2,
                    new Azure.Core.AzureLocation(_location));

                await storageAccountCollection.CreateOrUpdateAsync(
                    Azure.WaitUntil.Started,
                    _sharedStorageAccountName,
                    storageAccountData,
                    cancellationToken);

                // Wait a bit for storage account to be ready
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                var storageAccount = await storageAccountCollection.GetAsync(_sharedStorageAccountName, cancellationToken: cancellationToken);
                _sharedStorageAccount = storageAccount.Value;
            }

            var cachedKey = await TryGetStorageAccountKeyAsync(_sharedStorageAccount!, cancellationToken);
            if (string.IsNullOrEmpty(cachedKey))
            {
                throw new InvalidOperationException($"Failed to retrieve shared storage account key for {_sharedStorageAccountName}");
            }

            var sharedConnectionString = BuildBlobConnectionString(_sharedStorageAccountName!, cachedKey);
            var sharedContainerName = $"adapter-{adapterInstanceGuid.ToString("N").Substring(0, 8)}";
            await EnsureContainerExistsAsync(sharedConnectionString, sharedContainerName, cancellationToken);
            
            return (sharedConnectionString, sharedContainerName);
        }
        finally
        {
            _sharedStorageLock.Release();
        }
    }

    private async Task DeleteBlobStorageForInstanceAsync(
        Guid adapterInstanceGuid,
        ResourceGroupResource resourceGroup,
        CancellationToken cancellationToken)
    {
        var storageAccountName = $"st{adapterInstanceGuid.ToString("N").Substring(0, 20)}";
        var storageAccountCollection = resourceGroup.GetStorageAccounts();
        try
        {
            var storageAccount = await storageAccountCollection.GetAsync(storageAccountName, cancellationToken: cancellationToken);
            if (storageAccount.Value != null)
            {
                await storageAccount.Value.DeleteAsync(Azure.WaitUntil.Started, cancellationToken);
                _logger.LogInformation("Blob storage deletion initiated: {Name}", storageAccountName);
            }
        }
        catch (Azure.RequestFailedException)
        {
            // Storage account doesn't exist - this is fine
            _logger.LogInformation("Storage account not found: {Name}", storageAccountName);
        }
    }

    private static string BuildBlobConnectionString(string accountName, string accountKey)
        => $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={accountKey};EndpointSuffix=core.windows.net";

    private static async Task EnsureContainerExistsAsync(string connectionString, string containerName, CancellationToken cancellationToken)
    {
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    }

    private static async Task<string?> TryGetStorageAccountKeyAsync(StorageAccountResource storageAccount, CancellationToken cancellationToken)
    {
        await foreach (var key in storageAccount.GetKeysAsync(cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(key.Value))
            {
                return key.Value;
            }
        }

        return null;
    }

    private async Task StoreAdapterConfigurationAsync(
        string connectionString,
        string containerName,
        object adapterConfiguration,
        CancellationToken cancellationToken)
    {
        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient("adapter-config.json");
            var configJson = System.Text.Json.JsonSerializer.Serialize(adapterConfiguration, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await blobClient.UploadAsync(
                new BinaryData(configJson),
                overwrite: true,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Stored adapter configuration in blob storage: Container={Container}, Blob=adapter-config.json",
                containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing adapter configuration in blob storage");
            throw;
        }
    }

    public async Task UpdateContainerAppConfigurationAsync(
        Guid adapterInstanceGuid,
        object adapterConfiguration,
        CancellationToken cancellationToken = default)
    {
        if (_armClient == null)
        {
            throw new InvalidOperationException("ARM client not initialized. Cannot update container app configuration.");
        }

        try
        {
            _logger.LogInformation(
                "Updating container app configuration for adapter instance: Guid={Guid}",
                adapterInstanceGuid);

            var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken);
            var resourceGroup = await subscription.GetResourceGroupAsync(_resourceGroupName, cancellationToken);

            if (resourceGroup.Value == null)
            {
                throw new InvalidOperationException($"Resource group '{_resourceGroupName}' not found");
            }

            // Get blob storage info for this instance
            var storageAccountName = $"st{adapterInstanceGuid.ToString("N").Substring(0, 20)}";
            var containerName = $"adapter-{adapterInstanceGuid.ToString("N").Substring(0, 8)}";

            var storageAccountCollection = resourceGroup.Value.GetStorageAccounts();
            var storageAccountResponse = await storageAccountCollection.GetAsync(storageAccountName, cancellationToken: cancellationToken);
            var storageAccount = storageAccountResponse.Value;
            if (storageAccount == null)
            {
                throw new InvalidOperationException($"Storage account '{storageAccountName}' not found for adapter instance {adapterInstanceGuid}");
            }

            var keyValue = await TryGetStorageAccountKeyAsync(storageAccount, cancellationToken);
            if (string.IsNullOrEmpty(keyValue))
            {
                throw new InvalidOperationException($"Failed to retrieve storage account key for '{storageAccountName}'.");
            }
            var connectionString = BuildBlobConnectionString(storageAccountName, keyValue);

            // Update adapter configuration in blob storage
            await StoreAdapterConfigurationAsync(connectionString, containerName, adapterConfiguration, cancellationToken);

            _logger.LogInformation(
                "Container app configuration updated: Guid={Guid}",
                adapterInstanceGuid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error updating container app configuration for adapter instance {Guid}",
                adapterInstanceGuid);
            throw;
        }
    }
}


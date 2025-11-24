using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;

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
            _logger.LogInformation(
                "Creating container app for adapter instance: Guid={Guid}, Adapter={Adapter}, Type={Type}, Interface={Interface}",
                adapterInstanceGuid, adapterName, adapterType, interfaceName);

            var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken);
            var resourceGroup = await subscription.GetResourceGroupAsync(_resourceGroupName, cancellationToken);

            if (resourceGroup.Value == null)
            {
                throw new InvalidOperationException($"Resource group '{_resourceGroupName}' not found");
            }

            // Create blob storage for this container app instance
            var blobStorageInfo = await CreateBlobStorageForInstanceAsync(
                adapterInstanceGuid, resourceGroup.Value, cancellationToken);

            // Store adapter configuration in blob storage
            await StoreAdapterConfigurationAsync(
                blobStorageInfo.ConnectionString,
                blobStorageInfo.ContainerName,
                adapterConfiguration,
                cancellationToken);

            // Get container app environment
            var environment = await GetOrCreateContainerAppEnvironmentAsync(
                resourceGroup.Value, cancellationToken);

            // Create container app
            var containerAppName = GetContainerAppName(adapterInstanceGuid);
            var containerAppData = new ContainerAppData(_location)
            {
                ManagedEnvironmentId = environment.Id,
                Configuration = new ContainerAppConfiguration
                {
                    Ingress = new ContainerAppIngressConfiguration
                    {
                        External = false,
                        TargetPort = 8080,
                        Transport = ContainerAppIngressTransport.Http
                    },
                    Registries =
                    {
                        new ContainerAppRegistryCredentials
                        {
                            Server = _registryServer,
                            Username = _registryUsername,
                            PasswordSecretRef = "registry-password"
                        }
                    },
                    Secrets =
                    {
                        new ContainerAppSecret("registry-password", _registryPassword),
                        new ContainerAppSecret("blob-connection-string", blobStorageInfo.ConnectionString)
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
                                new ContainerAppEnvironmentVariable("ADAPTER_INSTANCE_GUID")
                                {
                                    Value = adapterInstanceGuid.ToString()
                                },
                                new ContainerAppEnvironmentVariable("ADAPTER_NAME")
                                {
                                    Value = adapterName
                                },
                                new ContainerAppEnvironmentVariable("ADAPTER_TYPE")
                                {
                                    Value = adapterType
                                },
                                new ContainerAppEnvironmentVariable("INTERFACE_NAME")
                                {
                                    Value = interfaceName
                                },
                                new ContainerAppEnvironmentVariable("INSTANCE_NAME")
                                {
                                    Value = instanceName
                                },
                                new ContainerAppEnvironmentVariable("BLOB_CONNECTION_STRING")
                                {
                                    SecretRef = "blob-connection-string"
                                },
                                new ContainerAppEnvironmentVariable("BLOB_CONTAINER_NAME")
                                {
                                    Value = blobStorageInfo.ContainerName
                                },
                                new ContainerAppEnvironmentVariable("ADAPTER_CONFIG_PATH")
                                {
                                    Value = "adapter-config.json"
                                }
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

            var containerAppCollection = environment.GetContainerApps();
            var containerAppOperation = await containerAppCollection.CreateOrUpdateAsync(
                WaitUntil.Started,
                containerAppName,
                containerAppData,
                cancellationToken);

            _logger.LogInformation(
                "Container app creation initiated: Name={Name}, Guid={Guid}",
                containerAppName, adapterInstanceGuid);

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
            var containerAppName = GetContainerAppName(adapterInstanceGuid);
            var subscription = await _armClient.GetDefaultSubscriptionAsync(cancellationToken);
            var resourceGroup = await subscription.GetResourceGroupAsync(_resourceGroupName, cancellationToken);

            if (resourceGroup.Value == null)
            {
                _logger.LogWarning("Resource group '{ResourceGroup}' not found", _resourceGroupName);
                return;
            }

            var environment = await GetOrCreateContainerAppEnvironmentAsync(
                resourceGroup.Value, cancellationToken);

            var containerApp = await environment.GetContainerAppAsync(containerAppName, cancellationToken);
            if (containerApp.Value != null)
            {
                await containerApp.Value.DeleteAsync(WaitUntil.Started, cancellationToken);
                _logger.LogInformation("Container app deletion initiated: {Name}", containerAppName);
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
            var containerAppName = GetContainerAppName(adapterInstanceGuid);
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

            var containerApp = await environment.GetContainerAppAsync(containerAppName, cancellationToken);
            if (containerApp.Value == null)
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
                Status = containerApp.Value.Data.ProvisioningState ?? "Unknown",
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

    private string GetContainerAppName(Guid adapterInstanceGuid)
    {
        // Container app names must be lowercase, alphanumeric, and hyphens only
        // Max 32 characters
        var guidStr = adapterInstanceGuid.ToString("N");
        return $"ca-{guidStr.Substring(0, Math.Min(24, guidStr.Length))}";
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

    private async Task<ContainerAppManagedEnvironmentResource> GetOrCreateContainerAppEnvironmentAsync(
        ResourceGroupResource resourceGroup,
        CancellationToken cancellationToken)
    {
        var environmentCollection = resourceGroup.GetContainerAppManagedEnvironments();
        var environment = await environmentCollection.GetAsync(_containerAppEnvironmentName, cancellationToken);

        if (environment.Value != null)
        {
            return environment.Value;
        }

        // Create environment if it doesn't exist
        var environmentData = new ContainerAppManagedEnvironmentData(_location)
        {
            AppLogsConfiguration = new ContainerAppLogsConfiguration
            {
                Destination = "log-analytics",
                LogAnalyticsConfiguration = new ContainerAppLogAnalyticsConfiguration
                {
                    CustomerId = _configuration["LogAnalyticsWorkspaceId"] ?? "",
                    SharedKey = _configuration["LogAnalyticsSharedKey"] ?? ""
                }
            }
        };

        var environmentOperation = await environmentCollection.CreateOrUpdateAsync(
            WaitUntil.Completed,
            _containerAppEnvironmentName,
            environmentData,
            cancellationToken);

        return environmentOperation.Value;
    }

    private async Task<(string ConnectionString, string ContainerName)> CreateBlobStorageForInstanceAsync(
        Guid adapterInstanceGuid,
        ResourceGroupResource resourceGroup,
        CancellationToken cancellationToken)
    {
        var storageAccountName = $"st{adapterInstanceGuid.ToString("N").Substring(0, 20)}";
        var containerName = $"adapter-{adapterInstanceGuid.ToString("N").Substring(0, 8)}";

        var storageAccountData = new StorageAccountData(_location)
        {
            Kind = StorageKind.StorageV2,
            Sku = new StorageSku(StorageSkuName.StandardLrs)
        };

        var storageAccountCollection = resourceGroup.GetStorageAccounts();
        var storageAccount = await storageAccountCollection.CreateOrUpdateAsync(
            WaitUntil.Completed,
            storageAccountName,
            storageAccountData,
            cancellationToken);

        var keys = await storageAccount.Value.GetKeysAsync(cancellationToken);
        var key = keys.Value.Keys.FirstOrDefault();
        var connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={key?.Value};EndpointSuffix=core.windows.net";

        // Create container
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        return (connectionString, containerName);
    }

    private async Task DeleteBlobStorageForInstanceAsync(
        Guid adapterInstanceGuid,
        ResourceGroupResource resourceGroup,
        CancellationToken cancellationToken)
    {
        var storageAccountName = $"st{adapterInstanceGuid.ToString("N").Substring(0, 20)}";
        var storageAccount = await resourceGroup.GetStorageAccountAsync(storageAccountName, cancellationToken);

        if (storageAccount.Value != null)
        {
            await storageAccount.Value.DeleteAsync(WaitUntil.Started, cancellationToken);
            _logger.LogInformation("Blob storage deletion initiated: {Name}", storageAccountName);
        }
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

            var storageAccount = await resourceGroup.Value.GetStorageAccountAsync(storageAccountName, cancellationToken);
            if (storageAccount.Value == null)
            {
                throw new InvalidOperationException($"Storage account '{storageAccountName}' not found for adapter instance {adapterInstanceGuid}");
            }

            var keys = await storageAccount.Value.GetKeysAsync(cancellationToken);
            var key = keys.Value.Keys.FirstOrDefault();
            var connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={key?.Value};EndpointSuffix=core.windows.net";

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


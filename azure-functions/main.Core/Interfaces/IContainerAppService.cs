using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Service for managing Azure Container Apps for adapter instances
/// Each adapter instance runs in its own isolated container app
/// </summary>
public interface IContainerAppService
{
    /// <summary>
    /// Create a container app for an adapter instance
    /// </summary>
    /// <param name="adapterInstanceGuid">Unique identifier for the adapter instance</param>
    /// <param name="adapterName">Type of adapter (CSV, SqlServer, etc.)</param>
    /// <param name="adapterType">Source or Destination</param>
    /// <param name="interfaceName">Interface name this adapter belongs to</param>
    /// <param name="instanceName">User-friendly instance name</param>
    /// <param name="adapterConfiguration">Full adapter instance configuration (all settings)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Container app details including name, URL, and blob storage connection</returns>
    Task<ContainerAppInfo> CreateContainerAppAsync(
        Guid adapterInstanceGuid,
        string adapterName,
        string adapterType,
        string interfaceName,
        string instanceName,
        object adapterConfiguration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update adapter instance configuration in container app
    /// </summary>
    Task UpdateContainerAppConfigurationAsync(
        Guid adapterInstanceGuid,
        object adapterConfiguration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a container app for an adapter instance
    /// </summary>
    Task DeleteContainerAppAsync(
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get container app status
    /// </summary>
    Task<ContainerAppStatus> GetContainerAppStatusAsync(
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if container app exists
    /// </summary>
    Task<bool> ContainerAppExistsAsync(
        Guid adapterInstanceGuid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get container app name for an adapter instance GUID
    /// </summary>
    string GetContainerAppName(Guid adapterInstanceGuid);
}

/// <summary>
/// Information about a created container app
/// </summary>
public class ContainerAppInfo
{
    public string ContainerAppName { get; set; } = string.Empty;
    public string ContainerAppUrl { get; set; } = string.Empty;
    public string BlobStorageConnectionString { get; set; } = string.Empty;
    public string BlobContainerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Status of a container app
/// </summary>
public class ContainerAppStatus
{
    public bool Exists { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime? LastChecked { get; set; }
}


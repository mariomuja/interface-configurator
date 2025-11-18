using System.ComponentModel.DataAnnotations;

namespace ProcessCsvBlobTrigger.Core.Models;

/// <summary>
/// Represents a destination adapter instance configuration
/// Multiple destination adapter instances can be configured for a single interface
/// All instances subscribe to the same MessageBox data from the source adapter
/// </summary>
public class DestinationAdapterInstance
{
    /// <summary>
    /// Unique GUID identifying this destination adapter instance
    /// </summary>
    [Required]
    public Guid AdapterInstanceGuid { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User-editable name for this adapter instance (default: "Destination")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string InstanceName { get; set; } = "Destination";

    /// <summary>
    /// Adapter type name (e.g., "CSV", "SqlServer")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string AdapterName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this adapter instance is enabled
    /// </summary>
    [Required]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Destination configuration JSON (e.g., {"destination": "TransportData"} or {"destination": "csv-files/csv-outgoing"})
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Configuration { get; set; } = string.Empty;

    /// <summary>
    /// When this instance was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this instance was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}





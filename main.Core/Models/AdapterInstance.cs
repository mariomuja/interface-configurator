using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Represents an adapter instance in the MessageBox database
/// Tracks which adapter instances have written data to the MessageBox
/// </summary>
[Table("AdapterInstances")]
public class AdapterInstance
{
    [Key]
    [Column("AdapterInstanceGuid")]
    public Guid AdapterInstanceGuid { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("InterfaceName")]
    public string InterfaceName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("InstanceName")]
    public string InstanceName { get; set; } = string.Empty; // User-editable name (SourceInstanceName or DestinationInstanceName)

    [Required]
    [MaxLength(100)]
    [Column("AdapterName")]
    public string AdapterName { get; set; } = string.Empty; // e.g., "CSV", "SqlServer"

    [Required]
    [Column("Configuration")]
    public string Configuration { get; set; } = string.Empty;

    [Column("SourceAdapterGuid")]
    public Guid? SourceAdapterGuid { get; set; }
    
    /// <summary>
    /// Determines if this adapter instance is a source adapter.
    /// Source adapters have SourceAdapterGuid = null/empty.
    /// </summary>
    public bool IsSourceAdapter => !SourceAdapterGuid.HasValue || SourceAdapterGuid.Value == Guid.Empty;
    
    /// <summary>
    /// Determines if this adapter instance is a destination adapter.
    /// Destination adapters have SourceAdapterGuid set (not null/empty).
    /// </summary>
    public bool IsDestinationAdapter => SourceAdapterGuid.HasValue && SourceAdapterGuid.Value != Guid.Empty;

    [Required]
    [Column("datetime_created", TypeName = "datetime2")]
    public DateTime datetime_created { get; set; } = DateTime.UtcNow;

    [Column("datetime_updated", TypeName = "datetime2")]
    public DateTime? datetime_updated { get; set; }

    [Column("IsEnabled")]
    public bool IsEnabled { get; set; } = true;
}





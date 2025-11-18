using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProcessCsvBlobTrigger.Core.Models;

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
    [MaxLength(50)]
    [Column("AdapterType")]
    public string AdapterType { get; set; } = string.Empty; // "Source" or "Destination"

    [Required]
    [Column("datetime_created", TypeName = "datetime2")]
    public DateTime datetime_created { get; set; } = DateTime.UtcNow;

    [Column("datetime_updated", TypeName = "datetime2")]
    public DateTime? datetime_updated { get; set; }

    [Column("IsEnabled")]
    public bool IsEnabled { get; set; } = true;
}





using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProcessCsvBlobTrigger.Core.Models;

/// <summary>
/// Represents adapter configuration for data integration interfaces
/// Similar to Logic Apps connectors - allows swapping source and destination adapters
/// </summary>
[Table("AdapterSettings")]
public class AdapterConfiguration
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string AdapterName { get; set; } = string.Empty; // e.g., "CSV", "JSON", "SAP", "SQLServer"
    
    [Required]
    [MaxLength(50)]
    public string AdapterType { get; set; } = string.Empty; // "Source" or "Destination"
    
    [Required]
    [MaxLength(200)]
    public string SettingKey { get; set; } = string.Empty; // e.g., "FieldSeparator", "Encoding", "ConnectionString"
    
    [MaxLength(1000)]
    public string? SettingValue { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [Required]
    [Column("datetime_created", TypeName = "datetime2")]
    public DateTime datetime_created { get; set; } = DateTime.UtcNow;
    
    [Column("datetime_updated", TypeName = "datetime2")]
    public DateTime? datetime_updated { get; set; }
    
    [Required]
    public bool IsActive { get; set; } = true;
}







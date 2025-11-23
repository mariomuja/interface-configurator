using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Defines subscription filters for destination adapters (BizTalk-style)
/// A subscription specifies which messages from the MessageBox an adapter is interested in receiving
/// This is a configuration that defines filter criteria, not a tracking record
/// </summary>
[Table("AdapterSubscriptions")]
public class AdapterSubscription
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("AdapterInstanceGuid")]
    public Guid AdapterInstanceGuid { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("InterfaceName")]
    public string InterfaceName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("AdapterName")]
    public string AdapterName { get; set; } = string.Empty; // e.g., "SqlServer", "CSV"

    /// <summary>
    /// Filter criteria as JSON (e.g., {"InterfaceName": "FromCsvToSqlServer", "AdapterType": "Source"})
    /// Allows for flexible filtering beyond just InterfaceName
    /// </summary>
    [Column("FilterCriteria", TypeName = "nvarchar(max)")]
    public string? FilterCriteria { get; set; }

    /// <summary>
    /// Whether this subscription is active
    /// </summary>
    [Required]
    [Column("IsEnabled")]
    public bool IsEnabled { get; set; } = true;

    [Required]
    [Column("datetime_created", TypeName = "datetime2")]
    public DateTime datetime_created { get; set; } = DateTime.UtcNow;

    [Column("datetime_updated", TypeName = "datetime2")]
    public DateTime? datetime_updated { get; set; }
}



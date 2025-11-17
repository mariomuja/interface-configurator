using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProcessCsvBlobTrigger.Core.Models;

/// <summary>
/// Represents a message in the MessageBox staging area
/// Similar to Microsoft BizTalk Server message box architecture
/// </summary>
[Table("Messages")]
public class MessageBoxMessage
{
    [Key]
    [Column("MessageId")]
    public Guid MessageId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    [Column("InterfaceName")]
    public string InterfaceName { get; set; } = string.Empty; // e.g., "FromCsvToSqlServerExample"

    [Required]
    [MaxLength(100)]
    [Column("AdapterName")]
    public string AdapterName { get; set; } = string.Empty; // e.g., "CSV", "SqlServer"

    [Required]
    [MaxLength(50)]
    [Column("AdapterType")]
    public string AdapterType { get; set; } = string.Empty; // "Source" or "Destination"

    [Required]
    [Column("MessageData", TypeName = "nvarchar(max)")]
    public string MessageData { get; set; } = string.Empty; // JSON: {"headers": [...], "record": {...}} - single record per message

    [Required]
    [MaxLength(50)]
    [Column("Status")]
    public string Status { get; set; } = "Pending"; // "Pending", "Processed", "Error"

    [Required]
    [Column("datetime_created", TypeName = "datetime2")]
    public DateTime datetime_created { get; set; } = DateTime.UtcNow;

    [Column("datetime_processed", TypeName = "datetime2")]
    public DateTime? datetime_processed { get; set; }

    [Column("ErrorMessage", TypeName = "nvarchar(max)")]
    public string? ErrorMessage { get; set; }

    [Column("ProcessingDetails", TypeName = "nvarchar(max)")]
    public string? ProcessingDetails { get; set; }
}



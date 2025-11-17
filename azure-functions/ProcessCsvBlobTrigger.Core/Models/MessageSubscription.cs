using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProcessCsvBlobTrigger.Core.Models;

/// <summary>
/// Tracks which destination adapters have processed which messages
/// Ensures messages are only removed after all subscribers have successfully processed them
/// </summary>
[Table("MessageSubscriptions")]
public class MessageSubscription
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("MessageId")]
    public Guid MessageId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("InterfaceName")]
    public string InterfaceName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("SubscriberAdapterName")]
    public string SubscriberAdapterName { get; set; } = string.Empty; // e.g., "SqlServer", "CSV"

    [Required]
    [MaxLength(50)]
    [Column("Status")]
    public string Status { get; set; } = "Pending"; // "Pending", "Processed", "Error"

    [Required]
    [Column("datetime_created", TypeName = "datetime2")]
    public DateTime datetime_created { get; set; } = DateTime.UtcNow;

    [Column("datetime_processed", TypeName = "datetime2")]
    public DateTime? datetime_processed { get; set; }

    [Column("ProcessingDetails", TypeName = "nvarchar(max)")]
    public string? ProcessingDetails { get; set; }

    [Column("ErrorMessage", TypeName = "nvarchar(max)")]
    public string? ErrorMessage { get; set; }
}


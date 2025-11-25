using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InterfaceConfigurator.Main.Core.Models;

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
    [Column("AdapterInstanceGuid")]
    public Guid AdapterInstanceGuid { get; set; } // GUID identifying the adapter instance that created this message

    [Required]
    [Column("MessageData", TypeName = "nvarchar(max)")]
    public string MessageData { get; set; } = string.Empty; // JSON: {"headers": [...], "record": {...}} - single record per message

    [Required]
    [MaxLength(50)]
    [Column("Status")]
    public string Status { get; set; } = "Pending"; // "Pending", "InProgress", "Processed", "Error", "DeadLetter"

    [Required]
    [Column("datetime_created", TypeName = "datetime2")]
    public DateTime datetime_created { get; set; } = DateTime.UtcNow;

    [Column("datetime_processed", TypeName = "datetime2")]
    public DateTime? datetime_processed { get; set; }

    [Column("ErrorMessage", TypeName = "nvarchar(max)")]
    public string? ErrorMessage { get; set; }

    [Column("ProcessingDetails", TypeName = "nvarchar(max)")]
    public string? ProcessingDetails { get; set; }

    /// <summary>
    /// Number of retry attempts made for this message
    /// </summary>
    [Column("RetryCount")]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum number of retry attempts before moving to dead letter
    /// Default: 3
    /// </summary>
    [Column("MaxRetries")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timestamp of the last retry attempt
    /// </summary>
    [Column("LastRetryTime", TypeName = "datetime2")]
    public DateTime? LastRetryTime { get; set; }

    /// <summary>
    /// Timestamp when message was locked for processing (InProgress status)
    /// Used to detect stale locks and allow retry after timeout
    /// </summary>
    [Column("InProgressUntil", TypeName = "datetime2")]
    public DateTime? InProgressUntil { get; set; }

    /// <summary>
    /// Whether this message has been moved to dead letter queue
    /// </summary>
    [Column("DeadLetter")]
    public bool DeadLetter { get; set; } = false;

    /// <summary>
    /// Hash of message data for idempotency checking
    /// </summary>
    [MaxLength(64)]
    [Column("MessageHash")]
    public string? MessageHash { get; set; }
}



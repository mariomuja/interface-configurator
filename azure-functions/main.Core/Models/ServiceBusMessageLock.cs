using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Tracks Service Bus message locks to prevent message loss during Container App restarts
/// Stores lock tokens and message metadata for recovery
/// </summary>
[Table("ServiceBusMessageLocks")]
public class ServiceBusMessageLock
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("MessageId")]
    public string MessageId { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Column("LockToken")]
    public string LockToken { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("TopicName")]
    public string TopicName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("SubscriptionName")]
    public string SubscriptionName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("InterfaceName")]
    public string InterfaceName { get; set; } = string.Empty;

    [Required]
    [Column("AdapterInstanceGuid")]
    public Guid AdapterInstanceGuid { get; set; }

    [Required]
    [Column("LockAcquiredAt")]
    public DateTime LockAcquiredAt { get; set; } = DateTime.UtcNow;

    [Column("LockExpiresAt")]
    public DateTime LockExpiresAt { get; set; }

    [Column("LastRenewedAt")]
    public DateTime? LastRenewedAt { get; set; }

    [Column("RenewalCount")]
    public int RenewalCount { get; set; } = 0;

    [Required]
    [Column("Status")]
    [MaxLength(50)]
    public string Status { get; set; } = "Active"; // Active, Completed, Abandoned, DeadLettered, Expired

    [Column("CompletedAt")]
    public DateTime? CompletedAt { get; set; }

    [MaxLength(1000)]
    [Column("CompletionReason")]
    public string? CompletionReason { get; set; }

    [Column("DeliveryCount")]
    public int DeliveryCount { get; set; } = 1;

    [Column("CreatedAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}


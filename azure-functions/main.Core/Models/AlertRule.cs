using System.ComponentModel.DataAnnotations;

namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Alert rule configuration
/// </summary>
public class AlertRule
{
    [Required]
    [MaxLength(200)]
    public string RuleName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string InterfaceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Alert condition type: ErrorRate, ProcessingTime, ConnectionFailure, SchemaDrift, QueueDepth, Custom
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ConditionType { get; set; } = string.Empty;
    
    /// <summary>
    /// Condition expression (e.g., "ErrorRate > 0.05" for 5% error rate)
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string ConditionExpression { get; set; } = string.Empty;
    
    /// <summary>
    /// Threshold value for the condition
    /// </summary>
    public double? ThresholdValue { get; set; }
    
    /// <summary>
    /// Time window in minutes for evaluating the condition
    /// </summary>
    public int? TimeWindowMinutes { get; set; } = 5;
    
    /// <summary>
    /// Alert severity: Critical, Warning, Info
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = "Warning";
    
    /// <summary>
    /// Whether this alert rule is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Notification channels: Email, Slack, Teams, SMS, Webhook
    /// </summary>
    public List<string> NotificationChannels { get; set; } = new();
    
    /// <summary>
    /// Email addresses to notify
    /// </summary>
    public List<string> EmailRecipients { get; set; } = new();
    
    /// <summary>
    /// Webhook URL for notifications
    /// </summary>
    [MaxLength(500)]
    public string? WebhookUrl { get; set; }
    
    /// <summary>
    /// Cooldown period in minutes (prevent duplicate alerts)
    /// </summary>
    public int CooldownMinutes { get; set; } = 15;
    
    /// <summary>
    /// Last time this alert was triggered
    /// </summary>
    public DateTime? LastTriggered { get; set; }
    
    /// <summary>
    /// Custom message template for the alert
    /// </summary>
    [MaxLength(1000)]
    public string? MessageTemplate { get; set; }
}

/// <summary>
/// Alert notification record
/// </summary>
public class AlertNotification
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string AlertRuleName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string InterfaceName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string Severity { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;
    
    public DateTime TriggeredDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether this alert has been acknowledged
    /// </summary>
    public bool IsAcknowledged { get; set; } = false;
    
    public DateTime? AcknowledgedDate { get; set; }
    
    [MaxLength(200)]
    public string? AcknowledgedBy { get; set; }
    
    /// <summary>
    /// Additional context data (JSON)
    /// </summary>
    [MaxLength(2000)]
    public string? ContextData { get; set; }
}


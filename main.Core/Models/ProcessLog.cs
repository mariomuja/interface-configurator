using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InterfaceConfigurator.Main.Core.Models;

[Table("ProcessLogs")]
public class ProcessLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Required]
    [Column("datetime_created", TypeName = "datetime2")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public DateTime datetime_created
    {
        get => Timestamp;
        set => Timestamp = value;
    }
    
    [Required]
    [MaxLength(50)]
    public string Level { get; set; } = string.Empty;
    
    [Required]
    [MaxLength]
    public string Message { get; set; } = string.Empty;
    
    [MaxLength]
    public string? Details { get; set; }

    [MaxLength(200)]
    [Column("Component")]
    public string? Component { get; set; }

    [MaxLength(200)]
    [Column("InterfaceName")]
    public string? InterfaceName { get; set; }
}


using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProcessCsvBlobTrigger.Models;

[Table("ProcessLogs")]
public class ProcessLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    // Every SQL table MUST have a datetime_created column with DEFAULT GETUTCDATE()
    [Required]
    [Column("datetime_created", TypeName = "datetime2")]
    public DateTime datetime_created { get; set; } = DateTime.UtcNow;
    
    // Keep Timestamp for backward compatibility (maps to same column)
    [NotMapped]
    public DateTime Timestamp 
    { 
        get => datetime_created; 
        set => datetime_created = value; 
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
    
    [Column("MessageId")]
    public Guid? MessageId { get; set; }
}



using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProcessCsvBlobTrigger.Core.Models;

[Table("ProcessLogs")]
public class ProcessLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [Required]
    [MaxLength(50)]
    public string Level { get; set; } = string.Empty;
    
    [Required]
    [MaxLength]
    public string Message { get; set; } = string.Empty;
    
    [MaxLength]
    public string? Details { get; set; }
}


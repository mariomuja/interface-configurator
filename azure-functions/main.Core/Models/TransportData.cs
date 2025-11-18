using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProcessCsvBlobTrigger.Core.Models;

[Table("TransportData")]
public class TransportData
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // Store all CSV columns as a dictionary
    // This allows dynamic mapping of any CSV structure
    public Dictionary<string, string> CsvColumns { get; set; } = new();
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


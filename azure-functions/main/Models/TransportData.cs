using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace InterfaceConfigurator.Main.Models;

[Table("TransportData")]
public class TransportData
{
    [Key]
    [Column("PrimaryKey", TypeName = "uniqueidentifier")]
    // NEVER use IDENTITY for primary keys - ALWAYS use GUID
    // Default value is set in SQL Server: DEFAULT NEWID()
    // Primary key column name is PrimaryKey to avoid conflicts with CSV 'id' columns
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid PrimaryKey { get; set; } = Guid.NewGuid();
    
    // Backward compatibility - map Id to PrimaryKey
    [NotMapped]
    public Guid Id 
    { 
        get => PrimaryKey; 
        set => PrimaryKey = value; 
    }
    
    // Every SQL table MUST have a datetime_created column with DEFAULT GETUTCDATE()
    [Required]
    [Column("datetime_created", TypeName = "datetime2")]
    public DateTime datetime_created { get; set; } = DateTime.UtcNow;
    
    // Keep CreatedAt for backward compatibility (maps to same column)
    [NotMapped]
    public DateTime CreatedAt 
    { 
        get => datetime_created; 
        set => datetime_created = value; 
    }
    
}



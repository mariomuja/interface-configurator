using System.ComponentModel.DataAnnotations;

namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Schema definition for CSV or SQL table
/// </summary>
public class SchemaDefinition
{
    [Required]
    [MaxLength(200)]
    public string SchemaName { get; set; } = string.Empty; // e.g., "CustomerOrders_CSV", "TransportData_SQL"
    
    [Required]
    [MaxLength(200)]
    public string InterfaceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Schema type: CSV or SQL
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string SchemaType { get; set; } = string.Empty; // "CSV" or "SQL"
    
    /// <summary>
    /// Schema version (semantic versioning: e.g., "1.0.0")
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";
    
    public List<ColumnDefinition> Columns { get; set; } = new();
    
    /// <summary>
    /// When this schema was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this schema was last updated
    /// </summary>
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether this schema is active (only one active schema per interface/type)
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Description of the schema
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}

/// <summary>
/// Column definition within a schema
/// </summary>
public class ColumnDefinition
{
    [Required]
    [MaxLength(200)]
    public string ColumnName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string DataType { get; set; } = string.Empty; // e.g., "INT", "NVARCHAR", "DATETIME2"
    
    /// <summary>
    /// Maximum length for string types
    /// </summary>
    public int? MaxLength { get; set; }
    
    /// <summary>
    /// Precision for decimal types
    /// </summary>
    public int? Precision { get; set; }
    
    /// <summary>
    /// Scale for decimal types
    /// </summary>
    public int? Scale { get; set; }
    
    /// <summary>
    /// Whether this column is nullable
    /// </summary>
    public bool IsNullable { get; set; } = true;
    
    /// <summary>
    /// Whether this column is required (NOT NULL)
    /// </summary>
    public bool IsRequired { get; set; } = false;
    
    /// <summary>
    /// Default value for this column
    /// </summary>
    [MaxLength(500)]
    public string? DefaultValue { get; set; }
    
    /// <summary>
    /// Validation pattern (regex) for this column
    /// </summary>
    [MaxLength(500)]
    public string? ValidationPattern { get; set; }
    
    /// <summary>
    /// Description of this column
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }
}

/// <summary>
/// Schema validation result
/// </summary>
public class SchemaValidationResult
{
    public bool IsValid { get; set; }
    public List<SchemaValidationIssue> Issues { get; set; } = new();
    public int TotalColumns { get; set; }
    public int ValidColumns { get; set; }
    public int MissingColumns { get; set; }
    public int ExtraColumns { get; set; }
    public int TypeMismatches { get; set; }
}

/// <summary>
/// Schema validation issue
/// </summary>
public class SchemaValidationIssue
{
    [Required]
    [MaxLength(50)]
    public string IssueType { get; set; } = string.Empty; // MissingColumn, ExtraColumn, TypeMismatch, RequiredMissing
    
    [Required]
    [MaxLength(200)]
    public string ColumnName { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Message { get; set; }
    
    [MaxLength(50)]
    public string? ExpectedType { get; set; }
    
    [MaxLength(50)]
    public string? ActualType { get; set; }
    
    public int? RowNumber { get; set; }
}


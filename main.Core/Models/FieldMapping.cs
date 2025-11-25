using System.ComponentModel.DataAnnotations;

namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Field mapping configuration - maps CSV columns to SQL columns with transformations
/// </summary>
public class FieldMapping
{
    [Required]
    [MaxLength(200)]
    public string SourceColumn { get; set; } = string.Empty; // CSV column name
    
    [Required]
    [MaxLength(200)]
    public string TargetColumn { get; set; } = string.Empty; // SQL column name
    
    /// <summary>
    /// Transformation type: None, Rename, Concatenate, Split, Format, Conditional, Lookup, Default
    /// </summary>
    [MaxLength(50)]
    public string TransformationType { get; set; } = "None";
    
    /// <summary>
    /// Transformation expression (e.g., "FirstName + ' ' + LastName" for concatenation)
    /// </summary>
    [MaxLength(1000)]
    public string? TransformationExpression { get; set; }
    
    /// <summary>
    /// Additional source columns needed for transformation (e.g., ["FirstName", "LastName"] for concatenation)
    /// </summary>
    public List<string> AdditionalSourceColumns { get; set; } = new();
    
    /// <summary>
    /// Default value if source column is missing or empty
    /// </summary>
    [MaxLength(500)]
    public string? DefaultValue { get; set; }
    
    /// <summary>
    /// Whether this mapping is required (target column is NOT NULL)
    /// </summary>
    public bool IsRequired { get; set; }
    
    /// <summary>
    /// Data type conversion rules (e.g., "Date", "Decimal", "Integer")
    /// </summary>
    [MaxLength(50)]
    public string? DataTypeConversion { get; set; }
    
    /// <summary>
    /// Format pattern for date/time conversions (e.g., "MM/dd/yyyy", "yyyy-MM-dd")
    /// </summary>
    [MaxLength(100)]
    public string? FormatPattern { get; set; }
    
    /// <summary>
    /// Conditional logic expression (e.g., "Status == 'Active'")
    /// </summary>
    [MaxLength(500)]
    public string? ConditionalExpression { get; set; }
    
    /// <summary>
    /// Value to use if conditional expression is true
    /// </summary>
    [MaxLength(500)]
    public string? ConditionalTrueValue { get; set; }
    
    /// <summary>
    /// Value to use if conditional expression is false
    /// </summary>
    [MaxLength(500)]
    public string? ConditionalFalseValue { get; set; }
}

/// <summary>
/// Collection of field mappings for an interface
/// </summary>
public class FieldMappingConfiguration
{
    [Required]
    [MaxLength(200)]
    public string InterfaceName { get; set; } = string.Empty;
    
    public List<FieldMapping> Mappings { get; set; } = new();
    
    /// <summary>
    /// Whether to skip rows that fail transformation (default: false, will throw error)
    /// </summary>
    public bool SkipFailedTransformations { get; set; } = false;
    
    /// <summary>
    /// Whether to log transformation details for debugging
    /// </summary>
    public bool LogTransformationDetails { get; set; } = false;
}


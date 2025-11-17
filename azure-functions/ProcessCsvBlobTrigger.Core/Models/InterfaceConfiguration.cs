using System.ComponentModel.DataAnnotations;

namespace ProcessCsvBlobTrigger.Core.Models;

/// <summary>
/// Configuration for an interface (Source -> Destination mapping)
/// </summary>
public class InterfaceConfiguration
{
    [Required]
    [MaxLength(200)]
    public string InterfaceName { get; set; } = string.Empty; // e.g., "FromCsvToSqlServerExample"

    [Required]
    [MaxLength(100)]
    public string SourceAdapterName { get; set; } = string.Empty; // e.g., "CSV", "SqlServer"

    [Required]
    [MaxLength(1000)]
    public string SourceConfiguration { get; set; } = string.Empty; // JSON: {"source": "csv-files/csv-incoming/file.csv", "enabled": true}

    [Required]
    [MaxLength(100)]
    public string DestinationAdapterName { get; set; } = string.Empty; // e.g., "SqlServer", "CSV"

    [Required]
    [MaxLength(1000)]
    public string DestinationConfiguration { get; set; } = string.Empty; // JSON: {"destination": "TransportData", "enabled": true}

    [Required]
    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}


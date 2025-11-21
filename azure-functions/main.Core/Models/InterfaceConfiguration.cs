using System.ComponentModel.DataAnnotations;

namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Configuration for an interface (Source -> Destination mapping)
/// </summary>
public class InterfaceConfiguration
{
    [Required]
    [MaxLength(200)]
    public string InterfaceName { get; set; } = string.Empty; // e.g., "FromCsvToSqlServerExample"

    /// <summary>
    /// Dictionary of source adapter instances, keyed by instance name
    /// Allows multiple source adapters per interface (for future use)
    /// </summary>
    public Dictionary<string, SourceAdapterInstance> Sources { get; set; } = new Dictionary<string, SourceAdapterInstance>();

    /// <summary>
    /// Dictionary of destination adapter instances, keyed by instance name
    /// Multiple destination adapters can subscribe to the same MessageBox data
    /// </summary>
    public Dictionary<string, DestinationAdapterInstance> Destinations { get; set; } = new Dictionary<string, DestinationAdapterInstance>();

    // ========== DEPRECATED PROPERTIES - Kept for backward compatibility and migration ==========
    // These properties are used during migration from old format to new format
    // They will be populated from Sources/Destinations dictionaries when serializing for backward compatibility

    [MaxLength(100)]
    [Obsolete("Use Sources dictionary instead")]
    public string? SourceAdapterName { get; set; }

    [MaxLength(1000)]
    [Obsolete("Use Sources dictionary instead")]
    public string? SourceConfiguration { get; set; }

    [MaxLength(100)]
    [Obsolete("Use Destinations dictionary instead")]
    public string? DestinationAdapterName { get; set; }

    [MaxLength(1000)]
    [Obsolete("Use Destinations dictionary instead")]
    public string? DestinationConfiguration { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public bool? SourceIsEnabled { get; set; }

    [Obsolete("Use Destinations dictionary instead")]
    public bool? DestinationIsEnabled { get; set; }

    [MaxLength(200)]
    [Obsolete("Use Sources dictionary instead")]
    public string? SourceInstanceName { get; set; }

    [MaxLength(200)]
    [Obsolete("Use Destinations dictionary instead")]
    public string? DestinationInstanceName { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public Guid? SourceAdapterInstanceGuid { get; set; }

    [Obsolete("Use Destinations dictionary instead")]
    public Guid? DestinationAdapterInstanceGuid { get; set; }

    [Obsolete("Use Destinations dictionary instead")]
    public List<DestinationAdapterInstance>? DestinationAdapterInstances { get; set; }

    // ========== DEPRECATED PROPERTIES - Kept for backward compatibility and migration ==========
    // These properties are populated during migration from old format and used for backward compatibility
    // They are moved to Sources/Destinations dictionaries in the new format

    [Obsolete("Use Sources dictionary instead")]
    public string? SourceReceiveFolder { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public string? SourceFileMask { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public int? SourceBatchSize { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public string? SourceFieldSeparator { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public string? CsvData { get; set; }

    [Obsolete("Use Destinations dictionary instead")]
    public string? DestinationReceiveFolder { get; set; }

    [Obsolete("Use Destinations dictionary instead")]
    public string? DestinationFileMask { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public string? CsvAdapterType { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public int? CsvPollingInterval { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public string? SftpHost { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public int? SftpPort { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public string? SftpUsername { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public string? SftpPassword { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public string? SftpSshKey { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public string? SftpFolder { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public string? SftpFileMask { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public int? SftpMaxConnectionPoolSize { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public int? SftpFileBufferSize { get; set; }

    [Obsolete("Use Sources/Destinations dictionaries instead")]
    public string? SqlServerName { get; set; }

    [Obsolete("Use Sources/Destinations dictionaries instead")]
    public string? SqlDatabaseName { get; set; }

    [Obsolete("Use Sources/Destinations dictionaries instead")]
    public string? SqlUserName { get; set; }

    [Obsolete("Use Sources/Destinations dictionaries instead")]
    public string? SqlPassword { get; set; }

    [Obsolete("Use Sources/Destinations dictionaries instead")]
    public bool? SqlIntegratedSecurity { get; set; }

    [Obsolete("Use Sources/Destinations dictionaries instead")]
    public string? SqlResourceGroup { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public string? SqlPollingStatement { get; set; }

    [Obsolete("Use Sources dictionary instead")]
    public int? SqlPollingInterval { get; set; }

    [Obsolete("Use Sources/Destinations dictionaries instead")]
    public string? SqlTableName { get; set; }

    [Obsolete("Use Destinations dictionary instead")]
    public bool? SqlUseTransaction { get; set; }

    [Obsolete("Use Sources/Destinations dictionaries instead")]
    public int? SqlBatchSize { get; set; }

    [Obsolete("Use Sources/Destinations dictionaries instead")]
    public int? SqlCommandTimeout { get; set; }

    [Obsolete("Use Sources/Destinations dictionaries instead")]
    public bool? SqlFailOnBadStatement { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

}


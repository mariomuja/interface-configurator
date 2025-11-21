using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Represents a source adapter instance configuration
/// Contains all properties specific to a source adapter instance
/// </summary>
public class SourceAdapterInstance
{
    [Required]
    [MaxLength(200)]
    public string InstanceName { get; set; } = "Source";

    [Required]
    [MaxLength(100)]
    public string AdapterName { get; set; } = string.Empty; // e.g., "CSV", "SqlServer"

    [Required]
    public bool IsEnabled { get; set; } = true;

    [Required]
    public Guid AdapterInstanceGuid { get; set; } = Guid.NewGuid();

    [MaxLength(1000)]
    public string Configuration { get; set; } = string.Empty; // JSON configuration

    // CSV Adapter Properties
    [MaxLength(500)]
    public string? SourceReceiveFolder { get; set; }

    [MaxLength(100)]
    public string SourceFileMask { get; set; } = "*.txt";

    public int SourceBatchSize { get; set; } = 100;

    [MaxLength(10)]
    public string SourceFieldSeparator { get; set; } = "║";

    [MaxLength(10000000)] // 10MB max
    public string? CsvData { get; set; }

    [MaxLength(20)]
    public string CsvAdapterType { get; set; } = "FILE"; // "RAW", "FILE", "SFTP"

    public int CsvPollingInterval { get; set; } = 10;

    // SFTP Properties
    [MaxLength(500)]
    public string? SftpHost { get; set; }

    public int SftpPort { get; set; } = 22;

    [MaxLength(200)]
    public string? SftpUsername { get; set; }

    [MaxLength(500)]
    public string? SftpPassword { get; set; }

    [MaxLength(5000)]
    public string? SftpSshKey { get; set; }

    [MaxLength(500)]
    public string? SftpFolder { get; set; }

    [MaxLength(100)]
    public string SftpFileMask { get; set; } = "*.txt";

    public int SftpMaxConnectionPoolSize { get; set; } = 5;

    public int SftpFileBufferSize { get; set; } = 8192;

    // SQL Server Adapter Properties
    [MaxLength(500)]
    public string? SqlServerName { get; set; }

    [MaxLength(200)]
    public string? SqlDatabaseName { get; set; }

    [MaxLength(200)]
    public string? SqlUserName { get; set; }

    [MaxLength(500)]
    public string? SqlPassword { get; set; }

    public bool SqlIntegratedSecurity { get; set; } = false;

    [MaxLength(200)]
    public string? SqlResourceGroup { get; set; }

    [MaxLength(2000)]
    public string? SqlPollingStatement { get; set; }

    public int SqlPollingInterval { get; set; } = 60;

    [MaxLength(200)]
    public string? SqlTableName { get; set; }

    public bool SqlUseTransaction { get; set; } = false;

    public int SqlBatchSize { get; set; } = 1000;

    public int SqlCommandTimeout { get; set; } = 30;

    public bool SqlFailOnBadStatement { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}


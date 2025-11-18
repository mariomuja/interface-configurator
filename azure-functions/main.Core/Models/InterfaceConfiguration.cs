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
    public string DestinationConfiguration { get; set; } = string.Empty; // JSON: {"destination": "TransportData"}

    /// <summary>
    /// Whether the Source adapter is enabled for this interface
    /// </summary>
    [Required]
    public bool SourceIsEnabled { get; set; } = true;

    /// <summary>
    /// Whether the Destination adapter is enabled for this interface
    /// </summary>
    [Required]
    public bool DestinationIsEnabled { get; set; } = true;

    /// <summary>
    /// User-editable name for the Source adapter instance (default: "Source")
    /// </summary>
    [MaxLength(200)]
    public string SourceInstanceName { get; set; } = "Source";

    /// <summary>
    /// User-editable name for the Destination adapter instance (default: "Destination")
    /// DEPRECATED: Use DestinationAdapterInstances list instead
    /// Kept for backward compatibility
    /// </summary>
    [MaxLength(200)]
    public string DestinationInstanceName { get; set; } = "Destination";

    /// <summary>
    /// Unique GUID identifying the Source adapter instance
    /// </summary>
    [Required]
    public Guid SourceAdapterInstanceGuid { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Unique GUID identifying the Destination adapter instance
    /// DEPRECATED: Use DestinationAdapterInstances list instead
    /// Kept for backward compatibility
    /// </summary>
    [Required]
    public Guid DestinationAdapterInstanceGuid { get; set; } = Guid.NewGuid();

    /// <summary>
    /// List of destination adapter instances for this interface
    /// Multiple destination adapters can subscribe to the same MessageBox data
    /// Each instance runs in a separate process
    /// </summary>
    public List<DestinationAdapterInstance> DestinationAdapterInstances { get; set; } = new List<DestinationAdapterInstance>();

    /// <summary>
    /// Receive folder URL/path for CSV adapter (e.g., blob storage path)
    /// When set, the adapter will watch this folder for new files
    /// </summary>
    [MaxLength(500)]
    public string? SourceReceiveFolder { get; set; }

    /// <summary>
    /// File mask pattern for CSV adapter (e.g., "*.txt", "*.csv", "data_*.txt")
    /// Used to filter files in the ReceiveFolder. Default: "*.txt"
    /// </summary>
    [MaxLength(100)]
    public string SourceFileMask { get; set; } = "*.txt";

    /// <summary>
    /// Batch size for CSV adapter - number of rows read from file in one chunk before debatching
    /// Each batch is debatched into single rows and written to MessageBox. Default: 100
    /// </summary>
    public int SourceBatchSize { get; set; } = 100;

    /// <summary>
    /// Field separator for CSV adapter - character used to separate fields in CSV files
    /// Default: "║" (Box Drawing Double Vertical Line, U+2551) - a seldomly used UTF-8 character
    /// Used when reading/validating CSV rows and when constructing new CSV files as destination
    /// </summary>
    [MaxLength(10)]
    public string SourceFieldSeparator { get; set; } = "║";

    /// <summary>
    /// CSV data that can be directly assigned to the adapter instance
    /// When set, the adapter will debatch this data and send it to the MessageBox
    /// This allows CSV data to be provided directly without needing a file
    /// </summary>
    [MaxLength(10000000)] // 10MB max
    public string? CsvData { get; set; }

    /// <summary>
    /// Destination folder/path for CSV adapter when used as destination
    /// Files will be written to this location with filenames constructed from FileMask pattern
    /// </summary>
    [MaxLength(500)]
    public string? DestinationReceiveFolder { get; set; }

    /// <summary>
    /// File mask pattern for CSV adapter destination (e.g., "output_*.txt", "data_$datetime.txt")
    /// Supports variables: $datetime (replaced with current date/time with milliseconds)
    /// Example: "text_" + $datetime + ".txt" becomes "text_20240101120000.123.txt"
    /// Default: "*.txt"
    /// </summary>
    [MaxLength(100)]
    public string DestinationFileMask { get; set; } = "*.txt";

    // CSV Adapter SFTP Properties (used when AdapterType is SFTP)
    /// <summary>
    /// CSV adapter type: "FILE" (Azure Blob Storage) or "SFTP" (SFTP server)
    /// When SFTP is set, FILE properties (SourceReceiveFolder) are ignored
    /// Default: "FILE"
    /// </summary>
    [MaxLength(20)]
    public string CsvAdapterType { get; set; } = "FILE";

    /// <summary>
    /// SFTP server hostname or IP address
    /// </summary>
    [MaxLength(500)]
    public string? SftpHost { get; set; }

    /// <summary>
    /// SFTP server port (default: 22)
    /// </summary>
    public int SftpPort { get; set; } = 22;

    /// <summary>
    /// SFTP username for authentication
    /// </summary>
    [MaxLength(200)]
    public string? SftpUsername { get; set; }

    /// <summary>
    /// SFTP password for authentication (alternative to SSH Key)
    /// </summary>
    [MaxLength(500)]
    public string? SftpPassword { get; set; }

    /// <summary>
    /// SFTP SSH private key (alternative to Password)
    /// Base64-encoded private key content
    /// </summary>
    [MaxLength(5000)]
    public string? SftpSshKey { get; set; }

    /// <summary>
    /// SFTP remote folder path where files are located
    /// </summary>
    [MaxLength(500)]
    public string? SftpFolder { get; set; }

    /// <summary>
    /// File mask pattern for SFTP adapter (e.g., "*.txt", "*.csv", "data_*.txt")
    /// Used to filter files in the SFTP folder. Default: "*.txt"
    /// </summary>
    [MaxLength(100)]
    public string SftpFileMask { get; set; } = "*.txt";

    /// <summary>
    /// Maximum number of concurrent SFTP connections in the connection pool
    /// Default: 5
    /// </summary>
    public int SftpMaxConnectionPoolSize { get; set; } = 5;

    /// <summary>
    /// Buffer size (in bytes) for reading files from SFTP server
    /// Larger buffer sizes improve performance but use more memory
    /// Default: 8192 bytes (8 KB)
    /// </summary>
    public int SftpFileBufferSize { get; set; } = 8192;

    // SQL Server Adapter Connection Properties (used for both Source and Destination)
    /// <summary>
    /// SQL Server name or IP address (e.g., "sql-server.database.windows.net" or "192.168.1.100")
    /// </summary>
    [MaxLength(500)]
    public string? SqlServerName { get; set; }

    /// <summary>
    /// Database name
    /// </summary>
    [MaxLength(200)]
    public string? SqlDatabaseName { get; set; }

    /// <summary>
    /// SQL login username (if using SQL authentication)
    /// </summary>
    [MaxLength(200)]
    public string? SqlUserName { get; set; }

    /// <summary>
    /// SQL password (if using SQL authentication)
    /// </summary>
    [MaxLength(500)]
    public string? SqlPassword { get; set; }

    /// <summary>
    /// Use Windows Authentication (Integrated Security). true = Windows Auth, false = SQL Auth
    /// </summary>
    public bool SqlIntegratedSecurity { get; set; } = false;

    /// <summary>
    /// Azure Resource Group name (for Azure SQL managed database access)
    /// </summary>
    [MaxLength(200)]
    public string? SqlResourceGroup { get; set; }

    // SQL Server Adapter Source-Specific Properties
    /// <summary>
    /// SQL SELECT or EXEC statement to poll for new data (only used when adapter is Source)
    /// Example: "SELECT * FROM Orders WHERE Processed = 0"
    /// </summary>
    [MaxLength(2000)]
    public string? SqlPollingStatement { get; set; }

    /// <summary>
    /// How often to run the polling statement (in seconds). Only used when adapter is Source.
    /// Default: 60 seconds
    /// </summary>
    public int SqlPollingInterval { get; set; } = 60;

    /// <summary>
    /// Table name for SQL Server adapter.
    /// When used as Source: Used in default PollingStatement if PollingStatement is not provided (default: "SELECT * FROM Table")
    /// When used as Destination: Used as the destination table name
    /// </summary>
    [MaxLength(200)]
    public string? SqlTableName { get; set; }

    /// <summary>
    /// Wrap execution in an explicit SQL transaction for SQL Server adapter.
    /// When true, all database operations are wrapped in a transaction that can be committed or rolled back.
    /// Default: false
    /// </summary>
    public bool SqlUseTransaction { get; set; } = false;

    /// <summary>
    /// Number of rows fetched at once for SQL Server adapter when reading data.
    /// Larger batch sizes improve performance but use more memory.
    /// Default: 1000 rows per batch
    /// </summary>
    public int SqlBatchSize { get; set; } = 1000;

    /// <summary>
    /// Time (in seconds) before SQL command times out.
    /// If a SQL command takes longer than this time, it will be cancelled.
    /// Default: 30 seconds
    /// </summary>
    public int SqlCommandTimeout { get; set; } = 30;

    /// <summary>
    /// Suspend adapter instance if SQL error occurs.
    /// When true, if a SQL error occurs, the adapter instance will be automatically disabled.
    /// Default: false
    /// </summary>
    public bool SqlFailOnBadStatement { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}


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

    // SAP Adapter Properties (Source: IDOC abrufen, Destination: IDOC senden)
    [MaxLength(500)]
    public string? SapApplicationServer { get; set; }

    [MaxLength(200)]
    public string? SapSystemNumber { get; set; }

    [MaxLength(200)]
    public string? SapClient { get; set; }

    [MaxLength(200)]
    public string? SapUsername { get; set; }

    [MaxLength(500)]
    public string? SapPassword { get; set; }

    [MaxLength(200)]
    public string? SapLanguage { get; set; } = "EN";

    [MaxLength(500)]
    public string? SapIdocType { get; set; } // z.B. "MATMAS01", "ORDERS05"

    [MaxLength(500)]
    public string? SapIdocMessageType { get; set; }

    [MaxLength(2000)]
    public string? SapIdocFilter { get; set; } // Filter für IDOC-Abfrage

    public int SapPollingInterval { get; set; } = 60; // Sekunden

    public int SapBatchSize { get; set; } = 100;

    public int SapConnectionTimeout { get; set; } = 30;

    public bool SapUseRfc { get; set; } = true; // RFC vs. HTTP

    [MaxLength(500)]
    public string? SapRfcDestination { get; set; }

    // Dynamics 365 Adapter Properties
    [MaxLength(500)]
    public string? Dynamics365TenantId { get; set; }

    [MaxLength(500)]
    public string? Dynamics365ClientId { get; set; }

    [MaxLength(500)]
    public string? Dynamics365ClientSecret { get; set; }

    [MaxLength(500)]
    public string? Dynamics365InstanceUrl { get; set; }

    [MaxLength(200)]
    public string? Dynamics365EntityName { get; set; } // z.B. "accounts", "contacts"

    [MaxLength(2000)]
    public string? Dynamics365ODataFilter { get; set; } // OData Filter Query

    public int Dynamics365PollingInterval { get; set; } = 60;

    public int Dynamics365BatchSize { get; set; } = 100;

    public int Dynamics365PageSize { get; set; } = 50; // OData Paging

    // Microsoft CRM Adapter Properties
    [MaxLength(500)]
    public string? CrmOrganizationUrl { get; set; }

    [MaxLength(200)]
    public string? CrmUsername { get; set; }

    [MaxLength(500)]
    public string? CrmPassword { get; set; }

    [MaxLength(200)]
    public string? CrmEntityName { get; set; } // z.B. "account", "contact"

    [MaxLength(2000)]
    public string? CrmFetchXml { get; set; } // FetchXML Query

    public int CrmPollingInterval { get; set; } = 60;

    public int CrmBatchSize { get; set; } = 100;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}


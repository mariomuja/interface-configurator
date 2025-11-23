using System.ComponentModel.DataAnnotations;

namespace InterfaceConfigurator.Main.Core.Models;

/// <summary>
/// Represents a destination adapter instance configuration
/// Multiple destination adapter instances can be configured for a single interface
/// All instances subscribe to the same MessageBox data from the source adapter
/// </summary>
public class DestinationAdapterInstance
{
    /// <summary>
    /// Unique GUID identifying this destination adapter instance
    /// </summary>
    [Required]
    public Guid AdapterInstanceGuid { get; set; } = Guid.NewGuid();

    /// <summary>
    /// User-editable name for this adapter instance (default: "Destination")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string InstanceName { get; set; } = "Destination";

    /// <summary>
    /// Adapter type name (e.g., "CSV", "SqlServer")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string AdapterName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this adapter instance is enabled
    /// </summary>
    [Required]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Destination configuration JSON (e.g., {"destination": "TransportData"} or {"destination": "csv-files/csv-outgoing"})
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Configuration { get; set; } = string.Empty;

    // CSV Adapter Destination Properties
    [MaxLength(500)]
    public string? DestinationReceiveFolder { get; set; }

    [MaxLength(100)]
    public string DestinationFileMask { get; set; } = "*.txt";

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

    [MaxLength(200)]
    public string? SqlTableName { get; set; }

    public bool SqlUseTransaction { get; set; } = false;

    public int SqlBatchSize { get; set; } = 1000;

    public int SqlCommandTimeout { get; set; } = 30;

    public bool SqlFailOnBadStatement { get; set; } = false;

    // SAP Adapter Properties (Destination: IDOC senden)
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
    public string? SapIdocType { get; set; } // IDOC-Typ für Outbound

    [MaxLength(500)]
    public string? SapIdocMessageType { get; set; }

    [MaxLength(500)]
    public string? SapReceiverPort { get; set; } // Receiver Port für Outbound IDOC

    [MaxLength(500)]
    public string? SapReceiverPartner { get; set; } // Partner für Outbound IDOC

    public int SapConnectionTimeout { get; set; } = 30;

    public bool SapUseRfc { get; set; } = true;

    [MaxLength(500)]
    public string? SapRfcDestination { get; set; }

    public int SapBatchSize { get; set; } = 100;

    // Dynamics 365 Adapter Properties (Destination)
    [MaxLength(500)]
    public string? Dynamics365TenantId { get; set; }

    [MaxLength(500)]
    public string? Dynamics365ClientId { get; set; }

    [MaxLength(500)]
    public string? Dynamics365ClientSecret { get; set; }

    [MaxLength(500)]
    public string? Dynamics365InstanceUrl { get; set; }

    [MaxLength(200)]
    public string? Dynamics365EntityName { get; set; }

    public int Dynamics365BatchSize { get; set; } = 100;

    public bool Dynamics365UseBatch { get; set; } = true; // OData Batch Requests

    // Microsoft CRM Adapter Properties (Destination)
    [MaxLength(500)]
    public string? CrmOrganizationUrl { get; set; }

    [MaxLength(200)]
    public string? CrmUsername { get; set; }

    [MaxLength(500)]
    public string? CrmPassword { get; set; }

    [MaxLength(200)]
    public string? CrmEntityName { get; set; }

    public int CrmBatchSize { get; set; } = 100;

    public bool CrmUseBatch { get; set; } = true; // ExecuteMultiple

    // JQ Transformation Properties (for Destination adapters)
    /// <summary>
    /// URI to a jq script file for JSON transformation
    /// The script will be applied to JSON data from MessageBox before sending to destination
    /// </summary>
    [MaxLength(1000)]
    public string? JQScriptFile { get; set; }

    /// <summary>
    /// GUID of the source adapter instance whose data this destination adapter subscribes to
    /// This creates a subscription in the MessageBox
    /// </summary>
    public Guid? SourceAdapterSubscription { get; set; }

    // SQL Server Adapter Custom Statement Properties
    /// <summary>
    /// Custom INSERT statement using OPENJSON to insert data from MessageBox JSON or jq-transformed JSON
    /// Example: INSERT INTO MyTable SELECT * FROM OPENJSON(@json) WITH (...)
    /// </summary>
    [MaxLength(5000)]
    public string? InsertStatement { get; set; }

    /// <summary>
    /// Custom UPDATE statement using OPENJSON to update data from MessageBox JSON or jq-transformed JSON
    /// Example: UPDATE MyTable SET ... FROM OPENJSON(@json) WITH (...)
    /// </summary>
    [MaxLength(5000)]
    public string? UpdateStatement { get; set; }

    /// <summary>
    /// Custom DELETE statement using OPENJSON to delete data from MessageBox JSON or jq-transformed JSON
    /// Example: DELETE FROM MyTable WHERE ... IN (SELECT ... FROM OPENJSON(@json) WITH (...))
    /// </summary>
    [MaxLength(5000)]
    public string? DeleteStatement { get; set; }

    /// <summary>
    /// When this instance was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this instance was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}





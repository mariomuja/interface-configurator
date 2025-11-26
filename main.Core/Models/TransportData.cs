using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InterfaceConfigurator.Main.Core.Models;

[Table("TransportData")]
public class TransportData
{
    /// <summary>
    /// Primary key stored in SQL as UNIQUEIDENTIFIER with DEFAULT NEWID()
    /// </summary>
    [Key]
    [Column("PrimaryKey")]
    public Guid PrimaryKey { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Mirror datetime_created column maintained by SQL default GETUTCDATE()
    /// </summary>
    [Column("datetime_created")]
    public DateTime DateTimeCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional catch-all for adapters that still serialize payloads.
    /// </summary>
    public string? CsvDataJson { get; set; }

    /// <summary>
    /// Legacy alias for PrimaryKey so older components/tests keep working.
    /// </summary>
    [NotMapped]
    public Guid Id
    {
        get => PrimaryKey;
        set => PrimaryKey = value;
    }

    /// <summary>
    /// Legacy alias for datetime_created.
    /// </summary>
    [NotMapped]
    public DateTime CreatedAt
    {
        get => DateTimeCreated;
        set => DateTimeCreated = value;
    }

    /// <summary>
    /// Non-persisted map of CSV columns used during processing.
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> CsvColumns { get; set; } = new();
}


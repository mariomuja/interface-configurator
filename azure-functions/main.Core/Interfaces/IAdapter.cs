using InterfaceConfigurator.Main.Core.Services;

namespace InterfaceConfigurator.Main.Core.Interfaces;

/// <summary>
/// Interface for data adapters that can be used as source or destination
/// </summary>
public interface IAdapter
{
    /// <summary>
    /// Technical name of the adapter (e.g., "CSV", "SqlServer", "JSON", "SAP")
    /// </summary>
    string AdapterName { get; }

    /// <summary>
    /// Display alias for the adapter (e.g., "CSV", "SQL Server", "JSON", "SAP")
    /// This is used for UI display and can be translated
    /// </summary>
    string AdapterAlias { get; }

    /// <summary>
    /// Indicates whether this adapter supports reading (can be used as source)
    /// </summary>
    bool SupportsRead { get; }

    /// <summary>
    /// Indicates whether this adapter supports writing (can be used as destination)
    /// </summary>
    bool SupportsWrite { get; }

    /// <summary>
    /// Reads data from the source and returns headers and records
    /// </summary>
    /// <param name="source">Source identifier (e.g., blob path, table name, file path)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing headers and records</returns>
    Task<(List<string> headers, List<Dictionary<string, string>> records)> ReadAsync(string source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes data to the destination
    /// </summary>
    /// <param name="destination">Destination identifier (e.g., blob path, table name, file path)</param>
    /// <param name="headers">Column headers</param>
    /// <param name="records">Data records</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WriteAsync(string destination, List<string> headers, List<Dictionary<string, string>> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the schema (column types) from the source
    /// </summary>
    /// <param name="source">Source identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping column names to their type information</returns>
    Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetSchemaAsync(string source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the destination structure exists and matches the provided schema
    /// </summary>
    /// <param name="destination">Destination identifier</param>
    /// <param name="columnTypes">Column type information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnsureDestinationStructureAsync(string destination, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default);
}



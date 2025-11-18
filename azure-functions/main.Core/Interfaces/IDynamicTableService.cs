using ProcessCsvBlobTrigger.Core.Services;

namespace ProcessCsvBlobTrigger.Core.Interfaces;

/// <summary>
/// Service for dynamically creating and managing SQL tables based on CSV structure
/// </summary>
public interface IDynamicTableService
{
    /// <summary>
    /// Ensures TransportData table exists and matches CSV column structure
    /// </summary>
    Task EnsureTableStructureAsync(Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds new columns to TransportData table if they don't exist
    /// </summary>
    Task AddMissingColumnsAsync(Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current column structure of TransportData table
    /// </summary>
    Task<Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo>> GetCurrentTableStructureAsync(CancellationToken cancellationToken = default);
}







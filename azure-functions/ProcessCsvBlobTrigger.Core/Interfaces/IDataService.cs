using ProcessCsvBlobTrigger.Core.Models;
using ProcessCsvBlobTrigger.Core.Services;

namespace ProcessCsvBlobTrigger.Core.Interfaces;

public interface IDataService
{
    Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken = default);
    Task ProcessChunksAsync(List<List<TransportData>> chunks, CancellationToken cancellationToken = default);
    Task InsertRowsAsync(List<Dictionary<string, string>> rows, Dictionary<string, CsvColumnAnalyzer.ColumnTypeInfo> columnTypes, CancellationToken cancellationToken = default);
}


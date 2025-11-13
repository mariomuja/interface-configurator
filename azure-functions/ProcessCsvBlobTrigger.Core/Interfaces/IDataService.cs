using ProcessCsvBlobTrigger.Core.Models;

namespace ProcessCsvBlobTrigger.Core.Interfaces;

public interface IDataService
{
    Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken = default);
    Task ProcessChunksAsync(List<List<TransportData>> chunks, CancellationToken cancellationToken = default);
}


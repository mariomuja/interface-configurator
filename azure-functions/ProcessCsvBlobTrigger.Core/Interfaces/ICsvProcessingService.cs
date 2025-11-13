using ProcessCsvBlobTrigger.Core.Models;

namespace ProcessCsvBlobTrigger.Core.Interfaces;

public interface ICsvProcessingService
{
    List<Dictionary<string, string>> ParseCsv(string csvContent);
    List<List<TransportData>> CreateChunks(List<Dictionary<string, string>> records);
}


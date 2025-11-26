using InterfaceConfigurator.Main.Core.Models;

namespace InterfaceConfigurator.Main.Core.Interfaces;

public interface ICsvProcessingService
{
    List<Dictionary<string, string>> ParseCsv(string csvContent);
    (List<string> headers, List<Dictionary<string, string>> records) ParseCsvWithHeaders(string csvContent);
    Task<(List<string> headers, List<Dictionary<string, string>> records)> ParseCsvWithHeadersAsync(
        string csvContent,
        string? fieldSeparator = null,
        int skipHeaderLines = 0,
        int skipFooterLines = 0,
        char quoteCharacter = '"',
        CancellationToken cancellationToken = default);
    List<List<TransportData>> CreateChunks(List<Dictionary<string, string>> records);
}


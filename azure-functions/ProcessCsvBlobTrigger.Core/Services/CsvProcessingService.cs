using System.Globalization;
using Microsoft.Extensions.Logging;
using ProcessCsvBlobTrigger.Core.Interfaces;
using ProcessCsvBlobTrigger.Core.Models;
using System.IO;

namespace ProcessCsvBlobTrigger.Core.Services;

public class CsvProcessingService : ICsvProcessingService
{
    private const int ChunkSize = 100;
    private readonly IAdapterConfigurationService _adapterConfig;
    private readonly ILogger<CsvProcessingService>? _logger;
    private string? _cachedSeparator;

    public CsvProcessingService(IAdapterConfigurationService adapterConfig, ILogger<CsvProcessingService>? logger = null)
    {
        _adapterConfig = adapterConfig;
        _logger = logger;
    }

    private async Task<string> GetFieldSeparatorAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedSeparator == null)
        {
            _cachedSeparator = await _adapterConfig.GetCsvFieldSeparatorAsync(cancellationToken);
        }
        return _cachedSeparator;
    }

    public List<Dictionary<string, string>> ParseCsv(string csvContent)
    {
        var (headers, records) = ParseCsvWithHeaders(csvContent);
        return records;
    }

    public async Task<(List<string> headers, List<Dictionary<string, string>> records)> ParseCsvWithHeadersAsync(string csvContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
            return (new List<string>(), new List<Dictionary<string, string>>());

        var separator = await GetFieldSeparatorAsync(cancellationToken);

        var lines = csvContent
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count < 2)
            return (new List<string>(), new List<Dictionary<string, string>>());

        // First line is headers - extract them using configured separator
        var headers = ParseCsvLine(lines[0], separator)
            .Select(h => h.Trim().Trim('"'))
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();

        var expectedColumnCount = headers.Count;
        if (expectedColumnCount == 0)
        {
            throw new InvalidOperationException("CSV file has no valid headers. Cannot determine expected column count.");
        }

        // Validate column count consistency across all data rows
        var invalidRows = new List<(int lineNumber, int actualColumnCount)>();
        
        for (int i = 1; i < lines.Count; i++)
        {
            var line = lines[i];
            var values = ParseCsvLine(line, separator);
            var actualColumnCount = values.Count;
            
            if (actualColumnCount != expectedColumnCount)
            {
                invalidRows.Add((i + 1, actualColumnCount)); // Line number is 1-based for user-friendly error messages
            }
        }

        // Throw exception if any rows have inconsistent column counts
        if (invalidRows.Any())
        {
            var errorDetails = string.Join(", ", invalidRows.Select(r => $"Line {r.lineNumber} has {r.actualColumnCount} columns"));
            var errorMessage = $"CSV file has inconsistent column counts. Expected {expectedColumnCount} columns (based on header row), but found rows with different counts: {errorDetails}";
            throw new InvalidDataException(errorMessage);
        }

        // Remaining lines are data rows - skip first line (header)
        var records = lines
            .Skip(1)
            .Select(line =>
            {
                var values = ParseCsvLine(line, separator)
                    .Select(v => v.Trim().Trim('"'))
                    .ToArray();

                return headers
                    .Select((header, index) => new { header, value = values.Length > index ? values[index] : string.Empty })
                    .ToDictionary(x => x.header, x => x.value);
            })
            .ToList();

        return (headers, records);
    }

    public (List<string> headers, List<Dictionary<string, string>> records) ParseCsvWithHeaders(string csvContent)
    {
        // Synchronous wrapper for backward compatibility
        return ParseCsvWithHeadersAsync(csvContent).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Parse a CSV line handling quoted values and custom field separator
    /// </summary>
    private List<string> ParseCsvLine(string line, string separator)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            var nextCh = i + 1 < line.Length ? line[i + 1] : '\0';

            if (ch == '"')
            {
                if (inQuotes && nextCh == '"')
                {
                    // Escaped quote
                    current.Append('"');
                    i++; // Skip next quote
                }
                else
                {
                    // Toggle quote state
                    inQuotes = !inQuotes;
                }
            }
            else if (!inQuotes && line.Substring(i).StartsWith(separator))
            {
                // End of value (using custom separator)
                values.Add(current.ToString());
                current.Clear();
                i += separator.Length - 1; // Skip separator
            }
            else
            {
                current.Append(ch);
            }
        }

        // Add last value
        values.Add(current.ToString());

        return values;
    }

    public List<List<TransportData>> CreateChunks(List<Dictionary<string, string>> records)
    {
        // Process all records - no filtering by id since we use GUID
        // All CSV columns are preserved in the CsvColumns dictionary
        var transportDataList = records
            .Select(record =>
            {
                try
                {
                    // Create TransportData with GUID and all CSV columns
                    var transportData = new TransportData
                    {
                        Id = Guid.NewGuid(), // Generate new GUID for each record
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    // Copy all CSV columns to the dictionary
                    foreach (var kvp in record)
                    {
                        transportData.CsvColumns[kvp.Key] = kvp.Value ?? string.Empty;
                    }
                    
                    return transportData;
                }
                catch
                {
                    return null;
                }
            })
            .Where(data => data != null)
            .Cast<TransportData>()
            .ToList();

        return transportDataList
            .Chunk(ChunkSize)
            .Select(chunk => chunk.ToList())
            .ToList();
    }
}


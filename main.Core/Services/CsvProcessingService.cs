using System.Globalization;
using Microsoft.Extensions.Logging;
using InterfaceConfigurator.Main.Core.Interfaces;
using InterfaceConfigurator.Main.Core.Models;
using System.IO;

namespace InterfaceConfigurator.Main.Core.Services;

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

    public async Task<(List<string> headers, List<Dictionary<string, string>> records)> ParseCsvWithHeadersAsync(string csvContent, string? fieldSeparator = null, int skipHeaderLines = 0, int skipFooterLines = 0, char quoteCharacter = '"', CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
            return (new List<string>(), new List<Dictionary<string, string>>());

        var separator = !string.IsNullOrWhiteSpace(fieldSeparator) ? fieldSeparator : await GetFieldSeparatorAsync(cancellationToken);

        // Remove header and footer lines if specified
        if (skipHeaderLines > 0 || skipFooterLines > 0)
        {
            var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            var totalLines = lines.Count;
            
            // Remove footer lines first (from end)
            if (skipFooterLines > 0 && totalLines > skipFooterLines)
            {
                lines.RemoveRange(totalLines - skipFooterLines, skipFooterLines);
            }
            
            // Remove header lines (from beginning)
            if (skipHeaderLines > 0 && lines.Count > skipHeaderLines)
            {
                lines.RemoveRange(0, skipHeaderLines);
            }
            
            csvContent = string.Join("\n", lines);
        }

        // Use streaming parsing for large files to reduce memory usage
        // For small files (< 1MB), use in-memory parsing for better performance
        if (csvContent.Length > 1024 * 1024) // 1MB threshold
        {
            return await ParseCsvWithHeadersStreamingAsync(csvContent, separator, quoteCharacter, cancellationToken);
        }
        else
        {
            return ParseCsvWithHeadersInMemory(csvContent, separator, quoteCharacter);
        }
    }

    /// <summary>
    /// Parse CSV with streaming to reduce memory usage for large files
    /// Invalid rows are skipped (not added to records) but processing continues
    /// </summary>
    private async Task<(List<string> headers, List<Dictionary<string, string>> records)> ParseCsvWithHeadersStreamingAsync(string csvContent, string separator, char quoteCharacter, CancellationToken cancellationToken)
    {
        var headers = new List<string>();
        var records = new List<Dictionary<string, string>>();
        var invalidRows = new List<(int lineNumber, int actualColumnCount, string? lineContent)>();
        
        using var reader = new StringReader(csvContent);
        
        // Read header line
        var headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(headerLine))
            return (new List<string>(), new List<Dictionary<string, string>>());

        headers = ParseCsvLine(headerLine, separator, quoteCharacter)
            .Select(h => h.Trim().Trim(quoteCharacter))
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();

        var expectedColumnCount = headers.Count;
        if (expectedColumnCount == 0)
        {
            throw new InvalidOperationException("CSV file has no valid headers. Cannot determine expected column count.");
        }

        // Process data rows line by line (streaming)
        int lineNumber = 2; // Start at 2 (after header)
        string? line;
        const int chunkSize = 1000; // Process in chunks to balance memory and performance
        var chunk = new List<Dictionary<string, string>>();

        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var values = ParseCsvLine(line, separator, quoteCharacter)
                .Select(v => v.Trim().Trim(quoteCharacter))
                .ToArray();

            var actualColumnCount = values.Length;
            
            if (actualColumnCount != expectedColumnCount)
            {
                // Skip invalid rows but continue processing - log for user awareness
                var linePreview = line.Length > 100 ? line.Substring(0, 100) + "..." : line;
                invalidRows.Add((lineNumber, actualColumnCount, linePreview));
                _logger?.LogWarning("Skipping invalid CSV row at line {LineNumber}: Expected {ExpectedColumns} columns, found {ActualColumns} columns. Line preview: {LinePreview}",
                    lineNumber, expectedColumnCount, actualColumnCount, linePreview);
                lineNumber++;
                continue; // Skip invalid rows but continue processing
            }

            var record = headers
                .Select((header, index) => new { header, value = values.Length > index ? values[index] : string.Empty })
                .ToDictionary(x => x.header, x => x.value);

            chunk.Add(record);
            lineNumber++;

            // Process chunk when it reaches chunkSize
            if (chunk.Count >= chunkSize)
            {
                records.AddRange(chunk);
                chunk.Clear();
            }
        }

        // Add remaining records from last chunk
        if (chunk.Count > 0)
        {
            records.AddRange(chunk);
        }

        // Log summary of skipped rows (but don't throw exception - processing continues)
        if (invalidRows.Any())
        {
            var skippedCount = invalidRows.Count;
            var errorDetails = string.Join(", ", invalidRows.Take(10).Select(r => $"Line {r.lineNumber} ({r.actualColumnCount} columns)"));
            var summaryMessage = $"CSV processing completed: {records.Count} valid rows processed, {skippedCount} invalid rows skipped. " +
                $"Expected {expectedColumnCount} columns. Skipped rows: {errorDetails}" +
                (skippedCount > 10 ? $" (and {skippedCount - 10} more)" : "");
            _logger?.LogWarning(summaryMessage);
        }

        return (headers, records);
    }

    /// <summary>
    /// Parse CSV in memory for small files (faster for small files)
    /// </summary>
    private (List<string> headers, List<Dictionary<string, string>> records) ParseCsvWithHeadersInMemory(string csvContent, string separator, char quoteCharacter)
    {
        var lines = csvContent
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count < 2)
            return (new List<string>(), new List<Dictionary<string, string>>());

        // First line is headers - extract them using configured separator
        var headers = ParseCsvLine(lines[0], separator, quoteCharacter)
            .Select(h => h.Trim().Trim(quoteCharacter))
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();

        var expectedColumnCount = headers.Count;
        if (expectedColumnCount == 0)
        {
            throw new InvalidOperationException("CSV file has no valid headers. Cannot determine expected column count.");
        }

        // Process data rows - skip invalid rows but continue processing
        var invalidRows = new List<(int lineNumber, int actualColumnCount, string? lineContent)>();
        var records = new List<Dictionary<string, string>>();
        
        for (int i = 1; i < lines.Count; i++)
        {
            var line = lines[i];
            var values = ParseCsvLine(line, separator, quoteCharacter);
            var actualColumnCount = values.Count;
            
            if (actualColumnCount != expectedColumnCount)
            {
                // Skip invalid rows but continue processing - log for user awareness
                var linePreview = line.Length > 100 ? line.Substring(0, 100) + "..." : line;
                invalidRows.Add((i + 1, actualColumnCount, linePreview)); // Line number is 1-based for user-friendly error messages
                _logger?.LogWarning("Skipping invalid CSV row at line {LineNumber}: Expected {ExpectedColumns} columns, found {ActualColumns} columns. Line preview: {LinePreview}",
                    i + 1, expectedColumnCount, actualColumnCount, linePreview);
                continue; // Skip invalid rows but continue processing
            }

            // Process valid row
            var trimmedValues = values
                .Select(v => v.Trim().Trim(quoteCharacter))
                .ToArray();

            var record = headers
                .Select((header, index) => new { header, value = trimmedValues.Length > index ? trimmedValues[index] : string.Empty })
                .ToDictionary(x => x.header, x => x.value);

            records.Add(record);
        }

        // Log summary of skipped rows (but don't throw exception - processing continues)
        if (invalidRows.Any())
        {
            var skippedCount = invalidRows.Count;
            var errorDetails = string.Join(", ", invalidRows.Take(10).Select(r => $"Line {r.lineNumber} ({r.actualColumnCount} columns)"));
            var summaryMessage = $"CSV processing completed: {records.Count} valid rows processed, {skippedCount} invalid rows skipped. " +
                $"Expected {expectedColumnCount} columns. Skipped rows: {errorDetails}" +
                (skippedCount > 10 ? $" (and {skippedCount - 10} more)" : "");
            _logger?.LogWarning(summaryMessage);
        }

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
    private List<string> ParseCsvLine(string line, string separator, char quoteChar = '"')
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            var nextCh = i + 1 < line.Length ? line[i + 1] : '\0';

            if (ch == quoteChar)
            {
                if (inQuotes && nextCh == quoteChar)
                {
                    // Escaped quote (double quote)
                    current.Append(quoteChar);
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


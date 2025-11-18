using System.Text;
using Microsoft.Extensions.Logging;

namespace ProcessCsvBlobTrigger.Services;

/// <summary>
/// Service for validating CSV files before processing (encoding, delimiters, structure)
/// </summary>
public class CsvValidationService
{
    private readonly ILogger<CsvValidationService>? _logger;

    public CsvValidationService(ILogger<CsvValidationService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validate CSV content
    /// </summary>
    public CsvValidationResult ValidateCsv(string csvContent, string? expectedDelimiter = null)
    {
        var result = new CsvValidationResult
        {
            IsValid = true,
            Issues = new List<string>()
        };

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            result.IsValid = false;
            result.Issues.Add("CSV content is empty");
            return result;
        }

        // Detect encoding
        result.Encoding = DetectEncoding(csvContent);
        if (result.Encoding == null)
        {
            result.IsValid = false;
            result.Issues.Add("Could not detect encoding. File may contain invalid characters.");
        }

        // Detect delimiter
        result.DetectedDelimiter = DetectDelimiter(csvContent, expectedDelimiter);
        if (string.IsNullOrEmpty(result.DetectedDelimiter))
        {
            result.IsValid = false;
            result.Issues.Add("Could not detect CSV delimiter");
        }
        else if (!string.IsNullOrEmpty(expectedDelimiter) && result.DetectedDelimiter != expectedDelimiter)
        {
            result.IsValid = false;
            result.Issues.Add($"Delimiter mismatch: Expected '{expectedDelimiter}', detected '{result.DetectedDelimiter}'");
        }

        // Validate structure
        var lines = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        if (lines.Length == 0)
        {
            result.IsValid = false;
            result.Issues.Add("CSV file has no lines");
            return result;
        }

        result.LineCount = lines.Length;
        result.HasHeader = lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]);

        if (!result.HasHeader)
        {
            result.IsValid = false;
            result.Issues.Add("CSV file appears to have no header row");
        }

        // Check column count consistency
        if (lines.Length > 0 && !string.IsNullOrEmpty(result.DetectedDelimiter))
        {
            var headerColumnCount = CountColumns(lines[0], result.DetectedDelimiter);
            result.ColumnCount = headerColumnCount;

            if (headerColumnCount == 0)
            {
                result.IsValid = false;
                result.Issues.Add("Header row has no columns");
            }

            // Check first 100 rows for consistency (to avoid performance issues with large files)
            var rowsToCheck = Math.Min(100, lines.Length - 1);
            var inconsistentRows = new List<int>();

            for (int i = 1; i <= rowsToCheck; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue; // Skip empty lines

                var columnCount = CountColumns(lines[i], result.DetectedDelimiter);
                if (columnCount != headerColumnCount)
                {
                    inconsistentRows.Add(i + 1); // +1 because line numbers are 1-based
                }
            }

            if (inconsistentRows.Count > 0)
            {
                result.IsValid = false;
                result.Issues.Add($"Column count inconsistency: {inconsistentRows.Count} rows have different column counts than header. Example rows: {string.Join(", ", inconsistentRows.Take(10))}");
            }
        }

        // Check for common issues
        if (csvContent.Contains("\0"))
        {
            result.IsValid = false;
            result.Issues.Add("CSV file contains null characters (\\0)");
        }

        // Check for BOM
        if (csvContent.StartsWith("\uFEFF", StringComparison.Ordinal))
        {
            result.HasBom = true;
            result.Issues.Add("CSV file has UTF-8 BOM (Byte Order Mark). This is usually fine but may cause issues with some parsers.");
        }

        return result;
    }

    private string? DetectEncoding(string content)
    {
        // Try to detect UTF-8
        try
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(content);
            var roundTrip = Encoding.UTF8.GetString(utf8Bytes);
            if (roundTrip == content)
            {
                return "UTF-8";
            }
        }
        catch { }

        // Try to detect UTF-16
        try
        {
            var utf16Bytes = Encoding.Unicode.GetBytes(content);
            var roundTrip = Encoding.Unicode.GetString(utf16Bytes);
            if (roundTrip == content)
            {
                return "UTF-16";
            }
        }
        catch { }

        // Default to UTF-8 if we can't detect
        return "UTF-8";
    }

    private string DetectDelimiter(string csvContent, string? expectedDelimiter = null)
    {
        if (!string.IsNullOrEmpty(expectedDelimiter))
        {
            // Verify expected delimiter exists
            var firstLine = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).FirstOrDefault();
            if (firstLine != null && firstLine.Contains(expectedDelimiter))
            {
                return expectedDelimiter;
            }
        }

        // Common delimiters to check
        var delimiters = new[] { ",", ";", "\t", "|" };
        var firstLine = csvContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).FirstOrDefault();

        if (string.IsNullOrEmpty(firstLine))
            return ","; // Default

        var delimiterCounts = delimiters.ToDictionary(d => d, d => firstLine.Count(c => c.ToString() == d));

        // Return delimiter with highest count, or comma as default
        var bestDelimiter = delimiterCounts.OrderByDescending(kvp => kvp.Value).First();
        return bestDelimiter.Value > 0 ? bestDelimiter.Key : ",";
    }

    private int CountColumns(string line, string delimiter)
    {
        if (string.IsNullOrEmpty(line))
            return 0;

        // Simple column count (doesn't handle quoted values with delimiters)
        // This is a basic check - full parsing would be done by CsvProcessingService
        var columns = line.Split(new[] { delimiter }, StringSplitOptions.None);
        return columns.Length;
    }
}

/// <summary>
/// Result of CSV validation
/// </summary>
public class CsvValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
    public string? Encoding { get; set; }
    public string? DetectedDelimiter { get; set; }
    public int LineCount { get; set; }
    public int ColumnCount { get; set; }
    public bool HasHeader { get; set; }
    public bool HasBom { get; set; }
}

